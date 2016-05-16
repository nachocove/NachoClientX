//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;

namespace NachoCore.Model
{
    public class NcMigration28 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (CancellationToken token)
        {
            NcModel.Instance.Db.Execute ("DROP TABLE IF EXISTS McEmailMessageScoreSyncInfo");
            NcModel.Instance.Db.Execute ("DROP TABLE IF EXISTS McEmailAddressScoreSyncInfo");
        }
    }
}

