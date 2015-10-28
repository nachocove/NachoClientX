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
        public static string KUserId = "UserId";
        public static string KFirstInstallDate = "FirstInstallDate";
        public static string KPurchaseDate = "PurchaseDate";
        public static string KIsAlreadyPurchased = "IsAlreadyPurchased";

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
            return Keychain.Instance.GetUserId ();
        }

        public void SetUserId (string UserId)
        {
            Keychain.Instance.SetUserId (UserId);
            BackupPrefs.SetKey (KUserId, UserId);
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
        }

        public void SetFirstInstallDate (DateTime installDate)
        {
            BackupPrefs.SetKey (KFirstInstallDate, installDate.ToBinary ());
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
                            var backupManager = new BackupManager (MainApplication.Instance);
                            backupManager.DataChanged ();
                        }
                    }
                }
                break;
            }
        }
        public bool CloudInstallDateEarlierThanLocal ()
        {
            long cloudInstallDateLong = BackupPrefs.GetKeyLong (KFirstInstallDate);
            if (cloudInstallDateLong == 0) {
                Log.Info (Log.LOG_SYS, "CloudHandler: Cloud first install date is 0");
                return false;
            }
            DateTime cloudInstallDate = DateTime.FromBinary (cloudInstallDateLong);

            string localInstallDateStr = NSUserDefaults.StandardUserDefaults.StringForKey (KFirstInstallDate);
            if (localInstallDateStr == null) {
                Log.Info (Log.LOG_SYS, "CloudHandler: Local first install date is null");
                return true;
            }
            DateTime localInstallDate = localInstallDateStr.ToDateTime (); 
            if (DateTime.Compare (cloudInstallDate, localInstallDate) < 0) {
                Log.Info (Log.LOG_SYS, "CloudHandler: Cloud first install date {0} is earlier than local {1}", cloudInstallDate, localInstallDate);
                return true;
            } else {
                Log.Info (Log.LOG_SYS, "CloudHandler: Cloud first install date {0} is not earlier than local {1}", cloudInstallDate, localInstallDate);
                return false;
            }
            Log.Info (Log.LOG_SYS, "CloudHandler: Cloud is not available");
            return false;
        }
    }

    // see https://developer.android.com/guide/topics/data/backup.html
    public class NcBackupAgent : BackupAgent
    {
        public override void OnCreate ()
        {
            base.OnCreate ();
        }
        public override void OnBackup (Android.OS.ParcelFileDescriptor oldState, BackupDataOutput data, Android.OS.ParcelFileDescriptor newState)
        {
            if (!NeedNewBackup (oldState)) {
                return;
            }
            var userid = Keychain.Instance.GetUserId ();
            if (string.IsNullOrEmpty (userid)) {
                return;
            }
            writeUserId (data, userid);
            writeNewState (newState);
        }

        public override void OnRestore (BackupDataInput data, int appVersionCode, Android.OS.ParcelFileDescriptor newState)
        {
            while (data.ReadNextHeader ()) {
                String key = data.Key;
                switch (key) {
                case CloudHandler.KUserId:
                    String userid = readUserid (data);
                    Keychain.Instance.SetUserId (userid);
                    break;

                default:
                    Log.Error (Log.LOG_SYS, "BackupManager gave us data element {0} which we don't know to handle", key);
                    data.SkipEntityData ();
                    break;
                }
            }
            writeNewState (newState);
        }

        private void writeUserId (BackupDataOutput data, string userId)
        {
            byte[] useridBytes = Encoding.ASCII.GetBytes (userId);
            data.WriteEntityHeader(CloudHandler.KUserId, useridBytes.Length);
            data.WriteEntityData(useridBytes, useridBytes.Length);
        }

        private string readUserid (BackupDataInput data)
        {
            int dataSize = data.DataSize;
            byte[] dataBuf = new byte[dataSize];
            data.ReadEntityData (dataBuf, 0, dataSize);
            return Encoding.ASCII.GetString (dataBuf);
        }

        #region EpochHandling
        private bool NeedNewBackup (Android.OS.ParcelFileDescriptor oldState)
        {
//            DataInputStream old = new DataInputStream(new FileInputStream (oldState.FileDescriptor));
//            try {
//                long stateModified = old.ReadLong();
//                if (CurrentStateEpoch.HasValue && CurrentStateEpoch.Value == stateModified) {
//                    return false;
//                }
//                return true;
//            } catch (Java.IO.IOException e) {
//                return true;
//            }
            return true;
        }

        private void writeNewState (Android.OS.ParcelFileDescriptor newState)
        {
//            DataOutputStream outstream = new DataOutputStream(new FileOutputStream(newState.FileDescriptor));
//            outstream.WriteLong (NextEpoch ());
        }

        private long NextEpoch ()
        {   
            long nextValue = CurrentStateEpoch.HasValue ? CurrentStateEpoch.Value : 0;
            nextValue++;
            CurrentStateEpoch = nextValue;
            return nextValue;
        }

        const string BackupPrefsEpoch = "BackupPrefsEpoch";
        private long? _CurrentStateEpoch;
        private long? CurrentStateEpoch {
            get {
                if (!_CurrentStateEpoch.HasValue) {
                    long current = NcBackupPrefs.Instance.GetKeyLong (BackupPrefsEpoch);
                    if (0 != current) {
                        _CurrentStateEpoch = current;
                    }
                }
                return _CurrentStateEpoch;
            }

            set {
                if (value.HasValue) {
                    _CurrentStateEpoch = value;
                    NcBackupPrefs.Instance.SetKey (BackupPrefsEpoch, value.Value);
                }
            }
        }
        #endregion
    }

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
        const string BackupPrefsKey = "NachoBackupPrefs";
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

}
