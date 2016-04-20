using System;
using SQLite;
using Android.Content;
using Android.OS;
using NachoClient.AndroidClient;
using Android.Telephony;
using System.Security.Cryptography;
using Portable.Text;
using System.Linq;
using Android.Bluetooth;
using System.Globalization;
using Android.Provider;

namespace NachoPlatform
{
    public sealed class Device : IPlatformDevice
    {
        private static volatile Device instance;
        private static object syncRoot = new Object();

        private Device () {}

        public static Device Instance
        {
            get 
            {
                if (instance == null) 
                {
                    lock (syncRoot) 
                    {
                        if (instance == null) 
                            instance = new Device ();
                    }
                }
                return instance;
            }
        }

        public string Model () {
            return Build.Model;
        }

        public string UserAgentModel () {
            return Build.Model;
        }

        public string OsType () {
            return "Android";
        }

        public string OsVersion () {
            return Build.VERSION.Release;
        }

        public string Type () {
            // NOTE: The native email client uses "Android". The NitroDesk client uses "Touchdown".
            return "Android";
        }

        private string _Identity;
        public string Identity() {
            // NOTE: The native email client uses "android1325419235512".
            // The NitroDesk client uses "49515649525250545154575557495751".
            if (null == _Identity) {
                _Identity = Keychain.Instance.GetDeviceId ();
                if (null == _Identity) {
                    _Identity = GetOrCreateDeviceId ();
                    Keychain.Instance.SetDeviceId (_Identity);
                }
            }
            return _Identity;
        }

        private string GetOrCreateDeviceId ()
        {
            var DeviceIdHashInput = Guid.NewGuid ().ToString ().Replace ("-", "").ToUpperInvariant ();
            string hashStr;
            using (SHA256Managed sha256 = new SHA256Managed()) {
                var hash = sha256.ComputeHash (Encoding.UTF8.GetBytes (MainApplication.Instance.ApplicationInfo.PackageName+DeviceIdHashInput));
                hashStr = string.Format("Ncho{0}", string.Join("", hash.Select(b => b.ToString(IsSimulator () ? "x2" : "X2")).ToArray()));
            }
            // We current truncate the string to 28 bytes, but we could by spec go for 32 bytes.
            return hashStr.Substring (0, IsSimulator () ? 16 : 28);
        }

        public string Os () {
            return "Android " + Build.VERSION.Release;
        }
        public OsCode BaseOs () {
            return OsCode.Android;
        }
        public string OsLanguage () {
            return CultureInfo.CurrentCulture.Name; // e.g. 'en-US', 'de-DE', etc
        }
        public string FriendlyName () {
            // get user-settable device name.
            BluetoothAdapter myDevice = BluetoothAdapter.DefaultAdapter;
            if (null != myDevice && !string.IsNullOrEmpty (myDevice.Name)) {
                return myDevice.Name;
            }
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(string.Format ("{0} {1}", Build.Brand, Build.Model));
        }
        public string UserAgent () {
            // NOTE: The native client uses "Android/4.3-EAS-1.3".
            // The NitroDesk client uses "TouchDown(MSRPC)/8.1.00052/".
            return "Android/4.3-EAS-1.3";
        }
        public bool IsSimulator ()
        {
            // http://stackoverflow.com/questions/2799097/how-can-i-detect-when-an-android-application-is-running-in-the-emulator
            // Fingerprint is something like this: Verizon/d2vzw/d2vzw:4.4.2/KOT49H/I535VRUDNE1:user/release-keys
            return Build.Fingerprint.StartsWith ("generic");
        }
        public static void SQLite3ErrorCallback (IntPtr pArg, int iErrCode, string zMsg)
        {
            ReverseSQLite3ErrorCallback (iErrCode, zMsg);
        }

        private static Action<int, string> ReverseSQLite3ErrorCallback;

        public SQLite3.ErrorLogCallback GetSQLite3ErrorCallback (Action<int, string> action)
        {
            ReverseSQLite3ErrorCallback = action;
            return SQLite3ErrorCallback;
        }
    }
}

