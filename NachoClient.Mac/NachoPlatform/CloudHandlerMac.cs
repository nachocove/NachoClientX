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

        private bool HasiCloud = true;
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
            // FIXME - remove following line after telemetry is ready.
            uburl = null;
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
            if (HasiCloud) {
                string remoteUserId = Store.GetString (KUserId);
                Log.Info (Log.LOG_SYS, "CloudHandler: Cloud UserId {0}", remoteUserId);
                if (remoteUserId != null) {
                    string localUserId = Keychain.Instance.GetUserId ();
                    if (localUserId != remoteUserId) {
                        // replace local cached UserId with Cloud UserId
                        Keychain.Instance.SetUserId (remoteUserId);
                    }
                    return remoteUserId;
                }
            }
            // if no icloud or not found in iCloud
            var userId = Keychain.Instance.GetUserId ();
            Log.Info (Log.LOG_SYS, "CloudHandler: UserId not stored in cloud. Getting keychain value : {0}", userId);
            return userId;
        }

        public void SetUserId (string UserId)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Setting UserId {0}", UserId);
            if (HasiCloud) {
                Store.SetString (KUserId, UserId);  
                Store.Synchronize ();
            }
            Keychain.Instance.SetUserId (UserId);
        }

        public bool IsAlreadyPurchased ()
        {
            if (HasiCloud) {
                bool remotePurchaseStatus = Store.GetBool (KIsAlreadyPurchased);
                Log.Info (Log.LOG_SYS, "CloudHandler: Purchase status {0} for license in cloud", remotePurchaseStatus);
                if (remotePurchaseStatus != false) {
                    if (remotePurchaseStatus == true) {
                        bool localPurchaseStatus = NSUserDefaults.StandardUserDefaults.BoolForKey (KIsAlreadyPurchased);
                        if (localPurchaseStatus != remotePurchaseStatus) {
                            if (localPurchaseStatus) { 
                                // locally purchased, update cloud)
                                Store.SetBool (KIsAlreadyPurchased, true);  
                                string purchaseDate = NSUserDefaults.StandardUserDefaults.StringForKey (KPurchaseDate);
                                Store.SetString (KPurchaseDate, purchaseDate);
                            } else {
                                // replace local cached purchase status with Cloud purchase status
                                NSUserDefaults.StandardUserDefaults.SetBool (remotePurchaseStatus, KIsAlreadyPurchased);
                                NSUserDefaults.StandardUserDefaults.Synchronize ();
                            }
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
            if (HasiCloud) {
                purchaseDate = Store.GetString (KPurchaseDate);
                Log.Info (Log.LOG_SYS, "CloudHandler: Purchase date {0} for license in cloud", purchaseDate);
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
                Log.Info (Log.LOG_SYS, "CloudHandler: First install date {0} from cloud", installDate);
                if (installDate != null) {
                    string localInstallDate = NSUserDefaults.StandardUserDefaults.StringForKey (KFirstInstallDate);
                    if (localInstallDate != installDate) {
                        // replace local cached install date with Cloud install date
                        NSUserDefaults.StandardUserDefaults.SetString (installDate, KFirstInstallDate);
                        NSUserDefaults.StandardUserDefaults.Synchronize ();
                    }
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
            case NcResult.SubKindEnum.Info_UserIdChanged:
                if (null == siea.Status.Value) {
                    // do nothing now
                } else {
                    string newUserId = (string)siea.Status.Value;
                    string userId = GetUserId ();
                    if (userId == null) {
                        Log.Info (Log.LOG_SYS, "CloudHandler: No UserId in cloud. Saving  UserId {0} to cloud", newUserId);
                        SetUserId (newUserId);
                    } else if (newUserId != userId) {
                        if (CloudInstallDateEarlierThanLocal ()) {
                            Log.Info (Log.LOG_SYS, "CloudHandler: Earlier UserId exists in cloud. Replacing local UserId {0} with {1}", NcApplication.Instance.ClientId, userId);
                            NcApplication.Instance.UserId = userId;
                        }
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
                            if (CloudInstallDateEarlierThanLocal ()) {
                                Log.Info (Log.LOG_SYS, "CloudHandler: Replacing localUserId {0} with {1} from Cloud", NcApplication.Instance.ClientId, userId);
                                NcApplication.Instance.UserId = userId;
                            }
                        }
                    }
                }
            });
        }

        public bool CloudInstallDateEarlierThanLocal ()
        {
            if (HasiCloud) {
                string cloudInstallDateStr = Store.GetString (KFirstInstallDate);
                if (cloudInstallDateStr == null) {
                    Log.Info (Log.LOG_SYS, "CloudHandler: Cloud first install date is null");
                    return false;
                }
                string localInstallDateStr = NSUserDefaults.StandardUserDefaults.StringForKey (KFirstInstallDate);
                if (localInstallDateStr == null) {
                    Log.Info (Log.LOG_SYS, "CloudHandler: Local first install date is null");
                    return true;
                }
                DateTime cloudInstallDate = cloudInstallDateStr.ToDateTime (); 
                DateTime localInstallDate = localInstallDateStr.ToDateTime (); 
                if (DateTime.Compare (cloudInstallDate, localInstallDate) < 0) {
                    Log.Info (Log.LOG_SYS, "CloudHandler: Cloud first install date {0} is earlier than local {1}", cloudInstallDateStr, localInstallDateStr);
                    return true;
                } else {
                    Log.Info (Log.LOG_SYS, "CloudHandler: Cloud first install date {0} is not earlier than local {1}", cloudInstallDateStr, localInstallDateStr);
                    return false;
                }
            }
            Log.Info (Log.LOG_SYS, "CloudHandler: Cloud is not available");
            return false;
        }

        public void Stop ()
        {
            NcApplication.Instance.StatusIndEvent -= TokensWatcher;
            NSNotificationCenter.DefaultCenter.RemoveObserver (CloudKeyObserver);
        }

    }
}