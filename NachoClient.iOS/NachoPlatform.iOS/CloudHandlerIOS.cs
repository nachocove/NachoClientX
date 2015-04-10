//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using NachoCore.Utils;
using NachoCore;
using System.Runtime.InteropServices;

namespace NachoPlatform
{
    public class CloudHandler : IPlatformCloudHandler
    {
        private const string KUserId = "UserId3";
        private const string KFirstInstallDate = "FirstInstallDate3";
        private const string KPurchaseDate = "PurchaseDate3";
        private const string KIsAlreadyPurchased = "IsAlreadyPurchased3";


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
            var uburl = NSFileManager.DefaultManager.GetUrlForUbiquityContainer (null);                
            // Connected to iCloud?
            if (uburl == null) {
                // No
                HasiCloud = false;
                iCloudUrl = null;
                Store = null;
                Log.Info (Log.LOG_SYS, "CloudHandler: Unable to connect to iCloud");
            } else {    
                // Yes, 
                HasiCloud = true;
                iCloudUrl = uburl;
                Store = NSUbiquitousKeyValueStore.DefaultStore;
                Log.Info (Log.LOG_SYS, "CloudHandler: Connected to iCloud. Url is {0}", iCloudUrl);
            }
        }

        public string GetUserId ()
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Getting UserId");
            if (HasiCloud) {
                string remoteUserId = Store.GetString (KUserId);
                if (remoteUserId != null) {
                    string localUserId = NSUserDefaults.StandardUserDefaults.StringForKey (KUserId);
                    if (localUserId != remoteUserId) {
                        // replace local cached UserId with Cloud UserId
                        NSUserDefaults.StandardUserDefaults.SetString (remoteUserId, KUserId);
                        NSUserDefaults.StandardUserDefaults.Synchronize ();
                    }
                    Log.Info (Log.LOG_SYS, "CloudHandler: Returning cloud UserId {0}", remoteUserId);
                    return remoteUserId;
                }
            }
            // if no icloud or not found in iCloud
            Log.Info (Log.LOG_SYS, "CloudHandler: UserId not stored in cloud. Getting user defaults value : {0}", NSUserDefaults.StandardUserDefaults.StringForKey (KUserId));
            return NSUserDefaults.StandardUserDefaults.StringForKey (KUserId);
        }

        public void SetUserId (string UserId)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Setting UserId {0}", UserId);
            if (HasiCloud) {
                Store.SetString (KUserId, UserId);  
                Store.Synchronize ();
            }
            NSUserDefaults.StandardUserDefaults.SetString (UserId, KUserId);
            NSUserDefaults.StandardUserDefaults.Synchronize ();
        }

        public bool IsAlreadyPurchased ()
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Getting license purchase status");
            if (HasiCloud) {
                bool remotePurchaseStatus = Store.GetBool (KIsAlreadyPurchased);
                if (remotePurchaseStatus != false) {
                    Log.Info (Log.LOG_SYS, "CloudHandler: Purchase status true for license in cloud");
                    if (remotePurchaseStatus == true) {
                        bool localPurchaseStatus = NSUserDefaults.StandardUserDefaults.BoolForKey (KIsAlreadyPurchased);
                        if (localPurchaseStatus != remotePurchaseStatus) {
                            // replace local cached purchase status with Cloud purchase status
                            NSUserDefaults.StandardUserDefaults.SetBool (remotePurchaseStatus, KIsAlreadyPurchased);
                            NSUserDefaults.StandardUserDefaults.Synchronize ();
                        }
                        return remotePurchaseStatus;
                    }
                }
            }
            // if no icloud or not found in iCloud
            Log.Info (Log.LOG_SYS, "CloudHandler: Purchase status false for license in cloud. Getting user defaults value : {0}", NSUserDefaults.StandardUserDefaults.BoolForKey (KIsAlreadyPurchased));
            return NSUserDefaults.StandardUserDefaults.BoolForKey (KIsAlreadyPurchased);
        }

        public DateTime GetPurchaseDate ()
        {
            string purchaseDate;
            Log.Info (Log.LOG_SYS, "CloudHandler: Getting purchase date for product");
            if (HasiCloud) {
                purchaseDate = Store.GetString (KPurchaseDate);
                if (purchaseDate != null) {
                    string localPurchaseDate = NSUserDefaults.StandardUserDefaults.StringForKey (KPurchaseDate);
                    if (localPurchaseDate != purchaseDate) {
                        // replace local cached purchase date with Cloud purchase date
                        NSUserDefaults.StandardUserDefaults.SetString (purchaseDate, KPurchaseDate);
                        NSUserDefaults.StandardUserDefaults.Synchronize ();
                    }
                    return purchaseDate.ToDateTime ();
                }
            }
            // if no icloud or not found in iCloud
            purchaseDate = NSUserDefaults.StandardUserDefaults.StringForKey (KPurchaseDate);
            Log.Info (Log.LOG_SYS, "CloudHandler: Purchase date not found for product in cloud. Getting user defaults value : {0}", purchaseDate);
            return purchaseDate.ToDateTime ();
        }

        public void RecordPurchase (DateTime purchaseDate)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Recording purchase status(true) on {0}", purchaseDate.ToAsUtcString ());
            if (HasiCloud) {
                Store.SetBool (KIsAlreadyPurchased, true);  
                Store.SetString (KPurchaseDate, purchaseDate.ToAsUtcString ());
                Store.Synchronize ();
            }
            NSUserDefaults.StandardUserDefaults.SetBool (true, KIsAlreadyPurchased);
            NSUserDefaults.StandardUserDefaults.SetString (purchaseDate.ToAsUtcString (), KPurchaseDate);
            NSUserDefaults.StandardUserDefaults.Synchronize ();        
        }

        public void SetFirstInstallDate (DateTime installDate)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Setting first install date {0}", installDate.ToAsUtcString ());
            if (HasiCloud) {
                Store.SetString (KFirstInstallDate, installDate.ToAsUtcString ());
                Store.Synchronize ();
            }
            NSUserDefaults.StandardUserDefaults.SetString (installDate.ToAsUtcString (), KFirstInstallDate);
            NSUserDefaults.StandardUserDefaults.Synchronize ();   
        }

        public DateTime GetFirstInstallDate ()
        {
            string installDate;

            Log.Info (Log.LOG_SYS, "CloudHandler: Getting first install date");
            if (HasiCloud) {
                installDate = Store.GetString (KFirstInstallDate);
                if (installDate != null) {
                    string localInstallDate = NSUserDefaults.StandardUserDefaults.StringForKey (KFirstInstallDate);
                    if (localInstallDate != installDate) {
                        // replace local cached install date with Cloud install date
                        NSUserDefaults.StandardUserDefaults.SetString (installDate, KFirstInstallDate);
                        NSUserDefaults.StandardUserDefaults.Synchronize ();
                    }
                    Log.Info (Log.LOG_SYS, "CloudHandler: Returning first install date {0} from cloud", installDate);
                    return installDate.ToDateTime ();
                }
            }
            // if no icloud or not found in iCloud
            installDate = NSUserDefaults.StandardUserDefaults.StringForKey (KFirstInstallDate);
            Log.Info (Log.LOG_SYS, "CloudHandler: First install date not found in cloud. Getting user defaults value {0}.", installDate);
            return installDate.ToDateTime ();        
        }

        private void TokensWatcher (object sender, EventArgs ea)
        {
            StatusIndEventArgs siea = (StatusIndEventArgs)ea;
            switch (siea.Status.SubKind) {
            case NcResult.SubKindEnum.Info_PushAssistClientToken:
                if (null == siea.Status.Value) {
                    // do nothing now
                    Log.Info (Log.LOG_SYS, "CloudHandler: No UserId created yet");

                } else {
                    string newUserId = (string)siea.Status.Value;
                    Log.Info (Log.LOG_SYS, "CloudHandler: NcApplication UserId is {0}", newUserId);
                    string userId = GetUserId ();
                    if (userId == null) {
                        Log.Info (Log.LOG_SYS, "CloudHandler: No UserId in cloud. Saving  UserId {0} to cloud", newUserId);
                        SetUserId (newUserId);
                    } else if (newUserId != userId) {
                        Log.Info (Log.LOG_SYS, "CloudHandler: UserId exists in cloud. Replacing local UserId {0} with {1}", NcApplication.Instance.ClientId, userId);
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
                NSNumber reasonNumber = (NSNumber)userInfo.ObjectForKey (NSUbiquitousKeyValueStore.ChangeReasonKey);
                int reason = reasonNumber.Int32Value; // reason change was triggered

                NSArray changedKeys = (NSArray)userInfo.ObjectForKey (NSUbiquitousKeyValueStore.ChangedKeysKey);
                for (uint i = 0; i < changedKeys.Count; i++) {
                    string key = Marshal.PtrToStringAuto (changedKeys.ValueAt (i));
                    if (key == KUserId) {
                        Log.Info (Log.LOG_SYS, "CloudHandler: Notification change reason {0}", reason);
                        // ICloud override local - TODO: confirm this
                        string userId = NSUbiquitousKeyValueStore.DefaultStore.GetString (KUserId);
                        Log.Info (Log.LOG_SYS, "CloudHandler: Notification from cloud. UserId changed to {0}", userId);
                        if ((userId != null) && (userId != NcApplication.Instance.ClientId)) {
                            Log.Info (Log.LOG_SYS, "CloudHandler: Replacing localUserId {0} with {1} from Cloud", NcApplication.Instance.ClientId, userId);
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
}