//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoClient;
using MonoTouch.UIKit;
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
            McMutables.SetBool ("hasSyncedFolders", accountId.ToString (), toWhat);
        }

        //Gets the status of the sync bit for given accountId
        //True if they have succesfully sync'd folders
        //False if not
        static public bool GetSyncedBit(int accountId)
        {
            return McMutables.GetBool("hasSyncedFolders", accountId.ToString ());
        }

        //Sets the status of the tutorial bit for given accountId
        static public void SetTutorialBit (int accountId, bool toWhat)
        {
            McMutables.SetBool ("hasViewedTutorial", accountId.ToString (), toWhat);
        }

        //Gets the status of the tutorial bit for given accountId
        //True if they have viewed tutorial
        //False if not
        static public bool GetTutorialBit(int accountId)
        {
            return McMutables.GetBool("hasViewedTutorial", accountId.ToString ());
        }

        //Sets the status of the creds bit for given accountId
        static public void SetCredsBit (int accountId, bool toWhat)
        {
            McMutables.SetBool ("hasProvidedCreds", accountId.ToString (), toWhat);
        }

        //Gets the status of the creds bit for given accountId
        //True if they have provided creds at least once
        //False if not
        static public bool GetCredsBit(int accountId)
        {
            return McMutables.GetBool("hasProvidedCreds", accountId.ToString ());
        }
        //Sets the status of the creds bit for given accountId
        static public void SetCertificateBit (int accountId, bool toWhat)
        {
            McMutables.SetBool ("hasAcceptedCertificate", accountId.ToString (), toWhat);
        }

        //Gets the status of the creds bit for given accountId
        //True if they have provided creds at least once
        //False if not
        static public bool GetCertificateBit(int accountId)
        {
            return McMutables.GetBool("hasAcceptedCertificate", accountId.ToString ());
        }

        //Sets the status of the creds bit for given accountId
        static public void SetBeStateBit (int accountId, bool toWhat)
        {
            McMutables.SetBool ("isBeRunning", accountId.ToString (), toWhat);
        }

        //Gets the status of the creds bit for given accountId
        //True if they have provided creds at least once
        //False if not
        static public bool GetBeStateBit(int accountId)
        {
            return McMutables.GetBool("isBeRunning", accountId.ToString ());
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


