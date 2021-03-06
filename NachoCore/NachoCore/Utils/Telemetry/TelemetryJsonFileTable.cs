﻿//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Linq;

namespace NachoCore.Utils
{
    public class TelemetryJsonFileTable : ITelementryDB
    {
        #region static stuff

        // TODO - The long-term value of this should be one day. Keep it small for now
        //        to get T3 to upload more frequently. Will re-adjust when T3 stabilizes.
        public static long MAX_DURATION = 5 * TimeSpan.TicksPerMinute;

        // TODO - The long-term value of this should be like 100,000.
        public static int MAX_EVENTS = 500;

        static Regex jsonFileRegEx;

        static IEnumerable<TelemetryEventClass> _AllEventClasses;

        public static IEnumerable<TelemetryEventClass> AllEventClasses {
            get {
                if (null == _AllEventClasses) {
                    _AllEventClasses = Enum.GetValues (typeof(TelemetryEventClass)).Cast<TelemetryEventClass> ().Where (x => x != TelemetryEventClass.None);
                }
                return _AllEventClasses;
            }
        }

        /// <summary>
        /// Overridden in tests.
        /// </summary>
        protected static TelemetryJsonFileTableDateTimeFunc GetNowUtc = DefaultGetUtcNow;

        public delegate DateTime TelemetryJsonFileTableDateTimeFunc ();

        protected static DateTime DefaultGetUtcNow ()
        {
            return DateTime.UtcNow;
        }

        protected static string FormatTimestamp (DateTime timestamp)
        {
            return String.Format ("{0:D04}{1:D2}{2:D2}{3:D2}{4:D2}{5:D2}{6:D3}",
                timestamp.Year, timestamp.Month, timestamp.Day,
                timestamp.Hour, timestamp.Minute, timestamp.Second, timestamp.Millisecond);
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

        /// <summary>
        /// Map the eventType into an TelemetryEventClass
        /// </summary>
        /// <returns>The TelemetryEventClass</returns>
        /// <param name="eventType">Event type.</param>
        public static TelemetryEventClass GetEventClass (string eventType)
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

        #endregion

        public void Initialize ()
        {
            // does nothing. Just a handy method to call on the instance to CREATE the shared instance
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
            WriteFiles = new Dictionary<TelemetryEventClass, TelemetryJsonFile> ();
            ReadFiles = new SortedSet<string> ();
            PendingCallbacks = new Dictionary<string, Action> ();

            foreach (var eventClass in AllEventClasses) {
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
                foreach (var readFilePath in GetReadFiles (filePath)) {
                    ReadFiles.Add (readFilePath);
                }
            }
        }

        protected Dictionary<TelemetryEventClass, TelemetryJsonFile> WriteFiles;
        protected SortedSet<string> ReadFiles;
        protected Dictionary<string, Action> PendingCallbacks;
        protected object LockObj = new object ();

        /// <summary>
        /// Create a ile path based on the eventClass.
        /// </summary>
        /// <remarks>Example: .../Documents/Data/support</remarks>
        /// <returns>The file path.</returns>
        /// <param name="eventClass">Event class.</param>
        protected string GetFilePath (TelemetryEventClass eventClass)
        {
            return Path.Combine (NcApplication.GetDataDirPath (), eventClass.ToString ().ToLowerInvariant ());
        }

        /// <summary>
        /// Given a filePath, return all files that start with numbers, and end in the event-type name,
        /// e.g. find all files in the directory like 20160324173253654.20160324173534188.log
        /// Do NOT find the gzip'ed files. Those are handled by a different part of the code.
        /// Also do NOT find the actual 'log' file (for example). It is also handled separately.
        /// </summary>
        /// <returns>The read file.</returns>
        /// <param name="filePath">File path.</param>
        public List<string> GetReadFiles (string filePath)
        {
            var dirName = Path.GetDirectoryName (filePath);
            var jsonFileNameSuffix = Path.GetFileName (filePath);
            var readFilePaths = new List<string> ();
            if (null == jsonFileRegEx) {
                jsonFileRegEx = new Regex (@"^([0-9]+)\.([0-9]+)\.", RegexOptions.Compiled);
            }
            foreach (var fileName in Directory.GetFiles(dirName, "*." + jsonFileNameSuffix)) {
                if (jsonFileRegEx.Match (Path.GetFileName (fileName)).Success) {
                    readFilePaths.Add (fileName);
                }
            }
            return readFilePaths;
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
                    FinalizeClass (eventClass);
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
                        FinalizeClass (eventClass, jsonEvent.callback);
                    }
                }
                return true;
            }
        }

        protected void FinalizeClass (TelemetryEventClass eventClass, Action callback = null)
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
                if (File.Exists (newFilePath)) {
                    // file exists from a previous incarnation. Delete it. Perhaps we crashed before the file could be uploaded
                    File.Delete (newFilePath);
                }
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
                foreach (var eventClass in AllEventClasses) {
                    FinalizeClass (eventClass);
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
