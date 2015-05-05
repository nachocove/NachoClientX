//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.Brain
{
    public partial class NcBrain
    {
        public static void NewEmailMessageSynced (object sender, EventArgs args)
        {
            StatusIndEventArgs eventArgs = args as StatusIndEventArgs;
            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged != eventArgs.Status.SubKind) {
                return;
            }
            NcBrain.SharedInstance.Enqueue (new NcBrainStateMachineEvent (eventArgs.Account.Id));
        }

        public static void UpdateAddressScore (Int64 emailAddressId, bool forcedUpdateDependentMessages = false)
        {
            NcBrainEvent e = new NcBrainUpdateAddressScoreEvent (emailAddressId, forcedUpdateDependentMessages);
            NcBrain.SharedInstance.Enqueue (e);
        }

        public static void UpdateMessageScore (Int64 accountId, Int64 emailMessageId)
        {
            NcBrainEvent e = new NcBrainUpdateMessageScoreEvent (accountId, emailMessageId);
            NcBrain.SharedInstance.Enqueue (e);
        }

        public static void UnindexEmailMessage (McEmailMessage emailMessage)
        {
            if ((null == emailMessage) || (0 == emailMessage.Id) || (0 == emailMessage.AccountId)) {
                return;
            }
            var brainEvent = new NcBrainUnindexMessageEvent (emailMessage.AccountId, emailMessage.Id);
            var dbEvent = new McBrainEvent (brainEvent);
            dbEvent.AccountId = emailMessage.AccountId;
            dbEvent.Insert ();
        }

        public static void UpdateEclipsing (McContact contact, McContact.McContactOpEnum op)
        {
            if ((null == contact) || (0 == contact.Id) || (0 == contact.AccountId)) {
                return;
            }
            var brainEvent = new NcBrainUpdateEclipsingEvent (contact.AccountId, contact.Id, op);
            var dbEvent = new McBrainEvent (brainEvent);
            dbEvent.AccountId = contact.AccountId;
            dbEvent.Insert ();
        }
    }
}

