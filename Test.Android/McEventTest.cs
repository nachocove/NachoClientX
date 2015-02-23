//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using NachoCore.Model;
using NachoCore.Utils;
using NUnit.Framework;
using Test.Common;

namespace Test.Common
{
    public class McEventTest : NcTestBase
    {
        public McEventTest ()
        {
        }

        [Test]
        public void TestQueryAllEventsInOrder ()
        {
            var e0 = new McEvent () {
                AccountId = 1,
                CalendarId = 100,
                StartTime = new DateTime (2015, 2, 1, 5, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime (2015, 2, 1, 6, 0, 0, DateTimeKind.Utc),
            };
            var e1 = new McEvent () {
                AccountId = 1,
                CalendarId = 101,
                StartTime = new DateTime (2015, 2, 1, 5, 5, 0, DateTimeKind.Utc),
                EndTime = new DateTime (2015, 2, 1, 5, 55, 0, DateTimeKind.Utc),
            };
            var e2 = new McEvent () {
                AccountId = 1,
                CalendarId = 102,
                StartTime = new DateTime (2015, 12, 25, 0, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime (2015, 12, 26, 0, 0, 0, DateTimeKind.Utc),
            };

            // Insert them into the database in a different order, to make sure ordering of
            // QueryAllEventsInOrder is explicit, and not just by chance.
            e1.Insert ();
            e2.Insert ();
            e0.Insert ();

            var allEvents = McEvent.QueryAllEventsInOrder ();

            Assert.AreEqual (3, allEvents.Count, "The wrong number of events were returned.");
            Assert.AreEqual (e0.CalendarId, allEvents [0].CalendarId, "Events were returned in the wrong order.");
            Assert.AreEqual (e1.CalendarId, allEvents [1].CalendarId, "Events were returned in the wrong order.");
            Assert.AreEqual (e2.CalendarId, allEvents [2].CalendarId, "Events were returned in the wrong order.");
        }

        [Test]
        public void TestQueryEventsWithRemindersInRange ()
        {
            var e0 = new McEvent () {
                AccountId = 1,
                CalendarId = 100,
                ReminderTime = new DateTime (2015, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            };
            var e1 = new McEvent () {
                AccountId = 1,
                CalendarId = 101,
                ReminderTime = new DateTime (2015, 2, 1, 0, 10, 0, DateTimeKind.Utc),
            };
            var e2 = new McEvent () {
                AccountId = 1,
                CalendarId = 102,
                ReminderTime = new DateTime (2015, 2, 1, 0, 20, 0, DateTimeKind.Utc),
            };
            var e3 = new McEvent () {
                AccountId = 1,
                CalendarId = 103,
                ReminderTime = new DateTime (2015, 2, 1, 0, 30, 0, DateTimeKind.Utc),
            };
            var e4 = new McEvent () {
                AccountId = 1,
                CalendarId = 104,
                ReminderTime = new DateTime (2015, 2, 1, 0, 40, 0, DateTimeKind.Utc),
            };

            e3.Insert ();
            e1.Insert ();
            e4.Insert ();
            e0.Insert ();
            e2.Insert ();

            // Query a range that picks the middle three but leaves out the earliest and the latest.
            var events = McEvent.QueryEventsWithRemindersInRange (
                             new DateTime (2015, 2, 1, 0, 5, 0, DateTimeKind.Utc),
                             new DateTime (2015, 2, 1, 0, 35, 0, DateTimeKind.Utc)).ToList ();

            Assert.AreEqual (3, events.Count, "The wrong number of events were returned.");
            Assert.AreEqual (e1.CalendarId, events [0].CalendarId, "The wrong events were returned, or they were returned in the wrong order.");
            Assert.AreEqual (e2.CalendarId, events [1].CalendarId, "The wrong events were returned, or they were returned in the wrong order.");
            Assert.AreEqual (e3.CalendarId, events [2].CalendarId, "The wrong events were returned, or they were returned in the wrong order.");
        }

        [Test]
        public void TestQueryEventsForCalendarItemAfter ()
        {
            var e0 = new McEvent () {
                AccountId = 1,
                CalendarId = 100,
                StartTime = new DateTime (2015, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime (2015, 3, 1, 1, 0, 0, DateTimeKind.Utc),
            };
            var e1 = new McEvent () {
                AccountId = 1,
                CalendarId = 101,
                StartTime = new DateTime (2015, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime (2015, 1, 1, 1, 0, 0, DateTimeKind.Utc),
            };
            var e2 = new McEvent () {
                AccountId = 1,
                CalendarId = 101,
                StartTime = new DateTime (2015, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime (2015, 2, 1, 1, 0, 0, DateTimeKind.Utc),
            };
            var e3 = new McEvent () {
                AccountId = 1,
                CalendarId = 101,
                StartTime = new DateTime (2015, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime (2015, 3, 1, 1, 0, 0, DateTimeKind.Utc),
            };

            e3.Insert ();
            e2.Insert ();
            e1.Insert ();
            e0.Insert ();

            var events = McEvent.QueryEventsForCalendarItemAfter (
                             101, new DateTime (2015, 1, 15, 0, 0, 0, DateTimeKind.Utc)).ToList ();

            Assert.AreEqual (2, events.Count, "The wrong number of events was returned.");
            // The results are not returned in any particular order, but both events should be for McCalendar.Id==101
            Assert.AreEqual (101, events [0].CalendarId, "The wrong events were returned.");
            Assert.AreEqual (101, events [1].CalendarId, "The wrong events were returned.");
            Assert.True (e2.Id == events [0].Id || e3.Id == events [0].Id, "The wrong events were returned.");
            Assert.True (e2.Id == events [1].Id || e3.Id == events [1].Id, "The wrong events were returned.");
        }

        [Test]
        public void TestGetCurrentOrNextEvent ()
        {
            var goodCal = new McCalendar () {
                AccountId = 1,
                MeetingStatusIsSet = true,
                MeetingStatus = NcMeetingStatus.Meeting,
            };
            var canceledCal = new McCalendar () {
                AccountId = 1,
                MeetingStatusIsSet = true,
                MeetingStatus = NcMeetingStatus.MeetingCancelled,
            };
            goodCal.Insert ();
            canceledCal.Insert ();

            // GetCurrentOrNextEvent() uses the current time as part of the query.  So all of the test events
            // need to be relative to the current time rather than some absolute time.
            DateTime now = DateTime.UtcNow;
            var e0 = new McEvent () {
                AccountId = 1,
                CalendarId = canceledCal.Id,
                StartTime = now + new TimeSpan (1, 0, 0),
                EndTime = now + new TimeSpan (2, 0, 0),
            };
            var e1 = new McEvent () {
                AccountId = 1,
                CalendarId = goodCal.Id,
                StartTime = now - new TimeSpan (2, 0, 0),
                EndTime = now - new TimeSpan (1, 0, 0),
            };
            var e2 = new McEvent () {
                AccountId = 1,
                CalendarId = goodCal.Id,
                StartTime = now + new TimeSpan (2, 0, 0),
                EndTime = now + new TimeSpan (3, 0, 0),
            };
            var e3 = new McEvent () {
                AccountId = 1,
                CalendarId = goodCal.Id,
                StartTime = now + new TimeSpan (3, 0, 0),
                EndTime = now + new TimeSpan (4, 0, 0),
            };

            e0.Insert ();
            e1.Insert ();
            e2.Insert ();
            e3.Insert ();

            // e0 should be ignored because it is associated with a cancelled event.
            // e1 is in the past.
            // e3 is after e2.
            // Therefore, e2 should be the selected event.
            var nextEvent = McEvent.GetCurrentOrNextEvent ();
            Assert.NotNull (nextEvent, "GetCurrentOrNextEvent() didn't find any event.");
            Assert.AreEqual (e2.Id, nextEvent.Id, "GetCurrentOrNextEvent() returned the wrong event.");
        }
    }
}

