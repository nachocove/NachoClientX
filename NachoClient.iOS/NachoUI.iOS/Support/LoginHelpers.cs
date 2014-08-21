//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoClient;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class LoginHelpers
    {
        public LoginHelpers ()
        {
        }

        //Sets the status of the sync bit for given accountId
        static public void SetFirstSyncCompleted (int accountId, bool toWhat)
        {
            NcAssert.True (GetCurrentAccountId() == accountId);
            McMutables.SetBool ("hasSyncedFolders", accountId.ToString (), toWhat);
        }

        //Gets the status of the sync bit for given accountId
        //True if they have succesfully sync'd folders
        //False if not
        static public bool HasFirstSyncCompleted(int accountId)
        {
            NcAssert.True (GetCurrentAccountId() == accountId);
            return McMutables.GetOrCreateBool("hasSyncedFolders", accountId.ToString (), false);
        }

        //Sets the status of the tutorial bit for given accountId
        static public void SetHasViewedTutorial (int accountId, bool toWhat)
        {
            NcAssert.True (GetCurrentAccountId() == accountId);
            McMutables.SetBool ("hasViewedTutorial", accountId.ToString (), toWhat);
        }

        //Gets the status of the tutorial bit for given accountId
        //True if they have viewed tutorial
        //False if not
        static public bool HasViewedTutorial(int accountId)
        {
            NcAssert.True (GetCurrentAccountId() == accountId);
            return McMutables.GetOrCreateBool("hasViewedTutorial", accountId.ToString (), false);
        }

        //Sets the status of the creds bit for given accountId
        static public void SetHasProvidedCreds (int accountId, bool toWhat)
        {
            NcAssert.True (GetCurrentAccountId() == accountId);
            McMutables.SetBool ("hasProvidedCreds", accountId.ToString (), toWhat);
        }

        //Gets the status of the creds bit for given accountId
        //True if they have provided creds at least once
        //False if not
        static public bool HasProvidedCreds(int accountId)
        {
            NcAssert.True (GetCurrentAccountId() == accountId);
            return McMutables.GetOrCreateBool("hasProvidedCreds", accountId.ToString (), false);
        }

        static public int GetCurrentAccountId()
        {
            NachoClient.iOS.AppDelegate appDelegate = (NachoClient.iOS.AppDelegate)UIApplication.SharedApplication.Delegate;
            NcAssert.True (null != appDelegate.Account);

            return appDelegate.Account.Id;
        }

        static public bool IsCurrentAccountSet()
        {
            NachoClient.iOS.AppDelegate appDelegate = (NachoClient.iOS.AppDelegate)UIApplication.SharedApplication.Delegate;
            if (null != appDelegate.Account) {
                return true;
            } else {
                return false;
            }
        }
    }
}


