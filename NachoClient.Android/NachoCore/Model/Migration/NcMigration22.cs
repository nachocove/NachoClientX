//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration22 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McCred> ().Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var cred in Db.Table<McCred> ()) {
                cred.Expiry = DateTime.MaxValue;
                cred.Update ();
            }
            UpdateProgress (Db.Table<McCred> ().Count ());
        }
    }
}



