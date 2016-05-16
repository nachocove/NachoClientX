//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    public class NcMigration40 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McAccount> ().Count ();
        }

        // Alpha users only; no beta has left with google in-progress account.
        // Remove left over accounts that have Google Callback in their name.
        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var account in Db.Table<McAccount> ()) {
                if (McAccount.ConfigurationInProgressEnum.GoogleCallback == account.ConfigurationInProgress) {
                    account.Delete ();
                }
            }
        }
    }
}

