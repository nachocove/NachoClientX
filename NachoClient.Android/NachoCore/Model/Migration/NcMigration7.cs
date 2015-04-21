//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration7 : NcMigration
    {
        public NcMigration7 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            return Db.Table<McAccount> ().Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var account in Db.Table<McAccount>()) {
                var outbox = McFolder.GetClientOwnedOutboxFolder (account.Id);
                if (null != outbox) {
                    if (outbox.IsClientOwned) {
                        MakeThisHidden (outbox);
                    }
                }
                var calDrafts = McFolder.GetCalDraftsFolder (account.Id);
                if (null != calDrafts) {
                    if (calDrafts.IsClientOwned) {
                        MakeThisHidden (calDrafts);
                    }
                }
            }
        }

        public void MakeThisHidden (McFolder folder)
        {
            if (!folder.IsHidden) {
                folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.IsHidden = true;
                    return true;
                });
            }
        }
    }
}

