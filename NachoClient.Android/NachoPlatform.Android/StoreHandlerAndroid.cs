//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoPlatform
{
    public class StoreHandler : IPlatformStoreHandler
    {
        private static volatile StoreHandler instance;
        private static object syncRoot = new Object ();

        public enum InAppPurchaseState : uint
        {
            NotPurchased = (St.Last + 1),
            PrdDataWait,
            PurchaseWait,
            Purchased,
            Expired,
            Last = Expired,
        };

        public static StoreHandler Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new StoreHandler ();
                    }
                }
                return instance;
            }
        }

        public StoreHandler ()
        {
        }

        // from IPlatformStoreHandler
        // call this when starting up
        public void Start ()
        {
        }

        // from IPlatformStoreHandler
        // purchase a new license
        public bool PurchaseLicense ()
        {
            return false;
        }

        // from IPlatformStoreHandler
        // restore previously bought license
        public bool RestoreLicense ()
        {
            return false;
        }

        // from IPlatformStoreHandler
        // get the purchase status
        public bool IsAlreadyPurchased ()
        {
            return false;
        }

        // from IPlatformStoreHandler
        // can we purchase now?
        public bool CanPurchase ()
        {
            return false;
        }
            
        // from IPlatformStoreHandler
        // call this when shutting down
        public void Stop ()
        {
        }
    }
}

