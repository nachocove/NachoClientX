//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
using System;
using NachoCore.Utils;
using Android.App.Backup;
using System.Text;
using Java.IO;
using Android.Content;
using NachoClient.AndroidClient;
using System.IO;

namespace NachoPlatform
{
    public class CloudHandler : IPlatformCloudHandler
    {
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

        public CloudHandler ()
        {
        }

        public string GetUserId ()
        {
            if (Keychain.Instance.HasKeychain ()) {
                return Keychain.Instance.GetUserId ();
            } else {
                return null;
            }
        }

        public void SetUserId (string UserId)
        {
            if (Keychain.Instance.HasKeychain ()) {
                if (Keychain.Instance.SetUserId (UserId)) {
                    var backupManager = new BackupManager (MainApplication.Instance);
                    backupManager.DataChanged ();
                }
            }
        }

        public bool IsAlreadyPurchased ()
        {
            return false;
        }

        public DateTime GetPurchaseDate ()
        {
            return DateTime.Now;
        }

        public void RecordPurchase (DateTime purchaseDate)
        {
        }

        public void SetFirstInstallDate (DateTime installDate)
        {
        }

        public DateTime GetFirstInstallDate ()
        {
            return DateTime.Now;
        }

        public void Start ()
        {
        }

        public void Stop ()
        {
        }

    }

    // see https://developer.android.com/guide/topics/data/backup.html
    public class NcBackupAgent : BackupAgent
    {
        const string NachoBackupUserId = "NachoUserId";
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
                case NachoBackupUserId:
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
            data.WriteEntityHeader(NachoBackupUserId, useridBytes.Length);
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
                    string oldEpochStr = NcBackupPrefs.Instance.GetKey (BackupPrefsEpoch);
                    if (null != oldEpochStr) {
                        long current;
                        if (long.TryParse (oldEpochStr, out current)) {
                            _CurrentStateEpoch = current;
                        }
                    }
                }
                return _CurrentStateEpoch;
            }

            set {
                if (value.HasValue) {
                    _CurrentStateEpoch = value;
                    NcBackupPrefs.Instance.SetKey (BackupPrefsEpoch, value.Value.ToString ());
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

        public string GetKey (string key, bool errorIfMissing = false)
        {
            var r = BackupPrefs.GetString(key, null);
            if (null == r) {
                if (errorIfMissing) {
                    throw new KeychainItemNotFoundException (string.Format ("Missing entry for {0}", key));
                }
            }
            return r;
        }

        public bool SetKey (string key, string value)
        {
            NcAssert.True (null != value);
            var editor = BackupPrefs.Edit ();
            editor.PutString(key, value);
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
