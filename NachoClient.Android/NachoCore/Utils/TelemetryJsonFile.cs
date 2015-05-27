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

            if (File.Exists (FilePath)) {
                JsonFile = File.Open (FilePath, FileMode.Create | FileMode.Append, FileAccess.Write);
            } else {
                JsonFile = File.Open (FilePath, FileMode.Open | FileMode.Append, FileAccess.Write);
                // Count how many lines;
                using (var reader = new StreamReader (JsonFile)) {
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
                    if (null == lastLine) {
                        LatestTimestamp = ExtractTimestamp (line);
                    }
                }
                JsonFile.Seek (0, SeekOrigin.End);
            }
        }

        protected DateTime ExtractTimestamp (string line)
        {
            // Extract the timestamp. Since they can be different type of JSON object,
            // just use regex to grab the tick value.
            if (null == TimestampRegex) {
                TimestampRegex = new Regex (@"timestamp=([0-9])+");
            }
            var match = TimestampRegex.Match (line);
            NcAssert.True (match.Success);
            NcAssert.True (1 == match.Groups.Count);
            return DateTime.MinValue.AddTicks (long.Parse (match.Groups [0].Value));
        }

        public bool Add (TelemetryJsonEvent jsonEvent)
        {
            if (null == jsonEvent) {
                return false;
            }
            LatestTimestamp = DateTime.MinValue.AddTicks (jsonEvent.timestamp);

            bool succeeded;
            try {
                byte[] bytes = Encoding.ASCII.GetBytes (jsonEvent.ToJson ());
                JsonFile.Write (bytes, 0, bytes.Length);
                NumberOfEntries += 1;
                succeeded = true;
                JsonFile.Flush ();
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
            }
        }
    }

    public class TelemetryJsonFileTable
    {
        /// <summary>
        /// TelemetryEventType are mapped into TelemetryEventClass. Event types with the same parameters
        /// can (and are) mapped into the same event class. This is done to aggregate different types of
        /// events into the same JSON file in order to reduce the number of S3 API calls (and hence
        /// reducing cost).
        /// 
        /// Event types 
        /// </summary>
        protected enum TelemetryEventClass
        {
            None,
            // ERROR, WARN
            Error,
            // INFO, DEBUG
            Info,
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
        };

        public const long MAX_DURATION = 60 * TimeSpan.TicksPerSecond;

        public const int MAX_EVENTS = 1000000;

        protected Dictionary<TelemetryEventClass, TelemetryJsonFile> WriteFiles;
        protected SortedSet<string> ReadFiles;
        protected object LockObj;

        protected static string GetFilePath (TelemetryEventClass eventClass)
        {
            return Path.Combine (NcApplication.GetDataDirPath (), eventClass.ToString ());
        }

        protected static TelemetryEventClass GetEventClass (string eventType)
        {
            TelemetryEventClass eventClass = TelemetryEventClass.None;
            switch (eventType) {
            case "ERROR":
            case "WARN":
                eventClass = TelemetryEventClass.Error;
                break;
            case "INFO":
            case "DEBUG":
                eventClass = TelemetryEventClass.Info;
                break;
            case "WBXML_REQUEST":
            case "WBXML_RESPONSE":
                eventClass = TelemetryEventClass.Wbxml;
                break;
            case "UI":
                eventClass = TelemetryEventClass.Ui;
                break;
            case "SUPPORT":
                eventClass = TelemetryEventClass.Support;
                break;
            case "SAMPLES":
                eventClass = TelemetryEventClass.Samples;
                break;
            case "STATISTICS2":
                eventClass = TelemetryEventClass.Statistics2;
                break;
            }
            return eventClass;
        }

        public static List<string> GetReadFile (string prefix)
        {
            var dirName = Path.GetDirectoryName (prefix);
            var regex = new Regex (@"\.([0-9]+)\.([0-9]+)$");
            var readFilePaths = new List<string> ();
            foreach (var fileName in Directory.GetFiles(dirName)) {
                if (!fileName.StartsWith (prefix)) {
                    continue;
                }
                if (regex.Match (fileName).Success) {
                    readFilePaths.Add (fileName);
                }
            }
            return readFilePaths;
        }

        protected string FormatTimestamp (DateTime timestamp)
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

        protected IEnumerable<TelemetryEventClass> AllEventClasses ()
        {
            return Enum.GetValues (typeof(TelemetryEventClass)).Cast<TelemetryEventClass> ();
        }

        public void Add (TelemetryJsonEvent jsonEvent)
        {
            lock (LockObj) {
                TelemetryJsonFile writeFile;
                var eventClass = GetEventClass (jsonEvent.event_type);
                if (!WriteFiles.TryGetValue (eventClass, out writeFile)) {
                    WriteFiles.Add (eventClass, new TelemetryJsonFile (GetFilePath (eventClass)));
                }

                bool doFinalize = false;
                var now = DateTime.UtcNow;
                if (writeFile.LatestTimestamp.Day != now.Day) {
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
                }

                if (!writeFile.Add (jsonEvent)) {
                    // TODO - Probably need to reset the file
                } else {
                    if (TelemetryEventClass.Support == eventClass) {
                        Finalize (eventClass); // always finalize support files after each write
                    }
                }
            }
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

                var startTimestamp = FormatTimestamp (writeFile.FirstTimestamp);
                var endTimestamp = FormatTimestamp (writeFile.LatestTimestamp);
                var dirName = Path.GetDirectoryName (writeFile.FilePath);
                var fileName = Path.GetFileName (writeFile.FilePath);
                var newFileName = startTimestamp + "." + endTimestamp + "." + fileName;
                var newFilePath = Path.Combine (dirName, newFileName);
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

