//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;

using NachoCore;
using NachoCore.Model;

namespace Test.Common
{
    [TestFixture]
    public class NcEventsCommonTest : NcTestBase
    {

        protected static McAccount account;
        protected static McCalendar cg;

        public NcEventsCommonTest ()
        {
            account = new McAccount ();
            account.Insert ();
        }

        public class EmptyInstance : NcEventsCalendarMapCommon
        {
            public EmptyInstance ()
                : base (DateTime.UtcNow.AddMonths (1))
            {
            }

            protected override System.Collections.Generic.List<McEvent> GetEventsWithDuplicates (DateTime start, DateTime end)
            {
                return new System.Collections.Generic.List<NachoCore.Model.McEvent> ();
            }
        }

        public class Instance1 : NcEventsCalendarMapCommon
        {
            public Instance1 ()
                : base (DateTime.UtcNow.AddDays (70))
            {
            }

            protected override System.Collections.Generic.List<McEvent> GetEventsWithDuplicates (DateTime start, DateTime end)
            {
                var id = account.Id;

                cg = new McCalendar ();
                cg.AccountId = id;
                cg.Insert ();

                var list = new System.Collections.Generic.List<NachoCore.Model.McEvent> ();
                list.Add (McEvent.Create (id, DateTime.Now.AddDays (-60), DateTime.Now.AddDays (-60).AddHours (1), "a", false, cg.Id, 0));
                list.Add (McEvent.Create (id, DateTime.Now.AddDays (-30), DateTime.Now.AddDays (-30).AddHours (1), "a", false, cg.Id, 0));
                list.Add (McEvent.Create (id, DateTime.Now.AddDays (0), DateTime.Now.AddDays (0).AddHours (1), "a", false, cg.Id, 0));
                list.Add (McEvent.Create (id, DateTime.Now.AddDays (30), DateTime.Now.AddDays (30).AddHours (1), "a", false, cg.Id, 0));
                list.Add (McEvent.Create (id, DateTime.Now.AddDays (60), DateTime.Now.AddDays (60).AddHours (1), "a", false, cg.Id, 0));
                return list;
            }
                 
        }


        [Test]
        public void LoadEmptyInstance ()
        {
            var instance = new EmptyInstance ();

            for (int i = 0; i < instance.NumberOfDays (); i++) {
                var date = instance.GetDateUsingDayIndex (i);
                var dateIndex = instance.IndexOfDate (date);
                Assert.AreEqual (i, dateIndex);
                Assert.AreEqual (0, instance.NumberOfItemsForDay (i));
                int item;
                int section;
                var b = instance.FindEventNearestTo (date, out item, out section);
                Assert.IsFalse (b);
            }
            var cnt1 = instance.NumberOfDays ();
            var ext1 = instance.ExtendEventMap (DateTime.Today.AddMonths (6));
            var cnt2 = instance.NumberOfDays ();
            Assert.AreEqual (cnt2, cnt1 + ext1);

            for (int i = 0; i < instance.NumberOfDays (); i++) {
                var date = instance.GetDateUsingDayIndex (i);
                var dateIndex = instance.IndexOfDate (date);
                Assert.AreEqual (i, dateIndex);
                Assert.AreEqual (0, instance.NumberOfItemsForDay (i));
                int item;
                int section;
                var b = instance.FindEventNearestTo (date, out item, out section);
                Assert.IsFalse (b);
            }
        }

        [Test]
        public void LoadInstance1 ()
        {
            var instance = new Instance1 ();


            for (int i = 0; i < instance.NumberOfDays (); i++) {
                var date = instance.GetDateUsingDayIndex (i);
                var dateIndex = instance.IndexOfDate (date);
                Assert.AreEqual (i, dateIndex);
                int item;
                int section;
                var b = instance.FindEventNearestTo (date, out item, out section);
                if (b) {
                    var c = instance.GetEvent (section, item);
                    Assert.NotNull (c);
                }
            }
            var cnt1 = instance.NumberOfDays ();
            var ext1 = instance.ExtendEventMap (DateTime.Today.AddMonths (6));
            var cnt2 = instance.NumberOfDays ();
            Assert.AreEqual (cnt2, cnt1 + ext1);

            for (int i = 0; i < instance.NumberOfDays (); i++) {
                var date = instance.GetDateUsingDayIndex (i);
                var dateIndex = instance.IndexOfDate (date);
                Assert.AreEqual (i, dateIndex);
                int item;
                int section;
                var b = instance.FindEventNearestTo (date, out item, out section);
                if (b) {
                    var c = instance.GetEvent (section, item);
                    Assert.NotNull (c);
                }
            }
        }

    }
}

