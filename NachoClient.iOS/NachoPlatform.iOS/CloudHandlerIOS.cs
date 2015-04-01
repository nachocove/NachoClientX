//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
using System;
using Foundation;
using NachoCore.Utils;
using NachoCore;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NachoPlatform
{
    public class CloudHandler : IPlatformCloudHandler
    {
        private static volatile CloudHandler instance;
        private static object syncRoot = new Object ();

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
        }

        public string GetUserId ()
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Getting UserId");

            var store = NSUbiquitousKeyValueStore.DefaultStore;
            if (store.GetString("UserId") != null){
                return store.GetString("UserId");
            }
            else{
                return NSUserDefaults.StandardUserDefaults.StringForKey ("UserId");
            }
        }

        public void SetUserId (string UserId)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Setting UserId {0}", UserId);

            var store = NSUbiquitousKeyValueStore.DefaultStore;
            store.SetString("UserId", UserId);  
            store.Synchronize();
            NSUserDefaults.StandardUserDefaults.SetString (UserId, "UserId");
            NSUserDefaults.StandardUserDefaults.Synchronize ();
        }

        public bool GetPurchasedStatus (string productId)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Getting purchase status for product {0}", productId);

            var store = NSUbiquitousKeyValueStore.DefaultStore;
            if (store.GetBool(productId) != false){
                Log.Info (Log.LOG_SYS, "CloudHandler: Purchase status true for product {0} in cloud", productId);
                return store.GetBool(productId);
            }
            else{
                Log.Info (Log.LOG_SYS, "CloudHandler: Purchase status false for product {0} in cloud. Checking user defaults.", productId);
                return NSUserDefaults.StandardUserDefaults.BoolForKey (productId);
            }
        }

        public string GetPurchasedDate (string productId)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Getting purchase date for product {0}", productId);

            var store = NSUbiquitousKeyValueStore.DefaultStore;
            string key = productId + ":PurchaseDate";
            if (store.GetString(key) != null){
                return store.GetString(key);
            }
            else{
                return NSUserDefaults.StandardUserDefaults.StringForKey (key);
            }
        }

        public void SetPurchasedStatus (string productId, DateTime purchaseDate)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler: Setting purchase status(true) for product {0} on {1}", productId, purchaseDate);

            var store = NSUbiquitousKeyValueStore.DefaultStore;
            store.SetBool(productId, true);  
            string key = "PurchaseDate";
            // TODO: handle exceptions
            store.SetString (key , purchaseDate.ToString ());
            store.Synchronize();
            NSUserDefaults.StandardUserDefaults.SetBool (true, productId);
            NSUserDefaults.StandardUserDefaults.SetString (purchaseDate.ToString(), key);
            NSUserDefaults.StandardUserDefaults.Synchronize ();        
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
                        Log.Info (Log.LOG_SYS, "CloudHandler: Saving new userId {0} to cloud", newUserId);
                        SetUserId (newUserId);
                    } else if (newUserId != userId) {
                        Log.Info (Log.LOG_SYS, "CloudHandler: Old userId {0} exists in cloud. Replacing NcApplication ClientId {1} with {0}", userId, NcApplication.Instance.ClientId, userId);
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
                        NSArray changedKeys = (NSArray)userInfo.ObjectForKey (NSUbiquitousKeyValueStore.ChangedKeysKey);
                        for (uint i = 0; i < changedKeys.Count; i++) {
                            string key = Marshal.PtrToStringAuto(changedKeys.ValueAt(i));
                            if (key == "UserId")
                            {
                                // ICloud override local - TODO: confirm this
                                string userId = NSUbiquitousKeyValueStore.DefaultStore.GetString("UserId");
                                if (userId != null) {
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
        public string AppStoreReceipt { get; set; }
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