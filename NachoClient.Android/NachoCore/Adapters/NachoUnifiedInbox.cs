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
            List<McEmailMessageThread> list;
            switch (FilterSetting) {
            case FolderFilterOptions.Hot:
                list = McEmailMessage.QueryUnifiedInboxItemsByScore (McEmailMessage.minHotScore);
                break;
            case FolderFilterOptions.Focused:
                list = McEmailMessage.QueryUnifiedItemsByScore2 (McEmailMessage.minHotScore, McEmailMessage.minLikelyToReadScore);
                break;
            case FolderFilterOptions.Unread:
                list = McEmailMessage.QueryUnreadUnifiedInboxItems ();
                break;
            case FolderFilterOptions.All:
            default:
                list = McEmailMessage.QueryUnifiedInboxItems ();
                break;
            }
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

        public override bool HasFilterSemantics ()
        {
            return true;
        }

        const string FILTER_SETTING_MODULE = "UnifiedAccount";
        const string FILTER_SETTING_KEY = "FilterSetting";

        public override FolderFilterOptions FilterSetting {
            get {
                return (FolderFilterOptions)McMutables.GetOrCreateInt (
                    McAccount.GetUnifiedAccount ().Id, FILTER_SETTING_MODULE, FILTER_SETTING_KEY, (int)FolderFilterOptions.All);
            }
            set {
                McMutables.SetInt (McAccount.GetUnifiedAccount ().Id, FILTER_SETTING_MODULE, FILTER_SETTING_KEY, (int)value);
            }
        }

        private static FolderFilterOptions[] possibleFilters = new FolderFilterOptions[] {
            FolderFilterOptions.All, FolderFilterOptions.Hot, FolderFilterOptions.Focused, FolderFilterOptions.Unread
        };

        public override FolderFilterOptions[] PossibleFilterSettings {
            get {
                return possibleFilters;
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
            return new NachoThreadedEmailMessages (inbox, thread.GetThreadId ());
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return McAccount.GetUnifiedAccount ().Id == account.Id;
        }

    }
}
