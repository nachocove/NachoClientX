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
    public class NachoLikelyToReadEmailMessages : NachoEmailMessagesBase, INachoEmailMessages
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

        public override bool Refresh (out List<int> adds, out List<int> deletes)
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
            } else {
                var thread = McEmailMessage.QueryActiveMessageItemsByThreadId (Folder.AccountId, Folder.Id, message.ConversationId);
                return thread;
            }
        }

        public override string DisplayName ()
        {
            return "Focused";
        }

        public override NcResult StartSync ()
        {
            if (null != Folder) {
                return BackEnd.Instance.SyncCmd (Folder.AccountId, Folder.Id);
            } else {
                return NachoSyncResult.DoesNotSync ();
            }
        }

        public override INachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            return new NachoThreadedEmailMessages (Folder, thread.GetThreadId());
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return account.Id == Folder.AccountId;
        }
    }
}

