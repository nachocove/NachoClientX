//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoClient;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace NachoCore.Utils
{
    public class LoginHelpers
    {
        public LoginHelpers ()
        {
        }

        //Sets the status of the sync bit for given accountId
        static public void SetSyncedBit (int accountId, bool toWhat)
        {
            string account = accountId.ToString ();
            string bitVal;

            if (toWhat == true) {
                bitVal = "1";
            } else {
                bitVal = "0";
            }
            McMutables.Set ("hasSyncedFolders", account, bitVal);
        }

        //Gets the status of the sync bit for given accountId
        //True if they have succesfully sync'd folders
        //False if not
        static public bool GetSyncedBit(int accountId)
        {
            string account = accountId.ToString ();
            string hasSyncedFoldersVal = McMutables.Get ("hasSyncedFolders", account);
            bool hasSyncCompleted = false;
            if (null != hasSyncedFoldersVal) {
                if (hasSyncedFoldersVal == "1") {
                    hasSyncCompleted = true;
                } else {
                    hasSyncCompleted = false;
                }
            } else {
                hasSyncCompleted = false;
            }
            return hasSyncCompleted;
        }

        //Sets the status of the tutorial bit for given accountId
        static public void SetTutorialBit (int accountId, bool toWhat)
        {
            string account = accountId.ToString ();
            string bitVal;

            if (toWhat == true) {
                bitVal = "1";
            } else {
                bitVal = "0";
            }
            McMutables.Set ("hasViewedTutorial", account, bitVal);
        }

        //Gets the status of the tutorial bit for given accountId
        //True if they have viewed tutorial
        //False if not
        static public bool GetTutorialBit(int accountId)
        {
            string account = accountId.ToString ();
            string hasViewedTutorialVal = McMutables.Get ("hasViewedTutorial", account);
            bool hasViewedTutorial = false;
            if (null != hasViewedTutorialVal) {
                if (hasViewedTutorialVal == "1") {
                    hasViewedTutorial = true;
                } else {
                    hasViewedTutorial = false;
                }
            } else {
                hasViewedTutorial = false;
            }
            return hasViewedTutorial;
        }

        //Sets the status of the creds bit for given accountId
        static public void SetCredsBit (int accountId, bool toWhat)
        {
            string account = accountId.ToString ();
            string bitVal;

            if (toWhat == true) {
                bitVal = "1";
            } else {
                bitVal = "0";
            }
            McMutables.Set ("hasProvidedCreds", account, bitVal);
        }

        //Gets the status of the creds bit for given accountId
        //True if they have provided creds at least once
        //False if not
        static public bool GetCredsBit(int accountId)
        {
            string account = accountId.ToString ();
            string hasProvidedCredsVal = McMutables.Get ("hasProvidedCreds", account);
            bool hasProvidedCreds = false;
            if (null != hasProvidedCredsVal) {
                if (hasProvidedCredsVal == "1") {
                    hasProvidedCreds = true;
                } else {
                    hasProvidedCreds = false;
                }
            } else {
                hasProvidedCreds = false;
            }
            return hasProvidedCreds;
        }

        static public int getCurrentAccountId()
        {
            NachoClient.iOS.AppDelegate appDelegate = (NachoClient.iOS.AppDelegate)UIApplication.SharedApplication.Delegate;
            if (null == appDelegate.Account) {
                return 0;
            } else {
                return appDelegate.Account.Id;
            }
        }
    }
}

