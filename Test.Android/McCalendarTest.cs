//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Test.Common;
using NUnit.Framework;
using NachoCore.Utils;
using NachoCore.Model;

namespace Test.Android
{
    public class McCalendarTest : NcTestBase
    {
        public McCalendarTest ()
        {
        }

        [Test]
        public void TestQueryByUID ()
        {
            var c1 = CalendarHelper.DefaultMeeting (DateTime.UtcNow, DateTime.UtcNow);
            c1.AccountId = 1;
            c1.UID = "aaa";
            c1.Subject = "c1";
            c1.Insert ();

            var c2 = CalendarHelper.DefaultMeeting (DateTime.UtcNow, DateTime.UtcNow);
            c2.AccountId = 1;
            c2.UID = "bbb";
            c2.Subject = "c2";
            c2.Insert ();

            var c3 = CalendarHelper.DefaultMeeting (DateTime.UtcNow, DateTime.UtcNow);
            c3.AccountId = 2;
            c3.UID = "aaa";
            c3.Subject = "c3";
            c3.Insert ();

            var c4 = CalendarHelper.DefaultMeeting (DateTime.UtcNow, DateTime.UtcNow);
            c4.AccountId = 2;
            c4.UID = "bbb";
            c4.Subject = "c4";
            c4.Insert ();

            var r0 = McCalendar.QueryByUID (20, "xyz");
            Assert.IsNull (r0, "McCalendar.QueryByUID (20, \"xyz\") found an item when it shouldn't have.");

            var r1 = McCalendar.QueryByUID (1, "aaa");
            Assert.NotNull (r1, "McCalendar.QueryByUID (1, \"aaa\") didn't find any item.");
            Assert.AreEqual ("c1", r1.Subject, "McCalendar.QueryByUID (1, \"aaa\") found the wrong item.");

            var r2 = McCalendar.QueryByUID (1, "bbb");
            Assert.NotNull (r2, "McCalendar.QueryByUID (1, \"bbb\") didn't find any item.");
            Assert.AreEqual ("c2", r2.Subject, "McCalendar.QueryByUID (1, \"bbb\") found the wrong item.");

            var r3 = McCalendar.QueryByUID (2, "aaa");
            Assert.NotNull (r3, "McCalendar.QueryByUID (2, \"aaa\") didn't find any item.");
            Assert.AreEqual ("c3", r3.Subject, "McCalendar.QueryByUID (2, \"aaa\") found the wrong item.");

            var r4 = McCalendar.QueryByUID (2, "bbb");
            Assert.NotNull (r4, "McCalendar.QueryByUID (2, \"bbb\") didn't find any item.");
            Assert.AreEqual ("c4", r4.Subject, "McCalendar.QueryByUID (2, \"bbb\") found the wrong item.");

            var c5 = CalendarHelper.DefaultMeeting (DateTime.UtcNow, DateTime.UtcNow);
            c5.AccountId = 1;
            c5.UID = "aaa";
            c5.Subject = "c5";
            c5.Insert ();

            var r5 = McCalendar.QueryByUID (1, "aaa");
            Assert.NotNull (r5, "McCalendar.QueryByUID (1, \"aaa\") didn't find any item when there should have been two matching items.");
            Assert.AreEqual ("c5", r5.Subject, "McCalendar.QueryByUID (1, \"aaa\") picked the wrong item of the two available.");

            var c6 = CalendarHelper.DefaultMeeting (DateTime.UtcNow, DateTime.UtcNow);
            c6.AccountId = 1;
            c6.UID = "ccc";
            c6.Subject = "c6";
            c6.Insert ();

            var r6 = McCalendar.QueryByUID (2, "ccc");
            Assert.IsNull (r6, "McCalendar.QueryByUID (2, \"ccc\") found an item when it shouldn't have.");
        }

        [Test]
        public void TestQueryRelatedEvents ()
        {
            var c1 = CalendarHelper.DefaultMeeting (DateTime.UtcNow, DateTime.UtcNow);
            c1.AccountId = 1;
            c1.Subject = "c1";
            c1.Insert (); 

            var e1 = new McEvent();
            e1.AccountId = 1;
            e1.CalendarId = 1;
            e1.Insert ();

            var e2 = new McEvent();
            e2.AccountId = 1;
            e2.CalendarId = 1;
            e2.Insert ();

            var e3 = new McEvent();
            e3.AccountId = 2;
            e3.CalendarId = 1;
            e3.Insert ();

            var e4 = new McEvent();
            e4.AccountId = 2;
            e4.CalendarId = 2;
            e4.Insert ();

            var r0 = c1.QueryRelatedEvents ();
            Assert.NotNull (r0, "c1.QueryRelatedEvents () didn't find any related events.");
            Assert.AreEqual (3, r0.Count, "Returned the wrong number of related events");

            Assert.AreEqual (1, r0[0].Id, "Returned the wrong event");
            Assert.AreEqual (2, r0[1].Id, "Returned the wrong event");
            Assert.AreEqual (3, r0[2].Id, "Returned the wrong event");

        }
    }
}

