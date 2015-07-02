//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;
using NachoPlatform;

namespace NachoCore.Utils
{
    // class for tracking the access health/quality of a server.
    // TODO - this could be enhanced by tracking RTT and timeouts, folding back into setting the timeout value.
    public class ServerTracker
    {
        public const double KSunsetMinutes = -3.0;

        // class for the result of a single access attempt on a server.
        public class ServerAccess
        {
            public bool DidFailGenerally { get; set; }

            public DateTime When { get; set; }
        }

        public int ServerId { get; set; }

        public NcCommStatus.CommQualityEnum Quality { get; set; }

        public List<ServerAccess> Accesses { get; set; }

        public DateTime DelayUntil { get; set; }

        public NcRateLimter Throttle { get; set; }

        bool IsThrottled { get; set; }

        public ServerTracker (int serverId)
        {
            Accesses = new List<ServerAccess> ();
            DelayUntil = DateTime.MinValue;
            ServerId = serverId;
            // TODO: we will want to vary the parameters by protcol & server.
            Throttle = new NcRateLimter (0.4, 1 * 60);
            Throttle.Enabled = true;
            Reset ();
        }

        public void UpdateQuality ()
        {
            // Forced delay eclipses the rest of the logic.
            if (DateTime.UtcNow < DelayUntil) {
                return;
            }
            // Remove stale entries.
            var trailing = DateTime.UtcNow;
            trailing = trailing.AddMinutes (KSunsetMinutes);
            Accesses.RemoveAll (x => x.When < trailing);

            // Say "OK" unless we have enough to judge failure.
            if (4 > Accesses.Count) {
                Quality = NcCommStatus.CommQualityEnum.OK;
                return;
            }

            // Compute quality.
            var neg = Convert.ToDouble (Accesses.Where (x => true == x.DidFailGenerally).Count ());
            var pos = Convert.ToDouble (Accesses.Where (x => false == x.DidFailGenerally).Count ());
            var total = pos + neg;
            if (0.0 == pos || 0.3 > (pos / total)) {
                Quality = NcCommStatus.CommQualityEnum.Unusable;
            } else if (0.8 > (pos / total)) {
                Quality = NcCommStatus.CommQualityEnum.Degraded;
            } else {
                Quality = NcCommStatus.CommQualityEnum.OK;
            }
        }

        public void UpdateQuality (DateTime delayUntil)
        {
            DelayUntil = delayUntil;
            Quality = NcCommStatus.CommQualityEnum.Unusable;
        }

        public void UpdateQuality (bool didFailGenerally)
        {
            Accesses.Add (new ServerAccess () { DidFailGenerally = didFailGenerally, When = DateTime.UtcNow });
            UpdateQuality ();

            // Count access against rate-limiter.
            Throttle.TakeToken ();
            if (Throttle.HasTokens ()) {
                IsThrottled = false;
            } else {
                if (!IsThrottled) {
                    Log.Info (Log.LOG_SYS, "Proactive throttling threshold reached.");
                    IsThrottled = true;
                }
            }
        }

        public bool Reset ()
        {
            bool noChange = (NcCommStatus.CommQualityEnum.OK == Quality);
            Quality = NcCommStatus.CommQualityEnum.OK;
            Accesses.Clear ();
            return noChange;
        }
    }
}

