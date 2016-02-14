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
    public class NachoUnifiedLikelyToRead : INachoEmailMessages
    {
        List<McEmailMessageThread> ThreadList;

        public NachoUnifiedLikelyToRead ()
        {
            List<int> adds;
            List<int> deletes;
            Refresh (out adds, out deletes);
        }

        public bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryUnifiedItemsByScore2 (McEmailMessage.minHotScore, McEmailMessage.minLikelyToReadScore);
            var threads = NcMessageThreads.ThreadByConversation (list);
            if (NcMessageThreads.AreDifferent (ThreadList, threads, out adds, out deletes)) {
                ThreadList = threads;
                return true;
            }
            return false;
        }

        public int Count ()
        {
            return ThreadList.Count;
        }

        public McEmailMessageThread GetEmailThread (int idx)
        {
            var retval = ThreadList.ElementAt (idx);
            retval.Source = this;
            return retval;
        }

        public List<McEmailMessageThread> GetEmailThreadMessages (int id)
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

        public string DisplayName ()
        {
            return "Focused";
        }

        public bool HasOutboxSemantics ()
        {
            return false;
        }

        public bool HasDraftsSemantics ()
        {
            return false;
        }

        public NcResult StartSync ()
        {
//            if (null != Folder) {
//                return BackEnd.Instance.SyncCmd (Folder.AccountId, Folder.Id);
//            } else {
//                return NachoSyncResult.DoesNotSync ();
//            }
            // FIXME Sync
            return NachoSyncResult.DoesNotSync ();
        }

        public INachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            var firstMessage = thread.FirstMessage ();
            var inbox = McFolder.GetDefaultInboxFolder (firstMessage.AccountId);
            return new NachoThreadedEmailMessages (inbox, thread.GetThreadId ());
        }

        public bool IsCompatibleWithAccount (McAccount account)
        {
            return NcApplication.Instance.Account.ContainsAccount (account.Id);
        }
    }
}

