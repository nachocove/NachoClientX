//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;

namespace NachoCore.Model
{
    public class McMapFolderItem : McObjectPerAccount
    {
        [Indexed]
        public int FolderId { get; set; }

        [Indexed]
        public int ItemId { get; set; }

        [Indexed]
        public uint ClassCode { get; set; }

        public McMapFolderItem ()
        {
        }

        public McMapFolderItem (int accountId)
        {
            AccountId = accountId;
        }

        public static McMapFolderItem QueryByFolderIdItemIdClassCode (int accountId, int folderId, int itemId,
                                                                      uint classCode)
        {
            return BackEnd.Instance.Db.Table<McMapFolderItem> ().SingleOrDefault (mm => 
                accountId == mm.AccountId && folderId == mm.FolderId && itemId == mm.ItemId &&
            classCode == mm.ClassCode);
        }
    }
}

