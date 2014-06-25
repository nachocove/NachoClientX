//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NachoCore.Model
{
    public class McPath : McObjectPerAccount
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
            while (McFolder.AsRootServerId != node.ParentId) {
                if (topId == node.ParentId) {
                    return true;
                }
            }
            return false;
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

