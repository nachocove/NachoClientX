//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using MimeKit;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.Index;

namespace NachoCore.Brain
{
    public partial class NcBrain
    {
        public const bool ENABLED = true;

        public static bool RegisterStatusIndHandler = false;

        public static int StartupDelayMsec = 10000;

        private static NcBrain _SharedInstance;

        public static NcBrain SharedInstance {
            get {
                if (null == _SharedInstance) {
                    _SharedInstance = new NcBrain ();
                }
                return _SharedInstance;
            }
        }

        public class OperationCounters
        {
            private NcCounter Root;
            public NcCounter Insert;
            public NcCounter Delete;
            public NcCounter Update;

            public OperationCounters (string name, NcCounter Parent)
            {
                Root = Parent.AddChild (name);
                Insert = Root.AddChild ("Insert");
                Delete = Root.AddChild ("Delete");
                Update = Root.AddChild ("Update");
            }
        }

        private NcQueue<NcBrainEvent> EventQueue;

        private ConcurrentDictionary<int, NcIndex> _Indexes;

        public NcCounter RootCounter;
        public OperationCounters McEmailMessageCounters;
        public OperationCounters McEmailMessageDependencyCounters;
        public OperationCounters McEmailMessageScoreSyncInfoCounters;
        public OperationCounters McEmailAddressCounters;
        public OperationCounters McEmailAddressScoreSyncInfo;

        private DateTime LastPeriodicGlean;
        private DateTime LastPeriodicGleanRestart;

        private object LockObj;

        private NcBrainNotification NotificationRateLimiter;

        public NcBrain ()
        {
            _Indexes = new ConcurrentDictionary<int, NcIndex> ();

            LastPeriodicGlean = new DateTime ();
            LastPeriodicGleanRestart = new DateTime ();
            NotificationRateLimiter = new NcBrainNotification ();

            RootCounter = new NcCounter ("Brain", true);
            McEmailMessageCounters = new OperationCounters ("McEmailMessage", RootCounter);
            McEmailMessageDependencyCounters = new OperationCounters ("McEmailMessageDependency", RootCounter);
            McEmailAddressCounters = new OperationCounters ("McEmailAddress", RootCounter);
            RootCounter.AutoReset = true;
            RootCounter.ReportPeriod = 5 * 60; // report once every 5 min

            EventQueue = new NcQueue<NcBrainEvent> ();
            LockObj = new object ();
        }

        public NcIndex Index (int accountId)
        {
            NcIndex index;
            if (_Indexes.TryGetValue (accountId, out index)) {
                return index;
            }
            var indexPath = NcModel.Instance.GetIndexPath (accountId);
            index = new Index.NcIndex (indexPath);
            if (!_Indexes.TryAdd (accountId, index)) {
                // A race happens and this thread loses. There should be an Index in the dictionary now
                index.Dispose ();
                index = null;
                var got = _Indexes.TryGetValue (accountId, out index);
                NcAssert.True (got);
            }
            return index;
        }

        public static void StartService ()
        {
            // Set up the logging functions for IndexLib
            NcBrain brain = NcBrain.SharedInstance;
            NcTask.Run (() => {
                brain.EventQueue.Token = NcTask.Cts.Token;
                brain.Process ();
            }, "Brain");
        }

        public static void StopService ()
        {
            NcBrain.SharedInstance.SignalTermination ();
        }

        public void Enqueue (NcBrainEvent brainEvent)
        {
            EventQueue.Enqueue (brainEvent);
        }

        public void SignalTermination ()
        {
            var brainEvent = new NcBrainEvent (NcBrainEventType.TERMINATE);
            EventQueue.Undequeue (brainEvent);
        }

        public bool IsQueueEmpty ()
        {
            return EventQueue.IsEmpty ();
        }

        private int GleanContacts (int count, Int64 accountId = -1)
        {
            int numGleaned = 0;
            bool quickGlean = false;
            string accountAddress = null;
            if (0 < accountId) {
                var account = McAccount.QueryById<McAccount> ((int)accountId);
                if ((null != account) && (!String.IsNullOrEmpty (account.EmailAddr))) {
                    accountAddress = account.EmailAddr;
                    quickGlean = true;
                }
            }
            var emailMessages = McEmailMessage.QueryNeedGleaning (accountId, count);
            count = emailMessages.Count;
            while (numGleaned < count && !NcApplication.Instance.IsBackgroundAbateRequired &&
                   !EventQueue.Token.IsCancellationRequested) {
                if (!GleanEmailMessage (emailMessages [numGleaned], accountAddress, quickGlean)) {
                    break;
                }
                numGleaned++;
            }
            if (0 != numGleaned) {
                Log.Info (Log.LOG_BRAIN, "{0} email message gleaned", numGleaned);
                NotificationRateLimiter.NotifyUpdates (NcResult.SubKindEnum.Info_ContactSetChanged);
            }
            return numGleaned;
        }

        private bool IsInterrupted ()
        {
            return EventQueue.Token.IsCancellationRequested || NcApplication.Instance.IsBackgroundAbateRequired;
        }



        private bool IsInUnitTest ()
        {
            return (0 == StartupDelayMsec);
        }

        public void Process ()
        {
            bool tvStarted = false;
            if (ENABLED && !IsInUnitTest ()) {
                // Delay brain to avoid initialization logjam
                if (!NcTask.CancelableSleep (StartupDelayMsec)) {
                    NcTask.Cts.Token.ThrowIfCancellationRequested ();
                }

                // If brain task is running under quick sync, do not start time variance
                // as it is a waste of time.
                if (NcApplication.ExecutionContextEnum.Background == NcApplication.Instance.ExecutionContext ||
                    NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext) {
                    McEmailMessage.StartTimeVariance (EventQueue.Token);
                    tvStarted = true;
                }
                if (!NcBrain.RegisterStatusIndHandler) {
                    NcApplication.Instance.StatusIndEvent += GenerateInitialContactScores;
                    NcApplication.Instance.StatusIndEvent += UIScrollingEnd;
                    NcApplication.Instance.StatusIndEvent += NewEmailMessageSynced;
                    NcBrain.RegisterStatusIndHandler = true;
                }
            }
            lock (LockObj) {
                while (true) {
                    var brainEvent = EventQueue.Dequeue ();
                    if (NcBrainEventType.TERMINATE == brainEvent.Type) {
                        Log.Info (Log.LOG_BRAIN, "NcBrain Task exits");
                        return;
                    }
                    if (!IsInUnitTest ()) {
                        if (!tvStarted &&
                            (NcApplication.ExecutionContextEnum.Background == NcApplication.Instance.ExecutionContext ||
                            NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext)) {
                            McEmailMessage.StartTimeVariance (EventQueue.Token);
                            tvStarted = true;
                        }
                        // TODO - scheduling of brain actions need to be smarter. This will be
                        // addressed in brain 2.0.
                        if (NcApplication.ExecutionContextEnum.Background != NcApplication.Instance.ExecutionContext &&
                            NcApplication.ExecutionContextEnum.Foreground != NcApplication.Instance.ExecutionContext) {
                            continue;
                        }
                    }
                    if (ENABLED) {
                        ProcessEvent (brainEvent);
                    }
                }
            }
        }



        public void NotifyEmailAddressUpdates ()
        {
            NotificationRateLimiter.NotifyUpdates (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated);
        }

        public void NotifyEmailMessageUpdates ()
        {
            NotificationRateLimiter.NotifyUpdates (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated);
        }

        /// Status indication handler for Info_RicInitialSyncCompleted. We do not 
        /// generate initial email address scores from RIC in this function for 2
        /// reasons. First, we do not want to hold up the status indication callback
        /// for a long duration. Second, the callback may be in a different threads
        /// as NcBrain task. So, we may have two threads updatind the same object.
        /// Therefore, we enqueue a brain event and let brain task to do the actual
        /// processing.
        public static void GenerateInitialContactScores (object sender, EventArgs args)
        {
            StatusIndEventArgs eventArgs = args as StatusIndEventArgs;
            if (NcResult.SubKindEnum.Info_RicInitialSyncCompleted != eventArgs.Status.SubKind) {
                return;
            }
            NcBrain.SharedInstance.Enqueue (new NcBrainInitialRicEvent (eventArgs.Account.Id));
        }

        public void UIScrollingEnd (object sender, EventArgs args)
        {
            StatusIndEventArgs eventArgs = args as StatusIndEventArgs;
            if (NcResult.SubKindEnum.Info_BackgroundAbateStopped != eventArgs.Status.SubKind) {
                return;
            }
            DateTime now = DateTime.Now;
            if (((double)NcContactGleaner.GLEAN_PERIOD < (now - LastPeriodicGlean).TotalSeconds) &&
                (LastPeriodicGleanRestart < LastPeriodicGlean)) {
                LastPeriodicGleanRestart = now;
                NcContactGleaner.Stop ();
                NcContactGleaner.Start ();
            }
        }
    }

    public delegate void NcBrainNotificationAction (NcResult.SubKindEnum type);

    // This class provides a simple rate limiting
    public class NcBrainNotification
    {
        private bool _Enabled = true;
        private object LockObj = new object ();
        private Dictionary<NcResult.SubKindEnum, DateTime> LastNotified;

        // The minimum duration between successive status indications in units of milliseconds
        public const int KMinDurationMsec = 2000;

        // The delegate interface is created for unit testing
        public NcBrainNotificationAction Action = SendStatusIndication;

        public bool Enabled {
            get {
                return _Enabled;
            }
            set {
                lock (LockObj) {
                    if (!value) {
                        _Enabled = false;
                        LastNotified.Clear ();
                    } else {
                        _Enabled = true;
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
                if (!Enabled) {
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
}

