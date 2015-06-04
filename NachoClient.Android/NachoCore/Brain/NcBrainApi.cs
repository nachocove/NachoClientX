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

        public static void NewEmailMessageSynced (object sender, EventArgs args)
        {
            StatusIndEventArgs eventArgs = args as StatusIndEventArgs;
            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged != eventArgs.Status.SubKind) {
                return;
            }
            NcBrain.SharedInstance.Enqueue (new NcBrainStateMachineEvent (eventArgs.Account.Id));
        }

        public static void UpdateAddressScore (int accountId, int emailAddressId, bool forcedUpdateDependentMessages = false)
        {
            if ((0 == accountId) || (0 == emailAddressId)) {
                return;
            }
            var brainEvent = new NcBrainUpdateAddressScoreEvent (emailAddressId, forcedUpdateDependentMessages);
            PersistentEnqueue (accountId, brainEvent);
        }

        public static void UpdateMessageScore (int accountId, int emailMessageId)
        {
            if ((0 == accountId) || (0 == emailMessageId)) {
                return;
            }
            var brainEvent = new NcBrainUpdateMessageScoreEvent (accountId, emailMessageId);
            PersistentEnqueue (accountId, brainEvent);
        }

        public static void UnindexEmailMessage (McEmailMessage emailMessage)
        {
            if ((null == emailMessage) || (0 == emailMessage.Id) || (0 == emailMessage.AccountId)) {
                return;
            }
            var brainEvent = new NcBrainUnindexMessageEvent (emailMessage.AccountId, emailMessage.Id);
            PersistentEnqueue (emailMessage.AccountId, brainEvent);
        }

        public static void UnindexContact (McContact contact)
        {
            if ((null == contact) || (0 == contact.Id) || (0 == contact.AccountId)) {
                return;
            }
            var brainEvent = new NcCBrainUnindexContactEvent (contact.AccountId, contact.Id);
            PersistentEnqueue (contact.AccountId, brainEvent);
        }
    }
}

