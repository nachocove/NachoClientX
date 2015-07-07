//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
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
        }

        public static void UpdateAddressScore (int accountId, int emailAddressId, bool forcedUpdateDependentMessages = false)
        {
            if ((0 == accountId) || (0 == emailAddressId)) {
                return;
            }
            var brainEvent = new NcBrainUpdateAddressScoreEvent (emailAddressId, forcedUpdateDependentMessages);
            PersistentEnqueue (accountId, brainEvent);
            SharedInstance.Enqueue (new NcBrainPersistentQueueEvent ());
        }

        public static void UpdateMessageScore (int accountId, int emailMessageId)
        {
            if ((0 == accountId) || (0 == emailMessageId)) {
                return;
            }
            var brainEvent = new NcBrainUpdateMessageScoreEvent (accountId, emailMessageId);
            PersistentEnqueue (accountId, brainEvent);
            SharedInstance.Enqueue (new NcBrainPersistentQueueEvent ());
        }

        public static void UnindexEmailMessage (McEmailMessage emailMessage)
        {
            if ((null == emailMessage) || (0 == emailMessage.Id) || (0 == emailMessage.AccountId)) {
                return;
            }
            var brainEvent = new NcBrainUnindexMessageEvent (emailMessage.AccountId, emailMessage.Id);
            PersistentEnqueue (emailMessage.AccountId, brainEvent);
        }

        public static bool ValidContact (McContact contact)
        {
            return ((null != contact) && (0 < contact.Id) && (0 < contact.AccountId));
        }

        public static void UnindexContact (McContact contact)
        {
            if (!ValidContact (contact)) {
                return;
            }
            var brainEvent = new NcBrainUnindexContactEvent (contact.AccountId, contact.Id);
            PersistentEnqueue (contact.AccountId, brainEvent);
        }

        public static void ReindexContact (McContact contact)
        {
            if (!ValidContact (contact)) {
                return;
            }
            var brainEvent = new NcBrainReindexContactEvent (contact.AccountId, contact.Id);
            PersistentEnqueue (contact.AccountId, brainEvent);
        }

        public static void MarkForDeletion (int accountId)
        {
            var index = NcBrain.SharedInstance.Index (accountId);
            if (null != index) {
                // Signal abatement to get brain to stop processing
                string caller = "LockIndexForDeletion";
                NcAbate.HighPriority (caller);
                NcAbate.RegularPriority (caller);

                // Mark the index to prevent further use
                index.MarkForDeletion ();
            }
        }
    }
}

