﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;
using NachoCore.Brain;
using NachoCore.Utils;

namespace NachoCore
{
    public class NachoUnifiedLikelyToRead : NachoEmailMessages
    {
        List<McEmailMessageThread> ThreadList;
        List<McEmailMessageThread> UpdatedThreadList;

        public NachoUnifiedLikelyToRead ()
        {
            List<int> adds;
            List<int> deletes;
            ThreadList = new List<McEmailMessageThread> ();
            Refresh (out adds, out deletes);
        }

        public override bool BeginRefresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryUnifiedItemsByScore2 (McEmailMessage.minHotScore, McEmailMessage.minLikelyToReadScore);
            UpdatedThreadList = NcMessageThreads.ThreadByConversation (list);
            RemoveIgnoredMessages (UpdatedThreadList);
            return NcMessageThreads.AreDifferent (ThreadList, UpdatedThreadList, out adds, out deletes);
        }

        public override void CommitRefresh ()
        {
            ClearCache ();
            ThreadList = UpdatedThreadList;
            UpdatedThreadList = null;
        }

        public override int Count ()
        {
            return ThreadList.Count;
        }

        public override void RemoveIgnoredMessages ()
        {
            RemoveIgnoredMessages (ThreadList);
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

        public override NachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            var inbox = GetFolderForThread (thread);
            return new NachoThreadedEmailMessages (inbox, thread.GetThreadId ());
        }

        public override McFolder GetFolderForThread (McEmailMessageThread thread)
        {
            var firstMessage = thread.FirstMessage ();
            return McFolder.GetDefaultInboxFolder (firstMessage.AccountId);
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return McAccount.GetUnifiedAccount ().Id == account.Id;
        }

        public override bool IncludesMultipleAccounts ()
        {
            return true;
        }
    }
}

