//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoCore.Utils
{
    public class LoginHelpers
    {
        protected const string MODULE = "ClientConfigurationBits";

        public LoginHelpers ()
        {
        }

        //Sets the status of the sync bit for given accountId
        //Implies that auto-d is complete too.
        static public void SetFirstSyncCompleted (int accountId, bool toWhat)
        {
            Log.Info (Log.LOG_UI, "SetFirstSyncCompleted: {0}={1}", accountId, toWhat);
            McMutables.SetBool (accountId, MODULE, "hasSyncedFolders", toWhat);
        }

        //Gets the status of the sync bit for given accountId
        //True if they have succesfully sync'd folders
        //False if not
        static public bool HasFirstSyncCompleted (int accountId)
        {
            return McMutables.GetOrCreateBool (accountId, MODULE, "hasSyncedFolders", false);
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
            foreach (var a in NcModel.Instance.Db.Table<McAccount> ()) {
                var cred = McCred.QueryByAccountId<McCred> (a.Id).SingleOrDefault ();
            }

            McAccount account = GetMostRecentAccount ();
            if (null != account) {
                if (!account.ConfigurationInProgress) {
                    return account;
                }
            }
            foreach (var a in NcModel.Instance.Db.Table<McAccount> ()) {
                if (a.ConfigurationInProgress) {
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

    }
}
