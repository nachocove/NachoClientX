//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoPlatform
{
    public class StoreHandler : IPlatformStoreHandler
    {
        private static volatile StoreHandler instance;
        private static object syncRoot = new Object ();

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

        public void Start ()
        {
        }

        // from IPlatformStoreHandler
        // initialize product data from store
        public void LoadProductDataFromStore ()
        {
        }

        // from IPlatformStoreHandler
        // buy a new license
        public bool BuyLicenseFromStore ()
        {
            return false;
        }

        // from IPlatformStoreHandler
        // restore previously bought license
        public bool RestoreLicenseFromStore ()
        {
            return false;
        }

        // from IPlatformStoreHandler
        public void SetPurchasingStatus (bool status)
        {
        }

        // from IPlatformStoreHandler
        // get the purchase status
        public bool GetPurchasedStatus ()
        {
            return false;
        }

        // from IPlatformStoreHandler
        public void RegisterPurchase (string productId, DateTime purchaseDate)
        {
        }
            
        // from IPlatformStoreHandler
        // call this when shutting down
        public void Stop ()
        {
        }
    }
}

