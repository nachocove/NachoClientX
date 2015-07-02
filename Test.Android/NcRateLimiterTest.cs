//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Utils;

namespace Test.Common
{
    public class NcRateLimiterTest : NcTestBase
    {
        [Test]
        public void Simple ()
        {
            var rl = new NcRateLimter (1.0, 1.0);
            Assert.NotNull (rl);
            for (var i = 0; i < 100; i++) {
                Assert.True (rl.TakeToken ());
            }
            rl.Enabled = true;
            NcAssert.True (rl.TakeToken ());
            NcAssert.True (!rl.TakeToken ());
        }

        [Test]
        public void MoreThanOne ()
        {
            var rl = new NcRateLimter (100.0, 0.25);
            rl.Enabled = true;
            Assert.NotNull (rl);
            var WaitFor = DateTime.UtcNow.AddMilliseconds (300);
            for (var i = 0; i < 25; i++) {
                Assert.True (rl.TakeToken ());
            }
            Assert.True (!rl.TakeToken ());
            while (DateTime.UtcNow < WaitFor) {
                ;
            }
            for (var i = 0; i < 25; i++) {
                Assert.True (rl.TakeToken ());
            }
            Assert.True (!rl.TakeToken ());
        }

        [Test]
        public void Sleepy ()
        {
            var rl = new NcRateLimter (100.0, 0.25);
            rl.Enabled = true;
            Assert.NotNull (rl);
            var start = DateTime.UtcNow;
            for (var i = 0; i < 25; i++) {
                Assert.True (rl.TakeToken ());
            }
            rl.TakeTokenOrSleep ();
            Assert.True ((DateTime.UtcNow - start).TotalSeconds >= 0.25);
            for (var i = 0; i < 24; i++) {
                Assert.True (rl.TakeToken ());
            }
            Assert.True (!rl.TakeToken ());
        }
    }
}

