//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;
using NachoPlatform;

namespace NachoCore.Utils
{
    public class NcCommStatus : INcCommStatus
    {
        List<ServerTracker> Trackers;

        NcTimer TrackerMonitorTimer;

        ServerTracker GetTracker (int serverId)
        {
            // Mutex so we get an atomic find-or-create tracker.
            lock (syncRoot) {
                var tracker = Trackers.Where (x => serverId == x.ServerId).SingleOrDefault ();
                if (null == tracker) {
                    tracker = new ServerTracker (serverId);
                    Trackers.Add (tracker);
                }
                return tracker;
            }
        }

        static volatile NcCommStatus instance;
        static object syncRoot = new Object ();

        NcCommStatus ()
        {
            Trackers = new List<ServerTracker> ();
            Status = NetStatusStatusEnum.Up;
            Speed = NetStatusSpeedEnum.WiFi_0;
            NetStatus.Instance.NetStatusEvent += NetStatusEventHandler;
            NcApplication.Instance.StatusIndEvent += StatusIndEventHandler;

            // TODO: we really only need to run the timer if one or more tracker reports degraded.
            TrackerMonitorTimer = new NcTimer ("NcCommStatus", status => ResetTrackers (true), null, 1000, 1000);
            TrackerMonitorTimer.Stfu = true;
        }

        public void StatusIndEventHandler (Object sender, EventArgs ea)
        {
            var siea = (StatusIndEventArgs)ea;
            switch (siea.Status.SubKind) {
            case NcResult.SubKindEnum.Info_ExecutionContextChanged:
                switch ((NcApplication.ExecutionContextEnum)siea.Status.Value) {
                case NcApplication.ExecutionContextEnum.Foreground:
                case NcApplication.ExecutionContextEnum.QuickSync:
                    Status = NetStatusStatusEnum.Up;
                    ResetTrackers (true);
                    break;
                }
                break;
            }
        }

        public void Reset ()
        {
            Status = NetStatusStatusEnum.Up;
            ResetTrackers (false);
        }

        void ResetTrackers (bool alert)
        {
            lock (syncRoot) {
                foreach (var tracker in Trackers) {
                    var oldQ = tracker.Quality;
                    tracker.UpdateQuality ();
                    if (alert) {
                        MaybeEvent (oldQ, tracker);
                    }
                }
            }
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

        void NetStatusEventHandler (Object sender, NetStatusEventArgs e)
        {
            UpdateState (e.Status, e.Speed, "NetStatusEventHandler");
        }

        public enum CommQualityEnum
        {
            OK,
            Degraded,
            Unusable,
        };

        public NetStatusStatusEnum Status { get; set; }

        public NetStatusSpeedEnum Speed { get; set; }

        public CommQualityEnum Quality (int serverId)
        {
            var tracker = GetTracker (serverId);
            if (null == tracker) {
                Log.Error (Log.LOG_STATE, "Quality: Can't find server with Id {0}.", serverId);
                return CommQualityEnum.OK;
            }
            return tracker.Quality;
        }

        public event NcCommStatusServerEventHandler CommStatusServerEvent;

        public event NetStatusEventHandler CommStatusNetEvent;

        void MaybeEvent (CommQualityEnum oldQ, ServerTracker tracker)
        {
            var newQ = tracker.Quality;
            if (oldQ != newQ && null != CommStatusServerEvent) {
                CommStatusServerEvent (this, new NcCommStatusServerEventArgs (tracker.ServerId, tracker.Quality));
            }
        }

        public void ReportCommResult (int serverId, bool didFailGenerally)
        {
            lock (syncRoot) {
                var tracker = GetTracker (serverId);
                var oldQ = tracker.Quality;
                tracker.UpdateQuality (didFailGenerally);
                MaybeEvent (oldQ, tracker);
            }
        }

        public void ReportCommResult (int serverId, DateTime delayUntil)
        {
            lock (syncRoot) {
                var tracker = GetTracker (serverId);
                var oldQ = tracker.Quality;
                tracker.UpdateQuality (delayUntil);
                MaybeEvent (oldQ, tracker);
            }
        }

        public void ReportCommResult (int accountId, McAccount.AccountCapabilityEnum capabilities, bool didFailGenerally)
        {
            lock (syncRoot) {
                ReportCommResult (GetServerId (accountId, capabilities), didFailGenerally);
            }
        }

        public void ReportCommResult (int accountId, string host, bool didFailGenerally)
        {
            lock (syncRoot) {
                ReportCommResult (GetServerId (accountId, host), didFailGenerally);
            }
        }

        public void ReportCommResult (int accountId, string host, DateTime delayUntil)
        {
            lock (syncRoot) {
                ReportCommResult (GetServerId (accountId, host), delayUntil);
            }
        }

        int GetServerId (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            var server = McServer.QueryByAccountIdAndCapabilities (accountId, capabilities);
            // Allow 0 for scenario when we don't yet have a McServer record in DB (ex: auto-d).
            return (null == server) ? 0 : server.Id;
        }

        int GetServerId (int accountId, string host)
        {
            var server = McServer.QueryByHost (accountId, host);
            // Allow 0 for scenario when we don't yet have a McServer record in DB (ex: auto-d).
            return (null == server) ? 0 : server.Id;
        }

        public void Reset (int serverId)
        {
            var tracker = GetTracker (serverId);
            var noChange = tracker.Reset ();
            if (null != CommStatusServerEvent && !noChange) {
                CommStatusServerEvent (this, new NcCommStatusServerEventArgs (serverId, tracker.Quality));
            }
        }

        DateTime LastRefreshed = DateTime.MinValue;
        const uint RefreshTimeoutSeconds = 10;

        /// <summary>
        /// Refresh the network status and speed. Called during Backend start for an account.
        /// </summary>
        public void Refresh ()
        {
            if (LastRefreshed.AddSeconds (RefreshTimeoutSeconds) < DateTime.UtcNow) {
                NetStatusStatusEnum currStatus;
                NetStatusSpeedEnum currSpeed;
                NetStatus.Instance.GetCurrentStatus (out currStatus, out currSpeed);
                UpdateState (currStatus, currSpeed, "Refresh");
                LastRefreshed = DateTime.UtcNow;
            }
        }

        public bool IsRateLimited (int serverId)
        {
            var tracker = GetTracker (serverId);
            return !tracker.Throttle.HasTokens ();
        }

        void UpdateState (NetStatusStatusEnum status, NetStatusSpeedEnum speed, string source)
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
                    var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_NetworkStatus);
                    result.Value = info;
                    NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                        Status = result,
                        Account = ConstMcAccount.NotAccountSpecific,
                    });
                }
            }
        }

        public void ForceUp (string source)
        {
            if (Status != NetStatusStatusEnum.Up) {
                UpdateState (NetStatusStatusEnum.Up, Speed, source);
            }
        }
    }
}
