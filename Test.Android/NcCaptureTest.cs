//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using System.Diagnostics;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;

namespace Test.common
{
    public class MockStopwatch : IStopwatch
    {
        public static long Tick;

        private long StartTick;
        private long _ElapsedMilliseconds;

        public long ElapsedMilliseconds {
            get {
                return _ElapsedMilliseconds;
            }
        }

        public MockStopwatch ()
        {
            StartTick = -1;
            _ElapsedMilliseconds = 0;
        }

        public void Start ()
        {
            Console.WriteLine ("MockStopwatch: Start");
            StartTick = Tick;
        }

        public void Stop ()
        {
            Console.WriteLine ("MockStopwatch: Stop");
            if (-1 == StartTick) {
                return;
            }
            _ElapsedMilliseconds += Tick - StartTick;
            StartTick = -1;
        }

        public void Reset ()
        {
            _ElapsedMilliseconds = 0;
        }

        public static void AddTick (long msec)
        {
            Tick += msec;
        }
    }

    [TestFixture]
    public class NcCaptureTest
    {
        const string thisKind1 = "Test1";
        const string thisKind2 = "Test2";
        const string thisKind3 = "Test3";
        const int numCaptures = 3;

        private NcCapture[] captures;

        [SetUp]
        public void SetUp ()
        {
            NcCapture.StopwatchClass = typeof(MockStopwatch);
            captures = new NcCapture[numCaptures];
        }

        [TearDown]
        public void TearDown ()
        {
            NcCapture.StopwatchClass = typeof(Stopwatch);
            for (int n = 0; n < numCaptures; n++) {
                if (null != captures [n]) {
                    captures [n].Dispose ();
                }
            }
            NcCapture.RemoveKind (thisKind1);
            NcCapture.RemoveKind (thisKind2);
        }

        private void CaptureStartStop (NcCapture cap, string kind, long elapsed, string expected)
        {
            cap.Start ();
            MockStopwatch.AddTick (elapsed);
            cap.Stop ();
            CaptureCheck (kind, expected);
        }

        private void CaptureCheck (string kind, string expected)
        {
            string summary = NcCapture.Summarize (kind);
            Assert.AreEqual (summary, expected);
        }

        [Test]
        public void Statistics ()
        {
            NcCapture.AddKind (thisKind1);
            captures[0] = NcCapture.Create (thisKind1);

            CaptureStartStop (captures[0], thisKind1, 50, "[Kind: Test1] Count = 1, Min = 50ms, Max = 50ms, Average = 50ms");

            captures[0].Reset ();
            CaptureStartStop (captures[0], thisKind1, 100, "[Kind: Test1] Count = 2, Min = 50ms, Max = 100ms, Average = 75ms");

            captures[0].Reset ();
            CaptureStartStop (captures[0], thisKind1, 30, "[Kind: Test1] Count = 3, Min = 30ms, Max = 100ms, Average = 60ms");
        }

        [Test]
        public void PauseResume ()
        {
            NcCapture.AddKind (thisKind1);
            captures [0] = NcCapture.CreateAndStart (thisKind1);
            captures [1] = NcCapture.Create (thisKind1);
            captures [2] = NcCapture.Create (thisKind1);

            Assert.True (captures [0].IsRunning);
            Assert.False (captures [1].IsRunning);
            Assert.False (captures [2].IsRunning);

            MockStopwatch.AddTick (40);

            captures [1].Start ();
            Assert.True (captures [1].IsRunning);

            MockStopwatch.AddTick (80);

            NcCapture.PauseKind (thisKind1);

            Assert.True (captures [0].IsRunning);
            Assert.True (captures [1].IsRunning);
            Assert.False (captures [2].IsRunning);

            MockStopwatch.AddTick (160);

            NcCapture.ResumeKind (thisKind1);

            Assert.True (captures [0].IsRunning);
            Assert.True (captures [1].IsRunning);
            Assert.False (captures [2].IsRunning);

            MockStopwatch.AddTick (60);

            captures [0].Stop ();

            CaptureCheck (thisKind1, "[Kind: Test1] Count = 1, Min = 180ms, Max = 180ms, Average = 180ms");

            captures [1].Stop ();

            CaptureCheck (thisKind1, "[Kind: Test1] Count = 2, Min = 140ms, Max = 180ms, Average = 160ms");

            Assert.False (captures [0].IsRunning);
            Assert.False (captures [1].IsRunning);
            Assert.False (captures [2].IsRunning);
        }
    }
}

