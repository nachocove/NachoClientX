//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.Brain
{
    public partial class NcBrain
    {
        private NcQueue<NcBrainEvent> EventQueue;
        protected OpenedIndexSet OpenedIndexes;
        private long BytesIndexed;
        private RoundRobinList Scheduler;
        private RoundRobinSource ContactIndexingSource;

        private void InitializeEventHandler ()
        {
            EventQueue = new NcQueue<NcBrainEvent> ();
            OpenedIndexes = new OpenedIndexSet (this);
            Scheduler = new RoundRobinList ();
            Scheduler.Add (new RoundRobinSource (McEmailMessage.QueryNeedUpdateObjectsAbove, UpdateEmailMessageScore, 5), 3);
            ContactIndexingSource = new RoundRobinSource (McContact.QueryNeedIndexingObjects, IndexContact, 5);
            Scheduler.Add (ContactIndexingSource, 10);
            Scheduler.Add (new RoundRobinSource (McEmailMessage.QueryNeedAnalysisObjects, AnalyzeEmailMessage, 5), 2);
            Scheduler.Add (new RoundRobinSource (McEmailMessage.QueryNeedsIndexingObjects, IndexEmailMessage, 5), 3);
        }

        public void Enqueue (NcBrainEvent brainEvent)
        {
            EventQueue.Enqueue (brainEvent);
        }

        public bool IsQueueEmpty ()
        {
            return EventQueue.IsEmpty ();
        }

        private bool IsInterrupted ()
        {
            return EventQueue.Token.IsCancellationRequested || NcApplication.Instance.IsBackgroundAbateRequired;
        }

        private void ProcessEvent (NcBrainEvent brainEvent)
        {
            Log.Info (Log.LOG_BRAIN, "event type = {0}", Enum.GetName (typeof(NcBrainEventType), brainEvent.Type));

            switch (brainEvent.Type) {
            case NcBrainEventType.PERIODIC_GLEAN:
                if (!NcApplication.Instance.IsBackgroundAbateRequired) {
                    LastPeriodicGlean = DateTime.Now;
                }
                NotificationRateLimiter.Running = false;
                var runTill = EvaluateRunTime (NcContactGleaner.GLEAN_PERIOD);
                ProcessPeriodic (runTill);
                NotificationRateLimiter.Running = true;
                break;
            case NcBrainEventType.STATE_MACHINE:
                var stateMachineEvent = (NcBrainStateMachineEvent)brainEvent;
                /// FIXME - Should get the number from the event arg
                var accountId = (int)stateMachineEvent.AccountId;
                QuickScoreEmailMessages (accountId, 100);
                GleanEmailMessages (100, stateMachineEvent.AccountId);
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
            case NcBrainEventType.PERSISTENT_QUEUE:
                var persistentQueueVent = (NcBrainPersistentQueueEvent)brainEvent;
                ProcessPersistedRequests (persistentQueueVent.EventCount);
                break;
            case NcBrainEventType.UNINDEX_CONTACT:
            case NcBrainEventType.UNINDEX_MESSAGE:
            case NcBrainEventType.UPDATE_ADDRESS_SCORE:
            case NcBrainEventType.UPDATE_MESSAGE_SCORE:
                var errMesg = String.Format ("Event type {0} should go to persistent queue instead", brainEvent.Type);
                throw new NotSupportedException (errMesg);
            case NcBrainEventType.TEST:
                // This is a no op. Serve as a synchronization barrier.
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure ("unknown brain event type");
            }
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

        private void ProcessPeriodic (DateTime runTill)
        {
            try {
                bool ranOnce = false;
                while (DateTime.UtcNow < runTill) {
                    // Process all events in the persistent queue first
                    if (0 < ProcessPersistedRequests (1)) {
                        continue;
                    }
                    // Handle all other events
                    if (!ranOnce) {
                        Scheduler.Initialize ();
                        ranOnce = true;
                    }
                    bool result;
                    if (!Scheduler.Run (out result)) {
                        break;
                    }
                }
                while (DateTime.UtcNow < runTill) {
                    var emailMessages = McEmailMessage.QueryNeedUpdate (5, above: false);
                    if (0 == emailMessages.Count) {
                        break;
                    }
                    foreach (var emailMessage in emailMessages) {
                        if (IsInterrupted ()) {
                            break;
                        }
                        UpdateEmailMessageScore (emailMessage);
                    }
                }
            } finally {
                OpenedIndexes.Cleanup ();
            }
        }

        private int ProcessPersistedRequests (int count)
        {
            bool sourceReset = false;
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
                    var contactEvent = brainEvent as NcBrainUnindexContactEvent;
                    UnindexContact ((int)contactEvent.AccountId, (int)contactEvent.ContactId);
                    break;
                case NcBrainEventType.REINDEX_CONTACT:
                    var reindexEvent = brainEvent as NcBrainReindexContactEvent;
                    var contact = McContact.QueryById<McContact> ((int)reindexEvent.ContactId);
                    UnindexContact ((int)reindexEvent.AccountId, (int)reindexEvent.ContactId);
                    if (null != contact) {
                        IndexContact (contact);
                    }
                    if (!sourceReset) {
                        // The contact is already indexed but it may already be sitting
                        // in contact indexing source's object list. Then, it will be
                        // indexed twice. So, we manually clears that source's object list
                        ContactIndexingSource.Reset ();
                        sourceReset = true;
                    }
                    break;
                case NcBrainEventType.UPDATE_ADDRESS_SCORE:
                    var updateAddressEvent = brainEvent as NcBrainUpdateAddressScoreEvent;
                    var emailAddress = McEmailAddress.QueryById<McEmailAddress> ((int)updateAddressEvent.EmailAddressId);
                    UpdateEmailAddressScore (emailAddress, updateAddressEvent.ForceUpdateDependentMessages);
                    break;
                case NcBrainEventType.UPDATE_MESSAGE_SCORE:
                    var updatedMessageEvent = brainEvent as NcBrainUpdateMessageScoreEvent;
                    var emailMessage = McEmailMessage.QueryById<McEmailMessage> ((int)updatedMessageEvent.EmailMessageId);
                    UpdateEmailMessageScore (emailMessage);
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
                emailAddress.MarkDependencies (NcEmailAddress.Kind.From);
            }
        }

        private void ProcessUpdateMessageEvent (NcBrainUpdateMessageScoreEvent brainEvent)
        {
            Log.Debug (Log.LOG_BRAIN, "ProcessUpdateMessageEvent: event={0}", brainEvent.ToString ());
            McEmailMessage emailMessage =
                McEmailMessage.QueryById<McEmailMessage> ((int)brainEvent.EmailMessageId);
            UpdateEmailMessageScore (emailMessage);
        }

        private int QuickScoreEmailMessages (int accountId, int count)
        {
            int numScored = 0;
            var emailMessages = McEmailMessage.QueryNeedQuickScoring (accountId, count);
            foreach (var emailMessage in emailMessages) {
                if (IsInterrupted ()) {
                    break;
                }
                emailMessage.Score = emailMessage.Classify ();
                emailMessage.UpdateByBrain ();
                numScored++;
            }
            if (0 != numScored) {
                Log.Info (Log.LOG_BRAIN, "{0} email message quick scored", numScored);
                NotificationRateLimiter.NotifyUpdates (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated);
            }
            return numScored;
        }

        private int GleanEmailMessages (int count, Int64 accountId)
        {
            int numGleaned = 0;
            var emailMessages = McEmailMessage.QueryNeedGleaning (accountId, count);
            foreach (var emailMessage in emailMessages) {
                if (IsInterrupted ()) {
                    break;
                }
                if (!GleanEmailMessage (emailMessages [numGleaned])) {
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
    }
}

