//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using System.Diagnostics;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;

namespace Test.Common
{
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
            NcCapture.StopwatchClass = typeof(PlatformStopwatch);
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
            MockStopwatch.CurrentMillisecond += elapsed;
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

            CaptureStartStop (captures[0], thisKind1, 50, "[Kind: Test1] Count = 1, Min = 50ms, Max = 50ms, Average = 50ms, StdDev = 0ms");

            captures[0].Reset ();
            CaptureStartStop (captures[0], thisKind1, 100, "[Kind: Test1] Count = 2, Min = 50ms, Max = 100ms, Average = 75ms, StdDev = 25ms");

            captures[0].Reset ();
            CaptureStartStop (captures[0], thisKind1, 30, "[Kind: Test1] Count = 3, Min = 30ms, Max = 100ms, Average = 60ms, StdDev = 29ms");
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

            MockStopwatch.CurrentMillisecond += 40;

            captures [1].Start ();
            Assert.True (captures [1].IsRunning);

            MockStopwatch.CurrentMillisecond += 80;

            NcCapture.PauseKind (thisKind1);

            Assert.True (captures [0].IsRunning);
            Assert.True (captures [1].IsRunning);
            Assert.False (captures [2].IsRunning);

            MockStopwatch.CurrentMillisecond += 160;

            NcCapture.ResumeKind (thisKind1);

            Assert.True (captures [0].IsRunning);
            Assert.True (captures [1].IsRunning);
            Assert.False (captures [2].IsRunning);

            MockStopwatch.CurrentMillisecond += 60;

            captures [0].Stop ();

            CaptureCheck (thisKind1, "[Kind: Test1] Count = 1, Min = 180ms, Max = 180ms, Average = 180ms, StdDev = 0ms");

            captures [1].Stop ();

            CaptureCheck (thisKind1, "[Kind: Test1] Count = 2, Min = 140ms, Max = 180ms, Average = 160ms, StdDev = 20ms");

            Assert.False (captures [0].IsRunning);
            Assert.False (captures [1].IsRunning);
            Assert.False (captures [2].IsRunning);
        }
    }
}

