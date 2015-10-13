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
using Portable.Text;
using System.IO;

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

        private const string KIdentifierForVendor = "IdentifierForVendor";
        private const string KAccessToken = "AccessToken";
        private const string KRefreshToken = "RefreshToken";
        private const string KUserId = "UserId";
        private const string KLogSalt = "LogSalt";

        public bool HasKeychain ()
        {
            return true;
        }

        private string CreateQuery (int handle, string subKey)
        {
            return CreateQuery (string.Format ("{0}:{1}", handle, subKey));
        }

        private string CreateQuery (string handle)
        {
            return  handle;
        }

        private string CreateQuery (int handle)
        {
            return CreateQuery (handle.ToString ());
        }

        #region Password
        public string PasswordKey (int handle)
        {
            return CreateQuery (handle);
        }

        public string GetPassword (int handle)
        {
            return Getter (PasswordKey (handle));
        }

        public bool SetPassword (int handle, string password)
        {
            return Setter (PasswordKey (handle), password);
        }

        public bool DeletePassword (int handle)
        {
            return Deleter (PasswordKey (handle));
        }
        #endregion

        #region AccessToken
        public string AccessTokenKey (int handle)
        {
            return CreateQuery (handle, KAccessToken);
        }

        public string GetAccessToken (int handle)
        {
            return Getter (AccessTokenKey (handle));
        }

        public bool SetAccessToken (int handle, string token)
        {
            return Setter (AccessTokenKey (handle), token);
        }

        public bool DeleteAccessToken (int handle)
        {
            return Deleter (AccessTokenKey (handle));
        }
        #endregion

        #region RefreshToken
        public string RefreshTokenKey (int handle)
        {
            return CreateQuery (handle, KRefreshToken);
        }

        public string GetRefreshToken (int handle)
        {
            return Getter (RefreshTokenKey (handle));
        }

        public bool SetRefreshToken (int handle, string token)
        {
            return Setter (RefreshTokenKey (handle), token);
        }

        public bool DeleteRefreshToken (int handle)
        {
            return Deleter (RefreshTokenKey (handle));
        }
        #endregion

        #region LogSalt
        public string LogSaltKey (int handle)
        {
            return CreateQuery (handle, KLogSalt);
        }

        public string GetLogSalt (int handle)
        {
            return Getter (RefreshTokenKey (handle));
        }

        public bool SetLogSalt(int handle, string logSalt)
        {
            return Setter (LogSaltKey (handle), logSalt);
        }

        public bool DeleteLogSalt (int handle)
        {
            return Deleter (LogSaltKey (handle));
        }
        #endregion

        #region UserId
        public string UserIdKey ()
        {
            return CreateQuery (KUserId);
        }

        public string GetUserId ()
        {
            return Getter (UserIdKey ());
        }

        public bool SetUserId (string userId)
        {
            return Setter (UserIdKey (), userId);
        }
        #endregion

        public string GetIdentifierForVendor ()
        {
            return Getter (CreateQuery (KIdentifierForVendor));
        }

        public bool SetIdentifierForVendor (string ident)
        {
            return Setter (CreateQuery (KIdentifierForVendor), ident);
        }

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

        private string Getter (string query, bool errorIfMissing = false)
        {
            var r = Prefs.GetString(query, null);
            if (null != r) {
                return DecryptString (r);
            } else {
                if (errorIfMissing) {
                    throw new Exception (string.Format ("Missing entry for {0}", query));
                }
                return null;
            }
        }

        public bool Setter (string query, string value)
        {
            if (null == value) {
                throw new Exception ("Null string passed");
            }
            var enc = EncryptString (value);
            // Make sure we encrypted properly
            var dec = DecryptString (enc);
            NcAssert.True (value == dec);

            var editor = Prefs.Edit ();
            editor.PutString(query, enc);
            editor.Commit();
            return true;
        }

        public bool Deleter (string query)
        {
            var editor = Prefs.Edit ();
            editor.Remove (query);
            editor.Commit ();
            return true;
        }
        #endregion

        #region PrefsKey
        private ISecretKey PrefsKey;
        private ISecretKey PrefsMacKey;
        private Mac PrefsMac;
        private Cipher AesCipher;
        const string KPrefsKeyKey = "PreferencesKey";
        const string KPrefsMACKey = "PreferencesHMAC";
        const int AES_IV_LEN = 16; // 128 bits, i.e. AES block len
        const int SHA256_LEN = 32;
        const int AES_KEY_LEN = 32; // 256 bits

        private void GetPrefsKey ()
        {
            if (null == PrefsKey) {
                Deleter (KPrefsKeyKey); // DEBUG
                var r = Prefs.GetString (KPrefsKeyKey, null);
                if (null == r) {
                    var editor = Prefs.Edit ();
                    editor.PutString (KPrefsKeyKey, RSAEncryptKey (MakeAES256Key ()));
                    editor.Commit ();
                    r = Prefs.GetString (KPrefsKeyKey, null);
                    NcAssert.True (null != r);
                }
                PrefsKey = (ISecretKey)RSADecryptKey (r);
                NcAssert.True (PrefsKey.GetEncoded ().Length == AES_KEY_LEN);
            }
            if (null == PrefsMacKey) {
                Deleter (KPrefsMACKey); // DEBUG
                var r = Prefs.GetString (KPrefsMACKey, null);
                if (null == r) {
                    var editor = Prefs.Edit ();
                    editor.PutString (KPrefsMACKey, RSAEncryptKey (MakeAES256Key ()));
                    editor.Commit ();
                    r = Prefs.GetString (KPrefsMACKey, null);
                    NcAssert.True (null != r);
                }
                PrefsMacKey = (ISecretKey)RSADecryptKey (r);
                NcAssert.True (PrefsMacKey.GetEncoded ().Length == AES_KEY_LEN);
            }
            AesCipher = Cipher.GetInstance ("AES/CBC/PKCS5Padding", "BC");
            PrefsMac = Mac.GetInstance("HmacSHA256");
        }

        private IKey MakeAES256Key ()
        {
            byte[] raw = new byte[32];
            new SecureRandom ().NextBytes (raw);
            return new SecretKeySpec (raw, "AES");
        }

        private string DecryptString (string encryptedTextB64)
        {
            byte[] encryptedPackage = Convert.FromBase64String (encryptedTextB64);
            if (encryptedPackage[0] != (byte)0) {
                throw new Exception (string.Format ("WRONG version {0}", encryptedPackage [0]));
            }

            byte[] iv = new byte[AES_IV_LEN];
            byte[] hmac = new byte[SHA256_LEN];
            int enc_len = encryptedPackage.Length - (1 + AES_IV_LEN + SHA256_LEN); // +1 for version
            byte[] encData = new byte[enc_len];

            int offset = 1; // skip version
            Array.Copy (encryptedPackage, offset, iv, 0, AES_IV_LEN);
            offset += AES_IV_LEN;
            Array.Copy (encryptedPackage, offset, hmac, 0, SHA256_LEN);
            offset += SHA256_LEN;
            Array.Copy (encryptedPackage, offset, encData, 0, enc_len);
            offset += enc_len;

            PrefsMac.Init (PrefsMacKey);
            PrefsMac.Reset ();
            byte[] computedHmac = PrefsMac.DoFinal (encData);
            if (!FixedTimeCompare(computedHmac, hmac)) {
                throw new Exception ("HMAC FAILED");
            }

            var ips = new IvParameterSpec (iv);
            AesCipher.Init (CipherMode.DecryptMode, PrefsKey, ips);
            byte[] decryptedData = AesCipher.DoFinal (encData);

            return Encoding.UTF8.GetString (decryptedData);
        }

        private string EncryptString (string data)
        {
            byte[] iv = new byte[AES_IV_LEN];
            new SecureRandom ().NextBytes (iv);
            var ips = new IvParameterSpec (iv);

            AesCipher.Init (CipherMode.EncryptMode, PrefsKey, ips);
            byte[] encrypted = AesCipher.DoFinal (Encoding.UTF8.GetBytes (data));

            PrefsMac.Init (PrefsMacKey);
            PrefsMac.Reset ();
            byte[] hmac = PrefsMac.DoFinal (encrypted);
            NcAssert.True (hmac.Length == SHA256_LEN);

            var encPackage = new MemoryStream();
            encPackage.WriteByte ((byte)0); // version number 0
            encPackage.Write (iv, 0, AES_IV_LEN);
            encPackage.Write (hmac, 0, SHA256_LEN);
            encPackage.Write (encrypted, 0, encrypted.Length);
            int encLen = (int)encPackage.Length;
            encPackage.Close ();
            byte[] encryptedData = new byte[encLen];
            Array.Copy (encPackage.GetBuffer (), encryptedData, encLen);
            return Convert.ToBase64String (encryptedData);
        }

        private bool FixedTimeCompare (byte[] a, byte[] b)
        {
            int result = a.Length ^ b.Length;
            for (var i=0; i<a.Length && i<b.Length; i++) {
                result |= a[i] ^ b[i];
            }
            return result == 0;
        }

        public static string ByteArrayToString(byte[] ba)
        {
            string hex = BitConverter.ToString(ba);
            return string.Format ("({0}):{1}", ba.Length, hex.Replace ("-", ""));
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
