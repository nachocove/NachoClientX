//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
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
    }
}

