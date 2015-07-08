//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoCore.Utils
{
    public static class LoginHelpers
    {
        static string MODULE = "ClientConfigurationBits";

        //Sets the status of the sync bit for given accountId
        //Implies that auto-d is complete too.
        static public void SetFirstSyncCompleted (int accountId, bool toWhat)
        {
            Log.Info (Log.LOG_UI, "SetFirstSyncCompleted: {0}={1}", accountId, toWhat);
            McMutables.SetBool (accountId, MODULE, "hasSyncedFolders", toWhat);
        }

        static public void SetDoesBackEndHaveIssues (int accountId, bool toWhat)
        {
            Log.Info (Log.LOG_UI, "SetDoesBackEndHaveIssues: {0}={1}", accountId, toWhat);
            McMutables.SetBool (accountId, MODULE, "doesBackEndHaveIssues", toWhat);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Status = NcResult.Info (NcResult.SubKindEnum.Info_UserInterventionFlagChanged),
                Account = McAccount.QueryById<McAccount> (accountId),
                Tokens = new string[] { DateTime.Now.ToString () },
            });
        }

        static public bool DoesBackEndHaveIssues (int accountId)
        {
            return McMutables.GetOrCreateBool (accountId, MODULE, "doesBackEndHaveIssues", false);
        }

        static public bool ShouldAlertUser ()
        {
            foreach (var accountId in McAccount.GetAllConfiguredNonDeviceAccountIds()) {
                if (ShouldAlertUser (accountId)) {
                    return true;
                }
            }
            return false;
        }

        static public bool ShouldAlertUser (int accountId)
        {
            if (DoesBackEndHaveIssues (accountId)) {
                return true;
            }
            DateTime expiry;
            string rectificationUrl;
            if (LoginHelpers.PasswordWillExpire (accountId, out expiry, out rectificationUrl)) {
                return true;
            }
            return false;
        }

        //Sets the status of the tutorial bit for given accountId
        static public void SetHasViewedTutorial (bool toWhat)
        {
            var accountId = McAccount.GetDeviceAccount ().Id;
            Log.Info (Log.LOG_UI, "SetHasViewedTutorial: {0}={1}", accountId, toWhat);
            // TODO: should this really be per-account or once for all accounts?
            McMutables.SetBool (accountId, MODULE, "hasViewedTutorial", toWhat);
        }

        /// <summary>
        /// Determines if the tutorial has been viewed.  The is a global flag.
        /// </summary>
        /// <returns><c>true</c> if has viewed tutorial the specified accountId; otherwise, <c>false</c>.</returns>
        /// <param name="accountId">Account identifier should only be used during migration.</param>
        static public bool HasViewedTutorial (int accountId = 0)
        {
            if (0 == accountId) {
                accountId = McAccount.GetDeviceAccount ().Id;
            }
            return McMutables.GetOrCreateBool (accountId, MODULE, "hasViewedTutorial", false);
        }

        // We want to fail if someone just plucks a null
        static public int GetCurrentAccountId ()
        {
            NcAssert.True (null != NcApplication.Instance.Account);
            return NcApplication.Instance.Account.Id;
        }

        // Pre-req before calling GetCurrentAccountId
        static public bool IsCurrentAccountSet ()
        {
            return (null != NcApplication.Instance.Account);
        }

        // Return true if a password associated with the account id will expire
        // soon and return information about the password that expires soonest.
        static public bool PasswordWillExpire (int accountId, out DateTime expiry, out string rectificationUrl)
        {
            expiry = DateTime.MaxValue;
            rectificationUrl = String.Empty;
            var creds = McCred.QueryByAccountId<McCred> (accountId);
            if (null == creds) {
                return false;
            }
            var gonnaExpireOn = DateTime.MaxValue;
            foreach (var cred in creds) {
                if (cred.Expiry < gonnaExpireOn) {
                    expiry = cred.Expiry;
                    gonnaExpireOn = cred.Expiry;
                    rectificationUrl = cred.RectificationUrl;
                }
            }
            return (DateTime.MaxValue != gonnaExpireOn);
        }

        static public int GlobalAccountId {
            get { return McAccount.GetDeviceAccount ().Id; }
        }

        static public void SetSwitchToTime (McAccount account)
        {
            // Save most recently used
            SetMostRecentAccount (account);
            var time = DateTime.UtcNow.ToString ();
            McMutables.Set (account.Id, "AccountSwitcher", "SwitchTo", time);
        }

        static public DateTime GetSwitchToTime (McAccount account)
        {
            var defaultTime = DateTime.UtcNow.ToString ();
            var switchToTime = McMutables.GetOrCreate (account.Id, "AccountSwitcher", "SwitchTo", defaultTime);
            return DateTime.Parse (switchToTime);
        }

        static public void SetMostRecentAccount (McAccount account)
        {
            var deviceId = McAccount.GetDeviceAccount ().Id;
            McMutables.SetInt (deviceId, "AccountSwitcher", "MostRecent", account.Id);
        }

        static public McAccount GetMostRecentAccount ()
        {
            var deviceAccount = McAccount.GetDeviceAccount ();
            if (null == deviceAccount) {
                return null;
            }
            var recentAccountId = McMutables.GetInt (deviceAccount.Id, "AccountSwitcher", "MostRecent", 0);
            if ((0 == recentAccountId) || (deviceAccount.Id == recentAccountId)) {
                return null;
            }
            return McAccount.QueryById<McAccount> (recentAccountId);
        }

        // Look for a configured account
        public static McAccount PickStartupAccount ()
        {
            McAccount account = GetMostRecentAccount ();
            if (null != account) {
                if (McAccount.ConfigurationInProgressEnum.Done == account.ConfigurationInProgress) {
                    return account;
                }
            }
            foreach (var a in NcModel.Instance.Db.Table<McAccount> ()) {
                if (McAccount.ConfigurationInProgressEnum.Done != a.ConfigurationInProgress) {
                    continue;
                }
                if (McAccount.AccountTypeEnum.Device == a.AccountType) {
                    continue;
                }
                return a;
            }
            // Default to device account
            return McAccount.GetDeviceAccount ();
        }

        public static string GetPassword (McAccount account)
        {
            if (McAccount.AccountServiceEnum.GoogleDefault == account.AccountService) {
                return "";
            }
            var creds = McCred.QueryByAccountId<McCred> (account.Id).SingleOrDefault ();
            if (null == creds) {
                return "";
            } else {
                return creds.GetPassword ();
            }
        }

        public static bool AccountExists (string emailAddress)
        {
            var existingAccount = McAccount.QueryByEmailAddr (emailAddress).SingleOrDefault ();
            return (null != existingAccount);
        }
    }
}
