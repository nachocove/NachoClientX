//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;

namespace NachoCore.Model
{
    public class McMapFolderFolderEntry : McObjectPerAccount
    {
        [Indexed]
        public int FolderId { get; set; }

        [Indexed]
        public int FolderEntryId { get; set; }

        [Indexed]
        public McFolderEntry.ClassCodeEnum ClassCode { get; set; }

        public McMapFolderFolderEntry ()
        {
        }

        public McMapFolderFolderEntry (int accountId)
        {
            AccountId = accountId;
        }

        public static McMapFolderFolderEntry QueryByFolderIdFolderEntryIdClassCode (int accountId, int folderId, int folderEntryId,
            McFolderEntry.ClassCodeEnum classCode)
        {
            return BackEnd.Instance.Db.Table<McMapFolderFolderEntry> ().SingleOrDefault (mm => 
                accountId == mm.AccountId && folderId == mm.FolderId && folderEntryId == mm.FolderEntryId &&
            classCode == mm.ClassCode);
        }
    }
}

