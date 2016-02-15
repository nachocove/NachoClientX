//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;
using NachoCore.Brain;
using NachoCore.Utils;

namespace NachoCore
{
    public class NachoUnifiedInbox : NachoEmailMessagesBase, INachoEmailMessages
    {
        List<McEmailMessageThread> threadList;

        public NachoUnifiedInbox ()
        {
            threadList = new List<McEmailMessageThread> ();
        }

        public override bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryUnifiedInboxItems ();
            var threads = NcMessageThreads.ThreadByConversation (list);
            if (NcMessageThreads.AreDifferent (threadList, threads, out adds, out deletes)) {
                threadList = threads;
                return true;
            }
            return false;
        }

        public override int Count ()
        {
            return threadList.Count;
        }

        public override McEmailMessageThread GetEmailThread (int i)
        {
            if (0 > i) {
                Log.Error (Log.LOG_UTILS, "GetEmailThread: {0}", i);
                return null;
            }
            if (threadList.Count <= i) {
                Log.Error (Log.LOG_UTILS, "GetEmailThread: {0}", i);
                return null;
            }
            var t = threadList.ElementAt (i);
            t.Source = this;
            return t;
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
            return "Inbox";
        }

        // FIXME Filtering for unified inbox is still to be implemented.

        public override bool HasFilterSemantics ()
        {
            return base.HasFilterSemantics ();
        }

        public override FolderFilterOptions FilterSetting {
            get {
                return base.FilterSetting;
            }
            set {
                base.FilterSetting = value;
            }
        }

        public override FolderFilterOptions[] PossibleFilterSettings {
            get {
                return base.PossibleFilterSettings;
            }
        }

        public override NcResult StartSync ()
        {
            // FIXME Unfied Sync All
            return NachoSyncResult.DoesNotSync ();
        }

        public override INachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            var firstMessage = thread.FirstMessage ();
            var inbox = McFolder.GetDefaultInboxFolder (firstMessage.AccountId);
            return new NachoThreadedEmailMessages (inbox, thread.GetThreadId());
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            var currentAccount = NcApplication.Instance.Account;
            return null != currentAccount && currentAccount.ContainsAccount (account.Id);
        }

    }
}
