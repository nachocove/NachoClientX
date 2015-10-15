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

        List<McFolder> ClientOwnedFolderList;
        public override int GetNumberOfObjects ()
        {
            ClientOwnedFolderList =  NcModel.Instance.Db.Query<McFolder>(
                string.Format ("SELECT * from McFolder WHERE ServerId IN ('{0}') AND IsClientOwned = 1", 
                    String.Join ("','", folderMapping.Keys)));
            // adding +1 here just lets us treat the FindAndDeleteDup call as a single 'object'.
            // We don't know how many objects we will actually process there.
            return ClientOwnedFolderList.Count + 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            RenameClientOwnedFolders ();
            FindAndDeleteDup ();
        }

        private void RenameClientOwnedFolders ()
        {
            foreach (var folder in ClientOwnedFolderList) {
                folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ServerId = folderMapping [folder.ServerId];
                    return true;
                });
                UpdateProgress (1);
            }
        }

        private void FindAndDeleteDup ()
        {
            foreach (var account in McAccount.QueryByAccountType (McAccount.AccountTypeEnum.IMAP_SMTP)) {
                bool tryAgain = true;
                while (tryAgain) {
                    bool somethingHappened = false;
                    foreach (var folder in McFolder.QueryByAccountId<McFolder> (account.Id)) {
                        LogFolder (folder, "Looking at"); 
                        var PossiblyDupFolders = McFolder.QueryByServerIdMult<McFolder> (account.Id, folder.ServerId).ToList ();
                        if (PossiblyDupFolders.Count () > 1) {
                            LogFolder (folder, string.Format ("Ocurrences: {0}", PossiblyDupFolders.Count ()));
                            foreach (var dup in PossiblyDupFolders) {
                                if (dup.Id != folder.Id) {
                                    LogFolder (dup, "DUP");
                                }
                            }
                            if (PossiblyDupFolders.Where (x => x.IsClientOwned == true).Any ()) {
                                foreach (var dup in PossiblyDupFolders.Where (x => x.IsClientOwned == false)) {
                                    LogFolder (dup, "Deleting");
                                    dup.Delete ();
                                    somethingHappened = true;
                                }
                            } else {
                                // delete all but the first one in the list.
                                foreach (var dup in PossiblyDupFolders.Skip (1)) {
                                    LogFolder (dup, "Deleting");
                                    dup.Delete ();
                                    somethingHappened = true;
                                }
                            }
                        }
                        if (somethingHappened) {
                            break; // we need to start again
                        }
                    }
                    if (!somethingHappened) {
                        tryAgain = false;
                    }
                }
            }
            UpdateProgress (1);
        }

        private void LogFolder (McFolder folder, string logPrefix)
        {
            Log.Warn (Log.LOG_UTILS, "Migration: {0} Account ID: {1}, folder ID: {2}, ServerId: {3}/{4}, clientOwned: {5}, distinguished: {6}, hidden: {7}",
                logPrefix, folder.AccountId, folder.Id, folder.ImapFolderNameRedacted (), folder.ServerIdHashString (),
                folder.IsClientOwned, folder.IsDistinguished, folder.IsHidden);
        }
    }
}

