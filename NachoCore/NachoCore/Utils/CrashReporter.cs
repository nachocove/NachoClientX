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

    public class CrashReporter
    {

        public static readonly CrashReporter Instance = new CrashReporter (DefaultCrashFolder);
        public static string Platform = NachoPlatform.Device.Instance.Os ();
        public static string NachoVersion = NcApplication.GetVersionString ();

        static string DefaultCrashFolder {
            get {
                return Path.Combine (NcApplication.GetDataDirPath (), "crashes");
            }
        }

        readonly string CrashFolder;

        CrashReporter (string crashFolder)
        {
            CrashFolder = crashFolder;
        }

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
                ExceptionHandler (e.Exception);
            };
        }

        public void ExceptionHandler (Exception e)
        {
            var logs = GetLogs ();
            var report = new CrashReport (e, logs);
            report.Save (CrashFolder);
        }

        public void ReportCrashes ()
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

        Queue<string> ReportQueue;

        void Report (string [] filenames)
        {
            ReportQueue = new Queue<string> (filenames);
            ReportNextInQueue ();
        }

        void ReportNextInQueue ()
        {
            if (ReportQueue.TryDequeue (out var filename)) {
                Report (filename, ReportNextInQueue);
            }
        }

        void Report (string filename, Action complete)
        {
            NcTask.Run (() => {
                if (CrashReport.TryLoad (filename, out var report)) {
                    Log.LOG_UTILS.Info ("Attempting to report crash log: {0}", filename);
                    FreshdeskSession.Shared.CreateTicket (report, (exception) => {
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

        int LogLimit = 100;
        readonly ConcurrentQueue<LogRecord> LogQueue = new ConcurrentQueue<LogRecord> ();

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

        string [] GetLogs ()
        {
            var records = new List<LogRecord> (LogQueue);
            records.Sort ((x, y) => {
                return y.Timestamp.CompareTo (x.Timestamp);
            });
            return records.Select (r => r.Line).ToArray ();
        }
    }

    public class CrashReport
    {

        const string VersionJsonKey = "version";
        const string TimestampJsonKey = "timestamp";
        const string ExceptionJsonKey = "exception";
        const string MessageJsonKey = "message";
        const string LogsJsonKey = "logs";
        const string StackJsonKey = "stacktrace";
        const string PlatformJsonKey = "platform";
        const string NachoVersionJsonKey = "nacho_version";

        const int LatestVersion = 1;
        public int Version = LatestVersion;
        public DateTime Timestamp = DateTime.UtcNow;
        public string Exception;
        public string Message;
        public string [] Logs;
        public string [] Stack;
        public string Platform;
        public string NachoVersion;

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

        CrashReport ()
        {
        }

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
