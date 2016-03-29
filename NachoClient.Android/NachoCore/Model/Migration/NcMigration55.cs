//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore;
using System.IO;

namespace NachoClient.AndroidClient
{
    public class NcMigration55 : NcMigration
    {
        public NcMigration55 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            if (NachoPlatform.Device.Instance.BaseOs () == NachoPlatform.OsCode.iOS) {
                var deviceAccount = McAccount.GetDeviceAccount ();
                NcModel.Instance.Db.Execute ("UPDATE McContact SET DeviceLastUpdate = 0 WHERE AccountId = ? AND PortraitId != 0", deviceAccount.Id);
            }
        }
    }
}

