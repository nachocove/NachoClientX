//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model    
{
    public class McMapAttachmentItem : McAbstrObjectPerAcc
    {
        [Indexed]
        public int AttachmentId { get; set; }

        [Indexed]
        public int ItemId { get; set; }

        [Indexed]
        public McAbstrFolderEntry.ClassCodeEnum ClassCode { get; set; }

        public bool IncludedInBody { get; set; }

        public McMapAttachmentItem ()
        {
        }

        public McMapAttachmentItem (int accountId)
        {
            AccountId = accountId;
        }

        public static McMapAttachmentItem QueryByAttachmentIdItemIdClassCode (int accountId, int attachmentId, int itemId, 
            McAbstrItem.ClassCodeEnum classCode)
        {
            var maps = NcModel.Instance.Db.Query<McMapAttachmentItem> ("SELECT mm.* FROM McMapAttachmentItem AS mm WHERE " +
                " likelihood (mm.AccountId = ?, 1.0) AND " +
                " likelihood (mm.AttachmentId = ?, 0.05) AND " +
                " likelihood (mm.ItemId = ?, 0.001) AND " +
                " likelihood (mm.ClassCode = ?, 0.2)",
                accountId, attachmentId, itemId, classCode);
            return maps.SingleOrDefault ();
        }

        public static int QueryItemCount (int attachmentId)
        {
            return NcModel.Instance.Db.Table<McMapAttachmentItem> ().Where (x => x.AttachmentId == attachmentId).Count ();
        }
    }
}

