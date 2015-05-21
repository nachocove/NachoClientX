//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration23 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McProtocolState> ().Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var pstate in Db.Table<McProtocolState> ()) {
                pstate.AsSyncLimit = McProtocolState.AsSyncLimit_Default;
                pstate.Update ();
            }
            UpdateProgress (Db.Table<McProtocolState> ().Count ());
        }
    }
}
