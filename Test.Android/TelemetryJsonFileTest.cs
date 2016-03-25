//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json;
using NachoCore;
using NachoCore.Utils;
using System.Text;

namespace Test.Common
{
    public class WrappedTelemetryJsonFileTable : Telemetry.TelemetryJsonFileTable
    {
        public static DateTime UtcNow;

        public Dictionary<Telemetry.TelemetryEventClass, Telemetry.TelemetryJsonFile> GetWriteFiles ()
        {
            return WriteFiles;
        }

        public Telemetry.TelemetryJsonFile GetWriteFile (Telemetry.TelemetryEventClass eventClass)
        {
            Telemetry.TelemetryJsonFile jsonFile;
            if (WriteFiles.TryGetValue (eventClass, out jsonFile)) {
                return jsonFile;
            }
            return null;
        }

        public SortedSet<string> GetReadFiles ()
        {
            return ReadFiles;
        }

        protected static DateTime MockGetUtcNow ()
        {
            return UtcNow;
        }

        public static void SetMockUtcNow (bool doMock)
        {
            if (doMock) {
                Telemetry.TelemetryJsonFileTable.GetNowUtc = MockGetUtcNow;
            } else {
                Telemetry.TelemetryJsonFileTable.GetNowUtc = DefaultGetUtcNow;
            }
        }
    }

    public class TelemetryJsonFileTest : NcTestBase
    {
        protected WrappedTelemetryJsonFileTable FileTable;

        protected int OriginalMaxEvents;

        protected long OriginalMaxDuration;

        protected void DeleteFiles ()
        {
            // Delete all JSON files
            var files = Directory.GetFiles (NcApplication.GetDataDirPath ());
            foreach (var eventClass in Telemetry.TelemetryJsonFileTable.AllEventClasses) {
                var eventClassString = eventClass.ToString ().ToLowerInvariant ();
                foreach (var file in files) {
                    if (file.Contains (eventClassString)) {
                        Console.WriteLine ("Deleting {0}", file);
                        File.Delete (file);
                    }
                }
            }
        }

        [SetUp]
        public void Setup ()
        {
            OriginalMaxEvents = Telemetry.TelemetryJsonFileTable.MAX_EVENTS;
            OriginalMaxDuration = Telemetry.TelemetryJsonFileTable.MAX_DURATION;

            DeleteFiles ();
            WrappedTelemetryJsonFileTable.SetMockUtcNow (true);
        }

        [TearDown]
        public void Teardown ()
        {
            Telemetry.TelemetryJsonFileTable.MAX_EVENTS = OriginalMaxEvents;
            Telemetry.TelemetryJsonFileTable.MAX_DURATION = OriginalMaxDuration;

            DeleteFiles ();
            if (null != FileTable) {
                foreach (var jsonFile in FileTable.GetWriteFiles().Values) {
                    jsonFile.Close ();
                }
                FileTable = null;
            }
            WrappedTelemetryJsonFileTable.SetMockUtcNow (false);
        }

        protected void CompareLogEvents (TelemetryLogEvent a, TelemetryLogEvent b)
        {
            Assert.AreEqual (a.id, b.id);
            Assert.AreEqual (a.client, b.client);
            Assert.AreEqual (a.event_type, b.event_type);
            Assert.AreEqual (a.thread_id, b.thread_id);
            Assert.AreEqual (a.message, b.message);
        }

        protected long TimestampTicks (string timestamp)
        {
            return TelemetryJsonEvent.Timestamp (timestamp).Ticks;
        }

        [Test]
        public void TestTelemetryJsonFile ()
        {
            var filePath = Path.Combine (NcApplication.GetDataDirPath (), "log");

            // Create the file. Check it is in the expected initial state with nothing
            var jsonFile = new Telemetry.TelemetryJsonFile (filePath);
            Assert.AreEqual (DateTime.MinValue, jsonFile.FirstTimestamp);
            Assert.AreEqual (DateTime.MinValue, jsonFile.LatestTimestamp);
            Assert.AreEqual (0, jsonFile.NumberOfEntries);
            Assert.True (File.Exists (filePath));

            // Write an error log
            var event1 = new TelemetryLogEvent (TelemetryEventType.ERROR) {
                thread_id = 2,
                message = "This is an error",
            };
            bool added = jsonFile.Add (event1);
            Assert.True (added);
            Assert.AreEqual (TimestampTicks (event1.timestamp), jsonFile.FirstTimestamp.Ticks);
            Assert.AreEqual (TimestampTicks (event1.timestamp), jsonFile.LatestTimestamp.Ticks);
            Assert.AreEqual (1, jsonFile.NumberOfEntries);

            // Write a warning log
            var event2 = new TelemetryLogEvent (TelemetryEventType.WARN) {
                thread_id = 3,
                message = "This is a warning",
            };
            added = jsonFile.Add (event2);
            Assert.True (added);
            Assert.AreEqual (TimestampTicks (event1.timestamp), jsonFile.FirstTimestamp.Ticks);
            Assert.AreEqual (TimestampTicks (event2.timestamp), jsonFile.LatestTimestamp.Ticks);
            Assert.AreEqual (2, jsonFile.NumberOfEntries);

            // Write an info log
            var event3 = new TelemetryLogEvent (TelemetryEventType.INFO) {
                thread_id = 4,
                message = "This is an info log",
            };
            added = jsonFile.Add (event3);
            Assert.True (added);
            Assert.AreEqual (TimestampTicks (event1.timestamp), jsonFile.FirstTimestamp.Ticks);
            Assert.AreEqual (TimestampTicks (event3.timestamp), jsonFile.LatestTimestamp.Ticks);
            Assert.AreEqual (3, jsonFile.NumberOfEntries);

            // Close the file and re-open it. Make sure the timestamp and # entries are correct
            jsonFile.Close ();
            jsonFile = new Telemetry.TelemetryJsonFile (filePath);
            Assert.AreEqual (TimestampTicks (event1.timestamp), jsonFile.FirstTimestamp.Ticks);
            Assert.AreEqual (TimestampTicks (event3.timestamp), jsonFile.LatestTimestamp.Ticks);
            Assert.AreEqual (3, jsonFile.NumberOfEntries);

            // Close it again. Read it line-by-line and make sure the content is correct
            jsonFile.Close ();
            using (var file = File.Open (filePath, FileMode.Open, FileAccess.Read)) {
                using (var stream = new StreamReader (file)) {
                    var json1 = stream.ReadLine ();
                    var decode1 = JsonConvert.DeserializeObject<TelemetryLogEvent> (json1);
                    CompareLogEvents (event1, decode1);

                    var json2 = stream.ReadLine ();
                    var decode2 = JsonConvert.DeserializeObject<TelemetryLogEvent> (json2);
                    CompareLogEvents (event2, decode2);

                    var json3 = stream.ReadLine ();
                    var decode3 = JsonConvert.DeserializeObject<TelemetryLogEvent> (json3);
                    CompareLogEvents (event3, decode3);
                }
            }
        }

        protected void AddEventAndCheck (TelemetryJsonEvent jsonEvent, bool shouldAdd = true, int numEntries = 1)
        {
            bool added = FileTable.Add (jsonEvent);
            Assert.True (added);

            var writeFiles = FileTable.GetWriteFiles ();
            Telemetry.TelemetryJsonFile jsonFile;
            bool found = writeFiles.TryGetValue (WrappedTelemetryJsonFileTable.GetEventClass (jsonEvent.event_type), out jsonFile);
            Assert.AreEqual (shouldAdd, found);
            if (shouldAdd) {
                CheckJsonFile (jsonFile, numEntries, jsonEvent);
            }
        }

        protected void CheckJsonFile (Telemetry.TelemetryJsonFile jsonFile, int numEntries, TelemetryJsonEvent jsonEvent)
        {
            Assert.AreEqual (numEntries, jsonFile.NumberOfEntries);
            if (1 == numEntries) {
                Assert.AreEqual (TimestampTicks (jsonEvent.timestamp), jsonFile.FirstTimestamp.Ticks);
            } else {
                Assert.AreNotEqual (TimestampTicks (jsonEvent.timestamp), jsonFile.FirstTimestamp.Ticks);
            }
            Assert.AreEqual (TimestampTicks (jsonEvent.timestamp), jsonFile.LatestTimestamp.Ticks);
        }

        protected void CheckWriteFilesCount (int expected)
        {
            Assert.AreEqual (expected, FileTable.GetWriteFiles ().Count);
        }

        protected void CheckReadFilesCount (int expected)
        {
            Assert.AreEqual (expected, FileTable.GetReadFiles ().Count);
        }

        [Test]
        public void TestTelemetryJsonFileTable ()
        {
            FileTable = new WrappedTelemetryJsonFileTable ();
            CheckWriteFilesCount (0);
            CheckReadFilesCount (0);
            Telemetry.TelemetryJsonFileTable.MAX_DURATION = long.MaxValue; // disable duration check temporarily

            // Add a log event. This should create the log file
            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 1, 2, 3, 456);
            var event1 = new TelemetryLogEvent (TelemetryEventType.INFO) {
                timestamp = TelemetryJsonEvent.AwsDateTime (WrappedTelemetryJsonFileTable.UtcNow),
                thread_id = 2,
                message = "This is an info",
            };
            AddEventAndCheck (event1);
            CheckWriteFilesCount (1);
            CheckReadFilesCount (0);

            // Add a UI event, This should create the UI JSON file
            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 2, 2, 3, 456);
            var event2 = new TelemetryUiEvent () {
                timestamp = TelemetryJsonEvent.AwsDateTime (WrappedTelemetryJsonFileTable.UtcNow),
                ui_type = "UIButton",
                ui_object = "OK",
            };
            AddEventAndCheck (event2);
            CheckWriteFilesCount (2);
            CheckReadFilesCount (0);

            // Add a WBXML event. This should create the WBXML JSON file
            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 3, 0, 0, 001);
            var event3 = new TelemetryProtocolEvent () {
                timestamp = TelemetryJsonEvent.AwsDateTime (WrappedTelemetryJsonFileTable.UtcNow),
                payload = new byte[3] { 0x11, 0x22, 0x33 },
            };
            AddEventAndCheck (event3);
            CheckWriteFilesCount (3);
            CheckReadFilesCount (0);

            // Add a samples event. This should create the samples JSON file
            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 4, 0, 0, 000);
            var event4 = new TelemetrySamplesEvent () {
                timestamp = TelemetryJsonEvent.AwsDateTime (WrappedTelemetryJsonFileTable.UtcNow),
                sample_name = "Process_memory",
                sample_int = 90,
            };
            AddEventAndCheck (event4);
            CheckWriteFilesCount (4);
            CheckReadFilesCount (0);

            // Add a statistics2 event. This should create the statistics2 file
            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 11, 0, 0, 22);
            var event5 = new TelemetryStatistics2Event () {
                timestamp = TelemetryJsonEvent.AwsDateTime (WrappedTelemetryJsonFileTable.UtcNow),
                stat2_name = "McEmailAddress_Insert",
                count = 100,
                min = 10,
                max = 1000,
                sum = 15000,
                sum2 = 3000000,
            };
            AddEventAndCheck (event5);
            CheckWriteFilesCount (5);
            CheckReadFilesCount (0);

            // Add a support event. This should cause the support file to finalize immediately
            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 12, 50, 47, 000);
            var event6 = new TelemetrySupportEvent () {
                timestamp = TelemetryJsonEvent.AwsDateTime (WrappedTelemetryJsonFileTable.UtcNow),
                support = "Help!",
            };
            AddEventAndCheck (event6, shouldAdd: false);
            CheckWriteFilesCount (5);
            CheckReadFilesCount (1);

            var readFile = FileTable.GetNextReadFile ();
            Assert.AreEqual ("20150526125047000.20150526125047000.support", Path.GetFileName (readFile));

            // Add another info log just to make sure the end time field is different from start time
            // and it is being filled in correctly.
            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 12, 55, 0, 101);
            var event7 = new TelemetryLogEvent (TelemetryEventType.INFO) {
                timestamp = TelemetryJsonEvent.AwsDateTime (WrappedTelemetryJsonFileTable.UtcNow),
                thread_id = 10,
                message = "Another info log",
            };
            AddEventAndCheck (event7, numEntries: 2);
            CheckWriteFilesCount (5);
            CheckReadFilesCount (1);

            FileTable.FinalizeAll ();
            CheckWriteFilesCount (0);
            CheckReadFilesCount (6);

            // Check all read files
            string[] expectedReadFiles = new [] {
                "20150526010203456.20150526125500101.log",
                "20150526020203456.20150526020203456.ui",
                "20150526030000001.20150526030000001.protocol",
                "20150526040000000.20150526040000000.samples",
                "20150526110000022.20150526110000022.statistics2",
                "20150526125047000.20150526125047000.support",
            };
            var readFiles = FileTable.GetReadFiles ();
            Assert.AreEqual (expectedReadFiles.Length, readFiles.Count);
            int n = 0;
            foreach (var rf in readFiles) {
                Assert.AreEqual (expectedReadFiles [n], Path.GetFileName (rf));
                n += 1;
            }

            // Get a new file table. Make sure the ctor recovers all read files
            FileTable = new WrappedTelemetryJsonFileTable ();
            readFiles = FileTable.GetReadFiles ();
            Assert.AreEqual (expectedReadFiles.Length, readFiles.Count);
            n = 0;
            foreach (var rf in readFiles) {
                Assert.AreEqual (expectedReadFiles [n], Path.GetFileName (rf));
                n += 1;
            }

            // Read each read file.
            Action dummyCallback;
            foreach (var expected in expectedReadFiles) {
                readFile = FileTable.GetNextReadFile ();
                Assert.AreEqual (expected, Path.GetFileName (readFile));
                Assert.True (File.Exists (readFile));
                FileTable.Remove (readFile, out dummyCallback);
                Assert.False (File.Exists (readFile));
            }

            // Set the max entries to 3. Write 4 entries and expect a new read file
            CheckReadFilesCount (0);
            CheckWriteFilesCount (0);
            WrappedTelemetryJsonFileTable.MAX_EVENTS = 4;

            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 13, 1, 0, 0, DateTimeKind.Utc);
            bool added = FileTable.Add (new TelemetryLogEvent (TelemetryEventType.INFO) {
                timestamp = TelemetryJsonEvent.AwsDateTime (WrappedTelemetryJsonFileTable.UtcNow),
                thread_id = 10,
                message = "This message should not generate a read file",
            });
            Assert.True (added);
            CheckReadFilesCount (0);
            CheckWriteFilesCount (1);
            Assert.AreEqual (1, FileTable.GetWriteFile (Telemetry.TelemetryEventClass.Log).NumberOfEntries);

            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 13, 2, 0, 0, DateTimeKind.Utc);
            added = FileTable.Add (new TelemetryLogEvent (TelemetryEventType.INFO) {
                timestamp = TelemetryJsonEvent.AwsDateTime (WrappedTelemetryJsonFileTable.UtcNow),
                thread_id = 11,
                message = "This message should not generate a read file",
            });
            Assert.True (added);
            CheckReadFilesCount (0);
            CheckWriteFilesCount (1);
            Assert.AreEqual (2, FileTable.GetWriteFile (Telemetry.TelemetryEventClass.Log).NumberOfEntries);

            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 13, 3, 0, 0);
            added = FileTable.Add (new TelemetryLogEvent (TelemetryEventType.INFO) {
                timestamp = TelemetryJsonEvent.AwsDateTime (WrappedTelemetryJsonFileTable.UtcNow),
                thread_id = 12,
                message = "This message should not generate a read file",
            });
            Assert.True (added);
            CheckReadFilesCount (0);
            CheckWriteFilesCount (1);
            Assert.AreEqual (3, FileTable.GetWriteFile (Telemetry.TelemetryEventClass.Log).NumberOfEntries);

            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 13, 4, 0, 0);
            added = FileTable.Add (new TelemetryLogEvent (TelemetryEventType.INFO) {
                timestamp = TelemetryJsonEvent.AwsDateTime (WrappedTelemetryJsonFileTable.UtcNow),
                thread_id = 13,
                message = "This message should generate a read file",
            });
            Assert.True (added);
            CheckReadFilesCount (1);
            CheckWriteFilesCount (0);
            readFile = FileTable.GetNextReadFile ();
            Assert.AreEqual ("20150526130100000.20150526130400000.log", Path.GetFileName (readFile));
            FileTable.Remove (readFile, out dummyCallback);

            // Set the max duration to 5 mins. Write 2 entries 5m0.001s apart and expect a new read file
            CheckReadFilesCount (0);
            CheckWriteFilesCount (0);

            Telemetry.TelemetryJsonFileTable.MAX_DURATION = 5 * TimeSpan.TicksPerMinute;
            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 13, 5, 0, 0);
            added = FileTable.Add (new TelemetryLogEvent (TelemetryEventType.INFO) {
                timestamp = TelemetryJsonEvent.AwsDateTime (WrappedTelemetryJsonFileTable.UtcNow),
                thread_id = 14,
                message = "This message should not generate a read file",
            });
            Assert.True (added);
            CheckReadFilesCount (0);
            CheckWriteFilesCount (1);
            Assert.AreEqual (1, FileTable.GetWriteFile (Telemetry.TelemetryEventClass.Log).NumberOfEntries);

            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 13, 10, 0, 1);
            added = FileTable.Add (new TelemetryLogEvent (TelemetryEventType.INFO) {
                timestamp = TelemetryJsonEvent.AwsDateTime (WrappedTelemetryJsonFileTable.UtcNow),
                thread_id = 15,
                message = "This message should generate a read file",
            });
            Assert.True (added);
            CheckReadFilesCount (1);
            CheckWriteFilesCount (1);
            readFile = FileTable.GetNextReadFile ();
            Assert.AreEqual ("20150526130500000.20150526130500000.log", Path.GetFileName (readFile));
        }

        [Test]
        public void TestTelemetryJsonFileError ()
        {
            // Manually an invalid JSON file and tries to instantiate a JSON file from it.
            var filePath = Path.Combine (NcApplication.GetDataDirPath (), "log");
            var jsonEvent = new TelemetryLogEvent (TelemetryEventType.INFO) {
                thread_id = 1,
                message = "This event has an invalid timestamp",
                timestamp = "INVALID",
            };
            var json = JsonConvert.SerializeObject (jsonEvent);
            using (var file = File.Open (filePath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite)) {
                var bytes = Encoding.UTF8.GetBytes (JsonConvert.SerializeObject (jsonEvent) + "\n");
                file.Write (bytes, 0, bytes.Length);
            }

            Assert.True (File.Exists (filePath));
            var jsonFile = new Telemetry.TelemetryJsonFile (filePath);
            Assert.AreEqual (DateTime.MinValue, jsonFile.FirstTimestamp);
            Assert.AreEqual (DateTime.MinValue, jsonFile.LatestTimestamp);
            Assert.AreEqual (0, jsonFile.NumberOfEntries);
            jsonFile.Close ();
            File.Delete (filePath);

            DateTime first;
            DateTime last;
            using (var file = File.Open (filePath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite)) {
                first = DateTime.UtcNow;
                jsonEvent = new TelemetryLogEvent (TelemetryEventType.INFO) {
                    thread_id = 1,
                    message = "Some message",
                    timestamp = TelemetryJsonEvent.AwsDateTime (first),
                };
                var bytes = Encoding.UTF8.GetBytes (JsonConvert.SerializeObject (jsonEvent) + "\n");
                file.Write (bytes, 0, bytes.Length);

                jsonEvent = new TelemetryLogEvent (TelemetryEventType.INFO) {
                    thread_id = 1,
                    message = "This event has an invalid timestamp",
                    timestamp = "INVALID",
                };
                bytes = Encoding.UTF8.GetBytes (JsonConvert.SerializeObject (jsonEvent) + "\n");
                file.Write (bytes, 0, bytes.Length);

                last = DateTime.UtcNow;
                jsonEvent = new TelemetryLogEvent (TelemetryEventType.INFO) {
                    thread_id = 1,
                    message = "Some other message",
                    timestamp = TelemetryJsonEvent.AwsDateTime (last),
                };
                bytes = Encoding.UTF8.GetBytes (JsonConvert.SerializeObject (jsonEvent) + "\n");
                file.Write (bytes, 0, bytes.Length);
            }
            jsonFile = new Telemetry.TelemetryJsonFile (filePath);
            Assert.AreEqual (first.ToString (), jsonFile.FirstTimestamp.ToString ());
            Assert.AreEqual (last.ToString (), jsonFile.LatestTimestamp.ToString ());
            Assert.AreEqual (2, jsonFile.NumberOfEntries);
            jsonFile.Close ();
        }
    }
}
