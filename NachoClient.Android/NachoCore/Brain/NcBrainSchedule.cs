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
            while (DateTime.UtcNow < runTill) {
                // Process all events in the persistent queue first
                if (0 < ProcessPersistedRequests (1)) {
                    continue;
                }

                // Email addresses analysis is trivial. It just updates all versions to the current version.
                //McEmailAdddress.AnalyzeAll ();

                // Look for all unindexed & unanalyzed emails within the last two weeks
                var unanalyzedEmails = McEmailMessage.QueryNeedAnalysis ();
                // Look for all unindex contacts

                // Round robins them.
            }
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
    }

    /// <summary>
    /// This class takes muliptle lists 
    /// </summary>
    public class RoundRobinList
    {
    }
}

