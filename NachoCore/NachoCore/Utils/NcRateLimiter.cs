//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;

namespace NachoCore.Utils
{
    public class NcRateLimter
    {
        public NcRateLimter (double nPerSecond, double refreshSeconds)
        {
            LockObj = new object ();
            Allowance = (int)(nPerSecond * refreshSeconds);
            NcAssert.True (0 < Allowance);
            RefreshMsecs = (int)(refreshSeconds * 1000.0);
            NcAssert.True (0 < RefreshMsecs);
            Refresh ();
        }

        public bool Enabled { set; get; }

        public int RefreshMsecs { get; set; }
        public int Allowance { get; set; }
        private object LockObj;
        private DateTime LastRefresh;
        private int Remaining;

        public void Refresh ()
        {
            lock (LockObj) {
                LastRefresh = DateTime.UtcNow;
                Remaining = Allowance;
            }
        }

        public bool HasTokens ()
        {
            return (0 < Remaining);
        }

        public bool TakeToken ()
        {
            if (!Enabled) {
                return true;
            }
            lock (LockObj) {
                if (LastRefresh.AddMilliseconds (RefreshMsecs) < DateTime.UtcNow) {
                    Refresh ();
                }
                if (0 < Remaining) {
                    --Remaining;
                    return true;
                }
            }
            return false;
        }

        public void TakeTokenOrSleep ()
        {
            while (!TakeToken ()) {
                var duration = (int)(LastRefresh.AddMilliseconds (RefreshMsecs) - DateTime.UtcNow).TotalMilliseconds;
                duration = (0 < duration) ? duration : 1;
                // Log.Info (Log.LOG_SYS, "NcRateLimiter:Sleep");
                Thread.Sleep (duration);
                // Log.Info (Log.LOG_SYS, "NcRateLimiter:Wake");
            }
        }
    }
}
