//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McMapFolderFolderEntry : McAbstrObjectPerAcc
    {
        [Indexed]
        public int FolderId { get; set; }

        [Indexed]
        public int FolderEntryId { get; set; }

        [Indexed]
        public McAbstrFolderEntry.ClassCodeEnum ClassCode { get; set; }

        [Indexed]
        public int AsSyncEpoch { get; set; }

        public McMapFolderFolderEntry ()
        {
            AsSyncEpoch = int.MaxValue;
        }

        public McMapFolderFolderEntry (int accountId)
        {
            AccountId = accountId;
        }

        public override int Insert ()
        {
            NcAssert.True (AsSyncEpoch < int.MaxValue);
            return base.Insert ();
        }

        public override int Update ()
        {
            NcAssert.True (AsSyncEpoch < int.MaxValue);
            return base.Update ();
        }

        // Gives all the map entries for this folder's contents (not deep).
        public static List<McMapFolderFolderEntry> QueryByFolderId (int accountId, int folderId)
        {
            return NcModel.Instance.Db.Query<McMapFolderFolderEntry> ("SELECT mm.* FROM McMapFolderFolderEntry AS mm WHERE " +
                " mm.AccountId = ? AND " +
                " mm.FolderId = ? ",
                accountId, folderId).ToList();
        }

        public static List<McMapFolderFolderEntry> QueryByFolderIdClassCode (int accountId, int folderId, McAbstrFolderEntry.ClassCodeEnum classCode)
        {
            return NcModel.Instance.Db.Query<McMapFolderFolderEntry> ("SELECT mm.* FROM McMapFolderFolderEntry AS mm WHERE " +
                " mm.AccountId = ? AND " +
                " mm.FolderId = ? AND " +
                " mm.ClassCode = ?",
                accountId, folderId, classCode).ToList();
        }

        public static McMapFolderFolderEntry QueryByFolderIdFolderEntryIdClassCode (int accountId, int folderId, int folderEntryId,
                                                                                    McAbstrFolderEntry.ClassCodeEnum classCode)
        {
            var maps = NcModel.Instance.Db.Query<McMapFolderFolderEntry> ("SELECT mm.* FROM McMapFolderFolderEntry AS mm WHERE " +
                       " mm.AccountId = ? AND " +
                       " mm.FolderId = ? AND " +
                       " mm.FolderEntryId = ? AND " +
                       " mm.ClassCode = ?",
                           accountId, folderId, folderEntryId, classCode);
            return maps.SingleOrDefault ();
        }

        public static List<McMapFolderFolderEntry> QueryByFolderEntryIdClassCode (int accountId, int folderEntryId, 
            McAbstrFolderEntry.ClassCodeEnum classCode)
        {
            var maps = NcModel.Instance.Db.Query<McMapFolderFolderEntry> ("SELECT mm.* FROM McMapFolderFolderEntry AS mm WHERE " +
                " mm.AccountId = ? AND " +
                " mm.FolderEntryId = ? AND " +
                " mm.ClassCode = ?",
                accountId, folderEntryId, classCode);
            return maps.ToList ();
        }
    }
}

