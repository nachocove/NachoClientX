//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
using System;
using NachoCore.Utils;
using NachoCore;
using System.Runtime.InteropServices;

namespace NachoPlatform
{
    public class CloudHandler : IPlatformCloudHandler
    {
        private static volatile CloudHandler instance;
        private static object syncRoot = new Object ();

        public static CloudHandler Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new CloudHandler ();
                    }
                }
                return instance;
            }
        }

        public CloudHandler ()
        {
        }

        public string GetUserId ()
        {
            if (Keychain.Instance.HasKeychain ()) {
                return Keychain.Instance.GetUserId ();
            } else {
                return null;
            }
        }

        public void SetUserId (string UserId)
        {
            if (Keychain.Instance.HasKeychain ()) {
                Keychain.Instance.SetUserId (UserId);
            }
        }

        public bool IsAlreadyPurchased ()
        {
            return false;
        }

        public DateTime GetPurchaseDate ()
        {
            return DateTime.Now;
        }

        public void RecordPurchase (DateTime purchaseDate)
        {
        }

        public void SetFirstInstallDate (DateTime installDate)
        {
        }

        public DateTime GetFirstInstallDate ()
        {
            return DateTime.Now;
        }

        public void Start ()
        {
        }

        public void Stop ()
        {
        }

    }
}