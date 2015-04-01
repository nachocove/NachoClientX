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
            return null;
        }

        public void SetUserId (string UserId)
        {
        }

        public bool GetPurchasedStatus (string productId)
        {
            return false;
        }

        public DateTime GetPurchasedDate (string productId)
        {
            return DateTime.Now;
        }

        public void SetPurchasedStatus (string productId, DateTime purchaseDate)
        {
        }

        public void SetAppInstallDate (DateTime installDate)
        {
        }

        public DateTime GetAppInstallDate ()
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