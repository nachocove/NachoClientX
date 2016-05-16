//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration20 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            var accounts = NcModel.Instance.Db.Table<McAccount> ();
            foreach (var account in accounts) {
                var folder = McFolder.GetClientOwnedDraftsFolder (account.Id);
                if (null != folder) {
                    folder.UpdateSet_DisplayName ("On-Device Drafts");
                }
            }
            UpdateProgress (1);
        }
    }
}

