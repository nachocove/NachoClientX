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
    public class NachoLikelyToReadEmailMessages : NachoEmailMessages
    {
        List<McEmailMessageThread> ThreadList;
        List<McEmailMessageThread> UpdatedThreadList;
        McFolder Folder;

        public NachoLikelyToReadEmailMessages (McFolder folder)
        {
            Folder = folder;
            List<int> adds;
            List<int> deletes;
            ThreadList = new List<McEmailMessageThread> ();
            Refresh (out adds, out deletes);
        }

        public override bool BeginRefresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryActiveMessageItemsByScore2 (Folder.AccountId, Folder.Id, 
                           McEmailMessage.minHotScore, McEmailMessage.minLikelyToReadScore);
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

        public override void RemoveIgnoredMessages ()
        {
            RemoveIgnoredMessages (ThreadList);
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

        public override NachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            return new NachoThreadedEmailMessages (Folder, thread.GetThreadId());
        }

        public override McFolder GetFolderForThread (McEmailMessageThread thread)
        {
            return Folder;
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return account.Id == Folder.AccountId;
        }

        public override DateTime? LastSuccessfulSyncTime ()
        {
            if (Folder == null) {
                return null;
            }
            if (Folder.IsClientOwned) {
                return null;
            }
            return Folder.LastSyncAttempt;
        }

        public override void RefetchSyncTime ()
        {
            Folder = McFolder.QueryById<McFolder> (Folder.Id);
        }
    }
}

