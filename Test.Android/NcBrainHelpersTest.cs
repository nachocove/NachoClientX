//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using NachoCore.Brain;
using NachoCore.Utils;
using NachoCore.Model;

namespace Test.Common
{
    public class TestSource<T> : List<T>
    {
        public static T Current { get; protected set; }

        public List<object> Query (int count)
        {
            var objects = new List<object> ();
            for (int n = 0; n < count; n++) {
                if (0 == Count) {
                    break;
                }
                objects.Add (this [0]);
                RemoveAt (0);
            }
            return objects;
        }

        public bool Process (object obj)
        {
            Current = (T)obj;
            return true;
        }
    }

    public class WrappedRoundRobinList : RoundRobinList
    {
        public List<RoundRobinList.ScheduleOrder> GetSchedule ()
        {
            return Schedule;
        }

        public List<RoundRobinList.RoundRobinListRecord> GetRecords ()
        {
            return Sources;
        }
    }

    public class NcBrainHelpersTest
    {
        int OriginalStartupDelayMsec;

        List<NcResult.SubKindEnum> NotificationsReceived;

        // Use sufficiently high fake account ids so they can never collide with actual account ids
        const int Account1 = 1000;
        const int Account2 = 1001;

        [SetUp]
        public void SetUp ()
        {
            OriginalStartupDelayMsec = NcBrain.StartupDelayMsec;
            NcBrain.StartupDelayMsec = 0;

            NotificationsReceived = new List<NcResult.SubKindEnum> ();
        }

        [TearDown]
        public void TearDown ()
        {
            NcBrain.StartupDelayMsec = OriginalStartupDelayMsec;
            SafeDirectoryDelete (NcModel.Instance.GetIndexPath (Account1));
            SafeDirectoryDelete (NcModel.Instance.GetIndexPath (Account2));
        }

        protected void SafeDirectoryDelete (string dirPath)
        {
            try {
                Directory.Delete (dirPath, true);
            } catch (IOException) {
            }
        }

        [Test]
        public void TestOpenedIndexSet ()
        {
            var openedIndexes = new OpenedIndexSet (NcBrain.SharedInstance);
            Assert.AreEqual (0, openedIndexes.Count);

            var index1 = openedIndexes.Get (Account1);
            Assert.NotNull (index1);
            Assert.AreEqual (1, openedIndexes.Count);
            Assert.True (index1.IsWriting);

            var index2 = openedIndexes.Get (Account2);
            Assert.NotNull (index2);
            Assert.AreEqual (2, openedIndexes.Count);
            Assert.True (index2.IsWriting);

            openedIndexes.Cleanup ();
            Assert.AreEqual (0, openedIndexes.Count);
            Assert.False (index1.IsWriting);
            Assert.False (index2.IsWriting);

            // Open 2nd account again
            var index2b = openedIndexes.Get (Account2);
            Assert.NotNull (index2b);
            Assert.AreEqual (1, openedIndexes.Count);
            Assert.True (index2b.IsWriting);
            Assert.True (index2.IsWriting);
            Assert.False (index1.IsWriting);

            openedIndexes.Cleanup ();
            Assert.AreEqual (0, openedIndexes.Count);
            Assert.False (index1.IsWriting);
            Assert.False (index2.IsWriting);
            Assert.False (index2b.IsWriting);
        }

        [Test]
        public void TestRoundRobinSource ()
        {
            var names = new string[] {
                "alan",
                "bob",
                "charles",
                "david",
                "ellen",
            };
            var namesList = new List<string> (names);
            string currentName = null;
            int chunkSize = 3;
            var source =
                new RoundRobinSource (
                    (count) => {
                        var objects = new List<object> ();
                        for (int n = 0; n < count; n++) {
                            if (0 == namesList.Count) {
                                break;
                            }
                            objects.Add (namesList [0]);
                            namesList.RemoveAt (0);
                        }
                        return objects;
                    },
                    (obj) => {
                        var s = (string)obj;
                        currentName = s.ToUpper ();
                        return ("charles" != s) && ("ellen" != s);
                    }, chunkSize);

            Assert.AreEqual (0, source.NumberOfObjects);
            bool processResult = false;
            bool ran = source.Process (out processResult);
            Assert.True (ran);
            Assert.AreEqual (2, source.NumberOfObjects);
            Assert.AreEqual (names [0].ToUpper (), currentName);
            Assert.True (processResult);

            ran = source.Process (out processResult);
            Assert.True (ran);
            Assert.AreEqual (1, source.NumberOfObjects);
            Assert.AreEqual (names [1].ToUpper (), currentName);
            Assert.True (processResult);

            ran = source.Process (out processResult);
            Assert.True (ran);
            Assert.AreEqual (0, source.NumberOfObjects);
            Assert.AreEqual (names [2].ToUpper (), currentName);
            Assert.False (processResult);

            ran = source.Process (out processResult);
            Assert.True (ran);
            Assert.AreEqual (1, source.NumberOfObjects);
            Assert.AreEqual (names [3].ToUpper (), currentName);
            Assert.True (processResult);

            ran = source.Process (out processResult);
            Assert.True (ran);
            Assert.AreEqual (0, source.NumberOfObjects);
            Assert.AreEqual (names [4].ToUpper (), currentName);
            Assert.False (processResult);

            ran = source.Process (out processResult);
            Assert.False (ran);
            Assert.AreEqual (0, source.NumberOfObjects);
        }

        [Test]
        public void TestRoundRobinList ()
        {
            // Test RoundRobinList.RoundRobinListRecord
            var numberSource = new TestSource<int> () { 1, 2 };
            var source = new RoundRobinSource (numberSource.Query, numberSource.Process, 5);
            var record = new RoundRobinList.RoundRobinListRecord ("TestRoundRobinList", source, 4);
            record.Initialize ();
            Assert.False (record.IsEmpty);

            // Verify the schedule
            var objects = new List<RoundRobinList.ScheduleOrder> ();
            record.AddToSchedule (objects);
            Assert.AreEqual (4, objects.Count);
            Assert.AreEqual (0.0, objects [0].Order);
            Assert.AreEqual (0.25, objects [1].Order);
            Assert.AreEqual (0.5, objects [2].Order);
            Assert.AreEqual (0.75, objects [3].Order);

            // Process 1st object
            bool processResult, ran;
            processResult = false;
            ran = record.Run (out processResult);
            Assert.True (ran && processResult);

            // Process 2nd object
            processResult = false;
            ran = record.Run (out processResult);
            Assert.True (ran && processResult);

            // Should not have more object to process
            processResult = false;
            ran = record.Run (out processResult);
            Assert.True (!ran && !processResult);

            // Verify that empty source produces empty schedule
            objects = new List<RoundRobinList.ScheduleOrder> ();
            record.Initialize ();
            Assert.True (record.IsEmpty);
            record.AddToSchedule (objects);
            Assert.AreEqual (0, objects.Count);

            // Test RoundRobinList
            var list2 = new TestSource<int> () { 100, 200 };
            var list3 = new TestSource<int> () { 10, 20, 30 };
            var list6 = new TestSource<int> () { 1, 2, 3, 4, 5, 6 };
            var source2 = new RoundRobinSource (list2.Query, list2.Process, 5);
            var source3 = new RoundRobinSource (list3.Query, list3.Process, 5);
            var source6 = new RoundRobinSource (list6.Query, list6.Process, 5);

            var rrList = new WrappedRoundRobinList ();
            rrList.Add ("list2", source2, 2);
            rrList.Add ("list3", source3, 3);
            rrList.Add ("list6", source6, 6);

            // Initialize and verify the schedule
            rrList.Initialize ();
            var schedule = rrList.GetSchedule ();
            var records = rrList.GetRecords ();
            var id2 = records [0].Id;
            var id3 = records [1].Id;
            var id6 = records [2].Id;
            var expectedSchedule = new List<KeyValuePair<double,int>> () {
                new KeyValuePair<double,int> (0.0 / 6.0, id2),
                new KeyValuePair<double,int> (0.0 / 6.0, id3),
                new KeyValuePair<double,int> (0.0 / 6.0, id6),
                new KeyValuePair<double,int> (1.0 / 6.0, id6),
                new KeyValuePair<double,int> (2.0 / 6.0, id3),
                new KeyValuePair<double,int> (2.0 / 6.0, id6),
                new KeyValuePair<double,int> (3.0 / 6.0, id2),
                new KeyValuePair<double,int> (3.0 / 6.0, id6),
                new KeyValuePair<double,int> (4.0 / 6.0, id3),
                new KeyValuePair<double,int> (4.0 / 6.0, id6),
                new KeyValuePair<double,int> (5.0 / 6.0, id6),
            };
            Assert.AreEqual (expectedSchedule.Count, schedule.Count);
            for (int n = 0; n < expectedSchedule.Count; n++) {
                Assert.AreEqual (expectedSchedule [n].Key, schedule [n].Order);
                Assert.AreEqual (expectedSchedule [n].Value, schedule [n].Id);
            }

            List<int> expectedList = new List<int> () {
                100, 10, 1, // 0
                2, // 1/6
                20, 3, // 2/6
                200, 4, // 3/6
                30, 5, // 4/6
                6, // 5/6
            };
            foreach (var expected in expectedList) {
                processResult = false;
                ran = rrList.Run (out processResult);
                Assert.True (ran && processResult);
                Assert.AreEqual (expected, TestSource<int>.Current);
            }
        }

        private void NotificationAction (NcResult.SubKindEnum type)
        {
            NotificationsReceived.Add (type);
        }

        [Test]
        public void TestNotificationRateLimiter ()
        {
            NcBrainNotification notif = new NcBrainNotification ();
            notif.Action = NotificationAction;

            Assert.True (notif.Running); // enabled by default

            // Send 1st notification - must receive
            notif.NotifyUpdates (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated);
            Assert.AreEqual (1, NotificationsReceived.Count);
            Assert.AreEqual (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated, NotificationsReceived [0]);
            NotificationsReceived.Clear ();

            // Wait KMinDurationMSec and send again - must received
            Thread.Sleep (NcBrainNotification.KMinDurationMsec + 200);
            var now = DateTime.Now;
            notif.NotifyUpdates (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated);
            Assert.AreEqual (1, NotificationsReceived.Count);
            Assert.AreEqual (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated, NotificationsReceived [0]);
            NotificationsReceived.Clear ();

            // Keep sending within the next KMinDurationMSec
            int count = 0;
            Assert.True (100 < NcBrainNotification.KMinDurationMsec);
            while ((DateTime.Now - now).TotalMilliseconds < (NcBrainNotification.KMinDurationMsec - 100)) {
                notif.NotifyUpdates (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated);
                count++;
            }
            Assert.True (0 < count); // should send at least one
            Assert.AreEqual (0, NotificationsReceived.Count); // must not get any
            NotificationsReceived.Clear ();
            Thread.Sleep (500); // make sure we are passed the KMinDurationMsec

            // Send two different types of notifications
            notif.NotifyUpdates (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated);
            notif.NotifyUpdates (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated);
            Assert.AreEqual (2, NotificationsReceived.Count);
            Assert.AreEqual (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated, NotificationsReceived [0]);
            Assert.AreEqual (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated, NotificationsReceived [1]);
            NotificationsReceived.Clear ();

            // Disable, send two notifications, enable
            notif.Running = false;
            notif.NotifyUpdates (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated);
            notif.NotifyUpdates (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated);
            Assert.AreEqual (0, NotificationsReceived.Count);
            notif.Running = true;
            Assert.AreEqual (2, NotificationsReceived.Count);
            if (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated == NotificationsReceived [0]) {
                Assert.AreEqual (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated, NotificationsReceived [1]);
            } else {
                Assert.AreEqual (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated, NotificationsReceived [0]);
                Assert.AreEqual (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated, NotificationsReceived [0]);
            }
        }
    }
}
