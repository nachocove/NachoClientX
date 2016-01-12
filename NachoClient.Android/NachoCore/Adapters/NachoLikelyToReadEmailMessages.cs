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
    public class NachoLikelyToReadEmailMessages : INachoEmailMessages
    {
        List<McEmailMessageThread> ThreadList;
        McFolder Folder;

        public NachoLikelyToReadEmailMessages (McFolder folder)
        {
            Folder = folder;
            List<int> adds;
            List<int> deletes;
            Refresh (out adds, out deletes);
        }

        public bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryActiveMessageItemsByScore2 (Folder.AccountId, Folder.Id, 
                           McEmailMessage.minHotScore, McEmailMessage.minLikelyToReadScore);
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
            } else {
                var thread = McEmailMessage.QueryActiveMessageItemsByThreadId (Folder.AccountId, Folder.Id, message.ConversationId);
                return thread;
            }
        }

        public string DisplayName ()
        {
            return "Likely To Read";
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
            if (null != Folder) {
                return BackEnd.Instance.SyncCmd (Folder.AccountId, Folder.Id);
            } else {
                return NachoSyncResult.DoesNotSync ();
            }
        }

        public INachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            return new NachoThreadedEmailMessages (Folder, thread.GetThreadId());
        }

        public bool IsCompatibleWithAccount (McAccount account)
        {
            return account.Id == Folder.AccountId;
        }
    }
}

