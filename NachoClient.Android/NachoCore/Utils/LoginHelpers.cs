//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;
using System.Globalization;

namespace NachoCore.Utils
{
    public static class LoginHelpers
    {
        static string MODULE = "ClientConfigurationBits";

        static public void UserInterventionStateChanged (int accountId)
        {
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Status = NcResult.Info (NcResult.SubKindEnum.Info_UserInterventionFlagChanged),
                Account = McAccount.QueryById<McAccount> (accountId),
                Tokens = new string[] { DateTime.Now.ToString () },
            });
        }

        static public bool IsUserInterventionRequired (int accountId, out McServer serverWithIssue, out BackEndStateEnum serverStatus)
        {
            var servers = McServer.QueryByAccountId<McServer> (accountId);

            foreach (var server in servers) {
                var status = BackEnd.Instance.BackEndState (accountId, server.Capabilities);
                switch (status) {
                case BackEndStateEnum.CertAskWait:
                case BackEndStateEnum.CredWait:
                case BackEndStateEnum.ServerConfWait:
                    Log.Info (Log.LOG_UTILS, "UserInterventionRequired: {0}", status);
                    serverWithIssue = server;
                    serverStatus = status; 
                    return true;
                }
            }
            serverWithIssue = null;
            serverStatus = BackEndStateEnum.NotYetStarted; 
            return false;
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
            McServer serverWithIssue;
            BackEndStateEnum serverStatus;
            if (IsUserInterventionRequired (accountId, out serverWithIssue, out serverStatus)) {
                Log.Info (Log.LOG_UTILS, "ShouldAlertUser: {0}: user intervention required {1}/{2}", accountId, serverWithIssue, serverStatus);
                return true;
            }
            DateTime expiry;
            string rectificationUrl;
            if (LoginHelpers.PasswordWillExpire (accountId, out expiry, out rectificationUrl)) {
                Log.Info (Log.LOG_UTILS, "ShouldAlertUser: {0}: password will expire {1}", accountId, expiry);
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
                // No warning for OAuth2 because it expires often & is auto-refreshed
                if (McCred.CredTypeEnum.OAuth2 == cred.CredType) {
                    continue;
                }
                if (cred.Expiry < gonnaExpireOn) {
                    expiry = cred.Expiry;
                    gonnaExpireOn = cred.Expiry;
                    rectificationUrl = cred.RectificationUrl;
                }
            }
            return (DateTime.MaxValue != gonnaExpireOn);
        }

        static public void ClearPasswordExpiration (int accountId)
        {
            var creds = McCred.QueryByAccountId<McCred> (accountId);
            if (null == creds) {
                return;
            }
            foreach (var cred in creds) {
                cred.ClearExpiry ();
            }
        }

        static public bool ShowHotCards ()
        {
            var accountId = McAccount.GetDeviceAccount ().Id;
            return McMutables.GetBoolDefault (accountId, "GlobalSettings", "ShowHotCards", true);
        }

        static public void SetShowHotCards (bool show)
        {
            var accountId = McAccount.GetDeviceAccount ().Id;
            McMutables.SetBool (accountId, "GlobalSettings", "ShowHotCards", show);
        }

        static public int GlobalAccountId {
            get { return McAccount.GetDeviceAccount ().Id; }
        }

        static public void SetSwitchToTime (McAccount account)
        {
            // Save most recently used
            SetMostRecentAccount (account);
            var time = DateTime.UtcNow.ToString ("O");
            McMutables.Set (account.Id, "AccountSwitcher", "SwitchTo", time);
        }

        static public DateTime GetSwitchToTime (McAccount account)
        {
            var defaultTime = DateTime.UtcNow.ToString ("O");
            var switchToTime = McMutables.GetOrCreate (account.Id, "AccountSwitcher", "SwitchTo", defaultTime);
            DateTime result;
            if (!DateTime.TryParseExact (switchToTime, "O", null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result)) {
                if (!DateTime.TryParse (switchToTime, out result)) {
                    Log.Warn (Log.LOG_UTILS, "Could not parse switch-to time for account {0}: {1}", account.Id, switchToTime);
                    result = DateTime.UtcNow;
                }
            }
            return result;
        }

        static public void SetMostRecentAccount (McAccount account)
        {
            var deviceId = McAccount.GetDeviceAccount ().Id;
            McMutables.SetInt (deviceId, "AccountSwitcher", "MostRecent", account.Id);
        }

        static McAccount GetMostRecentAccount ()
        {
            var accounts = McAccount.GetAllAccounts ();
            var deviceAccount = McAccount.GetDeviceAccount ();
            var recentAccountId = McMutables.GetInt (deviceAccount.Id, "AccountSwitcher", "MostRecent", 0);
            if ((0 == recentAccountId) || (deviceAccount.Id == recentAccountId)) {
                return null;
            }
            return accounts.Where (x => x.Id == recentAccountId).FirstOrDefault ();
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

        public static bool ConfiguredAccountExists (string emailAddress, McAccount.AccountServiceEnum service)
        {
            var existingAccount = McAccount.QueryByEmailAddrAndService (emailAddress, service).SingleOrDefault ();
            if (null != existingAccount) {
                return (McAccount.ConfigurationInProgressEnum.Done == existingAccount.ConfigurationInProgress);
            } else {
                return false;
            }
        }

        public static bool ConfiguredAccountExists (string emailAddress)
        {
            var existingAccount = McAccount.QueryByEmailAddr (emailAddress).SingleOrDefault ();
            if (null != existingAccount) {
                return (McAccount.ConfigurationInProgressEnum.Done == existingAccount.ConfigurationInProgress);
            } else {
                return false;
            }
        }

        public static void SetGoogleSignInCallbackArrived (bool value)
        {
            McMutables.SetBool (GlobalAccountId, MODULE, "GoogleSignInCallbackArrived", value);
        }

        public static bool GetGoogleSignInCallbackArrived ()
        {
            return McMutables.GetOrCreateBool (GlobalAccountId, MODULE, "GoogleSignInCallbackArrived", false);
        }
    }
}
