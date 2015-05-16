//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using PM = NachoCore.Utils.PermissionManager;

namespace NachoCore.Model
{
    public class NcMigration24 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            MigrateToDeviceAccount ();
        }

        // Permissions are associated with the device.
        // Migrate from single account to multi-account.
        public void MigrateToDeviceAccount ()
        {
            var account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();

            if (null != account) {
                foreach (var module in new string[] { PM.Module_Calendar, PM.Module_Contacts, PM.Module_Notifications }) {
                    if (null != McMutables.Get (account.Id, module, PM.Key_AskedUserForPermission)) {
                        var askedForPermission = McMutables.GetBool (account.Id, module, PM.Key_AskedUserForPermission);
                        McMutables.SetBool (McAccount.GetDeviceAccount ().Id, module, PM.Key_AskedUserForPermission, askedForPermission);
                    }
                    if (null != McMutables.Get (account.Id, module, PM.Key_UserGrantedUsPermission)) {
                        var grantedPermission = McMutables.GetBool (account.Id, module, PM.Key_UserGrantedUsPermission);
                        McMutables.SetBool (McAccount.GetDeviceAccount ().Id, module, PM.Key_UserGrantedUsPermission, grantedPermission);
                    }
                }
                if (NachoCore.Utils.LoginHelpers.HasViewedTutorial (account.Id)) {
                    NachoCore.Utils.LoginHelpers.SetHasViewedTutorial (true);
                }
            }
        }
    }
}
