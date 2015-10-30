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
        private bool RSAKeyGenerated = false;
        private bool PrefsKeyGenerated = false;
        private bool PrefsMacKeyGenerated = false;

        public static Keychain Instance {
            get {
                if (instance == null) {
                    bool firsttime = false;
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new Keychain ();
                            firsttime = true;
                        }
                    }
                    if (firsttime) {
                        if (instance.RSAKeyGenerated) {
                            Log.Info (Log.LOG_SYS, "KeychainAndroid: Generated new RSA Keypair in {0}ms", instance.RSAKeyGenerationTimeMilliseconds);
                        }
                        if (instance.PrefsKeyGenerated) {
                            Log.Info (Log.LOG_SYS, "KeychainAndroid: Generated new PrefsKey");
                        }
                        if (instance.PrefsMacKeyGenerated) {
                            Log.Info (Log.LOG_SYS, "KeychainAndroid: Generated new PrefsMacKey");
                        }
                    }
                }
                return instance;
            }
        }

        private Keychain ()
        {
            GetPrefsKeys ();
        }

        private const string KIdentifierForVendor = "IdentifierForVendor";
        private const string KAccessToken = "AccessToken";
        private const string KRefreshToken = "RefreshToken";
        private const string KUserId = "UserId";
        private const string KDeviceId = "DeviceId";
        private const string KLogSalt = "LogSalt";

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
            return Getter (LogSaltKey (handle));
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

        #region DeviceId
        public string DeviceId ()
        {
            return CreateQuery (KDeviceId);
        }
        public string GetDeviceId ()
        {
            return Getter (DeviceId ());
        }

        public bool SetDeviceId (string deviceId)
        {
            return Setter (DeviceId (), deviceId);
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
                try {
                    return DecryptString (r);
                } catch (KeychainDecryptionException ex) {
                    Log.Error (Log.LOG_SYS, "KeychainAndroid: Could not decrypt keychain item: {0}", ex.Message);
                    // FIXME Should we delete the entry here? It'll be useless anyway,
                    // and at least then we can create a new one for this key. But is this
                    // the right thing to do?
                    Deleter (query);
                }
            }
            if (errorIfMissing) {
                throw new KeychainItemNotFoundException (string.Format ("Missing entry for {0}", query));
            }
            return null;
        }

        public bool Setter (string query, string value)
        {
            NcAssert.True (null != value);
            var enc = EncryptString (value);
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

        #region RandomNumberGenerator
        private static System.Security.Cryptography.RandomNumberGenerator _Random;
        private void RandomBytes (byte[] bytes)
        {
            if (_Random == null) {
                _Random = new System.Security.Cryptography.RNGCryptoServiceProvider ();
            }
            _Random.GetBytes (bytes);
        }
        #endregion

        #region PrefsKey
        private IKey PrefsKey;
        private IKey PrefsMacKey;
        const string KPrefsKeyKey = "PreferencesKey";
        const string KPrefsMACKey = "PreferencesHMAC";
        const int AES_IV_LEN = 16; // 128 bits, i.e. AES block len
        const int SHA256_LEN = 32;
        const int AES_KEY_LEN = 32; // 256 bits

        private void GetPrefsKeys ()
        {
            GetKeyPair ();
            GetPrefsAESKeys ();
        }

        private void GetPrefsAESKeys ()
        {
            if (null == PrefsKey) {
                if (!Prefs.Contains (KPrefsKeyKey)) {
                    PrefsKeyGenerated = true;
                    var editor = Prefs.Edit ();
                    editor.PutString (KPrefsKeyKey, RSAEncryptKey (MakeAES256Key ()));
                    editor.Commit ();
                    NcAssert.True (Prefs.Contains(KPrefsKeyKey)); // make darn tootin' sure it's saved.
                }
                var r = Prefs.GetString (KPrefsKeyKey, null);
                PrefsKey = RSADecryptKey (r);
            }
            if (null == PrefsMacKey) {
                if (!Prefs.Contains (KPrefsMACKey)) {
                    PrefsMacKeyGenerated = true;
                    var editor = Prefs.Edit ();
                    editor.PutString (KPrefsMACKey, RSAEncryptKey (MakeAES256Key ()));
                    editor.Commit ();
                    NcAssert.True (Prefs.Contains(KPrefsMACKey)); // make darn tootin' sure it's saved.
                }
                var r = Prefs.GetString (KPrefsMACKey, null);
                PrefsMacKey = RSADecryptKey (r);
            }
        }

        private IKey MakeAES256Key ()
        {
            byte[] raw = new byte[32];
            RandomBytes (raw);
            return new SecretKeySpec (raw, "AES");
        }

        private Cipher AesCipher ()
        {
            return Cipher.GetInstance ("AES/CBC/PKCS7Padding");
        }

        private Mac AesMac()
        {
            return Mac.GetInstance ("HmacSHA256");
        }

        private string DecryptString (string encryptedTextB64)
        {
            byte[] encryptedPackage = Convert.FromBase64String (encryptedTextB64);
            if (encryptedPackage[0] != (byte)0) {
                throw new KeychainDecryptionException (string.Format ("WRONG version {0}", encryptedPackage [0]));
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

            var mac = AesMac ();
            mac.Init (PrefsMacKey);
            mac.Reset ();
            byte[] computedHmac = mac.DoFinal (encData);
            if (!FixedTimeCompare(computedHmac, hmac)) {
                throw new KeychainDecryptionException ("HMAC FAILED");
            }

            var ips = new IvParameterSpec (iv);
            var cipher = AesCipher ();
            cipher.Init (CipherMode.DecryptMode, PrefsKey, ips);
            byte[] decryptedData = cipher.DoFinal (encData);

            return Encoding.UTF8.GetString (decryptedData);
        }

        private string EncryptString (string data)
        {
            var cipher = AesCipher ();
            cipher.Init (CipherMode.EncryptMode, PrefsKey);
            byte[] encrypted = cipher.DoFinal (Encoding.UTF8.GetBytes (data));
            var iv = cipher.GetIV ();
            NcAssert.True (iv.Length == AES_IV_LEN);

            var mac = AesMac ();
            mac.Init (PrefsMacKey);
            mac.Reset ();
            byte[] hmac = mac.DoFinal (encrypted);
            NcAssert.True (hmac.Length == SHA256_LEN);

            var encPackage = new MemoryStream();
            encPackage.WriteByte ((byte)0); // version number 0
            encPackage.Write (iv, 0, iv.Length);
            encPackage.Write (hmac, 0, hmac.Length);
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
        const string KKeyStoreDefault = "AndroidKeyStore";
        const string KKeyStoreBouncyCastle = "BC";
        const string KKeyStoreOpenSSL = "AndroidOpenSSL";
        const string DefaultKeyStore = KKeyStoreDefault;

        private KeyStore getKeystore ()
        {
            var ks = KeyStore.GetInstance (DefaultKeyStore);
            ks.Load (null);
            return ks;
        }
        #endregion

        #region RSAKeyPair
        const int KeyPairSize = 2048;
        const string KDefaultKeyPair = "NachoMailDefaultKeyPair";
        private void GetKeyPair ()
        {
            using (var ks = getKeystore ()) {
                if (!ks.ContainsAlias (KDefaultKeyPair)) {
                    GenerateKeyPair ();
                }
            }
        }

        private long RSAKeyGenerationTimeMilliseconds;
        private void GenerateKeyPair ()
        {
            using (var ks = getKeystore ()) {
                NcAssert.False (ks.ContainsAlias (KDefaultKeyPair));
            }
            var st = new PlatformStopwatch ();
            st.Start ();
            Java.Util.Calendar start = Java.Util.Calendar.GetInstance (Java.Util.TimeZone.Default);
            Java.Util.Calendar end = Java.Util.Calendar.GetInstance (Java.Util.TimeZone.Default);
            end.Add (Java.Util.CalendarField.Year, 20);
            KeyPairGenerator generator = KeyPairGenerator.GetInstance("RSA", DefaultKeyStore);
            KeyPairGeneratorSpec spec = new KeyPairGeneratorSpec.Builder(MainApplication.Instance.ApplicationContext)
                .SetKeyType ("RSA")
                .SetKeySize (KeyPairSize)
                .SetAlias (KDefaultKeyPair)
                .SetSubject(new Javax.Security.Auth.X500.X500Principal("CN=NachoMail"))
                .SetSerialNumber (Java.Math.BigInteger.One)
                .SetStartDate(start.Time)
                .SetEndDate(end.Time)
                //.SetEncryptionRequired () // TODO We probably need to see if we can still use the key in the background if the device is locked.
                .Build ();
            generator.Initialize(spec);
            generator.GenerateKeyPair();
            st.Stop ();
            RSAKeyGenerated = true;
            RSAKeyGenerationTimeMilliseconds = st.ElapsedMilliseconds;
            using (var ks = getKeystore ()) {
                NcAssert.True (ks.ContainsAlias (KDefaultKeyPair)); // make sure it got saved to the keystore
            }
        }

        private Cipher RsaCipher ()
        {
            return Cipher.GetInstance ("RSA/ECB/PKCS1Padding");
        }

        private string RSAEncryptKey (IKey key)
        {
            byte[] encryptedData;
            KeyStore.PrivateKeyEntry privateKeyEntry;
            using (var ks = getKeystore ()) {
                privateKeyEntry = (KeyStore.PrivateKeyEntry)ks.GetEntry (KDefaultKeyPair, null);
            }
            var rsaCipher = RsaCipher ();
            rsaCipher.Init (CipherMode.WrapMode, privateKeyEntry.Certificate.PublicKey);
            encryptedData = rsaCipher.Wrap (key);
            return Convert.ToBase64String (encryptedData);
        }

        private IKey RSADecryptKey (string encryptedTextB64)
        {
            var bytesToDecrypt = Convert.FromBase64String (encryptedTextB64);
            IKey key;
            KeyStore.PrivateKeyEntry privateKeyEntry;
            using (var ks = getKeystore ()) {
                privateKeyEntry = (KeyStore.PrivateKeyEntry)ks.GetEntry (KDefaultKeyPair, null);
            }
            var rsaCipher = RsaCipher ();
            rsaCipher.Init (CipherMode.UnwrapMode, privateKeyEntry.PrivateKey);
            key = rsaCipher.Unwrap (bytesToDecrypt, "AES", KeyType.SecretKey);
            return key;
        }
        #endregion
    }
}
