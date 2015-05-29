//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

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
                TimestampRegex = new Regex (@"""timestamp""\s*:\s*([0-9]+)");
            }
            var match = TimestampRegex.Match (line);
            NcAssert.True (match.Success);
            NcAssert.True (2 == match.Groups.Count);
            return new DateTime (long.Parse (match.Groups [1].Value), DateTimeKind.Utc);
        }

        public bool Add (TelemetryJsonEvent jsonEvent)
        {
            if (null == jsonEvent) {
                return false;
            }
            var timestamp = new DateTime (jsonEvent.timestamp, DateTimeKind.Utc);
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
            }
            return succeeded;
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
        // WBXML_REQUEST, WBXML_RESPONSE
        Wbxml,
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
    };

    public class TelemetryJsonFileTable
    {
        public delegate DateTime TelemetryJsonFileTableDateTimeFunc ();

        // TODO - The long-term value of this should be one day. Keep it small for now
        //        to get T3 to upload more frequently. Will re-adjust when T3 stabilizes.
        public const long MAX_DURATION = 1 * TimeSpan.TicksPerMinute;

        // TODO - The long-term value of this should be like 100,000.
        public const int MAX_EVENTS = 500;

        protected static TelemetryJsonFileTableDateTimeFunc GetNowUtc = DefaultGetUtcNow;

        protected Dictionary<TelemetryEventClass, TelemetryJsonFile> WriteFiles;
        protected SortedSet<string> ReadFiles;
        protected object LockObj;

        protected static DateTime DefaultGetUtcNow ()
        {
            return DateTime.UtcNow;
        }

        protected static string GetFilePath (TelemetryEventClass eventClass)
        {
            return Path.Combine (NcApplication.GetDataDirPath (), eventClass.ToString ().ToLower ());
        }

        protected static TelemetryEventClass GetEventClass (string eventType)
        {
            TelemetryEventClass eventClass = TelemetryEventClass.None;
            switch (eventType) {
            case TelemetryLogEvent.ERROR:
            case TelemetryLogEvent.WARN:
            case TelemetryLogEvent.INFO:
            case TelemetryLogEvent.DEBUG:
                eventClass = TelemetryEventClass.Log;
                break;
            case TelemetryWbxmlEvent.REQUEST:
            case TelemetryWbxmlEvent.RESPONSE:
                eventClass = TelemetryEventClass.Wbxml;
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

            var eventClasses = AllEventClasses ();
            foreach (var eventClass in eventClasses) {
                var filePath = GetFilePath (eventClass);
                if (File.Exists (filePath)) {
                    WriteFiles.Add (eventClass, new TelemetryJsonFile (filePath));
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
                if ((0 < writeFile.NumberOfEntries) && (writeFile.LatestTimestamp.Day != now.Day)) {
                    doFinalize = true; // do not allow JSON file to span more than one day
                }
                if ((now.Ticks - jsonEvent.timestamp) > MAX_DURATION) {
                    doFinalize = true; // larger than max duration
                }
                if (writeFile.NumberOfEntries > MAX_EVENTS) {
                    doFinalize = true; // large than max # of events
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
                    if (TelemetryEventClass.Support == eventClass) {
                        Finalize (eventClass); // always finalize support files after each write
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

        protected void Finalize (TelemetryEventClass eventClass)
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

        public void Remove (string path)
        {
            lock (LockObj) {
                try {
                    File.Delete (path);
                } catch (IOException) {
                }
                ReadFiles.Remove (path);
            }
        }
    }
}

