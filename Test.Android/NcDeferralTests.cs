//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Brain;
using NachoCore.Utils;

namespace Test.Common
{
    public class NcDeferralTests
    {
        public NcDeferralTests ()
        {
        }

        public DateTime myMidnight ()
        {
            return new DateTime (2014, 3, 3, 0, 0, 0, DateTimeKind.Local);
        }

        public DateTime my6am ()
        {
            return new DateTime (2014, 3, 3, 6, 0, 0, DateTimeKind.Local);
        }

        public DateTime myNoon ()
        {
            return new DateTime (2014, 3, 3, 12, 0, 0, DateTimeKind.Local);
        }

        public DateTime my5pm ()
        {
            return new DateTime (2014, 3, 3, 17, 0, 0, DateTimeKind.Local);
        }

        public DateTime my6pm ()
        {
            return new DateTime (2014, 3, 3, 18, 0, 0, DateTimeKind.Local);
        }

        public DateTime my7pm ()
        {
            return new DateTime (2014, 3, 3, 19, 0, 0, DateTimeKind.Local);
        }

        public DateTime my9pm ()
        {
            return new DateTime (2014, 3, 3, 21, 0, 0, DateTimeKind.Local);
        }

        public DateTime my11pm ()
        {
            return new DateTime (2014, 3, 3, 23, 0, 0, DateTimeKind.Local);
        }

        public DateTime extractTime (NcResult r)
        {
            var t = r.GetValue<DateTime> ();
            Assert.AreEqual (DateTimeKind.Utc, t.Kind);
            return t;
        }

        [Test]
        public void Later ()
        {
            NcResult r;
            DateTime from;
            DateTime deferUntil;

            from = myMidnight ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Later, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (3), deferUntil.ToLocalTime ());

            from = my6am ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Later, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (3), deferUntil.ToLocalTime ());

            from = myNoon ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Later, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (3), deferUntil.ToLocalTime ());

            from = my6pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Later, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (3), deferUntil.ToLocalTime ());

            from = my9pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Later, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (3), deferUntil.ToLocalTime ());
        }

        [Test]
        public void OneHour ()
        {
            NcResult r;
            DateTime from;
            DateTime deferUntil;

            from = myMidnight ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.OneHour, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (1), deferUntil.ToLocalTime ());

            from = my6am ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.OneHour, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (1), deferUntil.ToLocalTime ());

            from = myNoon ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.OneHour, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (1), deferUntil.ToLocalTime ());

            from = my6pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.OneHour, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (1), deferUntil.ToLocalTime ());

            from = my9pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.OneHour, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (1), deferUntil.ToLocalTime ());
        }

        [Test]
        public void TwoHour ()
        {
            NcResult r;
            DateTime from;
            DateTime deferUntil;

            from = myMidnight ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.TwoHours, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (2), deferUntil.ToLocalTime ());

            from = my6am ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.TwoHours, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (2), deferUntil.ToLocalTime ());

            from = myNoon ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.TwoHours, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (2), deferUntil.ToLocalTime ());

            from = my6pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.TwoHours, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (2), deferUntil.ToLocalTime ());

            from = my9pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.TwoHours, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().AddHours (2), deferUntil.ToLocalTime ());
        }

        [Test]
        public void EndOfDay ()
        {
            NcResult r;
            DateTime from;
            DateTime deferUntil;

            from = myMidnight ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.EndOfDay, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (my5pm (), deferUntil.ToLocalTime ());

            from = my6am ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.EndOfDay, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (my5pm (), deferUntil.ToLocalTime ());

            from = myNoon ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.EndOfDay, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (my5pm (), deferUntil.ToLocalTime ());

            from = my6pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.EndOfDay, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (my11pm (), deferUntil.ToLocalTime ());

            from = my9pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.EndOfDay, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (my11pm (), deferUntil.ToLocalTime ());
        }

        [Test]
        public void Tonight ()
        {
            NcResult r;
            DateTime from;
            DateTime deferUntil;

            from = myMidnight ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Tonight, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (my7pm (), deferUntil.ToLocalTime ());

            from = my6am ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Tonight, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (my7pm (), deferUntil.ToLocalTime ());

            from = myNoon ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Tonight, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (my7pm (), deferUntil.ToLocalTime ());

            from = my6pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Tonight, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (my7pm (), deferUntil.ToLocalTime ());

            from = my9pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Tonight, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (my9pm (), deferUntil.ToLocalTime ());
        }


        [Test]
        public void Tomorrow ()
        {
            NcResult r;
            DateTime from;
            DateTime deferUntil;

            from = myMidnight ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Tomorrow, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().DayOfYear + 1, deferUntil.ToLocalTime ().DayOfYear);

            from = my6am ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Tomorrow, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().DayOfYear + 1, deferUntil.ToLocalTime ().DayOfYear);

            from = myNoon ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Tomorrow, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().DayOfYear + 1, deferUntil.ToLocalTime ().DayOfYear);

            from = my6pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Tomorrow, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().DayOfYear + 1, deferUntil.ToLocalTime ().DayOfYear);

            from = my9pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Tomorrow, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().DayOfYear + 1, deferUntil.ToLocalTime ().DayOfYear);
        }

        [Test]
        public void ThisWeek ()
        {
            NcResult r;
            DateTime from;
            DateTime deferUntil;

            from = myMidnight ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.ThisWeek, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (DayOfWeek.Friday, deferUntil.ToLocalTime ().DayOfWeek);

            from = my6am ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.ThisWeek, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (DayOfWeek.Friday, deferUntil.ToLocalTime ().DayOfWeek);

            from = myNoon ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.ThisWeek, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (DayOfWeek.Friday, deferUntil.ToLocalTime ().DayOfWeek);

            from = my6pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.ThisWeek, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (DayOfWeek.Friday, deferUntil.ToLocalTime ().DayOfWeek);

            from = my9pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.ThisWeek, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (DayOfWeek.Friday, deferUntil.ToLocalTime ().DayOfWeek);
        }

        [Test]
        public void Weekend ()
        {
            NcResult r;
            DateTime from;
            DateTime deferUntil;

            from = myMidnight ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Weekend, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (DayOfWeek.Saturday, deferUntil.ToLocalTime ().DayOfWeek);

            from = my6am ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Weekend, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (DayOfWeek.Saturday, deferUntil.ToLocalTime ().DayOfWeek);

            from = myNoon ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Weekend, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (DayOfWeek.Saturday, deferUntil.ToLocalTime ().DayOfWeek);

            from = my6pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Weekend, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (DayOfWeek.Saturday, deferUntil.ToLocalTime ().DayOfWeek);

            from = my9pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.Weekend, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (DayOfWeek.Saturday, deferUntil.ToLocalTime ().DayOfWeek);
        }

        [Test]
        public void NextWeek ()
        {
            NcResult r;
            DateTime from;
            DateTime deferUntil;

            from = myMidnight ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.NextWeek, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().DayOfWeek, deferUntil.ToLocalTime ().DayOfWeek);

            from = my6am ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.NextWeek, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().DayOfWeek, deferUntil.ToLocalTime ().DayOfWeek);

            from = myNoon ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.NextWeek, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().DayOfWeek, deferUntil.ToLocalTime ().DayOfWeek);

            from = my6pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.NextWeek, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().DayOfWeek, deferUntil.ToLocalTime ().DayOfWeek);

            from = my9pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.NextWeek, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (from.ToLocalTime ().DayOfWeek, deferUntil.ToLocalTime ().DayOfWeek);
        }

        [Test]
        public void MonthEnd ()
        {
            NcResult r;
            DateTime from;
            DateTime deferUntil;

            from = myMidnight ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.MonthEnd, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (3, deferUntil.ToLocalTime ().Month);
            Assert.AreEqual (31, deferUntil.ToLocalTime ().Day);

            from = my6am ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.MonthEnd, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (3, deferUntil.ToLocalTime ().Month);
            Assert.AreEqual (31, deferUntil.ToLocalTime ().Day);

            from = myNoon ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.MonthEnd, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (3, deferUntil.ToLocalTime ().Month);
            Assert.AreEqual (31, deferUntil.ToLocalTime ().Day);

            from = my6pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.MonthEnd, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (3, deferUntil.ToLocalTime ().Month);
            Assert.AreEqual (31, deferUntil.ToLocalTime ().Day);

            from = my9pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.MonthEnd, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (3, deferUntil.ToLocalTime ().Month);
            Assert.AreEqual (31, deferUntil.ToLocalTime ().Day);
        }


        [Test]
        public void NextMonth ()
        {
            NcResult r;
            DateTime from;
            DateTime deferUntil;

            from = myMidnight ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.NextMonth, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (4, deferUntil.ToLocalTime ().Month);
            Assert.AreEqual (1, deferUntil.ToLocalTime ().Day);

            from = my6am ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.NextMonth, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (4, deferUntil.ToLocalTime ().Month);
            Assert.AreEqual (1, deferUntil.ToLocalTime ().Day);

            from = myNoon ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.NextMonth, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (4, deferUntil.ToLocalTime ().Month);
            Assert.AreEqual (1, deferUntil.ToLocalTime ().Day);

            from = my6pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.NextMonth, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (4, deferUntil.ToLocalTime ().Month);
            Assert.AreEqual (1, deferUntil.ToLocalTime ().Day);

            from = my9pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (from, NachoCore.Model.MessageDeferralType.NextMonth, DateTime.MinValue);
            deferUntil = extractTime (r);
            Assert.AreEqual (4, deferUntil.ToLocalTime ().Month);
            Assert.AreEqual (1, deferUntil.ToLocalTime ().Day);
        }
    }
}

