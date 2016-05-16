//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration27 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McPending> ().Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var pending in Db.Table<McPending> ()) {
                pending.UpdateWithOCApply<McPending> ((record) => {
                    var target = (McPending)record;
                    target.Capability = McAccount.ActiveSyncCapabilities;
                    return true;
                });
            }
            UpdateProgress (Db.Table<McPending> ().Count ());
        }
    }
}

