//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;
using NachoCore.Brain;
using NachoCore.Utils;

namespace NachoCore
{
    public class NachoUnifiedHotList : NachoEmailMessages
    {
        List<McEmailMessageThread> threadList;
        List<McEmailMessageThread> updatedThreadList;
        public bool IncludeActions = true;
        int _CountIngoringLimit;
        int UpdatedCountIgnoringLimit;

        public NachoUnifiedHotList ()
        {
            threadList = new List<McEmailMessageThread> ();
            _CountIngoringLimit = 0;
        }

        public override bool BeginRefresh (out List<int> adds, out List<int> deletes)
        {
            double threshold = McEmailMessage.minHotScore;
            // Before statistics converge, there may be a period when there is no hot emails.
            // When that happens, lower the threshold until we found something
            var list = McEmailMessage.QueryUnifiedInboxItemsByScore (threshold, includeActions:IncludeActions);
            updatedThreadList = NcMessageThreads.ThreadByConversation (list);
            RemoveIgnoredMessages (updatedThreadList);
            UpdatedCountIgnoringLimit = updatedThreadList.Count;
            if (MessageLimit > 0 && updatedThreadList.Count > MessageLimit) {
                updatedThreadList = updatedThreadList.GetRange (0, MessageLimit);
            }
            return NcMessageThreads.AreDifferent (threadList, updatedThreadList, out adds, out deletes);
        }

        public override void CommitRefresh ()
        {
            ClearCache ();
            threadList = updatedThreadList;
            updatedThreadList = null;
            _CountIngoringLimit = UpdatedCountIgnoringLimit;
            UpdatedCountIgnoringLimit = 0;
        }

        public override int Count ()
        {
            return threadList.Count;
        }

        public override int CountIgnoringLimit ()
        {
            return _CountIngoringLimit;
        }

        public override void RemoveIgnoredMessages ()
        {
            RemoveIgnoredMessages (threadList);
        }

        public override McEmailMessageThread GetEmailThread (int i)
        {
            var t = threadList.ElementAt (i);
            t.Source = this;
            return t;
        }

        // Add messages, not just hot ones
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
            return "Hot List";
        }

        public override NcResult StartSync ()
        {
            return EmailHelper.SyncUnified ();
        }

        public override NachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            var firstMessage = thread.FirstMessage ();
            var inbox = McFolder.GetDefaultInboxFolder (firstMessage.AccountId);
            return new NachoThreadedEmailMessages (inbox, thread.GetThreadId ());
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


