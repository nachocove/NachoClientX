//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using NUnit.Framework;
using NachoCore.Utils;

namespace Test.Common
{
    public class NcPausableTimerTest
    {
        private Int64 CallbackTime;
        private AutoResetEvent Signal;
        public struct Int32Params {
            public static Int32 due;
            public static Int32 period;
        }
        public struct Int64Params {
            public static Int64 due;
            public static Int64 period;
        };
        public struct TimeSpanParams {
            public static TimeSpan due;
            public static TimeSpan period;
        };
        public struct UInt32Params {
            public static UInt32 due;
            public static UInt32 period;
        };

        [SetUp]
        public void SetUp ()
        {
            NcPausableTimer.StopWatchClass = typeof(MockStopwatch);

            CallbackTime = Timeout.Infinite;
            MockTimer.Start ();
            MockStopwatch.CurrentMillisecond = 0;
            Signal = new AutoResetEvent (false);

            Int32Params.due = 10;
            Int32Params.period = 20;

            Int64Params.due = 10;
            Int64Params.period = 30;

            TimeSpanParams.due = new TimeSpan (0, 0, 0, 0, 15);
            TimeSpanParams.period = new TimeSpan (0, 0, 0, 0, 20);

            UInt32Params.due = 15;
            UInt32Params.period = 30;
        }

        [TearDown]
        public void TearDown ()
        {
            MockTimer.Stop ();

            NcPausableTimer.StopWatchClass = typeof(PlatformStopwatch);
        }

        private void Callback (object obj)
        {
            if (null != obj) {
                Assert.AreEqual (obj, this);
            }
            CallbackTime = MockTimer.CurrentTime;
            Signal.Set ();
        }

        private void FireAndCheck (params Int64 [] times)
        {
            foreach (Int64 time in times) {
                MockTimer.CurrentTime = time;
                Signal.WaitOne ();
                Assert.AreEqual (time, CallbackTime);
            }
        }

        [TestCase]
        public void Constructor ()
        {
            NcPausableTimer timer;
            Assert.AreEqual (Timeout.Infinite, CallbackTime);

            /// Test all 4 types of constructor that starts the timer
            timer = new NcPausableTimer ("constructor 1", Callback, this, Int32Params.due, Int32Params.period);
            FireAndCheck (
                10, // initial firing = 0 + 10
                30, // periodic firing = 10 + 20
                50);
            timer.Dispose ();

            timer = new NcPausableTimer ("constructor 2", Callback, this, Int64Params.due, Int64Params.period);
            FireAndCheck (
                60, // initial firing = 50 + 10
                90, // periodic firing = 60 + 30
                120);
            timer.Dispose ();

            timer = new NcPausableTimer ("constructor 3", Callback, this, TimeSpanParams.due, TimeSpanParams.period);
            FireAndCheck (
                135, // intial firing = 120 + 15
                155, // periodic firing = 135 + 20
                175);
            timer.Dispose ();

            timer = new NcPausableTimer ("constructor 4", Callback, this, UInt32Params.due, UInt32Params.period);
            FireAndCheck (
                190, // initial firing = 175 + 15
                220, // periodic firing = 190 + 30
                250);
            timer.Dispose ();

            /// Test a due time of 0 which results in immediate firing
            CallbackTime = Timeout.Infinite;
            timer = new NcPausableTimer ("constructor 5", Callback, this, 0, Timeout.Infinite);
            Signal.WaitOne ();
            Assert.AreEqual (CallbackTime, MockTimer.CurrentTime);
        }

        [TestCase]
        public void Change ()
        {
            NcPausableTimer timer;
            bool changed;

            timer = new NcPausableTimer ("change 1", Callback);
            changed = timer.Change (Int32Params.due, Int32Params.period);
            Assert.True (changed);
            FireAndCheck (10, 30, 50);
            timer.Dispose ();

            timer = new NcPausableTimer ("change 2", Callback);
            changed = timer.Change (Int64Params.due, Int64Params.period);
            Assert.True (changed);
            FireAndCheck (60, 90, 120);
            timer.Dispose ();

            timer = new NcPausableTimer ("change 3", Callback);
            changed = timer.Change (TimeSpanParams.due, TimeSpanParams.period);
            Assert.True (changed);
            FireAndCheck (135, 155, 175);
            timer.Dispose ();

            timer = new NcPausableTimer ("change 4", Callback);
            changed = timer.Change (UInt32Params.due, UInt32Params.period);
            Assert.True (changed);
            FireAndCheck (190, 220, 250);
            timer.Dispose ();
        }

        [TestCase]
        public void PauseAndResume ()
        {
            Assert.AreEqual (Timeout.Infinite, CallbackTime);

            NcPausableTimer timer = new NcPausableTimer ("pause", Callback, this, 10, 30);

            // Advance 5 msec. Should not fire
            MockTimer.CurrentTime = 5;
            Assert.AreEqual (Timeout.Infinite, CallbackTime);

            // Pause 95 msec. Should not fire
            timer.Pause ();
            MockTimer.CurrentTime = 100;
            Assert.AreEqual (Timeout.Infinite, CallbackTime);

            // Resume and advance 5 msec. Should fire
            timer.Resume ();
            MockTimer.CurrentTime = 105;
            Signal.WaitOne ();
            Assert.AreEqual (105, CallbackTime);

            // Advance 25 msec. Should not fire
            MockTimer.CurrentTime = 130;
            Assert.AreEqual (105, CallbackTime);

            // Pause 50 msec. Should not fire
            timer.Pause ();
            MockTimer.CurrentTime = 180;
            Assert.AreEqual (105, CallbackTime);

            // Resume and advance 5 msec. Should fire
            timer.Resume ();
            MockTimer.CurrentTime = 185;
            Signal.WaitOne ();
            Assert.AreEqual (185, CallbackTime);
        }
    }
}

