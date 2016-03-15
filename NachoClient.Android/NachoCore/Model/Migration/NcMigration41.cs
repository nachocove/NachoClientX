//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    public class NcMigration41 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McAccount> ().Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var account in Db.Table<McAccount> ()) {
                bool foundItem = false;
                try {
                    foundItem = !string.IsNullOrEmpty (account.GetLogSalt ());
                } catch (NachoPlatform.KeychainItemNotFoundException) {}

                if (!foundItem) {
                    account.GenerateAndUpdateLogSalt ();
                }
            }
        }
    }
}

