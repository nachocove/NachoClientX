//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
using System;
using Foundation;
using NachoCore.Utils;
using NachoCore;
using System.Runtime.InteropServices;

namespace NachoPlatform
{
    public class CloudHandler : IPlatformCloudHandler
    {
        private static volatile CloudHandler instance;
        private static object syncRoot = new Object ();
        private NSUbiquitousKeyValueStore Store;

        private bool HasiCloud = false;
        private NSUrl iCloudUrl;


        private NSObject CloudKeyObserver;

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
            // Checks to see if the user of this device has iCloud enabled
            var uburl = NSFileManager.DefaultManager.GetUrlForUbiquityContainer(null);                
            // Connected to iCloud?
            if (uburl == null)
            {
                // No
                HasiCloud = false;
                iCloudUrl =null;
                Store = null;
                Log.Info (Log.LOG_SYS, "CloudHandler: Unable to connect to iCloud");
            }
            else
            {    
                // Yes, 
                HasiCloud = true;
                iCloudUrl = uburl;
                Store = NSUbiquitousKeyValueStore.DefaultStore;
                Log.Info (Log.LOG_SYS, "CloudHandler: Connected to iCloud. Url is {0}", iCloudUrl);
            }
            SetAppInstallDate (DateTime.UtcNow);
            Log.Info(Log.LOG_SYS, "App install date is {0}", GetAppInstallDate());
        }

        public string GetUserId ()
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Getting UserId");
            if (HasiCloud) {
                if (Store.GetString ("UserId") != null) {
                    return Store.GetString ("UserId");
                }
            }
            // if no icloud or not found in iCloud
            return NSUserDefaults.StandardUserDefaults.StringForKey ("UserId");
        }

        public void SetUserId (string UserId)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Setting UserId {0}", UserId);
            if (HasiCloud) {
                Store.SetString ("UserId", UserId);  
                Store.Synchronize ();
            }
            NSUserDefaults.StandardUserDefaults.SetString (UserId, "UserId");
            NSUserDefaults.StandardUserDefaults.Synchronize ();
        }

        public bool GetPurchasedStatus (string productId)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Getting purchase status for product {0}", productId);
            if (HasiCloud) {
                if (Store.GetBool (productId) != false) {
                    Log.Info (Log.LOG_SYS, "CloudHandler: Purchase status true for product {0} in cloud", productId);
                    return Store.GetBool (productId);
                }
            }
            // if no icloud or not found in iCloud
            Log.Info (Log.LOG_SYS, "CloudHandler: Purchase status false for product {0} in cloud. User defaults value is {1}.", productId, NSUserDefaults.StandardUserDefaults.BoolForKey (productId));
            return NSUserDefaults.StandardUserDefaults.BoolForKey (productId);
        }

        public DateTime GetPurchasedDate (string productId)
        {
            string purchaseDate;

            Log.Info (Log.LOG_SYS, "CloudHandler: Getting purchase date for product {0}", productId);
            if (HasiCloud) {
                if (Store.GetString ("PurchaseDate") != null) {
                    purchaseDate = Store.GetString ("PurchaseDate");
                    return purchaseDate.ToDateTime ();
                }
            }
            // if no icloud or not found in iCloud
            purchaseDate = NSUserDefaults.StandardUserDefaults.StringForKey ("PurchaseDate");
            Log.Info (Log.LOG_SYS, "CloudHandler: Purchase date not found for product {0} in cloud. User defaults value is {1}.", productId, purchaseDate);
            return purchaseDate.ToDateTime ();
        }

        public void SetPurchasedStatus (string productId, DateTime purchaseDate)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Setting purchase status(true) for product {0} on {1}", productId, purchaseDate);
            if (HasiCloud) {
                Store.SetBool (productId, true);  
                Store.SetString ("PurchaseDate", purchaseDate.ToAsUtcString ());
                Store.Synchronize ();
            }
            NSUserDefaults.StandardUserDefaults.SetBool (true, productId);
            NSUserDefaults.StandardUserDefaults.SetString (purchaseDate.ToAsUtcString (), "PurchaseDate");
            NSUserDefaults.StandardUserDefaults.Synchronize ();        
        }

        public void SetAppInstallDate (DateTime installDate)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Setting App Install Date {0}", installDate);
            if (HasiCloud) {
                Store.SetString ("InstallDate", installDate.ToAsUtcString ());
                Store.Synchronize ();
            }
            NSUserDefaults.StandardUserDefaults.SetString (installDate.ToAsUtcString (), "InstallDate");
            NSUserDefaults.StandardUserDefaults.Synchronize ();   
        }

        public DateTime GetAppInstallDate ()
        {
            string installDate;

            Log.Info (Log.LOG_SYS, "CloudHandler: Getting install date for App");
            if (HasiCloud) {
                if (Store.GetString ("InstallDate") != null) {
                    installDate = Store.GetString ("InstallDate");
                    return installDate.ToDateTime ();
                }
            }
            // if no icloud or not found in iCloud
            installDate = NSUserDefaults.StandardUserDefaults.StringForKey ("InstallDate");
            Log.Info (Log.LOG_SYS, "CloudHandler: App install date not found in cloud. User defaults value is {0}.", installDate);
            return installDate.ToDateTime ();        
        }
           
        // MISCELLANEOUS STUFF
        private void TokensWatcher (object sender, EventArgs ea)
        {
            StatusIndEventArgs siea = (StatusIndEventArgs)ea;
            switch (siea.Status.SubKind) {
            case NcResult.SubKindEnum.Info_PushAssistClientToken:
                if (null == siea.Status.Value) {
                    // do nothing now
                    Log.Info (Log.LOG_SYS, "CloudHandler: No userId yet");

                } else {
                    string newUserId = (string)siea.Status.Value;
                    Log.Info (Log.LOG_SYS, "CloudHandler: New userId is {0}", newUserId);
                    string userId = GetUserId ();
                    if (userId == null) {
                        Log.Info (Log.LOG_SYS, "CloudHandler: Saving new userId {0} to store", newUserId);
                        SetUserId (newUserId);
                    } else if (newUserId != userId) {
                        Log.Info (Log.LOG_SYS, "CloudHandler: Old userId {0} exists in store. Replacing NcApplication ClientId {1} with {0}", userId, NcApplication.Instance.ClientId, userId);
                        NcApplication.Instance.ClientId = userId;
                    }
                }
                break;
            }
        }

        public void Start ()
        {
            // start listening to changes in NcApplication
            NcApplication.Instance.StatusIndEvent += TokensWatcher;
            // start listening to changes in cloud keys
            CloudKeyObserver = 
                NSNotificationCenter.DefaultCenter.AddObserver (
                    NSUbiquitousKeyValueStore.DidChangeExternallyNotification
                    , delegate (NSNotification n) {
                        NSDictionary userInfo = n.UserInfo;
                        NSNumber reasonNumber = (NSNumber)userInfo.ObjectForKey(NSUbiquitousKeyValueStore.ChangeReasonKey);
                        int reason = reasonNumber.Int32Value; // reason change was triggered
                        Log.Info (Log.LOG_SYS, "CloudHandler: Notification change reason {0}", reason);

                        NSArray changedKeys = (NSArray)userInfo.ObjectForKey (NSUbiquitousKeyValueStore.ChangedKeysKey);
                        for (uint i = 0; i < changedKeys.Count; i++) {
                            string key = Marshal.PtrToStringAuto(changedKeys.ValueAt(i));
                            if (key == "UserId")
                            {
                                // ICloud override local - TODO: confirm this
                                string userId = NSUbiquitousKeyValueStore.DefaultStore.GetString("UserId");
                                if ((userId != null) && (userId != NcApplication.Instance.ClientId)) {
                                    Log.Info (Log.LOG_SYS, "CloudHandler: Replacing Client {0} with userId {0} from Cloud",NcApplication.Instance.ClientId, userId);
                                    NcApplication.Instance.ClientId = userId;
                                }
                            }
                        }
                    });
        }

        public void Stop ()
        {
            NcApplication.Instance.StatusIndEvent -= TokensWatcher;
            NSNotificationCenter.DefaultCenter.RemoveObserver (CloudKeyObserver);
        }

    }

    public class CIObject
    {
        public string UserId { get; set; }
        public NSData AppStoreReceipt { get; set; }
        public DateTime FirstInstallDate { get; set; }
        public DateTime PayByDate { get; set; }
        public DateTime AskByDate { get; set; }
        public DateTime PurchaseDate { get; set; }
        public bool IsReceiptValidated { get; set; }

        public CIObject ()
        {
        }
    }

}