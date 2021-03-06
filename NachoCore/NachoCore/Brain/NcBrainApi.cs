﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.Brain
{
    public partial class NcBrain
    {
        protected static void PersistentEnqueue (int accountId, NcBrainEvent brainEvent)
        {
            var dbEvent = new McBrainEvent (brainEvent);
            dbEvent.AccountId = accountId;
            dbEvent.Insert ();
            if (SharedInstance.EventQueue.IsEmpty ()) {
                // The brain might be asleep.  Wake it up.
                SharedInstance.Enqueue (new NcBrainPersistentQueueEvent ());
            }
        }

        protected static bool ValidEmailMessage (McEmailMessage emailMessage)
        {
            if (null == emailMessage) {
                return false;
            }
            if ((0 == emailMessage.Id) || (0 == emailMessage.AccountId)) {
                return false;
            }
            return true;
        }

        public static void UpdateAddressScore (int accountId, int emailAddressId, bool forcedUpdateDependentMessages = false)
        {
            if (!ENABLED) {
                return;
            }
            if ((0 == accountId) || (0 == emailAddressId)) {
                return;
            }
            SharedInstance.Enqueue (new NcBrainUpdateAddressScoreEvent (emailAddressId, forcedUpdateDependentMessages));
        }

        public static void UpdateMessageScore (int accountId, int emailMessageId)
        {
            if (!ENABLED) {
                return;
            }
            if ((0 == accountId) || (0 == emailMessageId)) {
                return;
            }
            SharedInstance.Enqueue (new NcBrainUpdateMessageScoreEvent (accountId, emailMessageId));
        }

        public static void UpdateUserAction (int accountId, int emailMessageId, int action)
        {
            if (!ENABLED) {
                return;
            }
            if ((0 == accountId) || (0 == emailMessageId)) {
                return;
            }
            SharedInstance.Enqueue (new NcBrainUpdateUserActionEvent (accountId, emailMessageId, action));
        }

        public static void MessageNotificationStatusUpdated (McEmailMessage emailMessage, DateTime notificationTime, double variance)
        {
            if (!ENABLED) {
                return;
            }
            if (!ValidEmailMessage (emailMessage) || (0 == variance)) {
                return;
            }
            // Sanity check if this may require an actual update
            if (!McEmailMessageScore.ShouldUpdateMinimum (emailMessage.ScoreStates.NotificationTime, notificationTime)) {
                return;
            }
            SharedInstance.Enqueue (new NcBrainUpdateMessageNotificationStatusEvent (emailMessage.AccountId, emailMessage.Id) {
                NotificationTime = notificationTime,
                Variance = variance,
            });
        }

        public static void MessageReadStatusUpdated (McEmailMessage emailMessage, DateTime readTime, double variance)
        {
            if (!ENABLED) {
                return;
            }
            if (!ValidEmailMessage (emailMessage) || (0 == variance)) {
                return;
            }
            // Sanity check if this may require an actual update
            if (!McEmailMessageScore.ShouldUpdateMinimum (emailMessage.ScoreStates.ReadTime, readTime)) {
                return;
            }
            SharedInstance.Enqueue (new NcBrainUpdateMessageReadStatusEvent (emailMessage.AccountId, emailMessage.Id) {
                ReadTime = readTime,
                Variance = variance,
            });
        }

        public static void MessageReplyStatusUpdated (McEmailMessage emailMessage, DateTime replyTime, double variance)
        {
            if (!ENABLED) {
                return;
            }
            if (!ValidEmailMessage (emailMessage) || (0 == variance)) {
                return;
            }
            // Sanity check if this may require an actual update
            if (!McEmailMessageScore.ShouldUpdateMinimum (emailMessage.ScoreStates.ReplyTime, replyTime)) {
                return;
            }
            SharedInstance.Enqueue (new NcBrainUpdateMessageReplyStatusEvent (emailMessage.AccountId, emailMessage.Id) {
                ReplyTime = replyTime,
                Variance = variance,
            });
        }
    }
}
