//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    public class NcRateLimter
    {
        public NcRateLimter (double nPerSecond, double refreshSeconds)
        {
            Allowance = (int)(nPerSecond * refreshSeconds);
            NcAssert.True (0 < Allowance);
            RefreshMsecs = (int)(refreshSeconds * 1000.0);
            NcAssert.True (0 < RefreshMsecs);
            Refresh ();
        }

        public bool Enabled { set; get; }
        private int RefreshMsecs;
        private int Allowance;
        private DateTime LastRefresh;
        private int Remaining;

        private void Refresh ()
        {
            LastRefresh = DateTime.UtcNow;
            Remaining = Allowance;
        }

        public bool TakeToken ()
        {
            if (!Enabled) {
                return true;
            }
            if (LastRefresh.AddMilliseconds(RefreshMsecs) < DateTime.UtcNow) {
                Refresh ();
            }
            if (0 < Remaining) {
                --Remaining;
                return true;
            }
            return false;
        }
    }
}
