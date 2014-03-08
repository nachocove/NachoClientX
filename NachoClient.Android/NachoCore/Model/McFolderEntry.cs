//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Linq;

namespace NachoCore.Model
{
    public class McFolderEntry : McObjectPerAccount
    {
        [Indexed]
        public string ServerId { get; set; }

        public static T QueryByServerId<T> (int accountId, string serverId) where T : McFolderEntry, new()
        {
            return BackEnd.Instance.Db.Query<T> (
                string.Format ("SELECT f.* FROM {0} AS f WHERE " +
                    " f.AccountId = ? AND " + 
                    " f.ServerId = ? ", 
                    typeof(T).Name), 
                accountId, serverId).SingleOrDefault ();
        }
    }
}
