//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;
using NachoPlatform;

namespace NachoCore.Utils
{
    /// <summary>
    /// Communication Status class
    /// </summary>
    public class NcCommStatus : INcCommStatus
    {
        /// <summary>
        /// List of trackers
        /// </summary>
        private List<ServerTracker> Trackers;

        /// <summary>
        /// Timer used to periodically recalculate all tracker qualities.
        /// </summary>
        /// <remarks>
        /// TODO: Change this from a periodic to an event-based processing.
        /// We really only need to run the timer if one or more tracker reports degraded.
        /// </remarks>
        private NcTimer TrackerMonitorTimer;

        private ServerTracker GetTracker (int serverId)
        {
            // Mutex so we get an atomic find-or-create tracker.
            lock (syncRoot) {
                var tracker = Trackers.SingleOrDefault (x => serverId == x.ServerId);
                if (null == tracker) {
                    tracker = new ServerTracker (serverId);
                    Trackers.Add (tracker);
                }
                return tracker;
            }
        }

        private static volatile NcCommStatus instance;
        private static object syncRoot = new Object ();

        private NcCommStatus ()
        {
            Trackers = new List<ServerTracker> ();
            Status = NetStatusStatusEnum.Up;
            Speed = NetStatusSpeedEnum.WiFi_0;
            NetStatus.Instance.NetStatusEvent += NetStatusEventHandler;
            // TODO: we really only need to run the timer if one or more tracker reports degraded.
            TrackerMonitorTimer = new NcTimer ("NcCommStatus", status => {
                lock (syncRoot) {
                    foreach (var tracker in Trackers) {
                        var oldQ = tracker.Quality;
                        tracker.UpdateQuality ();
                        MaybeEvent (oldQ, tracker);
                    }
                }
            }, null, 1000, 1000);
            TrackerMonitorTimer.Stfu = true;
        }

        public static NcCommStatus Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new NcCommStatus ();
                        }
                    }
                }
                return instance;
            }
        }

        private void NetStatusEventHandler (Object sender, NetStatusEventArgs e)
        {
            UpdateState (e.Status, e.Speed, "NetStatusEventHandler");
        }

        public enum CommQualityEnum
        {
            OK,
            Degraded,
            Unusable,
        };

        /// <summary>
        /// Overall status of network. Up or Down.
        /// </summary>
        /// <value>The status.</value>
        public NetStatusStatusEnum Status { get; set; }

        /// <summary>
        /// A best guess as to the speed of the network. We only care roughly, i.e.
        /// whether we're on wifi (we can do as much as we want) or on cell (where we
        /// want to do less and be more careful), etc.
        /// </summary>
        /// <value>The speed.</value>
        public NetStatusSpeedEnum Speed { get; set; }

        /// <summary>
        /// Get the quality of a server. Looks up the tracker, and returns the computed quality.
        /// </summary>
        /// <param name="serverId">Server identifier.</param>
        public CommQualityEnum Quality (int serverId)
        {
            var tracker = GetTracker (serverId);
            if (null == tracker) {
                Log.Error (Log.LOG_STATE, "Quality: Can't find server with Id {0}.", serverId);
                return CommQualityEnum.OK;
            }
            return tracker.Quality;
        }

        /// <summary>
        /// Callback delegate for when a Server Tracker quality changes.
        /// </summary>
        public event NcCommStatusServerEventHandler CommStatusServerEvent;

        /// <summary>
        /// Callback delegate for when the overall network status changes (only called
        /// if we switch from Down->Up or Up->Down).
        /// </summary>
        public event NetStatusEventHandler CommStatusNetEvent;

        /// <summary>
        /// If the tracker has changed quality, call the callback delegate.
        /// </summary>
        /// <param name="oldQ">Old quality.</param>
        /// <param name="tracker">Tracker.</param>
        private void MaybeEvent (CommQualityEnum oldQ, ServerTracker tracker)
        {
            var newQ = tracker.Quality;
            if (oldQ != newQ && null != CommStatusServerEvent) {
                CommStatusServerEvent (this, new NcCommStatusServerEventArgs (tracker.ServerId, tracker.Quality));
            }
        }

        /// <summary>
        /// Report a status for a server. Used for both 'good' and 'bad' accesses.
        /// </summary>
        /// <param name="serverId">Server identifier.</param>
        /// <param name="didFailGenerally">If set to <c>true</c> did fail generally.</param>
        public void ReportCommResult (int serverId, bool didFailGenerally)
        {
            lock (syncRoot) {
                var tracker = GetTracker (serverId);
                var oldQ = tracker.Quality;
                tracker.UpdateQuality (didFailGenerally);
                MaybeEvent (oldQ, tracker);
            }
        }

        /// <summary>
        /// Mark a server as unusable until a certain time
        /// </summary>
        /// <param name="serverId">Server identifier.</param>
        /// <param name="delayUntil">Delay until.</param>
        public void ReportCommResult (int serverId, DateTime delayUntil)
        {
            lock (syncRoot) {
                var tracker = GetTracker (serverId);
                var oldQ = tracker.Quality;
                tracker.UpdateQuality (delayUntil);
                MaybeEvent (oldQ, tracker);
            }
        }

        /// <summary>
        /// Report quality of a set of servers that match the account and capabilities. Used when, say,
        /// the EmailSender and EmailReader use the same server.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="capabilities">Capabilities.</param>
        /// <param name="didFailGenerally">If set to <c>true</c> did fail generally.</param>
        public void ReportCommResult (int accountId, McAccount.AccountCapabilityEnum capabilities, bool didFailGenerally)
        {
            lock (syncRoot) {
                ReportCommResult (GetServerId (accountId, capabilities), didFailGenerally);
            }
        }

        /// <summary>
        /// Reports quality of a server based on hostname, instead of server Id.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="host">Host.</param>
        /// <param name="didFailGenerally">If set to <c>true</c> did fail generally.</param>
        public void ReportCommResult (int accountId, string host, bool didFailGenerally)
        {
            lock (syncRoot) {
                ReportCommResult (GetServerId (accountId, host), didFailGenerally);
            }
        }

        /// <summary>
        /// Mark a server as unusable until a certain time based on the hostname, instead of server Id.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="host">Host.</param>
        /// <param name="delayUntil">Delay until.</param>
        public void ReportCommResult (int accountId, string host, DateTime delayUntil)
        {
            lock (syncRoot) {
                ReportCommResult (GetServerId (accountId, host), delayUntil);
            }
        }

        private int GetServerId (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            var server = McServer.QueryByAccountIdAndCapabilities (accountId, capabilities);
            // Allow 0 for scenario when we don't yet have a McServer record in DB (ex: auto-d).
            return (null == server) ? 0 : server.Id;
        }

        private int GetServerId (int accountId, string host)
        {
            var server = McServer.QueryByHost (accountId, host);
            // Allow 0 for scenario when we don't yet have a McServer record in DB (ex: auto-d).
            return (null == server) ? 0 : server.Id;
        }

        /// <summary>
        /// Reset the specified server and call the delegate if there's been a change.
        /// </summary>
        /// <param name="serverId">Server identifier.</param>
        public void Reset (int serverId)
        {
            var tracker = GetTracker (serverId);
            var noChange = tracker.Reset ();
            if (null != CommStatusServerEvent && !noChange) {
                CommStatusServerEvent (this, new NcCommStatusServerEventArgs (serverId, tracker.Quality));
            }
        }

        DateTime? LastRefreshed;
        const uint RefreshTimeoutSeconds = 10;

        /// <summary>
        /// Refresh the network status and speed. Called during Backend start for an account.
        /// </summary>
        public void Refresh (string tag)
        {
            if (!LastRefreshed.HasValue || LastRefreshed.Value.AddSeconds (RefreshTimeoutSeconds) < DateTime.UtcNow) {
                NetStatusStatusEnum currStatus;
                NetStatusSpeedEnum currSpeed;
                NetStatus.Instance.GetCurrentStatus (out currStatus, out currSpeed);
                UpdateState (currStatus, currSpeed, tag);
                LastRefreshed = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Determines whether the server is rate limited and should be throttled.
        /// </summary>
        /// <returns><c>true</c> if the server is rate limited and should be throttled; otherwise, <c>false</c>.</returns>
        /// <param name="serverId">Server identifier.</param>
        public bool IsRateLimited (int serverId)
        {
            var tracker = GetTracker (serverId);
            return !tracker.Throttle.HasTokens ();
        }

        /// <summary>
        /// Given outside inputs for speed and status, see if we need to update the internal state and call the delegates.
        /// </summary>
        /// <param name="status">Status.</param>
        /// <param name="speed">Speed.</param>
        /// <param name="source">Source (used for logging).</param>
        private void UpdateState (NetStatusStatusEnum status, NetStatusSpeedEnum speed, string source)
        {
            lock (syncRoot) {
                NetStatusStatusEnum oldStatus = Status;
                NetStatusSpeedEnum oldSpeed = Speed;
                Status = status;
                Speed = speed;
                Log.Info (Log.LOG_STATE, "UPDATE STATE {0}=>{1} {2}=>{3} (src {4})", oldStatus, Status, oldSpeed, Speed, source);
                if (oldStatus != Status && null != CommStatusNetEvent) {
                    var info = new NetStatusEventArgs (Status, Speed);
                    CommStatusNetEvent (this, info);
                    var result = NcResult.Info (NcResult.SubKindEnum.Info_NetworkStatus);
                    result.Value = info;
                    NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                        Status = result,
                        Account = ConstMcAccount.NotAccountSpecific,
                    });
                }
            }
        }

        /// <summary>
        /// For the network status up. Used when there's a user-initiated command (like a pull to refresh)
        /// in which case we want to try connecting regardless of what we THINK the network state is.
        /// </summary>
        /// <param name="source">Source.</param>
        public void ForceUp (string source)
        {
            if (Status != NetStatusStatusEnum.Up) {
                UpdateState (NetStatusStatusEnum.Up, Speed, source);
            }
        }
    }
}
