//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace Test.Common
{
    public class NcApplicationTest
    {
        [Test]
        public void RateLimitBgDbWritesInScroll ()
        {
            NcApplication.Instance.TestOnlyInvokeUseCurrentThread = true;
            var server = new McServer () {
                AccountId = 1,
                Capabilities = McAccount.ActiveSyncCapabilities,
                Host = "example.com"
            };
            server.Insert ();
            NcModel.Instance.EngageRateLimiter ();
            NcModel.Instance.RateLimiter.Refresh ();
            // See that writes aren't inhibited to begin with (UI/BG).
            var start = DateTime.UtcNow;
            for (var i = 0; i < 2 * NcModel.Instance.RateLimiter.Allowance; i++) {
                server.Update ();
            }
            var stop = DateTime.UtcNow;
            Assert.True (start.AddMilliseconds (NcModel.Instance.RateLimiter.RefreshMsecs) > stop);
            NcModel.Instance.RateLimiter.Refresh ();
            var task = Task.Run (() => {
                Assert.True (System.Threading.Thread.CurrentThread.ManagedThreadId !=
                    NcApplication.Instance.UiThreadId);
                start = DateTime.UtcNow;
                for (var i = 0; i < 2 * NcModel.Instance.RateLimiter.Allowance; i++) {
                    server.Update ();
                }
                stop = DateTime.UtcNow;
                var bound = start.AddMilliseconds (NcModel.Instance.RateLimiter.RefreshMsecs);
                // Console.WriteLine("{0} => {1} < {2}", start.Millisecond, stop.Millisecond, bound.Millisecond);
                Assert.True (bound > stop);
            });
            Assert.True (task.Wait (3 * NcModel.Instance.RateLimiter.RefreshMsecs));
            // Use the abatement mechanism to turn on the rate limiter.
            using (NcAbate.UIAbatement ()) {
                // See that writes aren't inhibited for UI thread.
                NcModel.Instance.RateLimiter.Refresh ();
                start = DateTime.UtcNow;
                for (var i = 0; i < 2 * NcModel.Instance.RateLimiter.Allowance; i++) {
                    server.Update ();
                }
                stop = DateTime.UtcNow;
                Assert.True (start.AddMilliseconds (NcModel.Instance.RateLimiter.RefreshMsecs) > stop);
                // See that writes are inhibited for BG thread.
                NcModel.Instance.RateLimiter.Refresh ();
                task = Task.Run (() => {
                    Assert.True (System.Threading.Thread.CurrentThread.ManagedThreadId !=
                    NcApplication.Instance.UiThreadId);
                    start = DateTime.UtcNow;
                    for (var i = 0; i < 2 * NcModel.Instance.RateLimiter.Allowance; i++) {
                        server.Update ();
                    }
                    stop = DateTime.UtcNow;
                    var bound = start.AddMilliseconds (NcModel.Instance.RateLimiter.RefreshMsecs);
                    // Console.WriteLine("{0} => {1} < {2}", start.Millisecond, stop.Millisecond, bound.Millisecond);
                    Assert.True (bound < stop);
                });
                Assert.True (task.Wait (10 * NcModel.Instance.RateLimiter.RefreshMsecs));
            }
            // See that writes aren't inhibited (UI/BG).
            NcModel.Instance.RateLimiter.Refresh ();
            start = DateTime.UtcNow;
            for (var i = 0; i < 2 * NcModel.Instance.RateLimiter.Allowance; i++) {
                server.Update ();
            }
            stop = DateTime.UtcNow;
            Assert.True (start.AddMilliseconds (NcModel.Instance.RateLimiter.RefreshMsecs) > stop);
            NcModel.Instance.RateLimiter.Refresh ();
            task = Task.Run (() => {
                start = DateTime.UtcNow;
                for (var i = 0; i < 2 * NcModel.Instance.RateLimiter.Allowance; i++) {
                    server.Update ();
                }
                stop = DateTime.UtcNow;
                var bound = start.AddMilliseconds (NcModel.Instance.RateLimiter.RefreshMsecs);
                // Console.WriteLine("{0} => {1} < {2}", start.Millisecond, stop.Millisecond, bound.Millisecond);
                Assert.True (bound > stop);
            });
            Assert.True (task.Wait (3 * NcModel.Instance.RateLimiter.RefreshMsecs));
        }
    }
}

