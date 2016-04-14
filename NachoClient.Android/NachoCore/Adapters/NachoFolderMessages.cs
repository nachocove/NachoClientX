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
    public class NachoFolderMessages : NachoEmailMessages
    {
        List<McEmailMessageThread> threadList;
        McFolder folder;

        public NachoFolderMessages (McFolder folder)
        {
            this.folder = folder;
            threadList = new List<McEmailMessageThread> ();
        }

        public override bool Refresh (out List<int> adds, out List<int> deletes)
        {
            List<McEmailMessageThread> list;
            switch (folder.FilterSetting) {
            case FolderFilterOptions.Hot:
                list = McEmailMessage.QueryActiveMessageItemsByScore (folder.AccountId, folder.Id, McEmailMessage.minHotScore);
                break;
            case FolderFilterOptions.Focused:
                list = McEmailMessage.QueryActiveMessageItemsByScore2 (folder.AccountId, folder.Id, McEmailMessage.minHotScore, McEmailMessage.minLikelyToReadScore);
                break;
            case FolderFilterOptions.Unread:
                list = McEmailMessage.QueryUnreadMessageItems (folder.AccountId, folder.Id);
                break;
            case FolderFilterOptions.All:
            default:
                list = McEmailMessage.QueryActiveMessageItems (folder.AccountId, folder.Id);
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
            } else {
                var thread = McEmailMessage.QueryActiveMessageItemsByThreadId (folder.AccountId, folder.Id, message.ConversationId);
                return thread;
            }
        }

        public override string DisplayName ()
        {
            return folder.DisplayName;
        }

        public override bool HasFilterSemantics ()
        {
            return true;
        }

        public override FolderFilterOptions FilterSetting {
            get {
                return folder.FilterSetting;
            }
            set {
                // Update the in-memory object in case the background task takes a while to run.
                folder.FilterSetting = value;
                NcTask.Run (() => {
                    folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                        var dbFolder = (McFolder)record;
                        dbFolder.FilterSetting = value;
                        return true;
                    });
                }, "FolderFilterSetting.Update");
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
            if (null != folder) {
                return BackEnd.Instance.SyncCmd (folder.AccountId, folder.Id);
            } else {
                return NachoSyncResult.DoesNotSync ();
            }
        }

        public override NachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            return new NachoThreadedEmailMessages (folder, thread.GetThreadId ());
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return account.ContainsAccount (folder.AccountId);
        }

    }
}
