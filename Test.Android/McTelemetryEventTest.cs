//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

using NachoCore.Model;
using NachoCore.Utils;

namespace Test.Common
{
    public class McTelemetryEventTest : NcTestBase
    {
        protected const int NumEvents = 10;
        protected byte[][] Data;

        protected byte[] DataToBytes (int index)
        {
            return Encoding.ASCII.GetBytes (String.Format ("data{0}", index));
        }

        protected bool VerifyData (byte[] bytes, int index)
        {
            return bytes.SequenceEqual (DataToBytes (index));
        }

        [SetUp]
        protected new void SetUp ()
        {
            base.SetUp ();
            Assert.IsFalse (Telemetry.ENABLED, "Telemetry needs to be disabled");
            // Drain the telemetry event tables
            var dbEventList = McTelemetryEvent.QueryMultiple (1);
            while (0 < dbEventList.Count) {
                foreach (var dbEvent in dbEventList) {
                    dbEvent.Delete ();
                }
                dbEventList = McTelemetryEvent.QueryMultiple (1);
            }
            var dbEventList2 = McTelemetrySupportEvent.QueryOne ();
            while (0 < dbEventList2.Count) {
                foreach (var dbEvent2 in dbEventList2) {
                    dbEvent2.Delete ();
                }
                dbEventList2 = McTelemetrySupportEvent.QueryOne ();
            }
        }

        [Test]
        public void Purge ()
        {
            int numEvents = 10;
            // Make sure that we cleanly purge the table
            int originalCount = McTelemetryEvent.QueryCount ();
            Assert.AreEqual (0, originalCount);

            List<int> dbEventIds = new List<int> ();
            for (int n = 0; n < numEvents; n++) {
                var dbEvent = new McTelemetryEvent () {
                    Data = DataToBytes (n)
                };
                dbEvent.Insert ();
                dbEventIds.Add (dbEvent.Id);
            }
            Assert.AreEqual (numEvents, McTelemetryEvent.QueryCount ());

            // Purge 4 events
            int numLeft = 6;
            McTelemetryEvent.Purge<McTelemetryEvent> (numLeft);

            // Should have 6 events left
            Assert.AreEqual (numLeft, McTelemetryEvent.QueryCount ());

            // Make sure they are the 6 oldest
            for (int n = 0; n < numLeft; n++) {
                var dbEvent = McTelemetryEvent.QueryById<McTelemetryEvent> (dbEventIds [n]);
                Assert.True (null != dbEvent);
                Assert.AreEqual (dbEvent.Id, dbEventIds [n]);
                Assert.True (VerifyData (dbEvent.Data, n));
            }

            // Repeat for McTelemetrySupportEvent. Cannot do this thru generic
            // because it gets really ugly.
            dbEventIds.Clear ();
            for (int n = 0; n < numEvents; n++) {
                var dbEvent2 = new McTelemetrySupportEvent () {
                    Data = DataToBytes (n)
                };
                dbEvent2.Insert ();
                dbEventIds.Add (dbEvent2.Id);
            }
            Assert.AreEqual (numEvents, McTelemetrySupportEvent.QueryCount ());

            // Purge 3 events
            McTelemetrySupportEvent.Purge<McTelemetrySupportEvent> (3);

            // Should have 7 events left
            numLeft = numEvents - 3;
            Assert.AreEqual (numLeft, McTelemetrySupportEvent.QueryCount ());

            // Make sure they are the 7 oldest
            for (int n = 0; n < numLeft; n++) {
                var dbEvent = McTelemetrySupportEvent.QueryById<McTelemetrySupportEvent> (dbEventIds [n]);
                Assert.True (null != dbEvent);
                Assert.AreEqual (dbEvent.Id, dbEventIds [n]);
                Assert.True (VerifyData (dbEvent.Data, n));
            }
        }
    }
}

