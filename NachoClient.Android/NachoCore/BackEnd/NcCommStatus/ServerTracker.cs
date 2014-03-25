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

        public ServerTracker (int serverId)
        {
            Accesses = new List<ServerAccess> ();
            ServerId = serverId;
            Reset ();
        }

        public void UpdateQuality ()
        {
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
            Log.Info (Log.LOG_SYS, "COMM QUALITY {0}:{1}/{2}", ServerId, (pos / total), Quality);
        }

        public void UpdateQuality (bool didFailGenerally)
        {
            Accesses.Add (new ServerAccess () { DidFailGenerally = didFailGenerally, When = DateTime.UtcNow });
            UpdateQuality ();
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

