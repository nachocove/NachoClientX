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
        BackupManager BackupManager;

        public CloudHandler ()
        {
            BackupManager = new BackupManager (MainApplication.Instance);
            BackupPrefs = NcBackupPrefs.Instance;
        }

        public string GetUserId ()
        {
            return BackupPrefs.GetKeyString (KUserId);
        }

        public void SetUserId (string UserId)
        {
            var previous = GetUserId ();
            if (previous != UserId) {
                BackupPrefs.SetKey (KUserId, UserId);
                BackupManager.DataChanged ();
            }
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
            var previous = GetPurchaseDate ();
            if (previous != purchaseDate) {
                BackupPrefs.SetKey (KPurchaseDate, purchaseDate.ToBinary ());
                BackupPrefs.SetKey (KIsAlreadyPurchased, true);
                BackupManager.DataChanged ();
            }
        }

        public void SetFirstInstallDate (DateTime installDate)
        {
            var previous = GetFirstInstallDate ();
            if (previous != installDate) {
                BackupPrefs.SetKey (KFirstInstallDate, installDate.ToBinary ());
                BackupManager.DataChanged ();
            }
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
                    if (userId == null || newUserId != userId) {
                        if (userId == null) {
                            Log.Info (Log.LOG_SYS, "CloudHandler: No UserId in cloud. Saving  UserId {0} to cloud", newUserId);
                        } else {
                            Log.Info (Log.LOG_SYS, "CloudHandler: Replacing local UserId {0} with {1}", newUserId, userId);
                        }
                        SetUserId (newUserId);
                    }
                }
                break;
            }
        }
    }

    /// <summary>
    /// These preferences are NOT ENCRYPTED. Anything that goes here will wind up on disk, and is visible
    /// to anyone with a USB cable. Data stored here is considered non-sensitive, and will be backed up
    /// to the Google Cloud Backup service.
    /// </summary>
    public class NcBackupPrefs
    {
        private static volatile NcBackupPrefs _instance;
        private static object syncRoot = new Object ();

        public static NcBackupPrefs Instance {
            get {
                if (_instance == null) {
                    lock (syncRoot) {
                        if (_instance == null) {
                            _instance = new NcBackupPrefs ();
                        }
                    }
                }
                return _instance;
            }
        }

        public string GetKeyString (string key)
        {
            return BackupPrefs.GetString (key, null);
        }

        public bool GetKeyBool (string key, bool defaultValue)
        {
            return BackupPrefs.GetBoolean (key, defaultValue);
        }

        public long GetKeyLong (string key)
        {
            return BackupPrefs.GetLong (key, 0);
        }

        public bool SetKey (string key, string value)
        {
            NcAssert.True (null != value);
            var editor = BackupPrefs.Edit ();
            editor.PutString (key, value);
            editor.Commit ();
            return true;
        }

        public bool SetKey (string key, bool value)
        {
            var editor = BackupPrefs.Edit ();
            editor.PutBoolean (key, value);
            editor.Commit ();
            return true;
        }

        public bool SetKey (string key, long value)
        {
            var editor = BackupPrefs.Edit ();
            editor.PutLong (key, value);
            editor.Commit ();
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

        const string BackupPrefsKey = "NachoBackupPrefs";
        ISharedPreferences _BackupPrefs;

        ISharedPreferences BackupPrefs {
            get {
                if (_BackupPrefs == null) {
                    _BackupPrefs = MainApplication.Instance.ApplicationContext.GetSharedPreferences (BackupPrefsKey, FileCreationMode.Private);
                }
                return _BackupPrefs;
            }
        }

        public static SharedPreferencesBackupHelper GetSharedPreferencesBackupHelper ()
        {
            return new SharedPreferencesBackupHelper (MainApplication.Instance.ApplicationContext, NcBackupPrefs.BackupPrefsKey);
        }

        public void ResetSettings ()
        {
            _BackupPrefs = null;
        }

        #endregion
    }

    #region Backupgent
    class NcBackupAgentHelper : BackupAgentHelper
    {
        static string KNachoInstallprefs = "NachoInstallPrefs";

        public override void OnCreate ()
        {
            base.OnCreate ();
            Log.Info (Log.LOG_SYS, "CloudHandler:NcBackUpAgentHelper: Backup Initialized");
            AddHelper (KNachoInstallprefs, NcBackupPrefs.GetSharedPreferencesBackupHelper ());
        }

        public override void OnRestore (BackupDataInput data, int appVersionCode, Android.OS.ParcelFileDescriptor newState)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler:NcBackupAgentHelper: Performing restore");
            base.OnRestore (data, appVersionCode, newState);
        }

        public override void OnBackup (Android.OS.ParcelFileDescriptor oldState, BackupDataOutput data, Android.OS.ParcelFileDescriptor newState)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler:NcBackupAgentHelper: Performing backup");
            base.OnBackup (oldState, data, newState);
        }

        public override void OnFullBackup (FullBackupDataOutput data)
        {
            Log.Info (Log.LOG_SYS, "CloudHandler:NcBackupAgentHelper: Fullbackup started");
            BackupFiles (MainApplication.Instance.FilesDir, data);
        }

        void BackupFiles (Java.IO.File root, FullBackupDataOutput data)
        {
            Java.IO.File[] list = root.ListFiles ();
            foreach (Java.IO.File f in list) {
                if (NcFileHandler.Instance.SkipFile (f.Name)) {
                    Log.Info (Log.LOG_SYS, "CloudHandler:NcBackupAgentHelper: skipped file {0}", f.Name);
                    continue;
                }
                if (f.IsDirectory) {
                    BackupFiles (f, data);
                } else {
                    Log.Info (Log.LOG_SYS, "CloudHandler:NcBackupAgentHelper: backing up file {0}", f.Name);
                    FullBackupFile (f, data);
                }
            }
        }
    }

    class NcRestoreObserver : RestoreObserver
    {
        public override void RestoreFinished (int error)
        {
            base.RestoreFinished (error);
            if (error == 0) {
                // close the file, and get a new file handle.
                //NcBackupPrefs.Instance.ResetSettings ();
                var userid = NcBackupPrefs.Instance.GetKeyString (CloudHandler.KUserId);
                if (!string.IsNullOrEmpty (userid)) {
                    Log.Info (Log.LOG_SYS, "CloudHandler:NcRestoreObserver: Found UserId in restored settings: {0}", userid);
                    NcApplication.Instance.UserId = userid;
                } else {
                    Log.Warn (Log.LOG_SYS, "CloudHandler:NcRestoreObserver: Restore finished, but no userid found.");
                }
            }
        }
    }
    #endregion
}
