//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using StoreKit;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore;

namespace NachoPlatform
{
    public class StoreHandler : SKProductsRequestDelegate, IPlatformStoreHandler, IPaymentObserverOwner
    {
        private static volatile StoreHandler instance;
        private static object syncRoot = new Object ();

        public NcStateMachine Sm { set; get; }

        private PaymentObserver PaymentObserver;
        private McLicenseInformation LicenseInformation;
        private NSMutableDictionary ValidProducts;

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
                            (uint)InAppPurchaseEvent.E.StopEvt,
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
                            (uint)InAppPurchaseEvent.E.PurchasedEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseExpiredEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseFailedEvt,
                            (uint)InAppPurchaseEvent.E.PurchaseRestoredEvt,
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
                            (uint)InAppPurchaseEvent.E.DoPurchaseEvt,
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
                                Act = DoSendProductDataRequest,
                                State = (uint)InAppPurchaseState.PurchaseWait
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
                                Event = (uint)InAppPurchaseEvent.E.PurchasedEvt,
                                Act = DoRecordPurchaseSuccess,
                                State = (uint)InAppPurchaseState.Purchased
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.PurchaseFailedEvt,
                                Act = DoRecordPurchaseFailed,
                                State = (uint)InAppPurchaseState.NotPurchased
                            },
                            new Trans {
                                Event = (uint)InAppPurchaseEvent.E.PurchaseRestoredEvt,
                                Act = DoRecordPurchaseRestore,
                                State = (uint)InAppPurchaseState.Purchased
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
                                Act = DoSendProductDataRequest,
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
                                State = (uint)InAppPurchaseState.PurchaseWait
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
            PaymentObserver = new PaymentObserver (this);
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
            if (SKPaymentQueue.CanMakePayments) {
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

        /// Request failed : Probably could not connect to the App Store (network unavailable?) - called for both RequestProductData
        public override void RequestFailed (SKRequest request, NSError error)
        {
            Log.Error (Log.LOG_SYS, "InAppPurchase: Product Data Request failed {0}", error.LocalizedDescription);
            PostEvent (InAppPurchaseEvent.E.PrdDataReqFailedEvt, "REQUEST_FAILED");
        }

        private void DoReceiveProductData ()
        {
            // already purchased
            if (IsAlreadyPurchased ()) {
                Sm.State = (uint)InAppPurchaseState.Purchased;
            } else if (LicenseInformation.InAppPurchaseState == (uint)InAppPurchaseState.PurchaseWait) {
                Sm.State = (uint)InAppPurchaseState.PurchaseWait;
            } else {
                Sm.State = (uint)InAppPurchaseState.NotPurchased;
            }
        }

        private void DoSendPurchaseRequest ()
        {
            SKProduct product = (SKProduct)ValidProducts [McLicenseInformation.NachoClientLicenseProductId];
            SKMutablePayment payment = SKMutablePayment.PaymentWithProduct (product);
            payment.ApplicationUsername = CloudHandler.Instance.GetUserId ();
            SKPaymentQueue.DefaultQueue.AddPayment (payment);
        }

        private void DoRecordPurchaseSuccess ()
        {
            Log.Info (Log.LOG_SYS, "InAppPurchase: Recording NachoClient license purchase at {0}", PaymentObserver.PurchaseDate.ToAsUtcString ());
            //TODO: confirm UTC date
            CloudHandler.Instance.RecordPurchase (PaymentObserver.PurchaseDate);
            LicenseInformation.PurchaseDate = PaymentObserver.PurchaseDate;
            LicenseInformation.IsAlreadyPurchased = true;
            LicenseInformation.Update ();
        }

        private void DoRecordPurchaseFailed ()
        {
            // TODO: update LicenseInformation
            LicenseInformation.IsAlreadyPurchased = false;
            LicenseInformation.Update ();
        }

        private void DoRecordPurchaseRestore ()
        {
            NcAssert.True (false, "InAppPurchase: Restore Purchase not supported yet");
        }

        private void DoExpirePurchase ()
        {           
            // TODO: handle expiry
            NcAssert.True (false, "InAppPurchase: No expiry configured yet");
        }

        // from IPlatformStoreHandler
        // call this when starting up
        public void Start ()
        {
            PostEvent (SmEvt.E.Launch, "INAPPSTART");
            // start listening to changes in NcApplication
            NcApplication.Instance.StatusIndEvent += TokensWatcher;
        }

        private void TokensWatcher (object sender, EventArgs ea)
        {
            StatusIndEventArgs siea = (StatusIndEventArgs)ea;
            switch (siea.Status.SubKind) {
            case NcResult.SubKindEnum.Info_UserIdChanged:
                if (null == siea.Status.Value) {
                    // do nothing now
                } else {
                    string userId = (string)siea.Status.Value;
                    if (userId != LicenseInformation.UserId) {
                        Log.Info (Log.LOG_SYS, "StoreHandler: Replacing existing LicenseInformation UserId {0} with {1}", LicenseInformation.UserId, userId);
                        LicenseInformation.UserId = userId;
                        LicenseInformation.Update ();
                    }
                }
                break;
            }
        }

        // from IPlatformStoreHandler
        // Purchase a new license
        public bool PurchaseLicense ()
        {
            if (IsAlreadyPurchased ()) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: NachoClient license already purchased. No need to buy again.");
                return true;
            } else if (LicenseInformation.InAppPurchaseState == (uint)InAppPurchaseState.PurchaseWait) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: PurchaseLicense: NachoClient license is already in the process of being purchased.");
                return true;
            } else {
                Log.Info (Log.LOG_SYS, "Purchasing NachoClient license from store...");
                PostEvent (InAppPurchaseEvent.E.DoPurchaseEvt, "SEND_PURCHASE_REQUEST");
                return true;
            }
        }

        // from IPlatformStoreHandler
        // restore previously bought license
        public bool RestoreLicense ()
        {
            if (IsAlreadyPurchased ()) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: NachoClient license already purchased. No need to restore.");
                return true;
            } else if (LicenseInformation.InAppPurchaseState == (uint)InAppPurchaseState.PurchaseWait) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: RestoreLicense: NachoClient license is already in the process of being purchased.");
                return true;            
            } else {
                Log.Info (Log.LOG_SYS, "Restoring NachoClient license from store...");
                // TODO : No point trying to restore non renewable subscription from Apple Store. We need to handle this ourselves.
                //SKPaymentQueue.DefaultQueue.RestoreCompletedTransactions (CloudHandler.Instance.GetUserId ());
                // TODO: the following is for testing only. This will result in duplicate purchase.
                NcAssert.True (false, "Restore Purchase not supported yet");
                return false;
            }
        }
            
        // from IPlatformStoreHandler
        // get the purchase status
        public bool IsAlreadyPurchased ()
        {
            if (CloudHandler.Instance.IsAlreadyPurchased ()) {
                SyncCloudToLicenseInformation ();
                return true;
            } else {
                return false;
            }
        }

        // from IPlatformStoreHandler
        // can we purchase now?
        public bool CanPurchase ()
        {
            if (ValidProducts.Count == 0) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: No valid products returned from store. Cannot purchase anything yet.");
                return false;
            } else if (ValidProducts [McLicenseInformation.NachoClientLicenseProductId] == null) {
                Log.Info (Log.LOG_SYS, "InAppPurchase: License product {0} not yet available in store. Cannot purchase it yet.", McLicenseInformation.NachoClientLicenseProductId);
                return false;
            } else {
                return true;
            }
        }

        private void SyncCloudToLicenseInformation ()
        {
            if (LicenseInformation.IsAlreadyPurchased != (CloudHandler.Instance.IsAlreadyPurchased ())) {
                LicenseInformation.IsAlreadyPurchased = CloudHandler.Instance.IsAlreadyPurchased ();
                LicenseInformation.PurchaseDate = CloudHandler.Instance.GetPurchaseDate ();
                LicenseInformation.FirstInstallDate = CloudHandler.Instance.GetFirstInstallDate ();

                LicenseInformation.Update ();
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
            PostEvent (InAppPurchaseEvent.E.StopEvt, "INAPPSTOP");
        }
    }

    public interface IPaymentObserverOwner
    {
        void PostEvent (StoreHandler.InAppPurchaseEvent.E evt, string mnemonic);
    }

    public class PaymentObserver : SKPaymentTransactionObserver
    {
        private IPaymentObserverOwner Owner;
        public DateTime PurchaseDate;

        public PaymentObserver (IPaymentObserverOwner owner)
        {
            Owner = owner;
            SKPaymentQueue.DefaultQueue.AddTransactionObserver (this);
        }


        // from SKPaymentTransactionObserver
        // called when the transaction status is updated
        public override void UpdatedTransactions (SKPaymentQueue queue, SKPaymentTransaction[] transactions)
        {
            foreach (SKPaymentTransaction transaction in transactions) {
                switch (transaction.TransactionState) {
                case SKPaymentTransactionState.Purchasing:
                    Log.Info (Log.LOG_SYS, "InAppPurchase: Purchasing...");
                    // do nothing
                    break;
                case SKPaymentTransactionState.Purchased:
                    Log.Info (Log.LOG_SYS, "InAppPurchase: Purchased.");
                    CompletePurchaseTransaction (transaction);
                    break;
                case SKPaymentTransactionState.Failed:
                    Log.Error (Log.LOG_SYS, "InAppPurchase: Purchase Failed.");
                    FailedTransaction (transaction);
                    break;
                case SKPaymentTransactionState.Restored:
                    Log.Info (Log.LOG_SYS, "InAppPurchase: Purchase Restored.");
                    RestoreTransaction (transaction);
                    break;
                case SKPaymentTransactionState.Deferred:
                    Log.Info (Log.LOG_SYS, "InAppPurchase: Purchase Deferred. Waiting for final status.");
                    // do nothing
                    break;
                default:
                    Log.Info (Log.LOG_SYS, "InAppPurchase: Updated Transactions called with unknown state {0}", transaction.TransactionState);
                    break;
                }
            }
        }

        // from SKPaymentTransactionObserver
        // Restore succeeded - you can update the UI : Optional
        public override void PaymentQueueRestoreCompletedTransactionsFinished (SKPaymentQueue queue)
        {
            Log.Info (Log.LOG_SYS, "InAppPurchase: Restore Completed - PaymentQueueRestoreCompletedTransactionsFinished ");
            // Do nothing for now.
        }

        // from SKPaymentTransactionObserver
        // Restore failed somewhere - you can update the UI : Optional
        public override void RestoreCompletedTransactionsFailedWithError (SKPaymentQueue queue, NSError error)
        {
            Log.Error (Log.LOG_SYS, "InAppPurchase: Restore Failed -  RestoreCompletedTransactionsFailedWithError " + error.LocalizedDescription);
            // Do nothing for now
        }

        // from SKPaymentTransactionObserver
        // Removed Transactions - you can update the UI : Optional
        public override void RemovedTransactions (SKPaymentQueue queue, SKPaymentTransaction[] transactions)
        {
            Log.Info (Log.LOG_SYS, "InAppPurchase: Transactions complete - RemovedTransactions");
            // Do nothing for now
        }

        public void CompletePurchaseTransaction (SKPaymentTransaction transaction)
        {
            string productId = transaction.Payment.ProductIdentifier;
            Log.Info (Log.LOG_SYS, "InAppPurchase: Completing Purchase Transaction {0} for {1} ", transaction.TransactionIdentifier, productId);
            SKPaymentQueue.DefaultQueue.FinishTransaction (transaction); 
            NcAssert.True ((productId == McLicenseInformation.NachoClientLicenseProductId), "Product bought " + productId + " does not match requested product " + McLicenseInformation.NachoClientLicenseProductId);
            PurchaseDate = transaction.TransactionDate.ToDateTime ();
            Owner.PostEvent (StoreHandler.InAppPurchaseEvent.E.PurchasedEvt, "PURCHASE_SUCCESSFUL");
        }

        public void FailedTransaction (SKPaymentTransaction transaction)
        {
            //SKErrorPaymentCancelled == 2
            if (transaction.Error != null) {
                string errorDescription = transaction.Error.Code == 2 ? "User Cancelled" : "FailedTransaction";
                Log.Error (Log.LOG_SYS, "InAppPurchase: Failed Purchase {0} Code={1} {2}", errorDescription, transaction.Error.Code, transaction.Error.LocalizedDescription);
            }
            SKPaymentQueue.DefaultQueue.FinishTransaction (transaction); 
            Owner.PostEvent (StoreHandler.InAppPurchaseEvent.E.PurchaseFailedEvt, "PURCHASE_FAILED");
        }

        public virtual void RestoreTransaction (SKPaymentTransaction transaction)
        {
            // This is not possible for non-renewable subscriptions. Do nothing
        }
    }

    public static class SKProductExtension
    {
        public static string LocalizedPrice (this SKProduct product)
        {
            var formatter = new NSNumberFormatter {
                FormatterBehavior = NSNumberFormatterBehavior.Version_10_4,
                NumberStyle = NSNumberFormatterStyle.Currency,
                Locale = product.PriceLocale,
            };

            string formattedString = formatter.StringFromNumber (product.Price);
            return formattedString;
        }
    }
}

