//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration21 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McEmailMessage> ().Where (x => 1 == x.HasBeenGleaned).Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            int numUpdated =
                Db.Execute ("UPDATE McEmailMessage SET HasBeenGleaned = 2 WHERE HasBeenGleaned = 1");
            UpdateProgress (numUpdated);
        }
    }
}

