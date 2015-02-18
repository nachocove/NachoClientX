//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using System.Collections.Generic;
using NUnit.Framework;
using NachoCore.Utils;


namespace Test.Common
{
    public struct CallbackEvent
    {
        public int ObjectIndex;
        public long Time;

        public CallbackEvent (int objIndex, Int64 time)
        {
            ObjectIndex = objIndex;
            Time = time;
        }
    }

    public class NcTimerPoolTest
    {
        const int ONE_SECOND = 1000;

        private List<CallbackEvent> CallbackObjects;
        private SemaphoreSlim Signal;

        [SetUp]
        public void SetUp ()
        {
            CallbackObjects = new List<CallbackEvent> ();
            Signal = new SemaphoreSlim (0);
            MockTimer.Start ();
        }

        [TearDown]
        public void TearDown ()
        {
            MockTimer.Stop ();
            CallbackObjects.Clear ();
            Signal.Dispose ();
        }

        private void Callback (object obj)
        {
            Log.Info (Log.LOG_TEST, "Callback: ObjectIndex={0}, Time={1}", (int)obj, MockTimer.CurrentTime);
            CallbackObjects.Add (new CallbackEvent ((int)obj, MockTimer.CurrentTime));
            Signal.Release ();
        }

        private void AdvanceTime (int seconds)
        {
            MockTimer.CurrentDateTime += new TimeSpan (0, 0, seconds);
            MockTimer.CurrentTime += seconds * ONE_SECOND;
        }

        private void Wait (int num_count)
        {
            for (int n = 0; n < num_count; n++) {
                bool got = Signal.Wait (new TimeSpan (0, 0, 1));
                Assert.True (got);
            }
            MockTimer.WaitForCallBack ();
        }

        private void CheckCallbackEvent (CallbackEvent cbEvent, int objIndex, int time)
        {
            Assert.AreEqual (cbEvent.ObjectIndex, objIndex);
            Assert.AreEqual (cbEvent.Time, time);
        }

        [TestCase]
        public void AddRemove ()
        {
            NcTimerPool pool = new NcTimerPool ("add_remove");
            NcTimerPoolTimer timer1 = new NcTimerPoolTimer (pool, "timer 1", Callback, 
                                          1, 2 * ONE_SECOND, ONE_SECOND);
            Log.Info (Log.LOG_TEST, "\n{0}", pool.ToString ());

            NcTimerPoolTimer timer2 = new NcTimerPoolTimer (pool, "timer 2", Callback,
                                          2, new TimeSpan (0, 0, 1), Timeout.InfiniteTimeSpan);
            Log.Info (Log.LOG_TEST, "\n{0}", pool.ToString ());

            NcTimerPoolTimer timer3 = new NcTimerPoolTimer (pool, "timer 3", Callback,
                                          3, (long)3 * ONE_SECOND, (long)2 * ONE_SECOND);
            Log.Info (Log.LOG_TEST, "\n{0}", pool.ToString ());
                
            // At t=1, only timer 2 should fire
            AdvanceTime (1);
            Wait (1);
            Assert.AreEqual (2, pool.ActiveCount);
            Assert.AreEqual (1, CallbackObjects.Count);
            CheckCallbackEvent (CallbackObjects [0], 2, ONE_SECOND);

            // At t=2, only timer 1 should fire
            AdvanceTime (1);
            Wait (1);
            Assert.AreEqual (2, CallbackObjects.Count);
            CheckCallbackEvent (CallbackObjects [1], 1, 2 * ONE_SECOND);

            // At t=3, timer 2 and timer 3 should fire.
            AdvanceTime (1);
            Wait (2);
            Assert.AreEqual (4, CallbackObjects.Count);
            CheckCallbackEvent (CallbackObjects [2], 1, 3 * ONE_SECOND);
            CheckCallbackEvent (CallbackObjects [3], 3, 3 * ONE_SECOND);

            timer1.Dispose ();
            Assert.AreEqual (1, pool.ActiveCount);
            timer2.Dispose ();
            Assert.AreEqual (1, pool.ActiveCount);

            // At t=5, timer 3 should fire.
            AdvanceTime (2);
            Wait (1);
            Assert.AreEqual (5, CallbackObjects.Count);
            CheckCallbackEvent (CallbackObjects [4], 3, 5 * ONE_SECOND);

            timer3.Dispose ();
            Assert.AreEqual (0, pool.ActiveCount);

            pool.Dispose ();
        }

        [TestCase]
        public void PauseResume ()
        {
            NcTimerPool pool = new NcTimerPool ("pause_resume");

            NcTimerPoolTimer timer1 = new NcTimerPoolTimer (pool, "timer 1", Callback, 1, ONE_SECOND, Timeout.Infinite);
            NcTimerPoolTimer timer2 = new NcTimerPoolTimer (pool, "timer 2", Callback, 2, 2 * ONE_SECOND, Timeout.Infinite);
            NcTimerPoolTimer timer3 = new NcTimerPoolTimer (pool, "timer 3", Callback, 3, 3 * ONE_SECOND, Timeout.Infinite);

            Assert.AreEqual (3, pool.ActiveCount);

            pool.Pause ();

            // At t=2, timer 1 is passed due and time 2 just fire
            AdvanceTime (2);
            pool.Resume ();
            Wait (2);
            Assert.AreEqual (2, CallbackObjects.Count);
            CheckCallbackEvent (CallbackObjects [0], 1, 2 * ONE_SECOND);
            CheckCallbackEvent (CallbackObjects [1], 2, 2 * ONE_SECOND);

            // At t=3, only timer 3 fires
            AdvanceTime (1);
            Wait (1);
            Assert.AreEqual (3, CallbackObjects.Count);
            CheckCallbackEvent (CallbackObjects [2], 3, 3 * ONE_SECOND);

            timer1.Dispose ();
            timer2.Dispose ();
            timer3.Dispose ();
        }
    }
}

