﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoCore.Model
{
    // If SQLite.Net would tolerate an abstract class, we'd be one.
    public class McFolderEntry : McObjectPerAccount
    {
        public enum ClassCodeEnum
        {
            // Values taken from ActiveSync are < 100.
            Tasks = 1,
            Email = 2,
            Calendar = 3,
            Contact = 4,
            Notes = 5,
            Sms = 6,
            MaxSyncable = 6,
            // Values created by Nacho are >= 100.
            Folder = 100,
            Journal = 101,
            Generic = 102,
        };

        [Indexed]
        public string ServerId { get; set; }

        [Indexed]
        public bool IsAwaitingDelete { get; set; }

        public static T QueryByServerId<T> (int accountId, string serverId) where T : McFolderEntry, new()
        {
            return NcModel.Instance.Db.Query<T> (
                string.Format ("SELECT f.* FROM {0} AS f WHERE " +
                    " f.AccountId = ? AND " + 
                    " f.IsAwaitingDelete = 0 AND " +
                    " f.ServerId = ? ", 
                    typeof(T).Name), 
                accountId, serverId).SingleOrDefault ();
        }

        public static McFolderEntry QueryAllForServerId (int accountId, string serverId)
        {
            List<McFolderEntry> folderEntries = new List<McFolderEntry>();
            CondAddToList (QueryByServerId<McEmailMessage> (accountId, serverId), ref folderEntries);
            CondAddToList (QueryByServerId<McCalendar> (accountId, serverId), ref folderEntries);
            CondAddToList (QueryByServerId<McContact> (accountId, serverId), ref folderEntries);
            CondAddToList (QueryByServerId<McTask> (accountId, serverId), ref folderEntries);
            NcAssert.True (folderEntries.Count <= 1, "Should not have multiple McFolderEntries with the same ServerId");
            return folderEntries.SingleOrDefault ();
        }

        private static void CondAddToList (McFolderEntry item, ref List<McFolderEntry> folderEntries)
        {
            if (item != null) {
                folderEntries.Add (item);
            }
        }
    }
}
