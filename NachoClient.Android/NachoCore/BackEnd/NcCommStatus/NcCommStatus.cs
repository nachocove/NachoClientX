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
        private List<ServerTracker> Trackers;

        #pragma warning disable 414
        private NcTimer TrackerMonitorTimer;
        #pragma warning restore 414

        private ServerTracker GetTracker (int serverId)
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

        private static volatile NcCommStatus instance;
        private static object syncRoot = new Object ();

        private NcCommStatus ()
        {
            Trackers = new List<ServerTracker> ();
            Status = NetStatusStatusEnum.Up;
            Speed = NetStatusSpeedEnum.WiFi;
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
            UpdateState (e.Status, e.Speed);
        }

        public enum CommQualityEnum
        {
            OK,
            Degraded,
            Unusable,
        };

        public NetStatusStatusEnum Status { get; set; }

        public NetStatusSpeedEnum Speed { get; set; }

        public event NcCommStatusServerEventHandler CommStatusServerEvent;

        public event NetStatusEventHandler CommStatusNetEvent;

        private void MaybeEvent (CommQualityEnum oldQ, ServerTracker tracker)
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

        public void ReportCommResult (string host, bool didFailGenerally)
        {
            lock (syncRoot) {
                ReportCommResult (GetServerId (host), didFailGenerally);
            }
        }

        private int GetServerId (string host)
        {
            var server = McServer.QueryByHost (host);
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

        public void Refresh ()
        {
            // TODO - don't call again if recent (cache).
            NetStatusStatusEnum currStatus;
            NetStatusSpeedEnum currSpeed;
            NetStatus.Instance.GetCurrentStatus (out currStatus, out currSpeed);
            UpdateState (currStatus, currSpeed);
        }

        private void UpdateState (NetStatusStatusEnum status, NetStatusSpeedEnum speed)
        {
            lock (syncRoot) {
                NetStatusStatusEnum oldStatus = Status;
                NetStatusSpeedEnum oldSpeed = Speed;
                Status = status;
                Speed = speed;
                Log.Info (Log.LOG_STATE, "UPDATE STATE {0}=>{1} {2}=>{3}", oldStatus, Status, oldSpeed, Speed);
                if (oldStatus != Status && null != CommStatusNetEvent) {
                    CommStatusNetEvent (this, new NetStatusEventArgs (Status, Speed));
                }
            }
        }
    }
}
