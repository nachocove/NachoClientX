//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using Android.Content;
using Android.Preferences;
using NachoClient.AndroidClient;

namespace NachoPlatform
{
    public class Keychain : IPlatformKeychain
    {
        private static volatile Keychain instance;
        private static object syncRoot = new Object ();

        public static Keychain Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new Keychain ();
                    }
                }
                return instance;
            }
        }

        private const string KDefaultAccount = "device";
        private const string KIdentifierForVendor = "IdentifierForVendor";
        private const string KAccessToken = "AccessToken";
        private const string KRefreshToken = "RefreshToken";
        private const string KUserId = "UserId";
        private const string KLogSalt = "LogSalt";

        public bool HasKeychain ()
        {
            return true;
        }

        #region Password
        public string PasswordKey (int handle)
        {
            return handle.ToString ();
        }

        public string GetPassword (int handle)
        {
            return GetKeyString (PasswordKey (handle));
        }

        public bool SetPassword (int handle, string password)
        {
            return SetKeyString (PasswordKey (handle), password);
        }

        public bool DeletePassword (int handle)
        {
            return DeleteKey (PasswordKey (handle));
        }
        #endregion

        #region AccessToken
        public static string AccessTokenKey (int handle)
        {
            return string.Format ("{0}.{1}", handle, KAccessToken);
        }

        public string GetAccessToken (int handle)
        {
            return GetKeyString (AccessTokenKey (handle));
        }

        public bool SetAccessToken (int handle, string token)
        {
            return SetKeyString (AccessTokenKey (handle), token);
        }

        public bool DeleteAccessToken (int handle)
        {
            return DeleteKey (AccessTokenKey (handle));
        }
        #endregion

        #region RefreshToken
        public static string RefreshTokenKey (int handle)
        {
            return string.Format ("{0}.{1}", handle, KRefreshToken);
        }

        public string GetRefreshToken (int handle)
        {
            return GetKeyString (RefreshTokenKey (handle));
        }

        public bool SetRefreshToken (int handle, string token)
        {
            return SetKeyString (RefreshTokenKey (handle), token);
        }

        public bool DeleteRefreshToken (int handle)
        {
            return DeleteKey (RefreshTokenKey (handle));
        }
        #endregion

        #region LogSalt
        public static string LogSaltKey (int handle)
        {
            return string.Format ("{0}.{1}", handle, KLogSalt);
        }

        public string GetLogSalt (int handle)
        {
            return GetKeyString (RefreshTokenKey (handle));
        }

        public bool SetLogSalt(int handle, string logSalt)
        {
            return SetKeyString (LogSaltKey (handle), logSalt);
        }

        public bool DeleteLogSalt (int handle)
        {
            return DeleteKey (LogSaltKey (handle));
        }
        #endregion

        #region UserId
        public static string UserIdKey ()
        {
            return KUserId;
        }

        public string GetUserId ()
        {
            return GetKeyString (UserIdKey ());
        }

        public bool SetUserId (string userId)
        {
            return SetKeyString (UserIdKey (), userId);
        }
        #endregion

        #region ISharedPreferences
        ISharedPreferences _Prefs = null;
        ISharedPreferences Prefs
        {
            get
            {
                if (_Prefs == null) {
                    _Prefs = PreferenceManager.GetDefaultSharedPreferences (MainApplication.Instance.ApplicationContext);
                }
                return _Prefs;
            }
        }

        public string GetKeyString (string key)
        {
            var r = Prefs.GetString(key, null);
            Log.Info (Log.LOG_SYS, "Keychain.Android: GetKeyString: {0}:{1}", key, r);
            return r;
        }

        public bool SetKeyString (string key, string value)
        {
            try {
                Log.Info (Log.LOG_SYS, "Keychain.Android: SetKeyString: {0}:{1}", key, value);
                var editor = Prefs.Edit ();
                editor.PutString(key, value);
                editor.Commit();
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "SetKeyString Exception: {0}", ex);
            }
            return true;
        }

        public bool DeleteKey (string key)
        {
            var editor = Prefs.Edit ();
            editor.Remove (key);
            editor.Commit ();
            return true;
        }
        #endregion
    }
}
