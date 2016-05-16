//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration17 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            NcModel.Instance.Db.Execute ("UPDATE McAccount SET FastNotificationEnabled = 1");
            UpdateProgress (1);
        }
    }
}

