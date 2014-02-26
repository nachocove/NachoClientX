//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;
using NachoPlatform;

namespace NachoCore.Utils
{
    public class NcCommStatus
    {
        private List<ServerTracker> Trackers;

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
            UserInterventionIsRequired = false;
            NetStatus.Instance.NetStatusEvent += NetStatusEventHandler;
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

        public void NetStatusEventHandler (Object sender, NetStatusEventArgs e)
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

        public bool UserInterventionIsRequired { get; set; }

        public delegate void NcCommStatusServerEventHandler (Object sender, NcCommStatusServerEventArgs e);

        public event NcCommStatusServerEventHandler CommStatusServerEvent;

        public event NetStatusEventHandler CommStatusNetEvent;
        // FIXME - user intervention.
        // NOTE - this could be enhanced by tracking RTT and timeouts, folding back into setting the timeout value.
        public void ReportCommResult (int serverId, bool didFailGenerally)
        {
            lock (syncRoot) {
                var tracker = GetTracker (serverId);
                var oldQ = tracker.Quality;
                tracker.UpdateQuality (didFailGenerally);
                var newQ = tracker.Quality;
                if (oldQ != newQ && null != CommStatusServerEvent) {
                    CommStatusServerEvent (this, new NcCommStatusServerEventArgs (serverId, tracker.Quality));
                }
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
