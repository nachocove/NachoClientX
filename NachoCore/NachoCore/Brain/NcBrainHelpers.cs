//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Index;
using NachoCore.Model;

namespace NachoCore.Brain
{

    public class BrainQueryAndProcess
    {
        public delegate List<object> QuerySourceFunction (int count);

        public delegate bool ProcessObjectFunction (object obj);

        public int ChunkSize { get; set; }

        public int NumberOfObjects {
            get {
                return Objects.Count;
            }
        }

        protected List<object> Objects;

        protected QuerySourceFunction QueryFunction;

        protected ProcessObjectFunction ProcessFunction;

        public BrainQueryAndProcess (QuerySourceFunction queryFunction, ProcessObjectFunction processFunction, int chunkSize = 5)
        {
            QueryFunction = queryFunction;
            ProcessFunction = processFunction;
            ChunkSize = chunkSize;
            Objects = new List<object> ();
        }

        public bool Initialize ()
        {
            Objects = QueryFunction (ChunkSize);
            return (0 < Objects.Count);
        }

        public bool Process (out bool processResult)
        {
            if (0 == Objects.Count) {
                Objects = QueryFunction (ChunkSize);
                if (0 == Objects.Count) {
                    processResult = false;
                    return false;
                }
            }
            var nextObject = Objects [0];
            Objects.RemoveAt (0);
            processResult = ProcessFunction (nextObject);
            return true;
        }

        public void Reset ()
        {
            Objects = new List<object> ();
        }
    }

    public delegate void NcBrainNotificationAction (NcResult.SubKindEnum type);

    // This class provides a simple rate limiting
    public class NcBrainNotification
    {
        private bool _Running = true;
        private object LockObj = new object ();
        private Dictionary<NcResult.SubKindEnum, DateTime> LastNotified;

        // The minimum duration between successive status indications in units of milliseconds
        public const int KMinDurationMsec = 2000;

        // The delegate interface is created for unit testing
        public NcBrainNotificationAction Action = SendStatusIndication;

        public bool Running {
            get {
                return _Running;
            }
            set {
                lock (LockObj) {
                    if (!value) {
                        _Running = false;
                        LastNotified.Clear ();
                    } else {
                        _Running = true;
                        // Send notifications
                        DateTime now = DateTime.Now;
                        var types = new List<NcResult.SubKindEnum> (LastNotified.Keys);
                        foreach (var type in types) {
                            SendAndUpdate (now, type);
                        }
                    }
                }
            }
        }

        private static void SendStatusIndication (NcResult.SubKindEnum type)
        {
            NcApplication.Instance.InvokeStatusIndEventInfo (null, type);
        }

        public NcBrainNotification ()
        {
            LastNotified = new Dictionary<NcResult.SubKindEnum, DateTime> ();
        }

        private void SendAndUpdate (DateTime now, NcResult.SubKindEnum type)
        {
            LastNotified [type] = now;
            if (null != Action) {
                Action (type);
            }
        }

        public void NotifyUpdates (NcResult.SubKindEnum type)
        {
            lock (LockObj) {
                if (!Running) {
                    // Save the notification and send it when it is re-enabled.
                    if (!LastNotified.ContainsKey (type)) {
                        LastNotified.Add (type, new DateTime ());
                    }
                    return;
                }

                if (!LastNotified.ContainsKey (type)) {
                    LastNotified.Add (type, new DateTime ());
                }
                DateTime last;
                bool got = LastNotified.TryGetValue (type, out last);
                NcAssert.True (got);

                // Rate limit to one notification per 2 seconds.
                DateTime now = DateTime.Now;
                if (KMinDurationMsec < (long)(now - last).TotalMilliseconds) {
                    SendAndUpdate (now, type);
                }
            }
        }
    }
}

