//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McPath : McAbstrObjectPerAcc
    {
        [Indexed]
        public string ParentId { get; set; }

        [Indexed]
        public string ServerId { get; set; }

        public bool WasMoveDest { get; set; }

        public bool IsFolder { get; set; }

        public McPath ()
        {
        }

        public McPath (int accountId)
        {
            AccountId = accountId;
        }

        public static bool Dominates (int accountId, string topId, string bottomId)
        {
            var node = QueryByServerId (accountId, bottomId);
            if (null == node) {
                return false;
            }
            while (node != null && McFolder.AsRootServerId != node.ParentId) {
                if (topId == node.ParentId) {
                    return true;
                }
                node = QueryByServerId (accountId, node.ParentId);
            }
            return false;
        }

        // Note: by design there should only be one entry per ServerId && AccountId.
        // We choose to announce inconsistency and keep running rather than crash w/r/t this.

        // Temporary - used to exclude GMail from error reporting.
        public int Insert (bool isGMail)
        {
            var preExists = McPath.QueryByServerId (AccountId, ServerId);
            Log.Debug (Log.LOG_DB, "McPath:Insert ServerId {0}", ServerId);
            if (null != preExists) {
                // In a move, expect the server to send one Add, which is not an error.
                if (!preExists.WasMoveDest && !isGMail) {
                    Log.Error (Log.LOG_DB, string.Format ("Duplicate McPath: old entry {0}/{1} replaced with {2}/{3} @ {4}.",
                        preExists.ParentId, preExists.ServerId,
                        ParentId, ServerId, new StackTrace ().ToString ()));
                }
                preExists.Delete ();
            }
            return base.Insert ();
        }

        public override int Insert ()
        {
            using (var capture = CaptureWithStart ("Insert")) {
                var preExists = McPath.QueryByServerId (AccountId, ServerId);
                Log.Debug (Log.LOG_DB, "McPath:Insert ServerId {0}", ServerId);
                if (null != preExists) {
                    // In a move, expect the server to send one Add, which is not an error.
                    if (!preExists.WasMoveDest) {
                        Log.Error (Log.LOG_DB, string.Format ("Duplicate McPath: old entry {0}/{1} replaced with {2}/{3} @ {4}.",
                            preExists.ParentId, preExists.ServerId,
                            ParentId, ServerId, new StackTrace ().ToString ()));
                    }
                    preExists.Delete ();
                }
                return base.Insert ();
            }
        }

        public override int Update ()
        {
            using (var capture = CaptureWithStart ("Update")) {
                var preExists = McPath.QueryByServerId (AccountId, ServerId);
                Log.Info (Log.LOG_DB, "McPath:Update ServerId {0}", ServerId);
                if (null != preExists && preExists.Id != Id) {
                    Log.Error (Log.LOG_DB, string.Format ("Duplicate McPath: old entry {0}/{1} replaced with {2}/{3} @ {4}.",
                        preExists.ParentId, preExists.ServerId,
                        ParentId, ServerId, new StackTrace ().ToString ()));
                    preExists.Delete ();
                }
                return base.Update ();
            }
        }

        public override int Delete ()
        {
            using (var capture = CaptureWithStart ("Delete")) {
                int retval = 0;
                NcModel.Instance.RunInTransaction (() => {
                    var subs = QueryByParentId (AccountId, ServerId, true);
                    Log.Info (Log.LOG_DB, "McPath:Delete ServerId {0}", ServerId);
                    foreach (var sub in subs) {
                        Log.Info (Log.LOG_DB, "McPath:Delete ServerId {0} (subordinate)", sub.ServerId);
                        sub.Delete ();
                    }
                    DeleteNonFolderByParentId (AccountId, ServerId);
                    retval = base.Delete ();
                }, true);
                return retval;
            }
        }

        public static List<McPath> QueryByParentId (int accountId, string parentId, bool isFolder)
        {
            return NcModel.Instance.Db.Query<McPath> (
                "SELECT * FROM McPath WHERE " +
                " likelihood (AccountId = ?, 1.0) AND " +
                " likelihood (IsFolder = ?, 0.5) AND " +
                " likelihood (ParentId = ?, 0.05) ", 
                accountId, isFolder, parentId);
        }

        public static McPath QueryByServerId (int accountId, string serverId)
        {
            var paths = NcModel.Instance.Db.Query<McPath> (
                            "SELECT * FROM McPath WHERE " +
                            " likelihood (AccountId = ?, 1.0) AND " +
                            " likelihood (ServerId = ?, 0.001) ", 
                            accountId, serverId);
            if (0 == paths.Count) {
                return null;
            }
            if (1 < paths.Count) {
                var pastFirst = false;
                Log.Error (Log.LOG_DB, "McPath.QueryByServerId: Multiple entries (returning first) ...");
                foreach (var path in paths) {
                    Log.Error (Log.LOG_DB, "... {0}/{1}", path.ParentId, path.ServerId);
                    if (pastFirst) {
                        path.Delete ();
                    } else {
                        pastFirst = true;
                    }
                }
            }
            return paths.First ();
        }

        public static void DeleteNonFolderByParentId (int accountId, string parentId)
        {
            NcAssert.True (NcModel.Instance.IsInTransaction ());
            NcModel.Instance.Db.Query<McPath> (
                "DELETE FROM McPath WHERE " +
                " likelihood (AccountId = ?, 1.0) AND " +
                " likelihood (ParentId = ?, 0.05) AND " +
                " likelihood (IsFolder = 0, 0.99) ", accountId, parentId);
        }
    }
}

