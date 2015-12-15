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

        public void Release (int accountId)
        {
            NcIndex index;
            if (!TryGetValue (accountId, out index)) {
                Log.Error (Log.LOG_BRAIN, "Attempt to release the index write lock for account {0} when the lock was not held.", accountId);
                return;
            }
            index.EndAddTransaction ();
            Remove (accountId);
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

    public class RoundRobinList
    {
        public class ScheduleOrder : IComparable
        {
            public double Order { get; protected set; }

            public int Id {
                get {
                    return Record.Id;
                }
            }

            public RoundRobinListRecord Record;

            public ScheduleOrder (double order, RoundRobinListRecord record)
            {
                Order = order;
                Record = record;
            }

            public int CompareTo (object obj)
            {
                ScheduleOrder other = (ScheduleOrder)obj;
                if (this.Order < other.Order) {
                    return -1;
                }
                if (this.Order > other.Order) {
                    return +1;
                }
                if (this.Id < other.Id) {
                    return -1;
                }
                if (this.Id > other.Id) {
                    return +1;
                }
                return 0;
            }
        }

        public class RoundRobinListRecord
        {
            protected static int NextId = 0;

            // Configuration
            public int Id { get; protected set; }

            public string Description { get; protected set; }

            protected RoundRobinSource Source;
            protected int Weight;

            // States
            protected int Count;

            // Counter
            public int RunCount { get; set; }

            public bool IsEmpty { get; protected set; }

            public RoundRobinListRecord (string description, RoundRobinSource source, int weight)
            {
                Id = NextId++;
                Description = description;
                NcAssert.True ((null != source) && (0 < weight));
                Source = source;
                Weight = weight;
                IsEmpty = false;
            }

            public void AddToSchedule (List<ScheduleOrder> schedule)
            {
                if (!IsEmpty) {
                    for (int n = 0; n < Weight; n++) {
                        schedule.Add (new ScheduleOrder ((double)n / (double)Weight, this));
                    }
                }
            }

            public void Initialize ()
            {
                IsEmpty = !Source.Initialize ();
            }

            public bool Run (out bool processResult)
            {
                processResult = false;
                if (!Source.Process (out processResult)) {
                    IsEmpty = true;
                    return false;
                }
                RunCount += 1;
                return true;
            }
        }

        protected List<RoundRobinListRecord> Sources;
        protected List<ScheduleOrder> Schedule;
        protected int CurrentSourceIndex;

        public RoundRobinList ()
        {
            Sources = new List<RoundRobinListRecord> ();
            Schedule = new List<ScheduleOrder> ();
        }

        public void Initialize ()
        {
            Schedule.Clear ();
            foreach (var source in Sources) {
                source.Initialize ();
                source.AddToSchedule (Schedule);
                source.RunCount = 0;
            }
            Schedule.Sort ();
            CurrentSourceIndex = 0;
        }

        public void Add (string description, RoundRobinSource source, int weight)
        {
            if (int.MaxValue == Sources.Count) {
                throw new IndexOutOfRangeException ();
            }
            Sources.Add (new RoundRobinListRecord (description, source, weight));
        }

        public bool Run (out bool processResult)
        {
            processResult = false;
            while (0 < Schedule.Count) {
                var record = Schedule [CurrentSourceIndex].Record;
                if (!record.Run (out processResult)) {
                    // This source has no more object to process. Remove this and try the next one
                    Schedule.RemoveAt (CurrentSourceIndex);
                    if (0 == Schedule.Count) {
                        CurrentSourceIndex = 0;
                        break;
                    } else {
                        CurrentSourceIndex = CurrentSourceIndex % Schedule.Count;
                    }
                } else {
                    CurrentSourceIndex = (CurrentSourceIndex + 1) % Schedule.Count;
                    return true;
                }
            }
            return false;
        }

        public void DumpRunCounts ()
        {
            foreach (var source in Sources) {
                if (0 == source.RunCount) {
                    continue;
                }
                Log.Info (Log.LOG_BRAIN, "{0}: {1}", source.RunCount, source.Description);
            }
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

