//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.Brain
{
    public partial class NcBrain
    {
        public const bool ENABLED = true;

        private static NcBrain _SharedInstance;

        public static NcBrain SharedInstance {
            get {
                if (null == _SharedInstance) {
                    _SharedInstance = new NcBrain ();
                }
                return _SharedInstance;
            }
        }

        public class OperationCounters {
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

        public NcCounter RootCounter;
        public OperationCounters McEmailMessageCounters;
        public OperationCounters McEmailMessageDependencyCounters;
        public OperationCounters McEmailMessageScoreSyncInfoCounters;
        public OperationCounters McEmailAddressCounters;
        public OperationCounters McEmailAddressScoreSyncInfo;

        // Last time a notification of score update was sent (via status indication).
        // Used for rate limit status indication.
        private DateTime LastEmailAddressScoreUpdate;
        private DateTime LastEmailMessageScoreUpdate;

        public NcBrain ()
        {
            LastEmailAddressScoreUpdate = new DateTime ();
            LastEmailMessageScoreUpdate = new DateTime ();

            RootCounter = new NcCounter ("Brain", true);
            McEmailMessageCounters = new OperationCounters ("McEmailMessage", RootCounter);
            McEmailMessageDependencyCounters = new OperationCounters ("McEmailMessageDependency", RootCounter);
            McEmailMessageScoreSyncInfoCounters = new OperationCounters ("McEmailMessageScoreSyncInfo", RootCounter);
            McEmailAddressCounters = new OperationCounters ("McEmailAddress", RootCounter);
            McEmailAddressScoreSyncInfo = new OperationCounters ("McEmailAddressScoreSyncInfo", RootCounter);
            RootCounter.AutoReset = true;
            RootCounter.ReportPeriod = 60 * 60; // report once per hour

            EventQueue = new NcQueue<NcBrainEvent> ();
            NcTask.Run (() => {
                EventQueue.Token = NcTask.Cts.Token;
                Process ();
            }, "Brain");
        }

        public void Enqueue (NcBrainEvent brainEvent)
        {
            EventQueue.Enqueue (brainEvent);
        }

        public bool IsQueueEmpty ()
        {
            return EventQueue.IsEmpty ();
        }

        private int GleanContacts (int count)
        {
            // Look for a list of emails
            int numGleaned = 0;
            while (numGleaned < count && !NcApplication.Instance.IsBackgroundAbateRequired) {
                McEmailMessage emailMessage = McEmailMessage.QueryNeedGleaning ();
                if (null == emailMessage) {
                    return numGleaned;
                }
                Log.Info (Log.LOG_BRAIN, "glean contact from email message {0}", emailMessage.Id);
                NcContactGleaner.GleanContacts (emailMessage.AccountId, emailMessage);
                numGleaned++;
            }
            return numGleaned;
        }

        private int AnalyzeEmailAddresses (int count)
        {
            int numAnalyzed = 0;
            while (numAnalyzed < count && !NcApplication.Instance.IsBackgroundAbateRequired) {
                McEmailAddress emailAddress = McEmailAddress.QueryNeedAnalysis ();
                if (null == emailAddress) {
                    break;
                }
                Log.Info (Log.LOG_BRAIN, "analyze email address {0}", emailAddress.Id);
                emailAddress.ScoreObject ();
                numAnalyzed++;
            }
            Log.Info (Log.LOG_BRAIN, "{0} email addresses analyzed", numAnalyzed);
            return numAnalyzed;
        }

        private int AnalyzeEmails (int count)
        {
            int numAnalyzed = 0;
            while (numAnalyzed < count && !NcApplication.Instance.IsBackgroundAbateRequired) {
                McEmailMessage emailMessage = McEmailMessage.QueryNeedAnalysis ();
                if (null == emailMessage) {
                    break;
                }
                Log.Debug (Log.LOG_BRAIN, "analyze email message {0}", emailMessage.Id);
                emailMessage.ScoreObject ();
                numAnalyzed++;
            }
            Log.Info (Log.LOG_BRAIN, "{0} email messages analyzed", numAnalyzed);
            return numAnalyzed;
        }

        private int UpdateEmailAddressScores (int count)
        {
            int numUpdated = 0;
            while (numUpdated < count && !NcApplication.Instance.IsBackgroundAbateRequired) {
                McEmailAddress emailAddress = McEmailAddress.QueryNeedUpdate ();
                if (null == emailAddress) {
                    return numUpdated;
                }
                emailAddress.Score = emailAddress.GetScore ();
                Log.Debug (Log.LOG_BRAIN, "[McEmailAddress:{0}] update score -> {1:F6}",
                    emailAddress.Id, emailAddress.Score);
                emailAddress.NeedUpdate = false;
                emailAddress.Update ();

                numUpdated++;
            }
            return numUpdated;
        }

        private int UpdateEmailMessageScores (int count)
        {
            int numUpdated = 0;
            while (numUpdated < count  && !NcApplication.Instance.IsBackgroundAbateRequired) {
                McEmailMessage emailMessage = McEmailMessage.QueryNeedUpdate ();
                if (null == emailMessage) {
                    return numUpdated;
                }
                emailMessage.Score = emailMessage.GetScore ();
                Log.Debug (Log.LOG_BRAIN, "[McEmailMessage:{0}] update score -> {1:F6}",
                    emailMessage.Id, emailMessage.Score);
                emailMessage.NeedUpdate = false;
                emailMessage.Update ();

                numUpdated++;
            }
            return numUpdated;
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
                    address.Update ();
                }
            }
        }

        private void ProcessUpdateAddressEvent (NcBrainUpdateAddressScoreEvent brainEvent)
        {
            Log.Debug (Log.LOG_BRAIN, "ProcessUpdateAddressEvent: event={0}", brainEvent.ToString ());
            McEmailAddress emailAddress =
                McEmailAddress.QueryById<McEmailAddress> ((int)brainEvent.EmailAddressId);
            if (null == emailAddress) {
                return;
            }
            if (Scoring.Version != emailAddress.ScoreVersion) {
                NcAssert.True (Scoring.Version > emailAddress.ScoreVersion);
                return;
            }
            bool updateDependencies = brainEvent.ForceUpdateDependentMessages;
            double newScore = emailAddress.GetScore ();
            if (newScore != emailAddress.Score) {
                emailAddress.Score = newScore;
                emailAddress.UpdateByBrain ();
                updateDependencies = true;
            }
            if (updateDependencies) {
                emailAddress.MarkDependencies ();
            }
        }

        private void ProcessUpdateMessageEvent (NcBrainUpdateMessageScoreEvent brainEvent)
        {
            Log.Debug (Log.LOG_BRAIN, "ProcessUpdateMessageEvent: event={0}", brainEvent.ToString ());
            McEmailMessage emailMessage =
                McEmailMessage.QueryById<McEmailMessage> ((int)brainEvent.EmailMessageId);
            if (null == emailMessage) {
                return;
            }
            if (Scoring.Version != emailMessage.ScoreVersion) {
                NcAssert.True (Scoring.Version > emailMessage.ScoreVersion);
                return;
            }
            double newScore = emailMessage.GetScore ();
            if (newScore != emailMessage.Score) {
                emailMessage.Score = newScore;
                emailMessage.Update ();
            }
        }

        private void ProcessEvent (NcBrainEvent brainEvent)
        {
            Log.Info (Log.LOG_BRAIN, "event type = {0}", Enum.GetName (typeof(NcBrainEventType), brainEvent.Type));

            switch (brainEvent.Type) {
            case NcBrainEventType.PERIODIC_GLEAN:
                EvaluateRunRate ();
                int num_entries = WorkCredits;
                num_entries -= GleanContacts (num_entries);
                if (0 >= num_entries) {
                    break;
                }
                num_entries -= AnalyzeEmailAddresses (num_entries);
                if (0 >= num_entries) {
                    break;
                }
                num_entries -= AnalyzeEmails (num_entries);
                if (0 >= num_entries) {
                    break;
                }
                num_entries -= UpdateEmailAddressScores (num_entries);
                if (0 >= num_entries) {
                    break;
                }
                num_entries -= UpdateEmailMessageScores (num_entries);
                if (0 >= num_entries) {
                    break;
                }
                break;
            case NcBrainEventType.STATE_MACHINE:
                GleanContacts (int.MaxValue);
                AnalyzeEmails (int.MaxValue);
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

        private void Process ()
        {
            if (ENABLED) {
                McEmailMessage.StartTimeVariance ();
                NcApplication.Instance.StatusIndEvent += GenerateInitialContactScores;
            }
            while (true) {
                var brainEvent = EventQueue.Dequeue ();
                if (NcBrainEventType.TERMINATE == brainEvent.Type) {
                    return;
                }
                if (ENABLED) {
                    ProcessEvent (brainEvent);
                }
            }
        }

        private void NotifyUpdates (NcResult.SubKindEnum type, ref DateTime last)
        {
            // Rate limit to one notification per 2 seconds.
            DateTime now = DateTime.Now;
            if (2000 < (long)(now - last).TotalMilliseconds) {
                last = now;
                StatusIndEventArgs e = new StatusIndEventArgs ();
                e.Account = ConstMcAccount.NotAccountSpecific;
                e.Status = NcResult.Info (type);
                NcApplication.Instance.InvokeStatusIndEvent(e);
            }
        }

        public void NotifyEmailAddressUpdates ()
        {
            NotifyUpdates (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated,
                ref LastEmailAddressScoreUpdate);
        }

        public void NotifyEmailMessageUpdates ()
        {
            NotifyUpdates (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated,
                ref LastEmailMessageScoreUpdate);
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

        public static void UpdateAddressScore (Int64 emailAddressId, bool forcedUpdateDependentMessages = false)
        {
            NcBrainEvent e = new NcBrainUpdateAddressScoreEvent (emailAddressId, forcedUpdateDependentMessages);
            NcBrain.SharedInstance.Enqueue (e);
        }

        public static void UpdateMessageScore (Int64 emailMessageId)
        {
            NcBrainEvent e = new NcBrainUpdateMessageScoreEvent (emailMessageId);
            NcBrain.SharedInstance.Enqueue (e);
        }
    }
}

