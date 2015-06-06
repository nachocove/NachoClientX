//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Index;
using NachoCore.Model;

namespace NachoCore.Brain
{
    public class OpenedIndexSet : Dictionary<int, NcIndex>
    {
        protected NcBrain Brain;

        public OpenedIndexSet (NcBrain brain)
        {
            Brain = brain;
        }

        public NcIndex Get (int accountId)
        {
            NcIndex index;
            if (!TryGetValue (accountId, out index)) {
                index = Brain.Index (accountId);
                if (null == index) {
                    Log.Warn (Log.LOG_BRAIN, "fail to get index for account {0}", accountId);
                    return null;
                }
                if (!index.BeginAddTransaction ()) {
                    Log.Warn (Log.LOG_BRAIN, "fail to begin add transaction (accountId={0})", accountId);
                    return null;
                }
                Add (accountId, index);
            }
            return index;
        }

        public void Cleanup ()
        {
            foreach (var index in Values) {
                index.EndAddTransaction ();
            }
            Clear ();
        }
    }

    public class RoundRobinSource
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

        public RoundRobinSource (QuerySourceFunction queryFunction, ProcessObjectFunction processFunction, int chunkSize = 5)
        {
            QueryFunction = queryFunction;
            ProcessFunction = processFunction;
            ChunkSize = chunkSize;
            Objects = new List<object> ();
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
    }

    public class RoundRobinList
    {
        public class RoundRobinListRecord
        {
            // Configuration
            protected RoundRobinSource Source;
            protected int Weight;

            // States
            protected int Count;

            public bool IsEmpty { get; protected set; }

            public RoundRobinListRecord (RoundRobinSource source, int weight)
            {
                NcAssert.True ((null != source) && (0 < weight));
                Source = source;
                Weight = weight;
            }

            public bool Run (out bool shouldSwitch)
            {
                bool processResult;
                shouldSwitch = false;
                if (!Source.Process (out processResult)) {
                    return false;
                }
                Count = (Count + 1) % Weight;
                if (0 == Count) {
                    shouldSwitch = true;
                }
                return true;
            }
        }

        protected List<RoundRobinSource> Sources;
        protected List<RoundRobinSource> EmptySources;
        protected int CurrentSourceIndex;

        public RoundRobinList ()
        {
            Sources = new List<RoundRobinSource> ();
            EmptySources = new List<RoundRobinSource> ();
            CurrentSourceIndex = 0;
        }

        public void Add (RoundRobinSource source, int weight)
        {
            if (int.MaxValue == Sources.Count) {
                throw new IndexOutOfRangeException ();
            }
            Sources.Add (source);
        }

        public bool Run ()
        {
            while (0 < Sources.Count) {
                bool processResult;
                if (!Sources [CurrentSourceIndex].Process (out processResult)) {
                    // This source has no more object to process. Remove this and try the next one
                    var emptySource = Sources [CurrentSourceIndex];
                    Sources.RemoveAt (CurrentSourceIndex);
                    EmptySources.Add (emptySource);
                    if (0 == Sources.Count) {
                        // There is no next source.
                        CurrentSourceIndex = 0;
                        break;
                    } else {
                        CurrentSourceIndex = CurrentSourceIndex % Sources.Count;
                    }
                } else {
                    CurrentSourceIndex = (CurrentSourceIndex + 1) % Sources.Count;
                    return true;
                }
            }
            return false;
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

