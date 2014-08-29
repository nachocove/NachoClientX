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

        public override int Insert ()
        {
            var preExists = McPath.QueryByServerId (AccountId, ServerId);
            if (null != preExists) {
                Log.Error (Log.LOG_DB, string.Format ("Duplicate McPath: old entry {0}/{1} replaced with {2}/{3} @ {4}.",
                    preExists.ParentId, preExists.ServerId,
                    ParentId, ServerId, new StackTrace ().ToString ()));
                preExists.Delete ();
            }
            return base.Insert ();
        }

        public override int Update ()
        {
            var preExists = McPath.QueryByServerId (AccountId, ServerId);
            if (null != preExists && preExists.Id != Id) {
                Log.Error (Log.LOG_DB, string.Format ("Duplicate McPath: old entry {0}/{1} replaced with {2}/{3} @ {4}.",
                    preExists.ParentId, preExists.ServerId,
                    ParentId, ServerId, new StackTrace ().ToString ()));
                preExists.Delete ();
            }
            return base.Update ();
        }

        public override int Delete ()
        {
            var subs = QueryByParentId (AccountId, ServerId);
            foreach (var sub in subs) {
                sub.Delete ();
            }
            return base.Delete ();
        }

        public static IEnumerable<McPath> QueryByParentId (int accountId, string parentId)
        {
            return NcModel.Instance.Db.Table<McPath> ().Where (pe =>
                pe.ParentId == parentId && pe.AccountId == accountId);
        }

        public static McPath QueryByServerId (int accountId, string serverId)
        {
            var path = NcModel.Instance.Db.Table<McPath> ().Where (pe => 
                pe.ServerId == serverId && pe.AccountId == accountId).SingleOrDefault ();
            return path;
        }
    }
}

