//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

#define EXCHANGE_WALL_ENABLED

using System;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class PermissionManager
    {
        public const string Module_Calendar = "DeviceCalendar";
        public const string Module_Contacts = "DeviceContacts";
        public const string Module_Notifications = "DeviceNotifications";

        public const string Key_AskedUserForPermission = "AskedUserForPermission";
        public const string Key_UserGrantedUsPermission = "UserGrantedUsPermission";

        private const string MutablesModule = "PermissionManager";
        private const string CanCreateExchangeKey = "CanCreateExchange";
        private const string EnableExchangeCode = "CHANGEME";

        private static PermissionManager _Instance;
        private static object Lock = new object ();
        public static PermissionManager Instance {
            get {
                lock (Lock) {
                    if (_Instance == null) {
                        _Instance = new PermissionManager ();
                    }
                }
                return _Instance;
            }
        }

        public PermissionManager ()
        {
        }

        private bool? _CanCreateExchange;

        public bool CanCreateExchange {
            get {

#if EXCHANGE_WALL_ENABLED
                if (!_CanCreateExchange.HasValue){
                    _CanCreateExchange = McMutables.GetBool (McAccount.GetDeviceAccount ().Id, MutablesModule, CanCreateExchangeKey);
                }
                return _CanCreateExchange.Value;
#else
                return true;
#endif
            }
        }

        public bool VerifyExchangeCode (string code) {
            bool verified = CanCreateExchange || code.Equals (EnableExchangeCode, StringComparison.OrdinalIgnoreCase);
            if (verified){
                McMutables.SetBool (McAccount.GetDeviceAccount ().Id, MutablesModule, CanCreateExchangeKey, true);
                _CanCreateExchange = true;
            }
            return verified;
        }

        public void ResetCanCreateExchange ()
        {
            McMutables.Delete (McAccount.GetDeviceAccount ().Id, MutablesModule, CanCreateExchangeKey);
            _CanCreateExchange = false;
        }
    }
}

