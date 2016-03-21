//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;
using NachoCore.Brain;
using NachoCore.Utils;

namespace NachoCore
{
    public class NachoUnifiedLikelyToRead : NachoEmailMessagesBase, INachoEmailMessages
    {
        List<McEmailMessageThread> ThreadList;

        public NachoUnifiedLikelyToRead ()
        {
            List<int> adds;
            List<int> deletes;
            Refresh (out adds, out deletes);
        }

        public override bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryUnifiedItemsByScore2 (McEmailMessage.minHotScore, McEmailMessage.minLikelyToReadScore);
            var threads = NcMessageThreads.ThreadByConversation (list);
            if (NcMessageThreads.AreDifferent (ThreadList, threads, out adds, out deletes)) {
                ThreadList = threads;
                return true;
            }
            return false;
        }

        public override int Count ()
        {
            return ThreadList.Count;
        }

        public override McEmailMessageThread GetEmailThread (int idx)
        {
            var retval = ThreadList.ElementAt (idx);
            retval.Source = this;
            return retval;
        }

        public override List<McEmailMessageThread> GetEmailThreadMessages (int id)
        {
            var message = McEmailMessage.QueryById<McEmailMessage> (id);
            if (null == message) {
                return new List<McEmailMessageThread> ();
            }

            var inbox = McFolder.GetDefaultInboxFolder (message.AccountId);
            if (null == inbox) {
                return new List<McEmailMessageThread> ();
            }

            var thread = McEmailMessage.QueryActiveMessageItemsByThreadId (inbox.AccountId, inbox.Id, message.ConversationId);
            return thread;
        }

        public override string DisplayName ()
        {
            return "Focused";
        }

        public override NcResult StartSync ()
        {
            return EmailHelper.SyncUnified ();
        }

        public override INachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            var firstMessage = thread.FirstMessage ();
            var inbox = McFolder.GetDefaultInboxFolder (firstMessage.AccountId);
            return new NachoThreadedEmailMessages (inbox, thread.GetThreadId ());
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return McAccount.GetUnifiedAccount ().Id == account.Id;
        }
    }
}

