//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration19 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        // After discussion, decided on a different default for Notifications
        public override void Run (System.Threading.CancellationToken token)
        {
            NcModel.Instance.Db.Execute (
                "UPDATE McAccount SET NotificationConfiguration = ?", (int)McAccount.DefaultNotificationConfiguration);
            UpdateProgress (1);
        }
    }
}

