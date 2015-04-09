//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using Foundation;
using StoreKit;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoPlatform
{
    public class StoreHandler : SKProductsRequestDelegate, IPlatformStoreHandler
    {
        private static volatile StoreHandler instance;
        private static object syncRoot = new Object ();

        public NcStateMachine Sm { set; get; }

        private McLicenseInformation LicenseInformation;
        private NSMutableDictionary ValidProducts;


        private InAppPurchaseManager InAppPurchaseManager;
        private PaymentObserver PaymentObserver;
        private NSObject PriceObserver, PurchaseSucceedObserver, PurchaseFailedObserver, RequestObserver;
        private bool ProductDataLoaded = false;
        private bool NachoClientLicensePurchased = false;
        private bool Purchasing = false;

        public enum InAppPurchaseState : uint
        {
            NotPurchased = (St.Last + 1),
            PrdDataWait,
            PurchaseWait,
            Purchased,
            Expired,
            Last = Expired,
        };

        public class InAppPurchaseEvent : SmEvt
        {
            new public enum E : uint
            {
                PrdDataReqSendEvt = (SmEvt.E.Last + 1),
                PrdDataRecvdEvt,
                PrdDataReqFailedEvt,
                DoPurchaseEvt,
                PurchasedEvt,
                PurchaseExpiredEvt,
                PurchaseFailedEvt,
                PurchaseRestoredEvt,
                PurchaseDeferredEvt,
                StopEvt,
                Last = StopEvt,
            };
        }

        // State-machine's state persistance callback.
        private void UpdateInAppPurchaseState ()
        {
            // do this only for purchase relevant states
            if ((Sm.State == (uint)InAppPurchaseState.NotPurchased) ||
                (Sm.State == (uint)InAppPurchaseState.PurchaseWait) ||
                (Sm.State == (uint)InAppPurchaseState.Purchased) ||
                (Sm.State == (uint)InAppPurchaseState.Expired)) {
                LicenseInformation.InAppPurchaseState = Sm.State;
                LicenseInformation.Update ();
            }
        }

        public StoreHandler ()
        {
            /*
             * State Machine design:
             * Events triggered by our code:
             *     PrdDataReqSentEvt
             *     DoPurchaseEvt
             *     PurchaseExpiredEvt
             * 
             * Events reported back by StoreKit:
             *     PrdDataRecvdEvt
             *     PrdDataReqFailedEvt
             *     PurchasedEvt
             *     PurchaseFailedEvt
             *     PurchaseRestoredEvt
             *     PurchaseDeferredEvt
             * 
             * States:
             *     NotPurchased
             *     PrdDataWait
             *     PurchaseWait
             *     Purchased
             *     Expired
             * 
             *  The simpleest state transition would be:
             * 
             * Start -> LoadProductDataWait -> NotPurchased -> Purchasing -> Purchased
             */
            Sm = new NcStateMachine ("IAPMSM") { 
                Name = string.Format ("InAppPurchaseMgrSM"),
                LocalEventType = typeof(InAppPurchaseEvent),
                LocalStateType = typeof(InAppPurchaseState),
                StateChangeIndication = UpdateInAppPurchaseState,
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Drop = new [] {
                            (uint)InAppPurchaseEvent.E.PrdDataReqFailedEvt,
                            (uint)InAppPurchaseEvent.E.PrdDataRecvdEvt,
                            (uint)InAppPurchaseEvent.E.PurchasedEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseExpiredEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseFailedEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseRestoredEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseDeferredEvt,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)InAppPurchaseEvent.E.StopEvt,
                        },
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = DoSendProductDataRequest,
                                State = (uint)InAppPurchaseState.PrdDataWait
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.PrdDataReqSendEvt,
                                Act = DoSendProductDataRequest,
                                State = (uint)InAppPurchaseState.PrdDataWait
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.DoPurchaseEvt,
                                Act = DoSendProductDataRequest,
                                State = (uint)InAppPurchaseState.PrdDataWait
                            },
                        }
                    },
                    new Node {
                        State = (uint)InAppPurchaseState.PrdDataWait,
                        Drop = new [] {
                            (uint)InAppPurchaseEvent.E.DoPurchaseEvt,
                            (uint)InAppPurchaseEvent.E.PurchasedEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseExpiredEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseFailedEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseRestoredEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseDeferredEvt,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = DoSendProductDataRequest,
                                State = (uint)InAppPurchaseState.PrdDataWait
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.PrdDataReqSendEvt,
                                Act = DoSendProductDataRequest,
                                State = (uint)InAppPurchaseState.PrdDataWait
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.PrdDataRecvdEvt,
                                Act = DoReceiveProductData,
                                ActSetsState = true
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.PrdDataReqFailedEvt,
                                Act = DoNop,
                                State = (uint)St.Start
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.StopEvt,
                                Act = DoNop,
                                State = (uint)St.Start
                            },
                        }
                    }, 
                    new Node {
                        State = (uint)InAppPurchaseState.NotPurchased,
                        Drop = new [] {
                            (uint)InAppPurchaseEvent.E.PrdDataReqFailedEvt,
                            (uint)InAppPurchaseEvent.E.PrdDataRecvdEvt,
                            (uint)InAppPurchaseEvent.E.PurchasedEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseExpiredEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseFailedEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseRestoredEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseDeferredEvt,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = DoSendProductDataRequest,
                                State = (uint)InAppPurchaseState.PrdDataWait
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.PrdDataReqSendEvt,
                                Act = DoSendProductDataRequest,
                                State = (uint)InAppPurchaseState.PrdDataWait
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.DoPurchaseEvt,
                                Act = DoSendPurchaseRequest,
                                State = (uint)InAppPurchaseState.PurchaseWait
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.StopEvt,
                                Act = DoNop,
                                State = (uint)St.Start
                            },
                        }
                    }, 
                    new Node {
                        State = (uint)InAppPurchaseState.PurchaseWait,
                        Drop = new [] {
                            (uint)InAppPurchaseEvent.E.PrdDataReqFailedEvt,
                            (uint)InAppPurchaseEvent.E.PrdDataRecvdEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseExpiredEvt,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = DoNop,
                                State = (uint)InAppPurchaseState.PurchaseWait
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.DoPurchaseEvt,
                                Act = DoRestorePurchase,
                                State = (uint)InAppPurchaseState.PurchaseWait
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.PrdDataReqSendEvt,
                                Act = DoSendPurchaseRequest,
                                State = (uint)InAppPurchaseState.PrdDataWait
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.PurchasedEvt,
                                Act = DoRecordPurchaseSuccess,
                                State = (uint)InAppPurchaseState.Purchased
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.PurchaseFailedEvt,
                                Act = DoRecordPurchaseFail,
                                State = (uint)InAppPurchaseState.NotPurchased
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.PurchaseRestoredEvt,
                                Act = DoRecordPurchaseRestore,
                                State = (uint)InAppPurchaseState.Purchased
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.PurchaseDeferredEvt,
                                Act = DoNop,
                                State = (uint)InAppPurchaseState.PurchaseWait
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.StopEvt,
                                Act = DoNop,
                                State = (uint)St.Start
                            },
                        }
                    }, 
                    new Node {
                        State = (uint)InAppPurchaseState.Purchased,
                        Drop = new [] {
                            (uint)InAppPurchaseEvent.E.PrdDataReqSendEvt,
                            (uint)InAppPurchaseEvent.E.PrdDataReqFailedEvt,
                            (uint)InAppPurchaseEvent.E.PrdDataRecvdEvt,
                            (uint)InAppPurchaseEvent.E.DoPurchaseEvt,
                            (uint)InAppPurchaseEvent.E.PurchasedEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseFailedEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseRestoredEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseDeferredEvt,

                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = DoNop,
                                State = (uint)InAppPurchaseState.Purchased
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.PurchaseExpiredEvt,
                                Act = DoExpirePurchase,
                                State = (uint)InAppPurchaseState.Expired
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.StopEvt,
                                Act = DoNop,
                                State = (uint)St.Start
                            },
                        }
                    },
                    new Node {
                        State = (uint)InAppPurchaseState.Expired,
                        Drop = new [] {
                            (uint)InAppPurchaseEvent.E.PrdDataReqFailedEvt,
                            (uint)InAppPurchaseEvent.E.PrdDataRecvdEvt,
                            (uint)InAppPurchaseEvent.E.PurchasedEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseFailedEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseRestoredEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseDeferredEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseExpiredEvt,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = DoNop,
                                State = (uint)InAppPurchaseState.Expired
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.PrdDataReqSendEvt,
                                Act = DoSendProductDataRequest,
                                State = (uint)InAppPurchaseState.PrdDataWait
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.DoPurchaseEvt,
                                Act = DoSendPurchaseRequest,
                                State = (uint)InAppPurchaseState.Expired
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.StopEvt,
                                Act = DoNop,
                                State = (uint)St.Start
                            },
                        }
                    },
                }
            };
            Sm.Validate ();
            LicenseInformation = McLicenseInformation.Get ();
            NcAssert.NotNull (LicenseInformation, "Null LicenseInformation object.");
            Sm.State = LicenseInformation.InAppPurchaseState;
        }

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

        private void PostEvent (SmEvt.E evt, string mnemonic)
        {
            Sm.PostEvent ((uint)evt, mnemonic);
        }

        public void PostEvent (InAppPurchaseEvent.E evt, string mnemoic)
        {
            Sm.PostEvent ((uint)evt, mnemoic);
        }
            
        // ACTION FUNCTIONS FOR STATE MACHINE
        private void DoNop ()
        {
        }

        // send Product Data Request
        private void DoSendProductDataRequest ()
        {
            // only if we can make payments, request the prices
            if (InAppPurchaseManager.CanMakePayments ()) {
                // now go get prices,
                Log.Info (Log.LOG_SYS, "InAppPurchase: Requesting product data from store");

                NSSet productIdentifiers = new NSSet (McLicenseInformation.NachoClientLicenseProductId);

                //set up product request for in-app purchase
                SKProductsRequest ProductsRequest = new SKProductsRequest (productIdentifiers);
                ProductsRequest.Delegate = this; // SKProductsRequestDelegate.ReceivedResponse
                ProductsRequest.Start ();
            } else {
                // can't make payments (purchases turned off in Settings?)
                Log.Info (Log.LOG_SYS, "InAppPurchase: Cannot make payments");
                PostEvent (InAppPurchaseEvent.E.PrdDataReqFailedEvt, "CANT_MAKE_PAYMENT");
            }        
        }

        // received response to RequestProductData - with price,title,description info
        public override void ReceivedResponse (SKProductsRequest request, SKProductsResponse response)
        {
            SKProduct[] products = response.Products;
            if (ValidProducts == null) {
                ValidProducts = new NSMutableDictionary ();
            } else {
                ValidProducts.Clear ();
            }
            for (int i = 0; i < products.Length; i++)
                ValidProducts.Add ((NSString)products [i].ProductIdentifier, products [i]);

            if (ValidProducts.Count == 0) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: No valid products returned from store.");
                PostEvent (InAppPurchaseEvent.E.PrdDataReqFailedEvt, "NO_VALID_PRODUCTS");
            } else {
                SKProduct product = (SKProduct)ValidProducts [McLicenseInformation.NachoClientLicenseProductId];
                NcAssert.NotNull (product, "Product not loaded " + McLicenseInformation.NachoClientLicenseProductId);
                PrintProductDetails (product);
                PostEvent (InAppPurchaseEvent.E.PrdDataRecvdEvt, "FOUND_VALID_PRODUCT");
                foreach (string invalidProductId in response.InvalidProducts) {
                    Log.Info (Log.LOG_SYS, "InAppPurchase: Invalid product id: {0}", invalidProductId);
                }
            }
        }

        /// Request failed : Probably could not connect to the App Store (network unavailable?) - called for both RequestProductData and Purchase
        public override void RequestFailed (SKRequest request, NSError error)
        {
            Log.Error (Log.LOG_SYS, "InAppPurchase: Purchase Request failed {0}", error.LocalizedDescription);
            PostEvent (InAppPurchaseEvent.E.PrdDataReqFailedEvt, "REQUEST_FAILED");
        }

        private void DoReceiveProductData ()
        {
            // already purchased
            if (LicenseInformation.AlreadyPurchased) {
                Sm.State = (uint)InAppPurchaseState.Purchased;
            } else if (LicenseInformation.InAppPurchaseState == (uint)InAppPurchaseState.PurchaseWait) {
                // restore purchase after restart in purchase wait state - TODO : confirm this scenario
                Sm.State = (uint)InAppPurchaseState.PurchaseWait;
                PostEvent (InAppPurchaseEvent.E.DoPurchaseEvt, "RESTORE_PURCHASE");
            } else {
                Sm.State = (uint)InAppPurchaseState.NotPurchased;
            }
        }

        private void DoLogProductDataRequestFailed ()
        {
        }

        private void DoSendPurchaseRequest ()
        {
            SKProduct product = (SKProduct)ValidProducts [McLicenseInformation.NachoClientLicenseProductId];
            SKMutablePayment payment = SKMutablePayment.PaymentWithProduct (product);
            payment.ApplicationUsername = CloudHandler.Instance.GetUserId ();
            SKPaymentQueue.DefaultQueue.AddPayment (payment);
        }

        private void DoRestorePurchase ()
        {
        }

        private void DoRecordPurchaseSuccess ()
        {
        }

        private void DoRecordPurchaseFail ()
        {
        }

        private void DoRecordPurchaseRestore ()
        {
        }

        private void DoExpirePurchase ()
        {
        }

        // from IPlatformStoreHandler
        // call this when starting up
        public void Start ()
        {
            InAppPurchaseManager = new InAppPurchaseManager ();
            PaymentObserver = new PaymentObserver (InAppPurchaseManager);
            SetupObservers ();
            PostEvent (SmEvt.E.Launch, "INAPPSTART");
        }

        // from IPlatformStoreHandler
        // buy a new license
        public bool BuyLicense ()
        {
            if (LicenseInformation.AlreadyPurchased) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: NachoClient license already purchased. No need to buy again.");
                return true;
            } else if (LicenseInformation.InAppPurchaseState == (uint)InAppPurchaseState.PurchaseWait) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: NachoClient license is already in the process of being purchased.");
                return true;
            } else {
                Log.Info (Log.LOG_SYS, "Purchasing NachoClient license from store...");
                DoSendPurchaseRequest ();
                return true;
            }
        }

        // from IPlatformStoreHandler
        // restore previously bought license
        public bool RestoreLicense ()
        {
            if (NachoClientLicensePurchased) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: NachoClient license already purchased. No need to restore.");
                return true;
            }
            if (!ProductDataLoaded) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: Product Information not yet received from store. Trying to load again.");
                PostEvent (InAppPurchaseEvent.E.PrdDataReqSendEvt, "CANT_RESTORE_YET");
                return false;
            } else if (Purchasing) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: NachoClient license is already in the process of being purchased.");
                return true;
            } else {
                Log.Info (Log.LOG_SYS, "Restoring NachoClient license from store...");
                // No point trying to restore non renewable subscription from Apple Store. We need to handle this ourselves.
                // TODO: the following is for testing only. This will result in duplicate purchase.
                DoSendPurchaseRequest();
                return true;
            }
        }

        // from IPlatformStoreHandler
        public void SetPurchasingStatus (bool status)
        {
            Purchasing = status;
        }

        public void SetupObservers ()
        {
            // Call this once upon startup of in-app-purchase activities
            // This also kicks off the TransactionObserver which handles the various communications
            SKPaymentQueue.DefaultQueue.AddTransactionObserver (PaymentObserver);


            // setup an observer to wait for prices to come back from StoreKit <- AppStore
            PriceObserver = NSNotificationCenter.DefaultCenter.AddObserver (InAppPurchaseManager.InAppPurchaseManagerProductsFetchedNotification,
                (notification) => {
                  
                });

            // setup an observer to wait for purchase successful notification
            PurchaseSucceedObserver = NSNotificationCenter.DefaultCenter.AddObserver (InAppPurchaseManager.InAppPurchaseManagerTransactionSucceededNotification,
                (notification) => {
                    // update the status after a successful purchase
                    Log.Info (Log.LOG_SYS, "InAppPurchase: Purchase transaction successful");         
                    if (GetPurchasedStatus () == true) {
                        Log.Info (Log.LOG_SYS, "InAppPurchase: Purchased {0}", McLicenseInformation.NachoClientLicenseProductId);         
                        NachoClientLicensePurchased = true;
                        Purchasing = false;
                    }
                });

            //set up an observer to wait for transaction failed notifications
            PurchaseFailedObserver = NSNotificationCenter.DefaultCenter.AddObserver (InAppPurchaseManager.InAppPurchaseManagerTransactionFailedNotification,
                (notification) => {
                    Log.Info (Log.LOG_SYS, "InAppPurchase: Purchase transaction failed"); 
                    Purchasing = false;
                });

            //set up an observer to wait for request failed notifications
            RequestObserver = NSNotificationCenter.DefaultCenter.AddObserver (InAppPurchaseManager.InAppPurchaseManagerRequestFailedNotification,
                (notification) => {
                    Log.Info (Log.LOG_SYS, "InAppPurchase: Request failed");
                    Purchasing = false;
                });
        }

        // from IPlatformStoreHandler
        // get the purchase status
        public bool GetPurchasedStatus ()
        {
            return CloudHandler.Instance.GetPurchasedStatus (McLicenseInformation.NachoClientLicenseProductId);
        }


        // from IPlatformStoreHandler
        public void RegisterPurchase (string productId, DateTime purchaseDate)
        {
            // Register the purchase, so it is remembered for next time 
            if (productId == McLicenseInformation.NachoClientLicenseProductId) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: Registering NachoClient license purchase status.");
                CloudHandler.Instance.SetPurchasedStatus (McLicenseInformation.NachoClientLicenseProductId, purchaseDate);
            } else {
                Log.Warn (Log.LOG_SYS, "InAppPurchase: Cannot register product purchased status for product {0}", productId);
            }
        }

        private void PrintProductDetails (SKProduct product)
        {            
            Log.Info (Log.LOG_SYS, "InAppPurchase: Found product id: {0}", product.ProductIdentifier);
            Log.Info (Log.LOG_SYS, "InAppPurchase: Product title: {0}", product.LocalizedTitle);
            Log.Info (Log.LOG_SYS, "InAppPurchase: Product description: {0}", product.LocalizedDescription);
            Log.Info (Log.LOG_SYS, "InAppPurchase: Product l10n price: {0}", product.LocalizedPrice ());
        }



        // from IPlatformStoreHandler
        // call this when shutting down
        public void Stop ()
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver (PriceObserver);
            NSNotificationCenter.DefaultCenter.RemoveObserver (PurchaseSucceedObserver);
            NSNotificationCenter.DefaultCenter.RemoveObserver (PurchaseFailedObserver);
            NSNotificationCenter.DefaultCenter.RemoveObserver (RequestObserver);
            PostEvent (InAppPurchaseEvent.E.StopEvt, "INAPPSTOP");
        }
    }
}

