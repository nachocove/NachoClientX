//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;

namespace NachoCore.Model
{
    public class NcMigration30 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McCred> ().Count ();
        }

        public override void Run (CancellationToken token)
        {
            foreach (var cred in Db.Table<McCred> ()) {
                cred.CredType = McCred.CredTypeEnum.Password;
                cred.Update ();
            }
        }
    }
}
