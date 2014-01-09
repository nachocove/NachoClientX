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
            public bool DidSucceed { get; set; }

            public DateTime When { get; set; }
        }

        public class ServerTracker
        {
            public int ServerId { get; set; }

            public CommQualityEnum Quality { get; set; }

            public CommStatusEnum Status { get; set; }

            public CommSpeedEnum Speed { get; set; }

            public List<ServerAccess> Accesses { get; set; }

            public ServerTracker ()
            {
                Accesses = new List<ServerAccess> ();
                Reset ();
            }

            public CommQualityEnum UpdateQuality (bool didSucceed)
            {
                Accesses.Add (new ServerAccess () { DidSucceed = didSucceed, When = DateTime.UtcNow });

                // Remove stale entries.
                var trailing = DateTime.UtcNow;
                trailing.AddMinutes (-3.0);
                Accesses.RemoveAll (x => x.When < trailing);

                // Say "OK" unless we have enough failure.
                if (4 < Accesses.Count) {
                    Quality = CommQualityEnum.OK;
                    return Quality;
                }

                // Compute quality.
                var pos = Convert.ToDouble (Accesses.Where (x => true == x.DidSucceed).Count ());
                var neg = Convert.ToDouble (Accesses.Where (x => false == x.DidSucceed).Count ());
                var total = pos + neg;
                if (0.0 == total) {
                    Quality = CommQualityEnum.OK;
                } else if (0.0 == pos || 0.3 > (pos / total)) {
                    Quality = CommQualityEnum.Unusable;
                } else if (0.8 > (pos / total)) {
                    Quality = CommQualityEnum.Degraded;
                } else {
                    Quality = CommQualityEnum.OK;
                }
                Console.WriteLine ("COMM QUALITY {0}:{1}/{2}", ServerId, (pos / total), Quality);
                return Quality;
            }

            public void Reset ()
            {
                Quality = CommQualityEnum.OK;
                Status = CommStatusEnum.Up;
                Speed = CommSpeedEnum.WiFi;
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
        // NOTE - this could be enhanced by tracking RTT and timeouts, folding back into setting the timeout value.
        public void ReportCommResult (int serverId, bool didSucceed)
        {
            var tracker = GetTracker (serverId);
            var oldQ = tracker.Quality;
            var newQ = tracker.UpdateQuality (didSucceed);
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
        public void ReportCommResult (string host, bool didSucceed)
        {
            ReportCommResult (GetServerId (host), didSucceed);
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

