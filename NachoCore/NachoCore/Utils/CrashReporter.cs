//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace NachoCore.Utils
{

    /// <summary>
    /// A crash reporter than can handle uncaught exception and save crash logs that can later be
    /// reported to a bug/support ticketing system.  The singleton <see cref="Instance"/> property
    /// is the only crash reporter that can be created and used because we only need one central
    /// place to handle uncaught exceptions.
    /// </summary>
    public class CrashReporter
    {

        #region Getting a Crash Reporter

        /// <summary>
        /// The singleton shared crash reporter instanace
        /// </summary>
        public static readonly CrashReporter Instance = new CrashReporter (DefaultCrashFolder);

        /// <summary>
        /// Create a new crash reporter with the given folder for report storage
        /// </summary>
        /// <param name="crashFolder">Crash folder.</param>
		CrashReporter (string crashFolder)
        {
            CrashFolder = crashFolder;
        }

        /// <summary>
        /// The default folder for storing crash reports, used by <see cref="Instance"/>
        /// </summary>
        /// <value>The default crash folder.</value>
        static string DefaultCrashFolder {
            get {
                return Path.Combine (NcApplication.GetDataDirPath (), "crashes");
            }
        }

        /// <summary>
        /// The folder in which to store reports for this instance.
        /// </summary>
		readonly string CrashFolder;

        #endregion

        #region Cached Properties

        /// <summary>
        /// A cached value for the operating system name and version, to be saved in each crash report
        /// </summary>
        /// <remarks>
        /// The original design used <see cref="NachoPlatform.Device.Os()"/> directly when constructing
        /// a crash report, but Android's Device implementation would crash at that time.  Since the value
        /// doesn't change during the lifetime of the app, we can just cache the value here when the app
        /// starts up, and access it when creating a <see cref="CrashReport"/>
        /// </remarks>
        public static string Platform = NachoPlatform.Device.Instance.Os ();

        /// <summary>
        /// A cached value for the nacho mail version version, to be saved in each crash report
        /// </summary>
        /// <remarks>
        /// While this doesn't suffer the same crash on Android as <see cref="Platform"/>, it shares
        /// the same trait of never changing during the app lifetime, so for consistency, it's also
        /// cached here at app startup.
        /// </remarks>
        public static string NachoVersion = NcApplication.GetVersionString ();

        #endregion

        #region Application Lifecycle

        /// <summary>
        /// Typically called at app startup, register exception handlers and report any crashes from
        /// the previous app run.  On Android, the default <see cref="AppDomain.CurrentDomain.UnhandledException"/>
        /// listener gets called with exceptions that have no stack trace, so on Android we use an Android-specific
        /// listener.  The usingCustomMainHandler argument allows the caller to specify that it's using its own
        /// listener.
        /// </summary>
        /// <param name="usingCustomMainHandler">Set to <c>true</c> if the caller defines its own unhandled exception handler</param>
        public void Start (bool usingCustomMainHandler = false)
        {
            ReportCrashes ();

            if (!usingCustomMainHandler) {
                AppDomain.CurrentDomain.UnhandledException += (sender, e) => {
                    if (e.ExceptionObject is Exception) {
                        ExceptionHandler (e.ExceptionObject as Exception);
                    } else {
                    }
                };
            }

            NcApplication.UnobservedTaskException += (sender, e) => {
                if (e.Exception.InnerException != null) {
                    ExceptionHandler (e.Exception.InnerException);
                } else {
                    ExceptionHandler (e.Exception);
                }
            };
        }

        #endregion

        #region Custom Crash Reporting

        /// <summary>
        /// Can by called by a custom exception listener to report an uncaught exception that this reporter isn't listening for.
        /// Used by Android, which has a platform-specific unhandled exception listener.
        /// </summary>
        /// <param name="e">The exception to report as a crash</param>
        public void ExceptionHandler (Exception e)
        {
            var logs = GetLogs ();
            var report = new CrashReport (e, logs);
            report.Save (CrashFolder);
        }

        #endregion

        #region Sending Reports to External Service

        /// <summary>
        /// Called on startup, scan the crash folder for reports, and send each to an external service.
        /// </summary>
        /// <remarks>
        /// The design here is to scan the <see cref="CrashFolder"/> on a background task, and then call
        /// the <see cref="Report(string[])"/> method back on the main thread.  We want to avoid any filesystem
        ///  work on the main thread.
        /// </remarks>
        void ReportCrashes ()
        {
            NcTask.Run (() => {
                try {
                    var files = Directory.EnumerateFiles (CrashFolder).ToArray ();
                    if (files.Length > 0) {
                        NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                            Report (files);
                        });
                    }
                } catch (DirectoryNotFoundException) {
                    // No problem, we just haven't writting anything to the folder yet, so it doesn't exist
                }
            }, "CrashReporter.ReportCrashes");
        }

        /// <summary>
        /// Report all the filenames.
        /// </summary>
        /// <param name="filenames">Filenames.</param>
        void Report (string [] filenames)
        {
            ReportQueue = new Queue<string> (filenames);
            ReportNextInQueue ();
        }

        /// <summary>
        /// A queue of reports that need to be sent.  Since each report is sent via an async method
        /// and we want to do one at a time, a queue works well because we can check it after each
        /// send completes and send the next queued report until the queue is empty.
        /// </summary>
        Queue<string> ReportQueue;

        /// <summary>
        /// Check the <see cref="ReportQueue"/> and send the next one, if any
        /// </summary>
        void ReportNextInQueue ()
        {
            if (ReportQueue.TryDequeue (out var filename)) {
                Report (filename, ReportNextInQueue);
            }
        }

        /// <summary>
        /// Report a given crash to an external service
        /// </summary>
        /// <remarks>
        /// The reporting is done on a background task to avoid any filesystem reads or http work on the main thread
        /// </remarks>
        /// <param name="filename">Filename.</param>
        /// <param name="complete">Complete.</param>
        void Report (string filename, Action complete)
        {
            NcTask.Run (() => {
                if (CrashReport.TryLoad (filename, out var report)) {
                    Log.LOG_UTILS.Info ("Attempting to report crash log: {0}", filename);
                    GithubSession.Shared.CreateIssue (report, (exception) => {
                        if (exception == null) {
                            try {
                                File.Delete (filename);
                            } catch (FileNotFoundException) {
                            }
                            Log.LOG_UTILS.Info ("Successfully reported crash log: {0}", filename);
                        } else {
                            Log.LOG_UTILS.Warn ("Could not create ticket for crash log: {0}", exception);
                        }
                        NachoPlatform.InvokeOnUIThread.Instance.Invoke (complete);
                    });
                } else {
                    Log.LOG_UTILS.Warn ("Could not open crashlog at: {0}", filename);
                    try {
                        File.Delete (filename);
                    } catch (FileNotFoundException) {
                    }
                    NachoPlatform.InvokeOnUIThread.Instance.Invoke (complete);
                }
            }, "CrashReporter.Report");
        }

        #endregion

        #region Log Capturing

        /// <summary>
        /// Typically called by the <see cref="Log"/> system, this allows the crash reporter
        /// to remember recent logs and include them in a crash report.
        /// </summary>
        /// <param name="level">The log level</param>
        /// <param name="category">The log category/tag</param>
        /// <param name="fmt">The log message, with optional format placeholder</param>
        /// <param name="args">The format arguments for the log message, if any</param>
        public void ReceiveLog (Log.Level level, string category, string fmt, object [] args)
        {
            if (level == Log.Level.Debug) {
                return;
            }
            var record = new LogRecord {
                Level = level,
                Category = category,
                Message = fmt,
                Arguments = args,
                ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId
            };
            LogQueue.Enqueue (record);
            if (LogLimit > 0) {
                while (LogQueue.Count > LogLimit) {
                    LogQueue.TryDequeue (out var _);
                }
            }
        }

        /// <summary>
        /// The maximum number of logs to remember in a crash report.
        /// </summary>
        /// <remarks>
        /// This number of 100 was just a guess at what might work.  We may adjust as needed after some real
        /// world experience.
        /// </remarks>
        int LogLimit = 100;
        readonly ConcurrentQueue<LogRecord> LogQueue = new ConcurrentQueue<LogRecord> ();

        /// <summary>
        /// A log record, with the log message and metadata.  Since we'll be receiving every log message,
        /// but only writing those that come just before a crash, the idea is to not waste time
        /// formatting a log message until we actually need to write it out during a crash report.
        /// So the LogRecord remembers the log information without doing any work up front.
        /// </summary>
        class LogRecord
        {
            public Log.Level Level;
            public string Category;
            public string Message;
            public object [] Arguments;
            public DateTime Timestamp = DateTime.UtcNow;
            public int ThreadId;

            public string FormattedMessage {
                get {
                    if (Arguments.Length == 0) {
                        return Message;
                    }
                    return string.Format (Message, Arguments);
                }
            }

            public string Line {
                get {
                    return string.Format ("{0} [{1,6}] {2,-5} {3} {4}", Timestamp.ToString ("O"), ThreadId, Level, Category, FormattedMessage);
                }
            }
        }

        /// <summary>
        /// Get the log messages that have been remembered, formatted and timestamped
        /// </summary>
        /// <returns>The logs.</returns>
        string [] GetLogs ()
        {
            var records = new List<LogRecord> (LogQueue);
            records.Sort ((x, y) => {
                return y.Timestamp.CompareTo (x.Timestamp);
            });
            return records.Select (r => r.Line).ToArray ();
        }

        #endregion
    }

    /// <summary>
    /// A single crash report with the exception and log information related to the crash
    /// </summary>
    public class CrashReport
    {

        const int LatestVersion = 1;

        public int Version = LatestVersion;
        public DateTime Timestamp = DateTime.UtcNow;
        public string Exception;
        public string Message;
        public string [] Logs;
        public string [] Stack;
        public string Platform;
        public string NachoVersion;

        #region Creating a Crash Report

        /// <summary>
        /// Create a crash report from an exception and collection of logs
        /// </summary>
        /// <param name="exception">Exception.</param>
        /// <param name="logs">Logs.</param>
        public CrashReport (Exception exception, string [] logs)
        {
            Exception = exception.GetType ().ToString ();
            Message = exception.Message;
            Logs = logs;
            Stack = exception.NachoStack ();
            // Android crashes if we try to evaluate Device.Os() here, so we'll just use a cached
            // values since it never changes while the app runs
            Platform = CrashReporter.Platform;
            NachoVersion = CrashReporter.NachoVersion;
        }

        /// <summary>
        /// Private default constructor so only the public constructors are availble
        /// </summary>
        CrashReport ()
        {
        }

        #endregion

        #region Serialization

        const string VersionJsonKey = "version";
        const string TimestampJsonKey = "timestamp";
        const string ExceptionJsonKey = "exception";
        const string MessageJsonKey = "message";
        const string LogsJsonKey = "logs";
        const string StackJsonKey = "stacktrace";
        const string PlatformJsonKey = "platform";
        const string NachoVersionJsonKey = "nacho_version";

        /// <summary>
        /// Read a crash report from disk
        /// </summary>
        /// <returns><c>true</c>, if file could be read, <c>false</c> otherwise.</returns>
        /// <param name="path">The file to load</param>
        /// <param name="report">The loaded crash report</param>
		public static bool TryLoad (string path, out CrashReport report)
        {
            report = null;
            try {
                using (var stream = new FileStream (path, FileMode.Open, FileAccess.Read)) {
                    using (var fileReader = new StreamReader (stream)) {
                        using (var reader = new JsonTextReader (fileReader)) {
                            reader.DateParseHandling = DateParseHandling.None;
                            var crashData = JToken.ReadFrom (reader);
                            if (!(crashData is JObject)) {
                                return false;
                            }
                            var crashObj = crashData as JObject;
                            if (!crashObj.TryGetValue (VersionJsonKey, out var version) || version.Type != JTokenType.Integer) {
                                return false;
                            }
                            report = new CrashReport ();
                            if (version.ToObject<int> () == 1) {
                                if (!crashObj.TryGetValue (PlatformJsonKey, out var platform) || platform.Type != JTokenType.String) {
                                    return false;
                                }
                                report.Platform = platform.ToObject<string> ();
                                if (!crashObj.TryGetValue (NachoVersionJsonKey, out var nachoVersion) || nachoVersion.Type != JTokenType.String) {
                                    return false;
                                }
                                report.NachoVersion = nachoVersion.ToObject<string> ();
                                if (!crashObj.TryGetValue (ExceptionJsonKey, out var exception) || exception.Type != JTokenType.String) {
                                    return false;
                                }
                                report.Exception = exception.ToObject<string> ();
                                if (!crashObj.TryGetValue (MessageJsonKey, out var message) || message.Type != JTokenType.String) {
                                    return false;
                                }
                                report.Message = message.ToObject<string> ();
                                if (!crashObj.TryGetValue (TimestampJsonKey, out var timestamp) || timestamp.Type != JTokenType.String) {
                                    return false;
                                }
                                if (!DateTime.TryParseExact (timestamp.ToObject<string> (), "O", null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)) {
                                    return false;
                                }
                                report.Timestamp = dt;
                                var logs = new List<string> ();
                                if (!crashObj.TryGetValue (LogsJsonKey, out var logsToken) || logsToken.Type != JTokenType.Array) {
                                    return false;
                                }
                                foreach (var token in logsToken.ToObject<JArray> ()) {
                                    if (token.Type != JTokenType.String) {
                                        return false;
                                    }
                                    logs.Add (token.ToObject<string> ());
                                }
                                report.Logs = logs.ToArray ();
                                var stack = new List<string> ();
                                if (!crashObj.TryGetValue (StackJsonKey, out var stackToken) || logsToken.Type != JTokenType.Array) {
                                    return false;
                                }
                                foreach (var token in stackToken.ToObject<JArray> ()) {
                                    if (token.Type != JTokenType.String) {
                                        return false;
                                    }
                                    stack.Add (token.ToObject<string> ());
                                }
                                report.Stack = stack.ToArray ();
                                return true;
                            } else {
                                return false;
                            }
                        }
                    }
                }
            } catch (FileNotFoundException) {
                return false;
            }
        }

        /// <summary>
        /// Save the crash report to disk
        /// </summary>
        /// <param name="parentFolder">The folder in which to save the crash report.  A timestamp based filename will be generated automatically.</param>
        public void Save (string parentFolder)
        {
            var filename = Timestamp.ToString ("u").Replace (':', '-') + ".nachocrash";
            var path = Path.Combine (parentFolder, filename);
            if (!Directory.Exists (parentFolder)) {
                Directory.CreateDirectory (parentFolder);
            }
            using (var stream = new FileStream (path, FileMode.OpenOrCreate, FileAccess.Write)) {
                using (var fileWriter = new StreamWriter (stream)) {
                    using (var writer = new JsonTextWriter (fileWriter)) {
                        writer.WriteStartObject ();
                        writer.WritePropertyName (VersionJsonKey);
                        writer.WriteValue (Version);
                        writer.WritePropertyName (PlatformJsonKey);
                        writer.WriteValue (Platform);
                        writer.WritePropertyName (NachoVersionJsonKey);
                        writer.WriteValue (NachoVersion);
                        writer.WritePropertyName (ExceptionJsonKey);
                        writer.WriteValue (Exception);
                        writer.WritePropertyName (MessageJsonKey);
                        writer.WriteValue (Message);
                        writer.WritePropertyName (TimestampJsonKey);
                        writer.WriteValue (Timestamp.ToString ("O"));
                        writer.WritePropertyName (LogsJsonKey);
                        writer.WriteStartArray ();
                        foreach (var log in Logs) {
                            writer.WriteValue (log);
                        }
                        writer.WriteEndArray ();
                        writer.WritePropertyName (StackJsonKey);
                        writer.WriteStartArray ();
                        foreach (var line in Stack) {
                            writer.WriteValue (line);
                        }
                        writer.WriteEndArray ();
                        writer.WriteEndObject ();
                    }
                }
            }
        }

        #endregion

        public override string ToString ()
        {
            var lines = new List<string> ();
            lines.Add ("Nacho v" + NachoVersion);
            lines.Add (Platform);
            lines.Add (Timestamp.ToString ("O"));
            lines.Add ("");
            lines.Add (string.Format ("{0}: {1}", Exception, Message));
            lines.AddRange (Stack);
            lines.Add ("");
            lines.AddRange (Logs);
            return string.Join ("\n", lines);
        }
    }

    public static class ExceptionExtensions
    {
        /// <summary>
        /// Get the stack trace as an array of lines, including lines for inner exceptions
        /// </summary>
        /// <returns>The stack.</returns>
        /// <param name="exception">Exception.</param>
        public static string [] NachoStack (this Exception exception)
        {
            var stack = new List<string> ();
            stack.AddRange ((exception.StackTrace ?? "").Split ('\n'));
            var inner = exception.InnerException;
            while (inner != null) {
                stack.Add (string.Format ("-- InnerException: {0}: {1}", inner.GetType ().ToString (), inner.Message));
                stack.AddRange ((inner.StackTrace ?? "").Split ('\n'));
                inner = inner.InnerException;
            }
            return stack.ToArray ();
        }
    }
}
