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
            McEmailMessageScoreSyncInfoCounters = new OperationCounters ("McEmailMessageScoreSyncInfo", RootCounter);
            McEmailAddressCounters = new OperationCounters ("McEmailAddress", RootCounter);
            McEmailAddressScoreSyncInfo = new OperationCounters ("McEmailAddressScoreSyncInfo", RootCounter);
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

        private int ProcessLoop (int count, string message, Func<bool> action)
        {
            int numProcessed = 0;
            while (numProcessed < count && !IsInterrupted ()) {
                if (!action ()) {
                    break;
                }
                numProcessed++;
            }
            if (0 != numProcessed) {
                Log.Info (Log.LOG_BRAIN, "{0} {1}", numProcessed, message);
            }
            return numProcessed;
        }

        private int AnalyzeEmailAddresses (int count)
        {
            return ProcessLoop (count, "email addresses analyzed", () => {
                McEmailAddress emailAddress = McEmailAddress.QueryNeedAnalysis ();
                return AnalyzeEmailAddress (emailAddress);
            });
        }

        private int AnalyzeEmails (int count)
        {
            return ProcessLoop (count, "email messages analyzed", () => {
                McEmailMessage emailMessage = McEmailMessage.QueryNeedAnalysis ();
                return AnalyzeEmailMessage (emailMessage);
            });
        }

        private int UpdateEmailAddressScores (int count)
        {
            return ProcessLoop (count, "email address scores updated", () => {
                McEmailAddress emailAddress = McEmailAddress.QueryNeedUpdate ();
                return UpdateEmailAddressScore (emailAddress, false);
            });
        }

        private int UpdateEmailMessageScores (int count)
        {
            return ProcessLoop (count, "email message scores updated", () => {
                McEmailMessage emailMessage = McEmailMessage.QueryNeedUpdate ();
                return UpdateEmailMessageScore (emailMessage);
            });
        }

        private int IndexEmailMessages (int count)
        {
            if (0 == count) {
                return 0;
            }

            int numIndexed = 0;
            long bytesIndexed = 0;
            List<McEmailMessage> emailMessages = McEmailMessage.QueryNeedsIndexing (count);
            var indexes = new OpenedIndexSet (this);
            foreach (var emailMessage in emailMessages) {
                if (IsInterrupted ()) {
                    break;
                }

                var index = indexes.Get (emailMessage.AccountId);
                if (null == index) {
                    break;
                }
                if (IndexEmailMessage (index, emailMessage, ref bytesIndexed)) {
                    numIndexed += 1;
                }
            }

            indexes.Cleanup ();
            if (0 != numIndexed) {
                Log.Info (Log.LOG_BRAIN, "{0} email messages indexed", numIndexed);
            }
            if (0 != bytesIndexed) {
                Log.Info (Log.LOG_BRAIN, "{0:N0} bytes indexed", bytesIndexed);
            }
            return numIndexed;
        }

        private int IndexContacts (int count)
        {
            if (0 == count) {
                return 0;
            }

            int numIndexed = 0;
            long bytesIndexed = 0;
            List<McContact> contacts = McContact.QueryNeedIndexing (count);
            if (0 == contacts.Count) {
                return 0;
            }
            var indexes = new OpenedIndexSet (this);
            foreach (var contact in contacts) {
                if (IsInterrupted ()) {
                    break;
                }

                var index = indexes.Get (contact.AccountId);
                if (null == index) {
                    break;
                }
                if (IndexContact (index, contact, ref bytesIndexed)) {
                    numIndexed += 1;
                }
            }

            indexes.Cleanup ();
            if (0 != numIndexed) {
                Log.Info (Log.LOG_BRAIN, "{0} contacts indexed", numIndexed);
            }
            if (0 != bytesIndexed) {
                Log.Info (Log.LOG_BRAIN, "{0:N0} bytes indexed", bytesIndexed);
            }
            return numIndexed;
        }

        private int ProcessPersistedRequests (int count)
        {
            return ProcessLoop (count, "persisted requests processed", () => {
                var dbEvent = McBrainEvent.QueryNext ();
                if (null == dbEvent) {
                    return false;
                }
                var brainEvent = dbEvent.BrainEvent ();
                switch (brainEvent.Type) {
                case NcBrainEventType.UNINDEX_MESSAGE:
                    var unindexEvent = brainEvent as NcBrainUnindexMessageEvent;
                    UnindexEmailMessage ((int)unindexEvent.AccountId, (int)unindexEvent.EmailMessageId);
                    break;
                case NcBrainEventType.UNINDEX_CONTACT:
                    var contactEvent = brainEvent as NcCBrainUnindexContactEvent;
                    UnindexContact ((int)contactEvent.AccountId, (int)contactEvent.ContactId);
                    break;
                default:
                    Log.Warn (Log.LOG_BRAIN, "Unknown event type for persisted requests (type={0})", brainEvent.Type);
                    break;
                }
                dbEvent.Delete ();
                return true;
            });
        }

        private void ProcessUIEvent (NcBrainUIEvent brainEvent)
        {
            switch (brainEvent.UIType) {  
            case NcBrainUIEventType.MESSAGE_VIEW:
                // Update email and contact statistics
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure ("unknown brain ui event type");
            }
        }

        private void ProcessMessageFlagsEvent (NcBrainMessageFlagEvent brainEvent)
        {
            McEmailMessage emailMessage = McEmailMessage.QueryById<McEmailMessage> ((int)brainEvent.EmailMessageId);
            if (null == emailMessage) {
                return;
            }
            NcAssert.True (emailMessage.AccountId == brainEvent.AccountId);
            emailMessage.UpdateTimeVariance ();
        }

        private void ProcessInitialRicEvent (NcBrainInitialRicEvent brainEvent)
        {
            Log.Debug (Log.LOG_BRAIN, "ProcessInitialRicEvent: accountId={0}", brainEvent.AccountId);
            List<McContact> contactList = McContact.QueryAllRicContacts ((int)brainEvent.AccountId);
            if ((null == contactList) || (0 == contactList.Count)) {
                return;
            }

            /// We normalize all weighted rank by dividing against the maximum weight rank.
            /// QueryAllRicContacts return contacts (desendingly) sorted by weighted rank.
            double maxWeightedRank = (double)contactList [0].WeightedRank;
            foreach (McContact contact in contactList) {
                // Compute the score for all email addresses of the contact
                double score = 0.0;
                if (0 < maxWeightedRank) {
                    score = (double)contact.WeightedRank / maxWeightedRank;
                }
                foreach (McContactEmailAddressAttribute addressAttr in contact.EmailAddresses) {
                    // Find the corresponding McEmailAddress
                    McEmailAddress address = McEmailAddress.QueryById<McEmailAddress> (addressAttr.EmailAddress);
                    if (null == address) {
                        continue;
                    }
                    if (0 < address.ScoreVersion) {
                        /// If Scoreversion > 0, this object has already been scored by
                        /// the real algorithm. So, there is no need for a temporary
                        /// initial score.
                        continue;
                    }

                    // Generate an initial score
                    address.Score = score;
                    address.UpdateByBrain ();
                }
            }
        }

        private void ProcessUpdateAddressEvent (NcBrainUpdateAddressScoreEvent brainEvent)
        {
            Log.Debug (Log.LOG_BRAIN, "ProcessUpdateAddressEvent: event={0}", brainEvent.ToString ());
            McEmailAddress emailAddress =
                McEmailAddress.QueryById<McEmailAddress> ((int)brainEvent.EmailAddressId);
            bool updateDependencies = brainEvent.ForceUpdateDependentMessages;
            if (UpdateEmailAddressScore (emailAddress, true) && updateDependencies) {
                emailAddress.MarkDependencies ();
            }
        }

        private void ProcessUpdateMessageEvent (NcBrainUpdateMessageScoreEvent brainEvent)
        {
            Log.Debug (Log.LOG_BRAIN, "ProcessUpdateMessageEvent: event={0}", brainEvent.ToString ());
            McEmailMessage emailMessage =
                McEmailMessage.QueryById<McEmailMessage> ((int)brainEvent.EmailMessageId);
            UpdateEmailMessageScore (emailMessage);
        }

        private void ProcessEvent (NcBrainEvent brainEvent)
        {
            Log.Info (Log.LOG_BRAIN, "event type = {0}", Enum.GetName (typeof(NcBrainEventType), brainEvent.Type));

            switch (brainEvent.Type) {
            case NcBrainEventType.PERIODIC_GLEAN:
                if (!NcApplication.Instance.IsBackgroundAbateRequired) {
                    LastPeriodicGlean = DateTime.Now;
                }
                EvaluateRunRate ();
                int num_entries = WorkCredits;
                NotificationRateLimiter.Enabled = false;
                num_entries -= ProcessPersistedRequests (num_entries);
                num_entries -= AnalyzeEmailAddresses (num_entries);
                num_entries -= AnalyzeEmails (num_entries);
                num_entries -= GleanContacts (num_entries);
                num_entries -= UpdateEmailAddressScores (num_entries);
                // We need to index email message before updating the score because score update
                // always leads to hot view to flap the abatement signal and we never
                // get to index any message. What we do is to split the remaining allowed
                // entries into two halves. 1st half goes to indexing and the leftover goes to
                // score update.
                num_entries -= IndexContacts (num_entries);
                num_entries -= IndexEmailMessages (Math.Max (1, num_entries / 2));
                // This must be the last action. See comment above.
                num_entries -= UpdateEmailMessageScores (num_entries);
                NotificationRateLimiter.Enabled = true;
                break;
            case NcBrainEventType.STATE_MACHINE:
                var stateMachineEvent = (NcBrainStateMachineEvent)brainEvent;
                GleanContacts (100, stateMachineEvent.AccountId); /// FIXME - Should get the number from the event arg
                break;
            case NcBrainEventType.UI:
                ProcessUIEvent (brainEvent as NcBrainUIEvent);
                break;
            case NcBrainEventType.MESSAGE_FLAGS:
                ProcessMessageFlagsEvent (brainEvent as NcBrainMessageFlagEvent);
                break;
            case NcBrainEventType.INITIAL_RIC:
                ProcessInitialRicEvent (brainEvent as NcBrainInitialRicEvent);
                break;
            case NcBrainEventType.UPDATE_ADDRESS_SCORE:
                ProcessUpdateAddressEvent (brainEvent as NcBrainUpdateAddressScoreEvent);
                break;
            case NcBrainEventType.UPDATE_MESSAGE_SCORE:
                ProcessUpdateMessageEvent (brainEvent as NcBrainUpdateMessageScoreEvent);
                break;
            case NcBrainEventType.TEST:
                // This is a no op. Serve as a synchronization barrier.
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure ("unknown brain event type");
            }
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

