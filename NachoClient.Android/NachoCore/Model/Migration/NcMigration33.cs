//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    // McAccount.DeviceCapabilities was changed, removing CalWriter.  Update the device account with the new
    // set of capabilities.

    public class NcMigration33 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            var deviceAccount = McAccount.GetDeviceAccount ();
            if (null != deviceAccount) {
                deviceAccount.AccountCapability = McAccount.DeviceCapabilities;
                deviceAccount.Update ();
            }
            UpdateProgress (1);
        }
    }
}

