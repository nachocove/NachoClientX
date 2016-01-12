//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;

namespace NachoCore.Utils
{
    /// <summary>
    /// Server tracker. Class for tracking the access health/quality of a server.
    /// </summary>
    /// <remarks>
    /// TODO - this could be enhanced by tracking RTT and timeouts, folding back into setting the timeout value
    /// </remarks>
    public class ServerTracker
    {
        /// <summary>
        /// Minutes after which a report (A ServerAccess) times out and is removed from the list of Accesses
        /// </summary>
        const int KSunsetSeconds = 180;

        /// <summary>
        /// Default nPerSecond for a server throttle to engage.
        /// </summary>
        const double KDefaultAccessThreshold = 0.4;

        /// <summary>
        /// Default throttle refresh seconds
        /// </summary>
        const double KDefaultRefreshSeconds = 60;

        /// <summary>
        /// Ratio at which we declare a server unusable
        /// </summary>
        const double KUnusableThreshold = 0.3;

        /// <summary>
        /// Ratio at which we declare a server degraded
        /// </summary>
        const double KDegradedThreshold = 0.8;

        /// <summary>
        /// Minimum number of data points we need to make a judgement.
        /// </summary>
        const int KMinJudgementCount = 4;

        /// <summary>
        /// class for the result of a single access attempt on a server
        /// </summary>
        public class ServerAccess
        {
            public bool DidFailGenerally { get; protected set; }

            public DateTime When { get; protected set; }

            public ServerAccess (bool didFailGenerally)
            {
                When = DateTime.UtcNow;
                DidFailGenerally = didFailGenerally;
            }
        }

        /// <summary>
        /// The Server Id of the tracker
        /// </summary>
        /// <value>The server identifier.</value>
        public int ServerId { get; protected set; }

        /// <summary>
        /// The Quality of the server, i.e. Ok, Degraded, or Unusable.
        /// </summary>
        /// <value>The quality.</value>
        public NcCommStatus.CommQualityEnum Quality { get; protected set; }

        /// <summary>
        /// The throttling rate limiter. Based on the number of accesses reported by the Protocol Controller.
        /// </summary>
        /// <value>The throttle.</value>
        public NcRateLimter Throttle { get; protected set; }

        /// <summary>
        /// The list of accesses.
        /// </summary>
        /// <value>The accesses.</value>
        List<ServerAccess> Accesses { get; set; }

        DateTime DelayUntil { get; set; }

        bool IsThrottled { get; set; }

        public ServerTracker (int serverId)
        {
            Accesses = new List<ServerAccess> ();
            DelayUntil = DateTime.MinValue;
            ServerId = serverId;
            // TODO: we will want to vary the parameters by protcol & server.
            Throttle = new NcRateLimter (KDefaultAccessThreshold, KDefaultRefreshSeconds);
            Throttle.Enabled = true;
            Reset ();
        }

        /// <summary>
        /// Updates the quality of a tracked server. Any 'timed out' (sunsetted) Access records are
        /// removed, then a tally of good and bad accesses are calculated and a ration of the two
        /// is used to determine whether the server is Ok, Degraded, or Unusable.
        /// </summary>
        public void UpdateQuality ()
        {
            // Forced delay eclipses the rest of the logic.
            if (DateTime.UtcNow < DelayUntil) {
                return;
            }
            // Remove stale entries.
            var trailing = DateTime.UtcNow;
            trailing = trailing.AddSeconds (-KSunsetSeconds);
            Accesses.RemoveAll (x => x.When < trailing);

            // Say "OK" unless we have enough to judge failure.
            if (KMinJudgementCount > Accesses.Count) {
                Quality = NcCommStatus.CommQualityEnum.OK;
                return;
            }

            // Compute quality.
            var neg = Accesses.Count (x => x.DidFailGenerally);
            var pos = Accesses.Count (x => !x.DidFailGenerally);
            var total = pos + neg;
            var ratio = ((double)pos / (double)total);
            if (0 == pos || KUnusableThreshold > ratio) {
                Quality = NcCommStatus.CommQualityEnum.Unusable;
            } else if (KDegradedThreshold > ratio) {
                Quality = NcCommStatus.CommQualityEnum.Degraded;
            } else {
                Quality = NcCommStatus.CommQualityEnum.OK;
            }
        }

        /// <summary>
        /// Force-mark the server as unusable until a certain time
        /// </summary>
        /// <param name="delayUntil">Delay until.</param>
        public void UpdateQuality (DateTime delayUntil)
        {
            DelayUntil = delayUntil;
            Quality = NcCommStatus.CommQualityEnum.Unusable;
        }

        /// <summary>
        /// Add an access to the list for the server, indicating whether the server was available or not.
        /// </summary>
        /// <remarks>
        /// Also updates the ratelimiter and determines if subsequent accesses should be throttled.
        /// </remarks>
        /// <param name="didFailGenerally">If set to <c>true</c> did fail generally.</param>
        public void UpdateQuality (bool didFailGenerally)
        {
            Accesses.Add (new ServerAccess (didFailGenerally));
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

        /// <summary>
        /// Force the server as 'Ok', and remove all accesses.
        /// </summary>
        public bool Reset ()
        {
            bool noChange = (NcCommStatus.CommQualityEnum.OK == Quality);
            Quality = NcCommStatus.CommQualityEnum.OK;
            Accesses.Clear ();
            return noChange;
        }
    }
}

