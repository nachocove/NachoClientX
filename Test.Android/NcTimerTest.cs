//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using NUnit.Framework;
using NachoCore.Utils;

namespace Test.Common
{
    public class NcTimerTest
    {
        const long ONE_HOUR = 3600L * 1000L;
        const long ONE_DAY = 86400L * 1000L;

        private static int CallbackCount;

        private AutoResetEvent Signal;

        [SetUp]
        public void SetUp ()
        {
            CallbackCount = 0;
            Signal = new AutoResetEvent (false);
            MockTimer.Start ();
        }

        [TearDown]
        public void TearDown ()
        {
            MockTimer.Stop ();
        }

        private void Callback (object obj)
        {
            Log.Info (Log.LOG_TIMER, "NcTimerTest callback {0}", MockTimer.GetCurrentDateTime ());
            CallbackCount++;
            Signal.Set ();
        }

        private void AdvanceTime (int days, int hours, int minutes, int seconds)
        {
            TimeSpan timeInterval = new TimeSpan (days, hours, minutes, seconds);
            MockTimer.CurrentDateTime += timeInterval;
            MockTimer.CurrentTime += (Int64)timeInterval.TotalMilliseconds;
        }

        private void CheckFired (int count)
        {
            Assert.True (Signal.WaitOne ());
            MockTimer.WaitForCallBack ();
            Assert.AreEqual (count, CallbackCount);
        }

        private void CheckNotFired (int count)
        {
            Assert.False (Signal.WaitOne (100));
            Assert.AreEqual (count, CallbackCount);
        }

        [TestCase]
        public void Normal ()
        {
            NcTimer timer = new NcTimer ("normal", Callback, this, 2 * ONE_HOUR, ONE_HOUR);

            AdvanceTime (0, 2, 0, 0);
            CheckFired (1);

            AdvanceTime (0, 1, 0, 0);
            CheckFired (2);

            timer.Dispose ();
        }

        [TestCase]
        public void LongDueTime ()
        {
            NcTimer timer = new NcTimer ("long due", Callback, this, 80 * ONE_DAY, Timeout.Infinite);

            AdvanceTime (40, 0, 0, 0);
            CheckNotFired (0);

            AdvanceTime (40, 0, 0, 0);
            CheckFired (1);

            AdvanceTime (1, 0, 0, 0);
            CheckNotFired (1);

            timer.Dispose ();
        }

        [TestCase]
        public void LongPeriod ()
        {
            NcTimer timer = new NcTimer ("long period", Callback, this, ONE_HOUR, 80 * ONE_DAY);

            AdvanceTime (0, 1, 0, 0);
            CheckFired (1);

            AdvanceTime (40, 0, 0, 0);
            CheckNotFired (1);

            AdvanceTime (40, 0, 0, 0);
            CheckFired (2);

            AdvanceTime (40, 0, 0, 0);
            CheckNotFired (2);

            AdvanceTime (40, 0, 0, 0);
            CheckFired (3);

            timer.Dispose ();
        }

        [TestCase]
        public void LongDueTimeLongPeriod ()
        {
            NcTimer timer = new NcTimer ("long due time long period", Callback, this, 50 * ONE_DAY, 60 * ONE_DAY);

            // Due time
            AdvanceTime (40, 0, 0, 0);
            CheckNotFired (0);

            AdvanceTime (10, 0, 0, 0);
            CheckFired (1);

            // 1st period
            AdvanceTime (40, 0, 0, 0);
            CheckNotFired (1);

            AdvanceTime (20, 0, 0, 0);
            CheckFired (2);

            // 2nd period
            AdvanceTime (40, 0, 0, 0);
            CheckNotFired (2);

            AdvanceTime (20, 0, 0, 0);
            CheckFired (3);

            timer.Dispose ();
        }
    }
}

