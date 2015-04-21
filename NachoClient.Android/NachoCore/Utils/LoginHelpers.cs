//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
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
            if (toWhat) {
                LoginHelpers.SetAutoDCompleted (accountId, true);
            }
            Log.Info (Log.LOG_UI, "SetFirstSyncCompleted: {0}={1}", accountId, toWhat);
            NcAssert.True (GetCurrentAccountId () == accountId);
            McMutables.SetBool (accountId, MODULE, "hasSyncedFolders", toWhat);
        }

        //Gets the status of the sync bit for given accountId
        //True if they have succesfully sync'd folders
        //False if not
        static public bool HasFirstSyncCompleted (int accountId)
        {
            NcAssert.True (GetCurrentAccountId () == accountId);
            return McMutables.GetOrCreateBool (accountId, MODULE, "hasSyncedFolders", false);
        }

        static public void SetDoesBackEndHaveIssues (int accountId, bool toWhat)
        {
            Log.Info (Log.LOG_UI, "SetDoesBackEndHaveIssues: {0}={1}", accountId, toWhat);
            NcAssert.True (GetCurrentAccountId () == accountId);
            McMutables.SetBool (accountId, MODULE, "doesBackEndHaveIssues", toWhat);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Status = NcResult.Info (NcResult.SubKindEnum.Info_UserInterventionFlagChanged),
                Account = McAccount.QueryById<McAccount> (accountId),
                Tokens = new string[] { DateTime.Now.ToString () },
            });
        }

        static public bool DoesBackEndHaveIssues (int accountId)
        {
            NcAssert.True (GetCurrentAccountId () == accountId);
            return McMutables.GetOrCreateBool (accountId, MODULE, "doesBackEndHaveIssues", false);
        }

        //Sets the status of the tutorial bit for given accountId
        static public void SetHasViewedTutorial (int accountId, bool toWhat)
        {
            Log.Info (Log.LOG_UI, "SetHasViewedTutorial: {0}={1}", accountId, toWhat);
            NcAssert.True (GetCurrentAccountId () == accountId);
            // TODO: should this really be per-account or once for all accounts?
            McMutables.SetBool (accountId, MODULE, "hasViewedTutorial", toWhat);
        }

        //Gets the status of the tutorial bit for given accountId
        //True if they have viewed tutorial
        //False if not
        static public bool HasViewedTutorial (int accountId)
        {
            NcAssert.True (GetCurrentAccountId () == accountId);
            return McMutables.GetOrCreateBool (accountId, MODULE, "hasViewedTutorial", false);
        }

        //Sets the status of the auto-d success bit for given accountId
        static public void SetAutoDCompleted (int accountId, bool toWhat)
        {
            Log.Info (Log.LOG_UI, "SetAutoDCompleted: {0}={1}", accountId, toWhat);
            NcAssert.True (GetCurrentAccountId () == accountId);
            McMutables.SetBool (accountId, MODULE, "hasAutoDCompleted", toWhat);
        }

        //Gets the status of the auto-d success bit for given accountId
        //True if they have succesfully sync'd folders
        //False if not
        static public bool HasAutoDCompleted (int accountId)
        {
            NcAssert.True (GetCurrentAccountId () == accountId);
            return McMutables.GetOrCreateBool (accountId, MODULE, "hasAutoDCompleted", false);
        }

        //Sets the status of the creds bit for given accountId
        static public void SetHasProvidedCreds (int accountId, bool toWhat)
        {
            Log.Info (Log.LOG_UI, "SetHasProvidedCreds: {0}={1}", accountId, toWhat);
            NcAssert.True (GetCurrentAccountId () == accountId);
            McMutables.SetBool (accountId, MODULE, "hasProvidedCreds", toWhat);
        }

        //Gets the status of the creds bit for given accountId
        //True if they have provided creds at least once
        //False if not
        static public bool HasProvidedCreds (int accountId)
        {
            NcAssert.True (GetCurrentAccountId () == accountId);
            return McMutables.GetOrCreateBool (accountId, MODULE, "hasProvidedCreds", false);
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
    }
}