//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using Foundation;
using StoreKit;
using NachoCore.Utils;

namespace NachoPlatform
{
    public class StoreHandler : IPlatformStoreHandler
    {
        private static volatile StoreHandler instance;
        private static object syncRoot = new Object ();

        private List<string> productNamesList;
        private const string NachoClientLicenseProductId = "com.alternateworlds.storekit.testing.annually";
        private InAppPurchaseManager InAppPurchaseManager;
        private PaymentObserver PaymentObserver;
        private NSObject PriceObserver, PurchaseSucceedObserver, PurchaseFailedObserver, RequestObserver;
        private bool ProductDataLoaded = false;
        private bool NachoClientLicensePurchased = false;
        private bool Purchasing = false;

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
            productNamesList = new List<string>() {NachoClientLicenseProductId};
            InAppPurchaseManager = new InAppPurchaseManager();
            PaymentObserver = new PaymentObserver (InAppPurchaseManager);
            SetupObservers ();
            LoadProductDataFromStore ();
        }

        // from IPlatformStoreHandler
        // initialize product data from store
        public void LoadProductDataFromStore ()
        {
            // only if we can make payments, request the prices
            if (InAppPurchaseManager.CanMakePayments()) {
                // now go get prices, if we don't have them already
                if (!ProductDataLoaded) {
                    Log.Info(Log.LOG_SYS, "InAppPurchase: Requesting product data from store");
                    InAppPurchaseManager.RequestProductData (productNamesList); // async request via StoreKit -> App Store
                }
            } else {
                // can't make payments (purchases turned off in Settings?)
                Log.Info(Log.LOG_SYS, "InAppPurchase: Cannot make payments");
            }
        }

        // from IPlatformStoreHandler
        // buy a new license
        public bool BuyLicenseFromStore ()
        {
            if (NachoClientLicensePurchased) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: NachoClient license already purchased. No need to buy again.");
                return true;
            }
            if (!ProductDataLoaded) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: Product Information not yet received from store. Trying to load again.");
                LoadProductDataFromStore ();
                return false;
            } else if (Purchasing) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: NachoClient license is already in the process of being purchased.");
                return true;
            } else {
                Log.Info (Log.LOG_SYS, "Purchasing NachoClient license from store...");
                InAppPurchaseManager.PurchaseProduct (NachoClientLicenseProductId);
                Purchasing = true;
                return true;
            }
        }

        // from IPlatformStoreHandler
        // restore previously bought license
        public bool RestoreLicenseFromStore ()
        {
            if (NachoClientLicensePurchased) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: NachoClient license already purchased. No need to restore.");
                return true;
            }
            if (!ProductDataLoaded) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: Product Information not yet received from store. Trying to load again.");
                LoadProductDataFromStore ();
                return false;
            } else if (Purchasing) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: NachoClient license is already in the process of being purchased.");
                return true;
            } else {
                Log.Info (Log.LOG_SYS, "Restoring NachoClient license from store...");
                // No point restoring non renewable subscription
                //InAppPurchaseManager.Restore ();
                // buy again instead
                // you will need to do this only in the case that iCloud is disabled and this is a fresh install
                InAppPurchaseManager.PurchaseProduct (NachoClientLicenseProductId);
                Purchasing = true;
                return true;
            }
        }

        // from IPlatformStoreHandler
        public void SetPurchasingStatus(bool status)
        {
            Purchasing = status;
        }
      
        public void SetupObservers()
        {
            // Call this once upon startup of in-app-purchase activities
            // This also kicks off the TransactionObserver which handles the various communications
            SKPaymentQueue.DefaultQueue.AddTransactionObserver(PaymentObserver);


            // setup an observer to wait for prices to come back from StoreKit <- AppStore
            PriceObserver = NSNotificationCenter.DefaultCenter.AddObserver (InAppPurchaseManager.InAppPurchaseManagerProductsFetchedNotification,
                (notification) => {
                    NSDictionary info = notification.UserInfo;
                    if (info == null) {
                        // if info is null, probably NO valid prices returned, therefore it doesn't exist at all
                        Log.Info(Log.LOG_SYS, "InAppPurchase: No valid prices returned from store.");
                        return;
                    }

                    // mark that product information has been loaded
                    var key = new NSString(NachoClientLicenseProductId);
                    if (!NachoClientLicensePurchased && info.ContainsKey (key)) {
                        ProductDataLoaded = true;

                        SKProduct product = (SKProduct)info [NachoClientLicenseProductId];
                        PrintProductDetails (product);
                    }
                });

            // setup an observer to wait for purchase successful notification
            PurchaseSucceedObserver = NSNotificationCenter.DefaultCenter.AddObserver (InAppPurchaseManager.InAppPurchaseManagerTransactionSucceededNotification,
                (notification) => {
                    // update the status after a successful purchase
                    Log.Info(Log.LOG_SYS, "InAppPurchase: Purchase transaction successful");         
                    if (GetPurchasedStatus() == true) {
                        Log.Info(Log.LOG_SYS, "InAppPurchase: Purchased {0}", NachoClientLicenseProductId);         
                        NachoClientLicensePurchased = true;
                        Purchasing = false;
                    }
                });

            //set up an observer to wait for transaction failed notifications
            PurchaseFailedObserver = NSNotificationCenter.DefaultCenter.AddObserver (InAppPurchaseManager.InAppPurchaseManagerTransactionFailedNotification,
                (notification) => {
                    Log.Info(Log.LOG_SYS, "InAppPurchase: Purchase transaction failed"); 
                    Purchasing = false;
                });

            //set up an observer to wait for request failed notifications
            RequestObserver = NSNotificationCenter.DefaultCenter.AddObserver (InAppPurchaseManager.InAppPurchaseManagerRequestFailedNotification,
                (notification) => {
                    Log.Info(Log.LOG_SYS, "InAppPurchase: Request failed");
                    Purchasing = false;
                });
        }

        // from IPlatformStoreHandler
        // get the purchase status
        public bool GetPurchasedStatus () {
            return CloudHandler.Instance.GetPurchasedStatus (NachoClientLicenseProductId);
        }


        // from IPlatformStoreHandler
        public void RegisterPurchase (string productId, DateTime purchaseDate)
        {
            // Register the purchase, so it is remembered for next time 
            if (productId == NachoClientLicenseProductId) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: Registering NachoClient license purchase status.");
                CloudHandler.Instance.SetPurchasedStatus (NachoClientLicenseProductId, purchaseDate);
            }
            else{
                Log.Warn(Log.LOG_SYS, "InAppPurchase: Cannot register product purchased status for product {0}", productId);
            }
        }

        private void PrintProductDetails(SKProduct product)
        {            
            Log.Info(Log.LOG_SYS, "InAppPurchase: Found product id: {0}", product.ProductIdentifier);
            Log.Info(Log.LOG_SYS, "InAppPurchase: Product title: {0}", product.LocalizedTitle);
            Log.Info(Log.LOG_SYS, "InAppPurchase: Product description: {0}", product.LocalizedDescription);
            Log.Info(Log.LOG_SYS, "InAppPurchase: Product l10n price: {0}", product.LocalizedPrice());
        }



        // from IPlatformStoreHandler
        // call this when shutting down
        public void Stop ()
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver (PriceObserver);
            NSNotificationCenter.DefaultCenter.RemoveObserver (PurchaseSucceedObserver);
            NSNotificationCenter.DefaultCenter.RemoveObserver (PurchaseFailedObserver);
            NSNotificationCenter.DefaultCenter.RemoveObserver (RequestObserver);
        }
    }
}

