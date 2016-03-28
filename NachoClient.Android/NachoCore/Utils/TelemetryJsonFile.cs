//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace NachoCore.Utils
{
    public partial class Telemetry
    {
        /// <summary>
        /// Represents a single json file, regardless of type (support, log, etc)
        /// </summary>
        public class TelemetryJsonFile
        {
            protected static Regex TimestampRegex;

            /// <summary>
            /// The 'file handle'
            /// </summary>
            protected FileStream JsonFile;

            /// <summary>
            /// Where the file is
            /// </summary>
            /// <value>The file path.</value>
            public string FilePath { get; protected set; }

            public TelemetryJsonFile (string filePath)
            {
                FilePath = filePath;
            }

            DateTime _FirstTimestamp = DateTime.MinValue;

            public DateTime FirstTimestamp {
                get {
                    Initialize ();
                    return _FirstTimestamp;
                }
            }

            DateTime _LatestTimestamp = DateTime.MinValue;

            public DateTime LatestTimestamp {
                get {
                    Initialize ();
                    return _LatestTimestamp;
                }
            }

            int _NumberOfEntries;

            public int NumberOfEntries {
                get {
                    Initialize ();
                    return _NumberOfEntries;
                }
            }

            object LockObj = new object ();
            object InitLockObj = new object ();
            // Analysis disable once MemberHidesStaticFromOuterClass
            bool Initialized;

            void Initialize ()
            {
                if (!Initialized) {
                    lock (InitLockObj) {
                        if (!Initialized) {
                            if (!File.Exists (FilePath)) {
                                // Analysis disable once BitwiseOperatorOnEnumWithoutFlags
                                JsonFile = File.Open (FilePath, FileMode.Create | FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                            } else {
                                // Open the JSON file for read and count how many entries. Also, extract the time stamp
                                var tmpFile = File.Open (FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                                using (var reader = new StreamReader (tmpFile)) {
                                    string line;
                                    while ((line = reader.ReadLine ()) != null) {
                                        if (String.IsNullOrEmpty (line)) {
                                            continue;
                                        }
                                        DateTime lastDateTime;
                                        try {
                                            lastDateTime = ExtractTimestamp (line);
                                        } catch (FormatException ex) {
                                            Console.WriteLine ("Bad timestamp in file: {0}", line);
                                            continue;
                                        }
                                        if (0 == _NumberOfEntries) {
                                            _FirstTimestamp = lastDateTime;
                                        }
                                        _LatestTimestamp = lastDateTime;
                                        _NumberOfEntries += 1;
                                    }
                                }
                                tmpFile.Close ();
                                JsonFile = File.Open (FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                                JsonFile.Seek (0, SeekOrigin.End);
                            }
                            Initialized = true;
                        }
                    }
                }
            }

            protected DateTime ExtractTimestamp (string line)
            {
                if (null == TimestampRegex) {
                    TimestampRegex = new Regex (@"""timestamp""\s*:\s*""([^""]+)""", RegexOptions.Compiled);
                }
                // Extract the timestamp. Since they can be different type of JSON object,
                // just use regex to grab the tick value.
                var match = TimestampRegex.Match (line);
                if (!match.Success || (2 != match.Groups.Count)) {
                    throw new FormatException (String.Format ("invalid timestamp in JSON {0}", line));
                }
                return TelemetryJsonEvent.Timestamp (match.Groups [1].Value);
            }

            public bool Add (TelemetryJsonEvent jsonEvent)
            {
                lock (LockObj) {
                    Initialize ();
                    if (null == jsonEvent) {
                        return false;
                    }
                    var timestamp = jsonEvent.Timestamp ();
                    if (0 == _NumberOfEntries) {
                        _FirstTimestamp = timestamp;
                    }
                    _LatestTimestamp = timestamp;

                    bool succeeded;
                    try {
                        byte[] bytes = Encoding.UTF8.GetBytes (jsonEvent.ToJson () + "\n");
                        JsonFile.Write (bytes, 0, bytes.Length);
                        _NumberOfEntries += 1;
                        succeeded = true;
                        JsonFile.Flush (true);
                    } catch (IOException e) {
                        Console.WriteLine ("fail to write a telemetry JSON event ({0})", e);
                        succeeded = false;
                    } catch (UnauthorizedAccessException ex) {
                        Console.WriteLine ("UnauthorizedAccessException: {0}\n{1}", JsonFile.Name, ex);
                        dumpCrashInfo ();
                        throw;
                    }
                    return succeeded;
                }
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
                lock (LockObj) {
                    lock (InitLockObj) {
                        if (null != JsonFile) {
                            JsonFile.Close ();
                            JsonFile = null;
                        }
                    }
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
            /// <summary>
            /// None
            /// </summary>
            None,
            /// <summary>
            /// ERROR, WARN, INFO, DEBUG
            /// </summary>
            Log,
            /// <summary>
            /// WBXML_REQUEST, WBXML_RESPONSE, IMAP_REQUEST, IMAP_RESPONSE
            /// </summary>
            Protocol,
            /// <summary>
            /// UI
            /// </summary>
            Ui,
            /// <summary>
            /// SUPPORT
            /// </summary>
            Support,
            /// <summary>
            /// SAMPLES
            /// </summary>
            Samples,
            /// <summary>
            /// STATISTICS2
            /// </summary>
            Statistics2,
            /// <summary>
            /// DISTRIBUTION
            /// </summary>
            Distribution,
            /// <summary>
            /// COUNTER
            /// </summary>
            Counter,
            /// <summary>
            /// TIME_SERIES
            /// </summary>
            Time_Series,
            /// <summary>
            /// SUPPORT_REQUEST
            /// </summary>
            Support_Request,
        };
    }
}
