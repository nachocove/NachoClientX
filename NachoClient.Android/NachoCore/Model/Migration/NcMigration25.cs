//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration25 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McServer> ().Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var server in Db.Table<McServer> ()) {
                server.Capabilities = McAccount.ActiveSyncCapabilities;
                server.Update ();
            }
            UpdateProgress (GetNumberOfObjects ());
        }
    }
}

