//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore
{
    public class NcAccountMonitor
    {
        public bool HasNewEmail;

        class AccountInfo
        {
            public bool hasNewEmail;
            public DateTime lastChanged;
            public DateTime lastChecked;
        }

        // Deleted account ids are never reused
        Dictionary<int,AccountInfo> accountInfo;

        public NcAccountMonitor ()
        {
            HasNewEmail = false;
            accountInfo = new Dictionary<int, AccountInfo> ();

            Update (null);
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        private static NcAccountMonitor instance;
        private static object syncRoot = new Object ();

        public static NcAccountMonitor Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new NcAccountMonitor ();
                    }
                }
                return instance; 
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
            case NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded:
            case NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded:
            case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                break;
            }
        }

        AccountInfo GetInfo (int accountId)
        {
            AccountInfo info;
            if (accountInfo.TryGetValue (accountId, out info)) {
                return info;
            }
            info = new AccountInfo ();
            info.hasNewEmail = false;
            info.lastChecked = DateTime.MinValue;
            info.lastChanged = DateTime.MinValue.AddSeconds (1);
            return info;
        }

        void Update (McAccount account)
        {
            if (null != account) {
                var info = GetInfo (account.Id);
                info.lastChanged = DateTime.UtcNow;
            }
            // Optimization: Updates to current account won't affect new mail beacon
            if ((null != account) && (NcApplication.Instance.Account.Id == account.Id)) {
                return;
            }
            // Notify on changes
            if (IsThereNewEmail () != HasNewEmail) {
                HasNewEmail = !HasNewEmail;
                // Send status ind
                var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_AccountBeaconChanged);
                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                    Status = result,
                    Account = NcApplication.Instance.Account,
                });
            }
        }

        bool IsThereNewEmail ()
        {
            int deviceId = McAccount.GetDeviceAccount ().Id;

            int currentId = 0;
            if (null != NcApplication.Instance.Account) {
                currentId = NcApplication.Instance.Account.Id;
            }

            // Check up to date accounts.
            foreach (var account in NcModel.Instance.Db.Table<McAccount> ()) {
                if (deviceId == account.Id) {
                    continue;
                }
                if (currentId == account.Id) {
                    continue;
                }
                var info = GetInfo (account.Id);
                if (info.lastChecked < info.lastChanged) {
                    continue;
                }
                if (info.hasNewEmail) {
                    return true;
                }
            }

            // Bring accounts up to date until we get a hit
            foreach (var account in NcModel.Instance.Db.Table<McAccount> ()) {
                if (deviceId == account.Id) {
                    continue;
                }
                if (currentId == account.Id) {
                    continue;
                }
                var info = GetInfo (account.Id);
                if (info.lastChecked >= info.lastChanged) {
                    continue;
                }
                var lastTime = EmailHelper.GetNewSincePreference (account.Id);
                var hot = McEmailMessage.QueryUnreadAndHotAfter (lastTime);
                info.lastChecked = DateTime.UtcNow;
                info.hasNewEmail = (0 < hot.Count);
                if (info.hasNewEmail) {
                    return true;
                }
            }
            return false;
        }

    }
}

