using System.Collections.Generic;
using System.Linq;
using StoreKit;
using Foundation;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoPlatform
{

    public class InAppPurchaseManager : NSObject
    {
        private NSMutableDictionary ValidProducts;

        public static readonly NSString InAppPurchaseManagerProductsFetchedNotification = new NSString ("InAppPurchaseManagerProductsFetchedNotification");
        public static readonly NSString InAppPurchaseManagerTransactionFailedNotification = new NSString ("InAppPurchaseManagerTransactionFailedNotification");
        public static readonly NSString InAppPurchaseManagerTransactionSucceededNotification = new NSString ("InAppPurchaseManagerTransactionSucceededNotification");
        public static readonly NSString InAppPurchaseManagerRequestFailedNotification = new NSString ("InAppPurchaseManagerRequestFailedNotification");

        public InAppPurchaseManager ()
        {
        }
            
        // Verify that the iTunes account can make this purchase for this application
        public bool CanMakePayments ()
        {
            return SKPaymentQueue.CanMakePayments;
        }

        public void FailedTransaction (SKPaymentTransaction transaction)
        {
            //SKErrorPaymentCancelled == 2
            if (transaction.Error != null) {
                string errorDescription = transaction.Error.Code == 2 ? "User Cancelled FailedTransaction" : "FailedTransaction";
                Log.Error (Log.LOG_SYS, "InAppPurchase: {0} Code={1} {2}", errorDescription, transaction.Error.Code, transaction.Error.LocalizedDescription);
            }
            FinishTransaction (transaction, false);
        }

        public void CompleteTransaction (SKPaymentTransaction transaction)
        {
            Log.Info (Log.LOG_SYS, "InAppPurchase: CompleteTransaction {0}", transaction.TransactionIdentifier);
            string productId = transaction.Payment.ProductIdentifier;

            // Register the purchase, so it is remembered for next time
            StoreHandler.Instance.RegisterPurchase (productId, transaction.TransactionDate.ToDateTime ());
            FinishTransaction (transaction, true);
        }

        public void FinishTransaction (SKPaymentTransaction transaction, bool wasSuccessful)
        {
            Log.Info (Log.LOG_SYS, "InAppPurchase: FinishTransaction {0}", wasSuccessful);
            // remove the transaction from the payment queue.
            SKPaymentQueue.DefaultQueue.FinishTransaction (transaction);		// THIS IS IMPORTANT - LET'S APPLE KNOW WE'RE DONE !!!!

            NSDictionary userInfo = new NSDictionary ("transaction", transaction);
            var notificationKey = wasSuccessful ? InAppPurchaseManagerTransactionSucceededNotification : InAppPurchaseManagerTransactionFailedNotification;
            NSNotificationCenter.DefaultCenter.PostNotificationName (notificationKey, this, userInfo);
        }

        /// <summary>
        /// Restore any transactions that occurred for this Apple ID, either on
        /// this device or any other logged in with that account.
        /// </summary>
        public void Restore ()
        {
            // the observer will be notified of when the restored transactions start arriving <- AppStore
            SKPaymentQueue.DefaultQueue.RestoreCompletedTransactions (CloudHandler.Instance.GetUserId ());
        }

        public virtual void RestoreTransaction (SKPaymentTransaction transaction)
        {
            // Restored Transactions always have an 'original transaction' attached
            SKPaymentTransaction orgTransaction = transaction.OriginalTransaction;
            Log.Info (Log.LOG_SYS, "InAppPurchase: RestoreTransaction {0}; OriginalTransaction {1} {2}", transaction.TransactionIdentifier, orgTransaction.TransactionIdentifier, orgTransaction.TransactionDate);
            string productId = transaction.OriginalTransaction.Payment.ProductIdentifier;
            // Register the purchase, so it is remembered for next time
            StoreHandler.Instance.RegisterPurchase (productId, transaction.TransactionDate.ToDateTime ());
            FinishTransaction (transaction, true);
        }
    }

    public class PaymentObserver : SKPaymentTransactionObserver
    {
        private InAppPurchaseManager InAppPurchaseManager;

        public PaymentObserver (InAppPurchaseManager inAppPurchaseManager)
        {
            InAppPurchaseManager = inAppPurchaseManager;
        }

        // from SKPaymentTransactionObserver
        // called when the transaction status is updated
        public override void UpdatedTransactions (SKPaymentQueue queue, SKPaymentTransaction[] transactions)
        {
            Log.Info (Log.LOG_SYS, "InAppPurchase: Updated Transactions. Handle state change.");
            foreach (SKPaymentTransaction transaction in transactions) {
                switch (transaction.TransactionState) {
                case SKPaymentTransactionState.Purchasing:
                    Log.Info (Log.LOG_SYS, "InAppPurchase: Purchasing...");
                    // do nothing
                    break;
                case SKPaymentTransactionState.Purchased:
                    Log.Info (Log.LOG_SYS, "InAppPurchase: Purchased.");
                    InAppPurchaseManager.CompleteTransaction (transaction);
                    break;
                case SKPaymentTransactionState.Failed:
                    Log.Error (Log.LOG_SYS, "InAppPurchase: Failed.");
                    InAppPurchaseManager.FailedTransaction (transaction);
                    break;
                case SKPaymentTransactionState.Restored:
                    Log.Info (Log.LOG_SYS, "InAppPurchase: Restored.");
                    InAppPurchaseManager.RestoreTransaction (transaction);
                    break;
                case SKPaymentTransactionState.Deferred:
                    Log.Info (Log.LOG_SYS, "InAppPurchase: Purchase Deferred. Waiting for final status.");
                    // do nothing
                    break;
                default:
                    break;
                }
            }
        }

        // from SKPaymentTransactionObserver
        // Restore succeeded - you can update the UI
        public override void PaymentQueueRestoreCompletedTransactionsFinished (SKPaymentQueue queue)
        {
            Log.Info (Log.LOG_SYS, "InAppPurchase: Restore Completed - PaymentQueueRestoreCompletedTransactionsFinished ");
            StoreHandler.Instance.SetPurchasingStatus (false);
            //TODO: Hookup UI to display restore successful. Optional
        }

        // from SKPaymentTransactionObserver
        // Restore failed somewhere - you can update the UI
        public override void RestoreCompletedTransactionsFailedWithError (SKPaymentQueue queue, NSError error)
        {
            Log.Error (Log.LOG_SYS, "InAppPurchase: Restore Failed -  RestoreCompletedTransactionsFailedWithError " + error.LocalizedDescription);
            StoreHandler.Instance.SetPurchasingStatus (false);
            //TODO: Hookup UI to display restore failed. Optional
        }


        // from SKPaymentTransactionObserver
        // Removed Transactions - you can update the UI
        public override void RemovedTransactions (SKPaymentQueue queue, SKPaymentTransaction[] transactions)
        {
            // Do nothing, unless you want to update the UI
            Log.Info (Log.LOG_SYS, "InAppPurchase: Removed transactions from queue");
            StoreHandler.Instance.SetPurchasingStatus (false);
            //TODO: Hookup UI to display transaction complete. Optional
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