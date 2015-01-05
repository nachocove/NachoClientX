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

        public DateTime myMidnight()
        {
            return new DateTime (2014, 3, 3, 0, 0, 0, DateTimeKind.Local);
        }

        public DateTime my6am()
        {
            return new DateTime (2014, 3, 3, 6, 0, 0, DateTimeKind.Local);
        }

        public DateTime myNoon()
        {
            return new DateTime (2014, 3, 3, 12, 0, 0, DateTimeKind.Local);
        }

        public DateTime my6pm()
        {
            return new DateTime (2014, 3, 3, 18, 0, 0, DateTimeKind.Local);
        }

        public DateTime my9pm()
        {
            return new DateTime (2014, 3, 3, 21, 0, 0, DateTimeKind.Local);
        }

        public DateTime extractTime(NcResult r)
        {
            var t = r.GetValue<DateTime> ();
            Assert.AreEqual (DateTimeKind.Utc, t.Kind);
            return t;
        }

        [Test]
        public void Later ()
        {
            NcResult r;
            DateTime deferUntil;

            r = NcMessageDeferral.ComputeDeferral (myMidnight().ToUniversalTime(), NachoCore.Model.MessageDeferralType.Later, DateTime.MinValue);
            deferUntil = extractTime(r);
        }


        [Test]
        public void Tomorrow()
        {
            NcResult r;
            DateTime from;
            DateTime deferUntil;

            from = myMidnight ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (myMidnight().ToUniversalTime(), NachoCore.Model.MessageDeferralType.Tomorrow, DateTime.MinValue);
            deferUntil = extractTime(r);
            Assert.AreEqual (from.ToLocalTime ().DayOfYear + 1, deferUntil.ToLocalTime ().DayOfYear);

            from = my6am ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (myMidnight().ToUniversalTime(), NachoCore.Model.MessageDeferralType.Tomorrow, DateTime.MinValue);
            deferUntil = extractTime(r);
            Assert.AreEqual (from.ToLocalTime ().DayOfYear + 1, deferUntil.ToLocalTime ().DayOfYear);

            from = myNoon ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (myMidnight().ToUniversalTime(), NachoCore.Model.MessageDeferralType.Tomorrow, DateTime.MinValue);
            deferUntil = extractTime(r);
            Assert.AreEqual (from.ToLocalTime ().DayOfYear + 1, deferUntil.ToLocalTime ().DayOfYear);

            from = my6pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (myMidnight().ToUniversalTime(), NachoCore.Model.MessageDeferralType.Tomorrow, DateTime.MinValue);
            deferUntil = extractTime(r);
            Assert.AreEqual (from.ToLocalTime ().DayOfYear + 1, deferUntil.ToLocalTime ().DayOfYear);

            from = my9pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (myMidnight().ToUniversalTime(), NachoCore.Model.MessageDeferralType.Tomorrow, DateTime.MinValue);
            deferUntil = extractTime(r);
            Assert.AreEqual (from.ToLocalTime ().DayOfYear + 1, deferUntil.ToLocalTime ().DayOfYear);
        }

        [Test]
        public void NextWeek()
        {
            NcResult r;
            DateTime from;
            DateTime deferUntil;

            from = myMidnight ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (myMidnight().ToUniversalTime(), NachoCore.Model.MessageDeferralType.NextWeek, DateTime.MinValue);
            deferUntil = extractTime(r);
            Assert.AreEqual (from.ToLocalTime ().DayOfWeek, deferUntil.ToLocalTime ().DayOfWeek);

            from = my6am ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (myMidnight().ToUniversalTime(), NachoCore.Model.MessageDeferralType.NextWeek, DateTime.MinValue);
            deferUntil = extractTime(r);
            Assert.AreEqual (from.ToLocalTime ().DayOfWeek, deferUntil.ToLocalTime ().DayOfWeek);

            from = myNoon ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (myMidnight().ToUniversalTime(), NachoCore.Model.MessageDeferralType.NextWeek, DateTime.MinValue);
            deferUntil = extractTime(r);
            Assert.AreEqual (from.ToLocalTime ().DayOfWeek, deferUntil.ToLocalTime ().DayOfWeek);

            from = my6pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (myMidnight().ToUniversalTime(), NachoCore.Model.MessageDeferralType.NextWeek, DateTime.MinValue);
            deferUntil = extractTime(r);
            Assert.AreEqual (from.ToLocalTime ().DayOfWeek, deferUntil.ToLocalTime ().DayOfWeek);

            from = my9pm ().ToUniversalTime ();
            r = NcMessageDeferral.ComputeDeferral (myMidnight().ToUniversalTime(), NachoCore.Model.MessageDeferralType.NextWeek, DateTime.MinValue);
            deferUntil = extractTime(r);
            Assert.AreEqual (from.ToLocalTime ().DayOfWeek, deferUntil.ToLocalTime ().DayOfWeek);
        }
    }
}

