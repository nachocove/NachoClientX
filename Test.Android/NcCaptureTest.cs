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
        [SetUp]
        public void SetUp ()
        {
            NcCapture.StopwatchClass = typeof(MockStopwatch);
        }

        [TearDown]
        public void TearDown ()
        {
            NcCapture.StopwatchClass = typeof(PlatformStopwatch);
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

        protected void CheckStatistics (Statistics2 stats, int count, int min, int max, int mean, int stddev)
        {
            Assert.AreEqual (count, stats.Count);
            Assert.AreEqual (min, stats.Min);
            Assert.AreEqual (max, stats.Max);
            Assert.AreEqual (mean, stats.Average);
            Assert.AreEqual (stddev, stats.StdDev);
        }

        [Test]
        public void TestStatistics ()
        {
            var stats = new Statistics2 ();

            stats.Update (50);
            CheckStatistics (stats, 1, 50, 50, 50, 0);

            stats.Update (100);
            CheckStatistics (stats, 2, 50, 100, 75, 25);

            stats.Update (30);
            CheckStatistics (stats, 3, 30, 100, 60, 29);

            stats.Reset ();
            CheckStatistics (stats, 0, 0, 0, 0, 0);
        }

        [Test]
        public void TestStartAndStop ()
        {
            const string kind = "TestCapture";
            const int duration = 1500;
            NcCapture.AddKind (kind);
            NcCapture.GetStatistics (kind).Reset ();

            // Test create and start
            var capture1 = NcCapture.CreateAndStart (kind);
            Assert.True (capture1.IsRunning);
            MockStopwatch.CurrentMillisecond += duration;
            capture1.Stop ();
            CheckStatistics (NcCapture.GetStatistics (kind), 1, duration, duration, duration, 0);

            // Restart using the same capture. Since it is not disposed, it should be reusable
            capture1.Start ();
            MockStopwatch.CurrentMillisecond += duration;
            capture1.Stop ();
            CheckStatistics (NcCapture.GetStatistics (kind), 2, duration, duration, duration, 0);

            // Capture with using block
            using (capture1) {
                capture1.Start ();
                MockStopwatch.CurrentMillisecond += duration;
            }
            CheckStatistics (NcCapture.GetStatistics (kind), 3, duration, duration, duration, 0);

            // Use it again and it should throw an exception
            Assert.Throws<ObjectDisposedException> (() => {
                capture1.Start ();
            });

            // Test a NcCapture.Start()
            // Test the recursive pattern. Designed for multiple derived class with and without
            // override basic class method.
            NcCapture.GetStatistics (kind).Reset ();
            using (capture1 = NcCapture.CreateAndStart (kind)) {
                Assert.False (capture1.IsRecursive);
                MockStopwatch.CurrentMillisecond += 1000;
                using (var capture2 = NcCapture.CreateAndStart (kind)) {
                    Assert.True (capture2.IsRecursive);
                    MockStopwatch.CurrentMillisecond += 2000;
                }
                MockStopwatch.CurrentMillisecond += 4000;
            }
            CheckStatistics (NcCapture.GetStatistics (kind), 1, 7000, 7000, 7000, 0);
        }

        [Test]
        public void TestPauseAndResume ()
        {
            const string kind = "TestPauseAndResume";
            NcCapture.AddKind (kind);
            NcCapture.GetStatistics (kind).Reset ();

            // Note: to use Pause() and Resume(), one should use using() block. As exiting that block
            //       (due to cancellation) will trigger a disposal and the capture will be stopped.

            // Start the stopwatch. Run for 40 msec
            var capture = NcCapture.CreateAndStart (kind);
            Assert.True (capture.IsRunning);
            MockStopwatch.CurrentMillisecond += 40;

            // Pause it. Run for another 160 msec but they should not factor into the capture duraiton
            NcCapture.PauseKind (kind);
            Assert.True (capture.IsRunning);
            MockStopwatch.CurrentMillisecond += 160;

            // Resume it. Run for another 60 msec. This should be include into the capture
            NcCapture.ResumeKind (kind);
            Assert.True (capture.IsRunning);
            MockStopwatch.CurrentMillisecond += 60;
            capture.Stop ();

            CheckStatistics (NcCapture.GetStatistics (kind), 1, 100, 100, 100, 0);

            Assert.False (capture.IsRunning);

            capture.Dispose ();
        }
    }
}

