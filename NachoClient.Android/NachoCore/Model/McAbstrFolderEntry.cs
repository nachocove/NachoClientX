//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoCore.Model
{
    // If SQLite.Net would tolerate an abstract class, we'd be one.
    public class McAbstrFolderEntry : McAbstrObjectPerAcc
    {
        public enum ClassCodeEnum
        {
            // Never-in-folder created by Nacho for derived classes that aren't in folders.
            NeverInFolder = 0,
            // Values taken from ActiveSync are < 100.
            Tasks = 1,
            Email = 2,
            Calendar = 3,
            Contact = 4,
            Notes = 5,
            Sms = 6,
            MaxSyncable = 6,
            // Values created by Nacho are >= 100, and CAN be in folders (just not synced).
            Folder = 100,
            // We are not using generic or journal.
            Journal = 101,
            Generic = 102,
            Chat = 103
        };

        [Indexed]
        public string ServerId { get; set; }

        public bool IsAwaitingDelete { get; set; }

        public bool IsAwaitingCreate { get; set; }

        public virtual ClassCodeEnum GetClassCode ()
        {
            NcAssert.True (false);
            return ClassCodeEnum.Sms; // Just to make compiler happy.
        }

        public static T QueryByServerId<T> (int accountId, string serverId) where T : McAbstrFolderEntry, new()
        {
            return NcModel.Instance.Db.Query<T> (
                string.Format ("SELECT f.* FROM {0} AS f WHERE " +
                    " likelihood (f.AccountId = ?, 1.0) AND " + 
                    " likelihood (f.IsAwaitingDelete = 0, 1.0) AND " +
                    " likelihood (f.ServerId = ?, 0.001) ", 
                    typeof(T).Name), 
                accountId, serverId).SingleOrDefault ();
        }

        public static IEnumerable<T> QueryByServerIdMult<T> (int accountId, string serverId) where T : McAbstrFolderEntry, new()
        {
            return NcModel.Instance.Db.Query<T> (
                string.Format ("SELECT f.* FROM {0} AS f WHERE " +
                    " likelihood (f.AccountId = ?, 1.0) AND " + 
                    " likelihood (f.IsAwaitingDelete = 0, 1.0) AND " +
                    " likelihood (f.ServerId = ?, 0.001) ", 
                    typeof(T).Name), 
                accountId, serverId);
        }

        public static McAbstrFolderEntry QueryAllForServerId (int accountId, string serverId)
        {
            List<McAbstrFolderEntry> folderEntries = new List<McAbstrFolderEntry>();
            CondAddToList (QueryByServerId<McEmailMessage> (accountId, serverId), ref folderEntries);
            CondAddToList (QueryByServerId<McCalendar> (accountId, serverId), ref folderEntries);
            CondAddToList (QueryByServerId<McContact> (accountId, serverId), ref folderEntries);
            CondAddToList (QueryByServerId<McTask> (accountId, serverId), ref folderEntries);
            NcAssert.True (folderEntries.Count <= 1, "Should not have multiple McFolderEntries with the same ServerId");
            return folderEntries.SingleOrDefault ();
        }

        private static void CondAddToList (McAbstrFolderEntry item, ref List<McAbstrFolderEntry> folderEntries)
        {
            if (item != null) {
                folderEntries.Add (item);
            }
        }

        public static void GloballyReWriteServerId (int accountId, string oldServerId, string newServerId)
        {
            var maybes = new List<McAbstrFolderEntry> ();
            maybes.Add (McFolder.QueryByServerId<McFolder> (accountId, oldServerId));
            maybes.Add (McEmailMessage.QueryByServerId<McEmailMessage> (accountId, oldServerId));
            maybes.Add (McContact.QueryByServerId<McContact> (accountId, oldServerId));
            maybes.Add (McCalendar.QueryByServerId<McCalendar> (accountId, oldServerId));
            maybes.Add (McTask.QueryByServerId<McTask> (accountId, oldServerId));

            foreach (var entry in maybes) {
                if (null != entry) {
                    var folder = entry as McFolder;
                    if (null != folder) {
                        folder.UpdateSet_ServerId (newServerId);
                    } else {
                        // TODO - figure out how to make this "scale".
                        if (entry is McEmailMessage) {
                            entry.UpdateWithOCApply<McEmailMessage> ((record) => {
                                var target = (McEmailMessage)record;
                                target.ServerId = newServerId;
                                return true;
                            });
                        } else {
                            entry.ServerId = newServerId;
                            entry.Update ();
                        }
                    }
                }
            }
            var folders = McFolder.QueryByParentId (accountId, oldServerId);
            foreach (var folder in folders) {
                folder.UpdateSet_ParentId (newServerId);
            }
        }
    }
}
