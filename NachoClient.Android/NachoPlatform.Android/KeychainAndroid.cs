//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using Android.Content;
using Android.Preferences;
using NachoClient.AndroidClient;
using Java.Security;
using Android.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;

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

        private Keychain ()
        {
            try {
                GetKeyPair ();
                GetPrefsKey ();

            } catch (Exception ex) {
                var str = ex.ToString ();
                Log.Error (Log.LOG_SYS, "Keypair generation error: {0}", str);
            }
        }

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
        ISharedPreferences _Prefs;
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
            if (null != r) {
                return DecryptString (r);
            } else {
                throw new Exception ("No string found");
            }
        }

        public bool SetKeyString (string key, string value)
        {
            if (null == value) {
                throw new Exception ("Null string passed");
            }
            var enc = EncryptString (value);

            // DEBUG
            var dec = DecryptString (enc);
            NcAssert.True (value == dec);

            var editor = Prefs.Edit ();
            editor.PutString(key, enc);
            editor.Commit();
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

        #region PrefsKey
        private ISecretKey PrefsKey;
        private Mac PrefsMac;
        const string KPrefsKeyKey = "PreferencesKey";
        private void GetPrefsKey ()
        {
            if (null == PrefsKey) {
                DeleteKey (KPrefsKeyKey); // DEBUG
                var r = Prefs.GetString (KPrefsKeyKey, null);
                if (null == r) {
                    byte[] raw = new byte[32]; // 32*8 == 256
                    new SecureRandom ().NextBytes (raw);
                    var key = new SecretKeySpec (raw, "AES");
                    var editor = Prefs.Edit ();
                    editor.PutString (KPrefsKeyKey, RSAEncryptKey (key));
                    editor.Commit ();
                    r = Prefs.GetString (KPrefsKeyKey, null);
                    NcAssert.True (null != r);
                }
                PrefsKey = (ISecretKey)RSADecryptKey (r);
                PrefsMac = Mac.GetInstance("HmacSHA256");
                PrefsMac.Init(PrefsKey); // TODO Same key? If not, we'll need to create a new one and also encrypt and store it.
            }
        }

        private string DecryptString (string encryptedTextB64)
        {
            Log.Error (Log.LOG_SYS, "DEBUG DecryptString DEBUG");
            return encryptedTextB64;
        }

        private string EncryptString (string data)
        {
            Log.Error (Log.LOG_SYS, "DEBUG EncryptString DEBUG");
            return data;
        }
        #endregion

        #region Keystore
        const int KeyPairSize = 2048;
        const string KDefaultKeyPair = "NachoMailDefaultKeyPair";
        const string KDefaultKeyStore = "AndroidKeyStore";
        string KeyStoreProvider;
        KeyStore _ks;
        KeyStore ks {
            get {
                if (_ks == null) {
                    KeyStoreProvider = KDefaultKeyStore;
                    _ks = KeyStore.GetInstance (KeyStoreProvider);
                    _ks.Load (null);
                }
                return _ks;
            }
        }

        IPrivateKey privateKey;
        IPublicKey publicKey;
        private void GetKeyPair ()
        {
            if (!ks.ContainsAlias (KDefaultKeyPair)) {
                GenerateKeyPair ();
            }
            KeyStore.PrivateKeyEntry privateKeyEntry = (KeyStore.PrivateKeyEntry)ks.GetEntry (KDefaultKeyPair, null);
            publicKey = privateKeyEntry.Certificate.PublicKey;
            privateKey = privateKeyEntry.PrivateKey;
        }

        private void GenerateKeyPair ()
        {
            Java.Util.Calendar start = Java.Util.Calendar.GetInstance (Java.Util.TimeZone.Default);
            Java.Util.Calendar end = Java.Util.Calendar.GetInstance (Java.Util.TimeZone.Default);
            end.Add (Java.Util.CalendarField.Year, 20);
            KeyPairGenerator generator = KeyPairGenerator.GetInstance("RSA", KeyStoreProvider);
            KeyPairGeneratorSpec spec = new KeyPairGeneratorSpec.Builder(MainApplication.Instance.ApplicationContext)
                .SetKeyType ("RSA")
                .SetKeySize (KeyPairSize)
                .SetAlias (KDefaultKeyPair)
                .SetSubject(new Javax.Security.Auth.X500.X500Principal("CN=NachoMail"))
                .SetSerialNumber (Java.Math.BigInteger.One)
                .SetStartDate(start.Time)
                .SetEndDate(end.Time)
                .SetEncryptionRequired () // TODO We probably need to see if we can still use the key in the background if the device is locked.
                .Build ();
            generator.Initialize(spec);
            generator.GenerateKeyPair();
            NcAssert.True (ks.ContainsAlias (KDefaultKeyPair)); // make sure it got saved to the keystore
        }

        private string RSAEncryptKey (IKey key)
        {
            byte[] encryptedData;
            var rsaCipher = Cipher.GetInstance("RSA/ECB/PKCS1Padding", "AndroidOpenSSL");
            rsaCipher.Init (CipherMode.WrapMode, publicKey);
            encryptedData = rsaCipher.Wrap (key);
            return Convert.ToBase64String (encryptedData);
        }

        private IKey RSADecryptKey (string encryptedTextB64)
        {
            var bytesToDecrypt = Convert.FromBase64String(encryptedTextB64);
            var rsaCipher = Cipher.GetInstance("RSA/ECB/PKCS1Padding", "AndroidOpenSSL");
            rsaCipher.Init (CipherMode.UnwrapMode, privateKey);
            return rsaCipher.Unwrap (bytesToDecrypt, "AES", KeyType.SecretKey);
        }
        #endregion
    }
}
