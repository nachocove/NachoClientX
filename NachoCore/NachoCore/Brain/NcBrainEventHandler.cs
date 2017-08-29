//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;
using System.Threading;

namespace NachoCore.Brain
{
    public partial class NcBrain
    {
        private NcQueue<NcBrainEvent> EventQueue;
        protected OpenedIndexSet OpenedIndexes;
        private long BytesIndexed;

        private BrainQueryAndProcess QuickScore;
        private BrainQueryAndProcess UpdateScoreHigh;
        private BrainQueryAndProcess UpdateScoreLow;
        private BrainQueryAndProcess AnalyzeEmail;
        private BrainQueryAndProcess IndexEmail;
        private BrainQueryAndProcess IndexContacts;

        private void InitializeEventHandler ()
        {
            EventQueue = new NcQueue<NcBrainEvent> ();
            OpenedIndexes = new OpenedIndexSet (this);

            QuickScore = new BrainQueryAndProcess (McEmailMessage.QueryRecentNeedQuickScoringObjects, QuickScoreEmailMessage, 50);
            UpdateScoreHigh = new BrainQueryAndProcess (McEmailMessage.QueryNeedUpdateObjectsAbove, UpdateEmailMessageScores, 50);
            UpdateScoreLow = new BrainQueryAndProcess (McEmailMessage.QueryNeedUpdateObjectsBelow, UpdateEmailMessageScores, 50);
            AnalyzeEmail = new BrainQueryAndProcess (McEmailMessage.QueryNeedAnalysisObjects, AnalyzeEmailMessage, 50);
            IndexEmail = new BrainQueryAndProcess (McEmailMessage.QueryNeedsIndexingObjects, IndexEmailMessage, 50);
            IndexContacts = new BrainQueryAndProcess (McContact.QueryNeedIndexingObjects, IndexContact, 50);
        }

        public void Enqueue (NcBrainEvent brainEvent)
        {
            EventQueue.Enqueue (brainEvent);
        }

        public void EnqueueIfNotAlreadyThere (NcBrainEvent brainEvent)
        {
            EventQueue.EnqueueIfNot (brainEvent, (obj) => {
                return obj.Type == brainEvent.Type;
            });
        }

        public void EnqueueIfNotAtTail (NcBrainEvent brainEvent)
        {
            EventQueue.EnqueueIfNotTail (brainEvent, (obj) => {
                return obj.Type == brainEvent.Type;
            });
        }

        public bool IsQueueEmpty ()
        {
            return EventQueue.IsEmpty ();
        }

        public void QueueClear ()
        {
            while (EventQueue.Count () > 0) {
                // It is possible that EventQueue could have been emptied on another thread in between
                // the call to Count() above and this point.  EventQueue.Dequeue() will block indefinitely
                // if the queue is empty.  EventQueue.DequeueIf() returns immediately if the queue is
                // empty.  So use DequeueIf().
                EventQueue.DequeueIf ((NcBrainEvent obj1) => {
                    return true;
                });
            }
        }

        private bool KeepGoing ()
        {
            NcAbate.PauseWhileAbated ();
            return IsRunning && !EventQueue.Token.IsCancellationRequested;
        }

        private void ProcessEvent (NcBrainEvent brainEvent)
        {
            NcAbate.PauseWhileAbated ();

            Log.Info (Log.LOG_BRAIN, "Brain event: {0}", brainEvent.Type.ToString ());

            switch (brainEvent.Type) {

            case NcBrainEventType.PERIODIC_GLEAN:
                // Used to wake up the brain from time to time, in case new work became
                // available while it was sleeping.
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
                // Used to wake up the brain whenever an persistent event is added to the database.
                break;

            case NcBrainEventType.INDEX_MESSAGE:
                IndexEmailMessage (McEmailMessage.QueryById<McEmailMessage> ((int)((NcBrainIndexMessageEvent)brainEvent).EmailMessageId));
                break;

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
                break;

            case NcBrainEventType.UPDATE_ADDRESS_SCORE:
                var updateAddressEvent = brainEvent as NcBrainUpdateAddressScoreEvent;
                var emailAddress = McEmailAddress.QueryById<McEmailAddress> ((int)updateAddressEvent.EmailAddressId);
                UpdateEmailAddressScores (emailAddress, updateAddressEvent.ForceUpdateDependentMessages);
                break;

            case NcBrainEventType.UPDATE_MESSAGE_SCORE:
                long emailMesasgeId;
                int action = 0;
                if (brainEvent is NcBrainUpdateUserActionEvent) {
                    var updateActionEvent = brainEvent as NcBrainUpdateUserActionEvent;
                    emailMesasgeId = updateActionEvent.EmailMessageId;
                    action = updateActionEvent.Action;
                } else {
                    var updatedMessageEvent = brainEvent as NcBrainUpdateMessageScoreEvent;
                    emailMesasgeId = updatedMessageEvent.EmailMessageId;
                }
                NcModel.Instance.RunInTransaction (() => {
                    var emailMessage = McEmailMessage.QueryById<McEmailMessage> ((int)emailMesasgeId);
                    if (UpdateEmailMessageScores (emailMessage)) {
                        if ((0 != action) && (0 != emailMessage.FromEmailAddressId)) {
                            var fromEmailAddress = McEmailAddress.QueryById<McEmailAddress> (emailMessage.FromEmailAddressId);
                            UpdateAddressUserAction (fromEmailAddress, action);
                        }
                    }
                });
                break;

            case NcBrainEventType.UPDATE_MESSAGE_NOTIFICATION_STATUS:
                var notifiedEvent = (NcBrainUpdateMessageNotificationStatusEvent)brainEvent;
                NcModel.Instance.RunInTransaction (() => {
                    var emailMessage = McEmailMessage.QueryById<McEmailMessage> ((int)notifiedEvent.EmailMessageId);
                    if (null != emailMessage) {
                        emailMessage.ScoreStates.UpdateNotificationTime (notifiedEvent.NotificationTime, notifiedEvent.Variance);
                    }
                });
                break;

            case NcBrainEventType.UPDATE_MESSAGE_READ_STATUS:
                ProcessMessageReadStatusUpdated ((NcBrainUpdateMessageReadStatusEvent)brainEvent);
                break;

            case NcBrainEventType.UPDATE_MESSAGE_REPLY_STATUS:
                ProcessMessageReplyStatusUpdated ((NcBrainUpdateMessageReplyStatusEvent)brainEvent);
                break;

            case NcBrainEventType.TEST:
                // This is a no op. Serve as a synchronization barrier.
                break;

            default:
                throw new NcAssert.NachoDefaultCaseFailure ("unknown brain event type");
            }
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

        private void QuickScoreEmailMessage (McEmailMessage emailMessage)
        {
            var newScores = emailMessage.Classify ();
            emailMessage.UpdateByBrain ((item) => {
                var em = (McEmailMessage)item;
                em.Score = newScores.Item1;
                em.Score2 = newScores.Item2;
                return true;
            });
        }

        private bool QuickScoreEmailMessage (object message)
        {
            QuickScoreEmailMessage ((McEmailMessage)message);
            return true;
        }

        private void ProcessMessageReadStatusUpdated (NcBrainUpdateMessageReadStatusEvent readEvent)
        {
            NcModel.Instance.RunInTransaction (() => {
                var emailMessage = McEmailMessage.QueryById<McEmailMessage> ((int)readEvent.EmailMessageId);
                UpdateEmailMessageReadStatus (emailMessage, readEvent.ReadTime, readEvent.Variance);
            });
        }

        private void ProcessMessageReplyStatusUpdated (NcBrainUpdateMessageReplyStatusEvent replyEvent)
        {
            NcModel.Instance.RunInTransaction (() => {
                var emailMessage = McEmailMessage.QueryById<McEmailMessage> ((int)replyEvent.EmailMessageId);
                UpdateEmailMessageReplyStatus (emailMessage, replyEvent.ReplyTime, replyEvent.Variance);
            });
        }
    }
}
