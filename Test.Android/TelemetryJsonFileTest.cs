//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json;
using NachoCore;
using NachoCore.Utils;

namespace Test.Common
{
    public class WrappedTelemetryJsonFileTable : TelemetryJsonFileTable
    {
        public static DateTime UtcNow;

        public Dictionary<TelemetryEventClass, TelemetryJsonFile> GetWriteFiles ()
        {
            return WriteFiles;
        }

        public SortedSet<string> GetReadFiles ()
        {
            return ReadFiles;
        }

        public new static TelemetryEventClass GetEventClass (string eventType)
        {
            return TelemetryJsonFileTable.GetEventClass (eventType);
        }

        protected static DateTime MockGetUtcNow ()
        {
            return UtcNow;
        }

        public static void SetMockUtcNow (bool doMock)
        {
            if (doMock) {
                TelemetryJsonFileTable.GetNowUtc = MockGetUtcNow;
            } else {
                TelemetryJsonFileTable.GetNowUtc = DefaultGetUtcNow;
            }
        }
    }

    public class TelemetryJsonFileTest
    {
        protected WrappedTelemetryJsonFileTable FileTable;

        protected void DeleteFiles ()
        {
            // Delete all JSON files
            var files = Directory.GetFiles (NcApplication.GetDataDirPath ());
            foreach (var eventClass in TelemetryJsonFileTable.AllEventClasses()) {
                var eventClassString = eventClass.ToString ().ToLower ();
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
            DeleteFiles ();
            WrappedTelemetryJsonFileTable.SetMockUtcNow (true);
        }

        [TearDown]
        public void Teardown ()
        {
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

        [Test]
        public void TestTelemetryJsonFile ()
        {
            var filePath = Path.Combine (NcApplication.GetDataDirPath (), "log");

            // Create the file. Check it is in the expected initial state with nothing
            var jsonFile = new TelemetryJsonFile (filePath);
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
            Assert.AreEqual (event1.timestamp, jsonFile.FirstTimestamp.Ticks);
            Assert.AreEqual (event1.timestamp, jsonFile.LatestTimestamp.Ticks);
            Assert.AreEqual (1, jsonFile.NumberOfEntries);

            // Write a warning log
            var event2 = new TelemetryLogEvent (TelemetryEventType.WARN) {
                thread_id = 3,
                message = "This is a warning",
            };
            added = jsonFile.Add (event2);
            Assert.True (added);
            Assert.AreEqual (event1.timestamp, jsonFile.FirstTimestamp.Ticks);
            Assert.AreEqual (event2.timestamp, jsonFile.LatestTimestamp.Ticks);
            Assert.AreEqual (2, jsonFile.NumberOfEntries);

            // Write an info log
            var event3 = new TelemetryLogEvent (TelemetryEventType.INFO) {
                thread_id = 4,
                message = "This is an info log",
            };
            added = jsonFile.Add (event3);
            Assert.True (added);
            Assert.AreEqual (event1.timestamp, jsonFile.FirstTimestamp.Ticks);
            Assert.AreEqual (event3.timestamp, jsonFile.LatestTimestamp.Ticks);
            Assert.AreEqual (3, jsonFile.NumberOfEntries);

            // Close the file and re-open it. Make sure the timestamp and # entries are correct
            jsonFile.Close ();
            jsonFile = new TelemetryJsonFile (filePath);
            Assert.AreEqual (event1.timestamp, jsonFile.FirstTimestamp.Ticks);
            Assert.AreEqual (event3.timestamp, jsonFile.LatestTimestamp.Ticks);
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
            TelemetryJsonFile jsonFile;
            bool found = writeFiles.TryGetValue (WrappedTelemetryJsonFileTable.GetEventClass (jsonEvent.event_type), out jsonFile);
            Assert.AreEqual (shouldAdd, found);
            if (shouldAdd) {
                CheckJsonFile (jsonFile, numEntries, jsonEvent);
            }
        }

        protected void CheckJsonFile (TelemetryJsonFile jsonFile, int numEntries, TelemetryJsonEvent jsonEvent)
        {
            Assert.AreEqual (numEntries, jsonFile.NumberOfEntries);
            if (1 == numEntries) {
                Assert.AreEqual (jsonEvent.timestamp, jsonFile.FirstTimestamp.Ticks);
            } else {
                Assert.AreNotEqual (jsonEvent.timestamp, jsonFile.FirstTimestamp.Ticks);
            }
            Assert.AreEqual (jsonEvent.timestamp, jsonFile.LatestTimestamp.Ticks);
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

            // Add a log event. This should create the log file
            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 1, 2, 3, 456);
            var event1 = new TelemetryLogEvent (TelemetryEventType.INFO) {
                timestamp = WrappedTelemetryJsonFileTable.UtcNow.Ticks,
                thread_id = 2,
                message = "This is an info",
            };
            AddEventAndCheck (event1);
            CheckWriteFilesCount (1);
            CheckReadFilesCount (0);

            // Add a UI event, This should create the UI JSON file
            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 2, 2, 3, 456);
            var event2 = new TelemetryUiEvent () {
                timestamp = WrappedTelemetryJsonFileTable.UtcNow.Ticks,
                ui_type = "UIButton",
                ui_object = "OK",
            };
            AddEventAndCheck (event2);
            CheckWriteFilesCount (2);
            CheckReadFilesCount (0);

            // Add a WBXML event. This should create the WBXML JSON file
            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 3, 0, 0, 001);
            var event3 = new TelemetryWbxmlEvent () {
                timestamp = WrappedTelemetryJsonFileTable.UtcNow.Ticks,
                wbxml = new byte[3] { 0x11, 0x22, 0x33 },
            };
            AddEventAndCheck (event3);
            CheckWriteFilesCount (3);
            CheckReadFilesCount (0);

            // Add a samples event. This should create the samples JSON file
            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 4, 0, 0, 000);
            var event4 = new TelemetrySamplesEvent () {
                timestamp = WrappedTelemetryJsonFileTable.UtcNow.Ticks,
                samples_name = "Process_memory",
                samples = new List<int> () { 90, 110, 70, 130 },
            };
            AddEventAndCheck (event4);
            CheckWriteFilesCount (4);
            CheckReadFilesCount (0);

            // Add a statistics2 event. This should create the statistics2 file
            WrappedTelemetryJsonFileTable.UtcNow = new DateTime (2015, 5, 26, 11, 0, 0, 22);
            var event5 = new TelemetryStatistics2Event () {
                timestamp = WrappedTelemetryJsonFileTable.UtcNow.Ticks,
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
                timestamp = WrappedTelemetryJsonFileTable.UtcNow.Ticks,
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
                timestamp = WrappedTelemetryJsonFileTable.UtcNow.Ticks,
                thread_id = 10,
                message = "Another info log",
            };
            AddEventAndCheck (event7, numEntries: 2);
            CheckWriteFilesCount (5);
            CheckReadFilesCount (1);

            FileTable.FinalizeAll ();
            CheckWriteFilesCount (0);
            CheckReadFilesCount (6);

            // Read and remove all read files
            string[] readFiles = new string[] {
                "20150526010203456.20150526125500101.log",
                "20150526020203456.20150526020203456.ui",
                "20150526030000001.20150526030000001.wbxml",
                "20150526040000000.20150526040000000.samples",
                "20150526110000022.20150526110000022.statistics2",
                "20150526125047000.20150526125047000.support",
            };

            foreach (var expected in readFiles) {
                readFile = FileTable.GetNextReadFile ();
                Assert.AreEqual (expected, Path.GetFileName (readFile));
                Assert.True (File.Exists (readFile));
                FileTable.Remove (readFile);
                Assert.False (File.Exists (readFile));
            }
        }
    }
}
