//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class NcCommStatus
    {
        public class ServerAccess
        {
            public bool DidFailGenerally { get; set; }

            public DateTime When { get; set; }
        }

        public class ServerTracker
        {
            public int ServerId { get; set; }

            public CommQualityEnum Quality { get; set; }

            private CommStatusEnum _Status;
            public CommStatusEnum Status {
                get { return _Status; }
                set { 
                    var oldS = _Status;
                    _Status = value;
                    if (oldS != _Status) {
                        NcCommStatus.Instance.CommStatusServerEvent (this, new NcCommStatusServerEventArgs (ServerId, Quality, 
                            _Status, _Speed));
                    }
                 }
            }

            private CommSpeedEnum _Speed;
            public CommSpeedEnum Speed { 
                get { return _Speed; }
                set { 
                    var oldS = _Speed;
                    _Speed = value;
                    if (oldS != _Speed) {
                        NcCommStatus.Instance.CommStatusServerEvent (this, new NcCommStatusServerEventArgs (ServerId, Quality, 
                            _Status, _Speed));
                    }
                }
            }

            public List<ServerAccess> Accesses { get; set; }

            public ServerTracker ()
            {
                Accesses = new List<ServerAccess> ();
                Reset ();
                // FIXME - register for Platform.Reachability.
                // FIXME - add a Dispose wher we release Platform.Reachability.
            }

            public void UpdateQuality (bool didFailGenerally)
            {
                Accesses.Add (new ServerAccess () { DidFailGenerally = didFailGenerally, When = DateTime.UtcNow });

                // Remove stale entries.
                var trailing = DateTime.UtcNow;
                trailing.AddMinutes (-3.0);
                Accesses.RemoveAll (x => x.When < trailing);

                // Say "OK" unless we have enough to judge failure.
                if (4 < Accesses.Count) {
                    Quality = CommQualityEnum.OK;
                    return;
                }

                // Compute quality.
                var neg = Convert.ToDouble (Accesses.Where (x => true == x.DidFailGenerally).Count ());
                var pos = Convert.ToDouble (Accesses.Where (x => false == x.DidFailGenerally).Count ());
                var total = pos + neg;
                if (0.0 == pos || 0.3 > (pos / total)) {
                    Quality = CommQualityEnum.Unusable;
                } else if (0.8 > (pos / total)) {
                    Quality = CommQualityEnum.Degraded;
                } else {
                    Quality = CommQualityEnum.OK;
                }
                Log.Info (Log.LOG_SYS, "COMM QUALITY {0}:{1}/{2}", ServerId, (pos / total), Quality);
            }

            public void Reset ()
            {
                Quality = CommQualityEnum.OK;
                _Status = CommStatusEnum.Up;
                _Speed = CommSpeedEnum.WiFi;
                Accesses.Clear ();
            }
        }

        private List<ServerTracker> Trackers;

        private ServerTracker GetTracker (int serverId)
        {
            var tracker = Trackers.Where (x => serverId == x.ServerId).SingleOrDefault ();
            if (null == tracker) {
                tracker = new ServerTracker () { ServerId = serverId };
                Trackers.Add (tracker);
            }
            return tracker;
        }

        private static volatile NcCommStatus instance;
        private static object syncRoot = new Object ();

        private NcCommStatus ()
        {
            Trackers = new List<ServerTracker> ();
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

        public enum CommStatusEnum
        {
            Up,
            Down,
        };

        public enum CommQualityEnum
        {
            OK,
            Degraded,
            Unusable,
        };

        public enum CommSpeedEnum
        {
            WiFi,
            CellFast,
            CellSlow,
        };

        public event EventHandler CommStatusServerEvent;
        // FIXME - user intervention.
        // NOTE - this could be enhanced by tracking RTT and timeouts, folding back into setting the timeout value.
        public void ReportCommResult (int serverId, bool didFailGenerally)
        {
            var tracker = GetTracker (serverId);
            var oldQ = tracker.Quality;
            tracker.UpdateQuality (didFailGenerally);
            var newQ = tracker.Quality;
            if (oldQ != newQ && null != CommStatusServerEvent) {
                CommStatusServerEvent (this, new NcCommStatusServerEventArgs (serverId, tracker.Quality, 
                    tracker.Status, tracker.Speed));
            }
        }

        private int GetServerId (string host)
        {
            var server = BackEnd.Instance.Db.Table<McServer> ().FirstOrDefault (x => x.Fqdn == host);
            // Allow 0 to track conditions when we don't yet have a McServer record in DB.
            return (null == server) ? 0 : server.Id;
        }

        public void ReportCommResult (string host, bool didFailGenerally)
        {
            ReportCommResult (GetServerId (host), didFailGenerally);
        }

        public void Reset (int serverId)
        {
            var tracker = GetTracker (serverId);
            tracker.Reset ();
            if (null != CommStatusServerEvent) {
                CommStatusServerEvent (this, new NcCommStatusServerEventArgs (serverId, tracker.Quality,
                    tracker.Status, tracker.Speed));
            }
        }

        public ServerTracker Tracker (string host)
        {
            return GetTracker (GetServerId (host));
        }
    }
}

