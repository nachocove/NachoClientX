//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NachoCore.Utils
{
    public class TelemetryJsonFile
    {
        protected FileStream JsonFile;

        protected static Regex TimestampRegex;

        public string FilePath { get; protected set; }

        public DateTime FirstTimestamp { get; protected set; }

        public DateTime LatestTimestamp { get; protected set; }

        public int NumberOfEntries { get; protected set; }

        public TelemetryJsonFile (string filePath)
        {
            FilePath = filePath;
            FirstTimestamp = DateTime.MinValue;
            LatestTimestamp = DateTime.MinValue;

            if (!File.Exists (FilePath)) {
                // Analysis disable once BitwiseOperatorOnEnumWithoutFlags
                JsonFile = File.Open (FilePath, FileMode.Create | FileMode.Append, FileAccess.Write);
            } else {
                // Open the JSON file for read and count how many entries. Also, extract the time stamp
                var tmpFile = File.Open (FilePath, FileMode.Open, FileAccess.Read);
                using (var reader = new StreamReader (tmpFile)) {
                    string lastLine = null;
                    var line = reader.ReadLine ();
                    while (!String.IsNullOrEmpty (line)) {
                        if (0 == NumberOfEntries) {
                            FirstTimestamp = ExtractTimestamp (line);
                        }
                        NumberOfEntries += 1;
                        lastLine = line;
                        line = reader.ReadLine ();
                    }
                    if (null != lastLine) {
                        LatestTimestamp = ExtractTimestamp (lastLine);
                    }
                }
                tmpFile.Close ();
                JsonFile = File.Open (FilePath, FileMode.Append, FileAccess.Write);
                JsonFile.Seek (0, SeekOrigin.End);
            }
        }

        protected DateTime ExtractTimestamp (string line)
        {
            // Extract the timestamp. Since they can be different type of JSON object,
            // just use regex to grab the tick value.
            if (null == TimestampRegex) {
                TimestampRegex = new Regex (@"""timestamp""\s*:\s*""([^""]+)""");
            }
            var match = TimestampRegex.Match (line);
            if (!match.Success || (2 != match.Groups.Count)) {
                throw new FormatException (String.Format ("invalid timestamp in JSON {0}", line));
            }
            return TelemetryJsonEvent.Timestamp (match.Groups [1].Value);
        }

        public bool Add (TelemetryJsonEvent jsonEvent)
        {
            if (null == jsonEvent) {
                return false;
            }
            var timestamp = jsonEvent.Timestamp ();
            if (0 == NumberOfEntries) {
                FirstTimestamp = timestamp;
            }
            LatestTimestamp = timestamp;

            bool succeeded;
            try {
                byte[] bytes = Encoding.UTF8.GetBytes (jsonEvent.ToJson () + "\n");
                JsonFile.Write (bytes, 0, bytes.Length);
                NumberOfEntries += 1;
                succeeded = true;
                JsonFile.Flush (true);
            } catch (IOException e) {
                Log.Warn (Log.LOG_UTILS, "fail to write a telemetry JSON event ({0})", e);
                succeeded = false;
            } catch (UnauthorizedAccessException ex) {
                Console.WriteLine ("UnauthorizedAccessException: {0}\n{1}", JsonFile.Name, ex);
                dumpCrashInfo ();
                throw;
            }
            return succeeded;
        }

        void dumpCrashInfo ()
        {
            if (!File.Exists (JsonFile.Name)) {
                Console.WriteLine ("TelemetryJsonFile {0} has disappeared", JsonFile.Name);
                string dirName = Path.GetDirectoryName (JsonFile.Name);
                if (!Directory.Exists (dirName)) {
                    Console.WriteLine ("TelemetryJsonFile parent dir {0} has disappeared", dirName);
                } else {
                    DirectoryInfo d = new DirectoryInfo (dirName);
                    FileInfo[] Files = d.GetFiles ();
                    foreach (FileInfo file in Files) {
                        Console.WriteLine ("File in {0}: {1} size {2}", dirName, file.Name, file.Length);
                    }
                }
            } else {
                Console.WriteLine ("TelemetryJsonFile {0} has size {1}", JsonFile.Name, new FileInfo (JsonFile.Name).Length);
            }
        }

        public void Close ()
        {
            if (null != JsonFile) {
                JsonFile.Close ();
                JsonFile = null;
            }
        }
    }

    /// <summary>
    /// TelemetryEventType are mapped into TelemetryEventClass. Event types with the same parameters
    /// can (and are) mapped into the same event class. This is done to aggregate different types of
    /// events into the same JSON file in order to reduce the number of S3 API calls (and hence
    /// reducing cost).
    /// 
    /// Event types 
    /// </summary>
    public enum TelemetryEventClass
    {
        None,
        // ERROR, WARN, INFO, DEBUG
        Log,
        // WBXML_REQUEST, WBXML_RESPONSE, IMAP_REQUEST, IMAP_RESPONSE
        Protocol,
        // UI
        Ui,
        // SUPPORT
        Support,
        // SAMPLES
        Samples,
        // STATISTICS2
        Statistics2,
        // DISTRIBUTION
        Distribution,
        // COUNTER
        Counter,
        // TIME_SERIES
        Time_Series,
        // SUPPORT_REQUEST
        Support_Request,
    };

    public class TelemetryJsonFileTable
    {
        public delegate DateTime TelemetryJsonFileTableDateTimeFunc ();

        // TODO - The long-term value of this should be one day. Keep it small for now
        //        to get T3 to upload more frequently. Will re-adjust when T3 stabilizes.
        public static long MAX_DURATION = 5 * TimeSpan.TicksPerMinute;

        // TODO - The long-term value of this should be like 100,000.
        public static int MAX_EVENTS = 500;

        protected static TelemetryJsonFileTableDateTimeFunc GetNowUtc = DefaultGetUtcNow;

        protected Dictionary<TelemetryEventClass, TelemetryJsonFile> WriteFiles;
        protected SortedSet<string> ReadFiles;
        protected Dictionary<string, Action> PendingCallbacks;
        protected object LockObj;

        protected static DateTime DefaultGetUtcNow ()
        {
            return DateTime.UtcNow;
        }

        protected static string GetFilePath (TelemetryEventClass eventClass)
        {
            return Path.Combine (NcApplication.GetDataDirPath (), eventClass.ToString ().ToLowerInvariant ());
        }

        protected static TelemetryEventClass GetEventClass (string eventType)
        {
            TelemetryEventClass eventClass;
            switch (eventType) {
            case TelemetryLogEvent.ERROR:
            case TelemetryLogEvent.WARN:
            case TelemetryLogEvent.INFO:
            case TelemetryLogEvent.DEBUG:
                eventClass = TelemetryEventClass.Log;
                break;
            case TelemetryProtocolEvent.WBXML_REQUEST:
            case TelemetryProtocolEvent.WBXML_RESPONSE:
            case TelemetryProtocolEvent.IMAP_REQUEST:
            case TelemetryProtocolEvent.IMAP_RESPONSE:
                eventClass = TelemetryEventClass.Protocol;
                break;
            case TelemetryUiEvent.UI:
                eventClass = TelemetryEventClass.Ui;
                break;
            case TelemetrySupportEvent.SUPPORT:
                eventClass = TelemetryEventClass.Support;
                break;
            case TelemetrySamplesEvent.SAMPLES:
                eventClass = TelemetryEventClass.Samples;
                break;
            case TelemetryStatistics2Event.STATISTICS2:
                eventClass = TelemetryEventClass.Statistics2;
                break;
            case TelemetryDistributionEvent.DISTRIBUTION:
                eventClass = TelemetryEventClass.Distribution;
                break;
            case TelemetryCounterEvent.COUNTER:
                eventClass = TelemetryEventClass.Counter;
                break;
            case TelemetryTimeSeriesEvent.TIME_SERIES:
                eventClass = TelemetryEventClass.Time_Series;
                break;
            case TelemetrySupportRequestEvent.SUPPORT_REQUEST:
                eventClass = TelemetryEventClass.Support_Request;
                break;
            default:
                var msg = String.Format ("GetEventClass: unknown type {0}", eventType);
                throw new NcAssert.NachoDefaultCaseFailure (msg);
            }
            return eventClass;
        }

        public static List<string> GetReadFile (string filePath)
        {
            var dirName = Path.GetDirectoryName (filePath);
            var suffix = Path.GetFileName (filePath);
            var regex = new Regex (@"^([0-9]+)\.([0-9]+)\.");
            var readFilePaths = new List<string> ();
            foreach (var fileName in Directory.GetFiles(dirName)) {
                if (!fileName.EndsWith (suffix)) {
                    continue;
                }
                if (regex.Match (Path.GetFileName (fileName)).Success) {
                    readFilePaths.Add (fileName);
                }
            }
            return readFilePaths;
        }

        protected static string FormatTimestamp (DateTime timestamp)
        {
            return String.Format ("{0:D04}{1:D2}{2:D2}{3:D2}{4:D2}{5:D2}{6:D3}",
                timestamp.Year, timestamp.Month, timestamp.Day,
                timestamp.Hour, timestamp.Minute, timestamp.Second, timestamp.Millisecond);
        }

        /// <summary>
        /// Responsible for JSON files management. There are two types of files:
        /// 1. Write files - Telemetry events are converted to JSON format and written to these files.
        ///     Write files are still opened for data being appended to it. When the file is finalized,
        ///     its file name is prefixed with the the timestamps of the first and last event.
        ///     At this point, a write file becomes a read file.
        /// 
        /// 2. Read files - They are closed files to be uploaded to S3. When upload is complete, the file is deleted.
        ///     No new read file for a given event class can be created until the existing one is deleted. The
        ///     read file is piped into a GZIP stream to convert to a gzip file during the upload.
        /// </summary>
        public TelemetryJsonFileTable ()
        {
            LockObj = new object ();
            WriteFiles = new Dictionary<TelemetryEventClass, TelemetryJsonFile> ();
            ReadFiles = new SortedSet<string> ();
            PendingCallbacks = new Dictionary<string, Action> ();

            var eventClasses = AllEventClasses ();
            foreach (var eventClass in eventClasses) {
                var filePath = GetFilePath (eventClass);
                if (File.Exists (filePath)) {
                    TelemetryJsonFile jsonFile = null;
                    try {
                        jsonFile = new TelemetryJsonFile (filePath);
                    } catch (Exception e) {
                        // JSON table is not initialized. Can log to console
                        Console.WriteLine ("Fail to open JSON file {0} (exception={1})", filePath, e);
                    }
                    if (null != jsonFile) {
                        WriteFiles.Add (eventClass, jsonFile);
                    } else {
                        File.Delete (filePath); // this file must be somehow in bad state. Delete it
                    }
                }
                var readFilePaths = GetReadFile (filePath);
                foreach (var readFilePath in readFilePaths) {
                    ReadFiles.Add (readFilePath);
                }
            }
        }

        public static IEnumerable<TelemetryEventClass> AllEventClasses ()
        {
            return Enum.GetValues (typeof(TelemetryEventClass)).Cast<TelemetryEventClass> ();
        }

        public bool Add (TelemetryJsonEvent jsonEvent)
        {
            lock (LockObj) {
                TelemetryJsonFile writeFile;
                var eventClass = GetEventClass (jsonEvent.event_type);
                if (!WriteFiles.TryGetValue (eventClass, out writeFile)) {
                    writeFile = new TelemetryJsonFile (GetFilePath (eventClass));
                    WriteFiles.Add (eventClass, writeFile);
                }

                bool doFinalize = false;
                var now = GetNowUtc ();
                if (0 < writeFile.NumberOfEntries) {
                    if (writeFile.LatestTimestamp.Day != now.Day) {
                        doFinalize = true; // do not allow JSON file to span more than one day
                    }
                    if ((jsonEvent.Timestamp () - writeFile.FirstTimestamp).Ticks > MAX_DURATION) {
                        doFinalize = true; // larger than max duration
                    }
                }
                if (doFinalize) {
                    Finalize (eventClass);
                    writeFile = new TelemetryJsonFile (GetFilePath (eventClass));
                    WriteFiles.Add (eventClass, writeFile);
                }

                if (!writeFile.Add (jsonEvent)) {
                    // TODO - Probably need to reset the file
                    return false;
                } else {
                    doFinalize = false;
                    if ((TelemetryEventClass.Support == eventClass) || (TelemetryEventClass.Support_Request == eventClass)) {
                        doFinalize = true; // always finalize AdSupport FileShare after each write
                        // Since each SUPPORT event is its own file, it is possible that two quick
                        // back-to-back SUPPORT events have the same timestamp down to millisecond resolution.
                        // This would result in two read JSON files with the same name and File.Move()
                        // would throw an exception. To mitigate this bug, force a millisecond sleep.
                        // There are a few SUPPORT events sent during the initialization of telemetry
                        // task but otherwise, it is rather infrequent. So, we should be able to afford
                        // this busy wait.
                        Thread.Sleep (1);
                    }
                    if (writeFile.NumberOfEntries >= MAX_EVENTS) {
                        doFinalize = true;
                    }
                    if (doFinalize) {
                        Finalize (eventClass, jsonEvent.callback);
                    }
                }
                return true;
            }
        }

        public static string GetReadFilePath (string filePath, DateTime start, DateTime end)
        {
            var startTimestamp = FormatTimestamp (start);
            var endTimestamp = FormatTimestamp (end);
            var dirName = Path.GetDirectoryName (filePath);
            var fileName = Path.GetFileName (filePath);

            var newFileName = startTimestamp + "." + endTimestamp + "." + fileName;
            return Path.Combine (dirName, newFileName);
        }

        protected void Finalize (TelemetryEventClass eventClass, Action callback = null)
        {
            lock (LockObj) {
                TelemetryJsonFile writeFile;
                if (!WriteFiles.TryGetValue (eventClass, out writeFile)) {
                    return;
                }
                if (0 == writeFile.NumberOfEntries) {
                    NcAssert.True (writeFile.FirstTimestamp == DateTime.MinValue);
                    return;
                }

                NcAssert.True ((DateTime.MinValue != writeFile.FirstTimestamp) && (DateTime.MinValue != writeFile.LatestTimestamp));
                WriteFiles.Remove (eventClass);
                writeFile.Close ();

                var newFilePath = GetReadFilePath (writeFile.FilePath, writeFile.FirstTimestamp, writeFile.LatestTimestamp);
                File.Move (writeFile.FilePath, newFilePath);

                ReadFiles.Add (newFilePath);
                if (null != callback) {
                    PendingCallbacks.Add (newFilePath, callback);
                }
            }
        }

        public void FinalizeAll ()
        {
            lock (LockObj) {
                foreach (var eventClass in AllEventClasses()) {
                    Finalize (eventClass);
                }
            }
        }

        public string GetNextReadFile ()
        {
            lock (LockObj) {
                if (0 == ReadFiles.Count) {
                    return null;
                }
                return ReadFiles.Min;
            }
        }

        public void Remove (string path, out Action callback)
        {
            lock (LockObj) {
                callback = null;
                try {
                    File.Delete (path);
                } catch (IOException) {
                }
                ReadFiles.Remove (path);
                PendingCallbacks.TryGetValue (path, out callback);
            }
        }
    }
}

