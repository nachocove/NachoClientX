//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class NcMigration47 : NcMigration
    {
        static Dictionary<string, string> folderMapping = new Dictionary<string, string> () {
            {McFolder.ClientOwned_Outbox_Deprecated, McFolder.ClientOwned_Outbox},
            {McFolder.ClientOwned_EmailDrafts_Deprecated, McFolder.ClientOwned_EmailDrafts},
            {McFolder.ClientOwned_CalDrafts_Deprecated, McFolder.ClientOwned_CalDrafts},
            {McFolder.ClientOwned_GalCache_Deprecated, McFolder.ClientOwned_GalCache},
            {McFolder.ClientOwned_Gleaned_Deprecated, McFolder.ClientOwned_Gleaned},
            {McFolder.ClientOwned_LostAndFound_Deprecated, McFolder.ClientOwned_LostAndFound},
            {McFolder.ClientOwned_DeviceContacts_Deprecated, McFolder.ClientOwned_DeviceContacts},
            {McFolder.ClientOwned_DeviceCalendars_Deprecated, McFolder.ClientOwned_DeviceCalendars},
        };

        List<McFolder> folderList;
        public override int GetNumberOfObjects ()
        {
            folderList =  NcModel.Instance.Db.Query<McFolder>(
                string.Format ("SELECT * from McFolder WHERE ServerId IN ('{0}') AND IsClientOwned = 1", 
                    String.Join ("','", folderMapping.Keys)));
            return folderList.Count;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            LogDups ();
            foreach (var folder in folderList) {
                folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ServerId = folderMapping [folder.ServerId];
                    return true;
                });
                UpdateProgress (1);
            }
        }

        private void LogDups ()
        {
            foreach (var account in McAccount.QueryByAccountType (McAccount.AccountTypeEnum.IMAP_SMTP)) {
                foreach (var folder in McFolder.QueryByAccountId<McFolder> (account.Id)) {
                    var n = McFolder.QueryByServerIdMult<McFolder> (account.Id, folder.ServerId).Count ();
                    if (n > 1) {
                        Log.Warn (Log.LOG_UTILS, "Migration: Account ID: {0}, folder ID: {1}, ServerId: {2}, occurrences: {3}, clientOwned: {4}, distinguished: {5}, hidden: {6}",
                            account.Id, folder.Id, folder.ImapFolderNameRedacted (), n, folder.IsClientOwned, folder.IsDistinguished, folder.IsHidden);
                    }
                }
            }
        }
    }
}

