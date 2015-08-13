//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    public class NcMigration24 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McAccount> ().Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var account in Db.Table<McAccount> ()) {
                if (string.IsNullOrEmpty (account.GetLogSalt ())) {
                    account.GenerateAndUpdateLogSalt ();
                }
            }
        }
    }
}

