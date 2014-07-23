﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using NUnit.Framework;
using NachoCore.Utils;
using NachoCore.Brain;

namespace Test.Common
{
    public class NcTimeVarianceTest
    {
        private const int ID_DEADLINE = 0;
        private const int ID_DEFERENCE = 1;
        private const int ID_AGING = 2;

        private int CallbackState;

        private double Adjustment;

        private NcTimeVariance[] TimeVariance;

        private AutoResetEvent Signal;

        [SetUp]
        public void SetUp ()
        {
            NcTimeVariance.GetCurrentDateTime = MockTimer.GetCurrentDateTime;
            CallbackState = 0;
            Adjustment = 0.0;
            Signal = new AutoResetEvent (false);
            TimeVariance = new NcTimeVariance[3];
            MockTimer.Start ();
        }

        [TearDown]
        public void TearDown ()
        {
            MockTimer.Stop ();
            NcTimeVariance.GetCurrentDateTime = NcTimeVariance.PlatformGetCurrentDateTime;
        }

        private void Callback (int state, Int64 objId)
        {
            NcAssert.True ((0 <= objId) && (2 >= objId));
            CallbackState = state;
            Adjustment = TimeVariance [objId].Adjustment (MockTimer.GetCurrentDateTime ());
            Signal.Set ();
        }

        private void AdvanceTime (int days, int hours, int minutes, int seconds)
        {
            TimeSpan timeInterval = new TimeSpan (days, hours, minutes, seconds);
            MockTimer.CurrentDateTime += timeInterval;
            MockTimer.CurrentTime += (Int64)timeInterval.TotalMilliseconds;
        }

        private void CheckState (int id, bool waitForSignal, int state, double adjustment)
        {
            if (waitForSignal) {
                bool signaled = Signal.WaitOne (2000);
                Assert.True (signaled);
            }
            Assert.AreEqual (state, CallbackState);
            Assert.AreEqual (adjustment, Adjustment);
            Assert.AreEqual (state, TimeVariance [id].State);
            Assert.AreEqual (adjustment, TimeVariance [id].Adjustment ());
        }

        private void AdvanceAndCheckState (int id, int state, double adjustment, int days)
        {
            /// Check that:
            /// 1. The callback has been called upon entering a state.
            /// 2. The state is correct.
            /// 3. The adjustment is correct
            CheckState (id, true, state, adjustment);

            /// Advance to 1 second before the end of this state. Check that
            /// the state and adjustment remain unchanged
            AdvanceTime (days - 1, 23, 59, 59);
            CheckState (id, false, state, adjustment);

            /// Advance 1 second to the next state
            AdvanceTime (0, 0, 0, 1);
        }

        private void CheckFinalState (int id, double adjustment)
        {
            CheckState (id, true, 0, adjustment);
            MockTimer.WaitForCallBack ();
            Assert.AreEqual (0, NcTimeVariance.ActiveList.Count);
        }

        [TestCase]
        public void DeadlineTimeVariance ()
        {
            DateTime deadline = MockTimer.GetCurrentDateTime () + new TimeSpan (5, 0, 0, 0);
            TimeVariance [ID_DEADLINE] =
                (NcTimeVariance)new NcDeadlineTimeVariance ("deadline", Callback, ID_DEADLINE, deadline);

            TimeVariance [ID_DEADLINE].Start ();
            AdvanceAndCheckState (ID_DEADLINE, 1, 1.0, 5);
            AdvanceAndCheckState (ID_DEADLINE, 2, 0.7, 1);
            AdvanceAndCheckState (ID_DEADLINE, 3, 0.4, 1);
            CheckFinalState (ID_DEADLINE, 0.1);
        }

        [TestCase]
        public void DeadlineTimeVarianceMaxDateTime ()
        {
            DateTime deadline = DateTime.MaxValue;
            TimeVariance [ID_DEADLINE] =
                (NcTimeVariance)new NcDeadlineTimeVariance ("deadline", Callback, ID_DEADLINE, deadline);

            TimeVariance [ID_DEADLINE].Start ();
            // Check that the state machine advances at the capped event time.
            AdvanceAndCheckState (ID_DEADLINE, 1, 1.0, 9998 * 365 + 2424);
            /// Due to out-of-range rounding, state 2 and 3 are skipped
            CheckFinalState (ID_DEADLINE, 0.1);
        }

        [TestCase]
        public void DeferenceTimeVariance ()
        {
            DateTime deferredUntil = MockTimer.GetCurrentDateTime () + new TimeSpan (3, 0, 0, 0);
            TimeVariance [ID_DEFERENCE] =
                (NcTimeVariance)new NcDeferenceTimeVariance ("deference", Callback, ID_DEFERENCE, deferredUntil);

            TimeVariance [ID_DEFERENCE].Start ();
            AdvanceAndCheckState (ID_DEFERENCE, 1, 0.1, 3);
            CheckFinalState (ID_DEFERENCE, 1.0);
        }

        [TestCase]
        public void AgingTimeVariance ()
        {
            DateTime dateReceived = MockTimer.GetCurrentDateTime ();
            TimeVariance [ID_AGING] =
                (NcTimeVariance)new NcAgingTimeVariance ("aging", Callback, ID_AGING, dateReceived);

            TimeVariance [ID_AGING].Start ();
            AdvanceAndCheckState (ID_AGING, 1, 1.0, 7);
            AdvanceAndCheckState (ID_AGING, 2, 0.8, 1);
            AdvanceAndCheckState (ID_AGING, 3, 0.7, 1);
            AdvanceAndCheckState (ID_AGING, 4, 0.6, 1);
            AdvanceAndCheckState (ID_AGING, 5, 0.5, 1);
            AdvanceAndCheckState (ID_AGING, 6, 0.4, 1);
            AdvanceAndCheckState (ID_AGING, 7, 0.3, 1);
            AdvanceAndCheckState (ID_AGING, 8, 0.2, 1);
            CheckFinalState (ID_AGING, 0.1);
        }

        [TestCase]
        public void PauseResume ()
        {
            DateTime dateReceived = MockTimer.GetCurrentDateTime ();
            TimeVariance [ID_AGING] =
                (NcTimeVariance)new NcAgingTimeVariance ("pause-n-resume", Callback, ID_AGING, dateReceived);
            NcTimeVariance tv = TimeVariance [ID_AGING];

            tv.Start ();
            CheckState (ID_AGING, true, 1, 1.0);
            AdvanceTime (4, 0, 0, 0); // +4 days. still in state 1
            CheckState (ID_AGING, false, 1, 1.0);

            // Pause, advance, and resume within the same state
            Assert.True (tv.IsRunning);
            tv.Pause ();
            Assert.False (tv.IsRunning);

            AdvanceTime (2, 0, 0, 0); // +2 days. still in state 1
            CheckState (ID_AGING, false, 1, 1.0);

            tv.Resume ();
            CheckState (ID_AGING, false, 1, 1.0);
            Assert.True (tv.IsRunning);

            AdvanceTime (1, 0, 0, 0); // +1 day. advance to state 2
            CheckState (ID_AGING, true, 2, 0.8);

            // Pause, advance to the middle a different state, and resume
            tv.Pause ();
            Assert.False (tv.IsRunning);
            AdvanceTime (1, 12, 0, 0); // +1.5 day. advance to state 3
            tv.Resume ();
            Assert.True (tv.IsRunning);
            CheckState (ID_AGING, true, 3, 0.7);

            // Pause, advance to the beginning of a different state, and resume
            tv.Pause ();
            Assert.False (tv.IsRunning);
            AdvanceTime (0, 12, 0, 0); // +0.5 day. advance to state 4
            CheckState (ID_AGING, false, 3, 0.7);
            tv.Resume ();
            Assert.True (tv.IsRunning);
            CheckState (ID_AGING, true, 4, 0.6);

            // Pause, advance to the last event
            Assert.AreEqual (1, NcTimeVariance.ActiveList.Count);
            tv.Pause ();
            Assert.False (tv.IsRunning);
            AdvanceTime (5, 0, 0, 0); // +5 days. advance to state 0
            tv.Resume ();
            Assert.False (tv.IsRunning); // false coz it is terminated
            CheckState (ID_AGING, true, 0, 0.1);
            MockTimer.WaitForCallBack ();
            Assert.AreEqual (0, NcTimeVariance.ActiveList.Count);
        }
    }
}

