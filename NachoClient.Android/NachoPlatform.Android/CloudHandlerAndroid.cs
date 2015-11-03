//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
using System;
using NachoCore.Utils;
using Android.App.Backup;
using System.Text;
using Java.IO;
using Android.Content;
using NachoClient.AndroidClient;
using System.IO;
using NachoCore;

namespace NachoPlatform
{
    public class CloudHandler : IPlatformCloudHandler
    {
        public const string KUserId = "UserId";
        public const string KFirstInstallDate = "FirstInstallDate";
        public const string KPurchaseDate = "PurchaseDate";
        public const string KIsAlreadyPurchased = "IsAlreadyPurchased";

        private static volatile CloudHandler instance;
        private static object syncRoot = new Object ();

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

        NcBackupPrefs BackupPrefs;
        public CloudHandler ()
        {
            BackupPrefs = NcBackupPrefs.Instance;
        }

        public string GetUserId ()
        {
            return BackupPrefs.GetKeyString (KUserId);
        }

        public void SetUserId (string UserId)
        {
            BackupPrefs.SetKey (KUserId, UserId);
            MainApplication.Instance.BackupManager.DataChanged ();
        }

        public bool IsAlreadyPurchased ()
        {
            return BackupPrefs.GetKeyBool (KIsAlreadyPurchased, false);
        }

        public DateTime GetPurchaseDate ()
        {
            long x = BackupPrefs.GetKeyLong (KPurchaseDate);
            if (x > 0) {
                return DateTime.FromBinary (x);
            } else {
                return DateTime.MinValue;
            }
        }

        public void RecordPurchase (DateTime purchaseDate)
        {
            BackupPrefs.SetKey (KPurchaseDate, purchaseDate.ToBinary ());
            BackupPrefs.SetKey (KIsAlreadyPurchased, true);
            MainApplication.Instance.BackupManager.DataChanged ();
        }

        public void SetFirstInstallDate (DateTime installDate)
        {
            BackupPrefs.SetKey (KFirstInstallDate, installDate.ToBinary ());
            MainApplication.Instance.BackupManager.DataChanged ();
        }

        public DateTime GetFirstInstallDate ()
        {
            long x = BackupPrefs.GetKeyLong (KFirstInstallDate);
            if (x > 0) {
                return DateTime.FromBinary (x);
            } else {
                return DateTime.MinValue;
            }
        }

        public void Start ()
        {
            // start listening to changes in NcApplication
            NcApplication.Instance.StatusIndEvent += TokensWatcher;
        }

        public void Stop ()
        {
            NcApplication.Instance.StatusIndEvent -= TokensWatcher;
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
                        } else {
                            MainApplication.Instance.BackupManager.DataChanged ();
                        }
                    }
                }
                break;
            }
        }
        public bool CloudInstallDateEarlierThanLocal ()
        {
//            long cloudInstallDateLong = BackupPrefs.GetKeyLong (KFirstInstallDate);
//            if (cloudInstallDateLong == 0) {
//                Log.Info (Log.LOG_SYS, "CloudHandler: Cloud first install date is 0");
//                return false;
//            }
//            DateTime cloudInstallDate = DateTime.FromBinary (cloudInstallDateLong);
//
//            string localInstallDateStr = NSUserDefaults.StandardUserDefaults.StringForKey (KFirstInstallDate);
//            if (localInstallDateStr == null) {
//                Log.Info (Log.LOG_SYS, "CloudHandler: Local first install date is null");
//                return true;
//            }
//            DateTime localInstallDate = localInstallDateStr.ToDateTime (); 
//            if (DateTime.Compare (cloudInstallDate, localInstallDate) < 0) {
//                Log.Info (Log.LOG_SYS, "CloudHandler: Cloud first install date {0} is earlier than local {1}", cloudInstallDate, localInstallDate);
//                return true;
//            } else {
//                Log.Info (Log.LOG_SYS, "CloudHandler: Cloud first install date {0} is not earlier than local {1}", cloudInstallDate, localInstallDate);
//                return false;
//            }
//            Log.Info (Log.LOG_SYS, "CloudHandler: Cloud is not available");
            // TODO Need to verify this, but I suspect that the restore operation happens before the app really starts,
            // so we'll always have the latest cloud-data here when we run.
            return false;
        }
    }

    /// <summary>
    /// These preferences are NOT ENCRYPTED. Anything that goes here will wind up on disk, and is visible
    /// to anyone with a USB cable. Data stored here is considered non-sensitive, and will be backed up
    /// to the Google Cloud Backup service.
    /// </summary>
    public class NcBackupPrefs
    {
        private static volatile NcBackupPrefs instance;
        private static object syncRoot = new Object ();
        public static NcBackupPrefs Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new NcBackupPrefs ();
                        }
                    }
                }
                return instance;
            }
        }

        public string GetKeyString (string key)
        {
            return BackupPrefs.GetString(key, null);
        }

        public bool GetKeyBool (string key, bool defaultValue)
        {
            return BackupPrefs.GetBoolean(key, defaultValue);
        }

        public long GetKeyLong (string key)
        {
            return BackupPrefs.GetLong(key, 0);
        }

        public bool SetKey (string key, string value)
        {
            NcAssert.True (null != value);
            var editor = BackupPrefs.Edit ();
            editor.PutString(key, value);
            editor.Commit();
            return true;
        }

        public bool SetKey (string key, bool value)
        {
            var editor = BackupPrefs.Edit ();
            editor.PutBoolean(key, value);
            editor.Commit();
            return true;
        }

        public bool SetKey (string key, long value)
        {
            var editor = BackupPrefs.Edit ();
            editor.PutLong(key, value);
            editor.Commit();
            return true;
        }

        public bool Deleter (string query)
        {
            var editor = BackupPrefs.Edit ();
            editor.Remove (query);
            editor.Commit ();
            return true;
        }

        #region ISharedPreferences
        public const string BackupPrefsKey = "NachoBackupPrefs";
        ISharedPreferences _BackupPrefs;
        ISharedPreferences BackupPrefs
        {
            get
            {
                if (_BackupPrefs == null) {
                    _BackupPrefs = MainApplication.Instance.ApplicationContext.GetSharedPreferences (BackupPrefsKey, FileCreationMode.Private);
                }
                return _BackupPrefs;
            }
        }
        #endregion
    }

    #region Backupgent
    class NcBackupAgentHelper : BackupAgentHelper
    {
        static string KNachoInstallprefs = "NachoInstallPrefs";
        public override void OnCreate()
        {
            base.OnCreate ();
            Log.Info (Log.LOG_SYS, "NcBackUpAgentHelper: Backup Initialized");
            NcSharedPrefsBackupHelper helper = new NcSharedPrefsBackupHelper(MainApplication.Instance.ApplicationContext, NcBackupPrefs.BackupPrefsKey);
            var backupAgentHelper = new BackupAgentHelper ();
            backupAgentHelper.AddHelper(KNachoInstallprefs, helper);
        }
    }

    /// <summary>
    /// This class exists only for debugging purposes. Once we know this works well, this can be removed.
    /// </summary>
    public class NcSharedPrefsBackupHelper : SharedPreferencesBackupHelper
    {
        public NcSharedPrefsBackupHelper (Context context, string key) : base(context, key)
        {
        }

        public override void PerformBackup (Android.OS.ParcelFileDescriptor oldState, BackupDataOutput data, Android.OS.ParcelFileDescriptor newState)
        {
            Log.Info (Log.LOG_SYS, "NcSharedPrefsBackupHelper: Performing backup");
            base.PerformBackup (oldState, data, newState);
        }

        public override void RestoreEntity (BackupDataInputStream data)
        {
            Log.Info (Log.LOG_SYS, "NcSharedPrefsBackupHelper: Performing Restore");
            base.RestoreEntity (data);
        }
    }
    #endregion
}
