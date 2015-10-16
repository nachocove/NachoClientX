using System;
using SQLite;
using Android.Content;
using Android.Provider;
using Android.OS;
using NachoClient.AndroidClient;
using Android.Telephony;
using System.Security.Cryptography;
using Portable.Text;
using System.Linq;

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
                if (Keychain.Instance.HasKeychain ()) {
                    _Identity = Keychain.Instance.GetDeviceId ();
                    if (null == _Identity) {
                        _Identity = GetOrCreateDeviceId ();
                        Keychain.Instance.SetDeviceId (_Identity);
                    }
                } else {
                    _Identity = GetOrCreateDeviceId ();
                }
            }
            return _Identity;
        }

        private string GetOrCreateDeviceId ()
        {
            string deviceId = null;
            var telephony = (TelephonyManager)MainApplication.Instance.ApplicationContext.GetSystemService (Context.TelephonyService);
            if (null != telephony) {
                if (!string.IsNullOrEmpty (telephony.DeviceId)) {
                    using (SHA1Managed sha1 = new SHA1Managed()) {
                        var hash = (new SHA1Managed ()).ComputeHash (Encoding.UTF8.GetBytes (telephony.DeviceId));
                        deviceId = "Ncho" + string.Join("", hash.Select(b => b.ToString("X2")).ToArray());
                    }
                }
            }
            if (string.IsNullOrEmpty (deviceId)) {
                deviceId = string.Format ("Ncho{0}", Guid.NewGuid ().ToString ().Replace ("-", "").ToUpperInvariant ());
            }
            return deviceId.Substring (0, 32);
        }

        public string Os () {
            return "Android " + Build.VERSION.Release;
        }
        public OsCode BaseOs () {
            return OsCode.Android;
        }
        public string OsLanguage () {
            // FIXME - we will need to update (a) to handle locale, (b) to take into account our bundle (possibly).
            return "en-us";
        }
        public string FriendlyName () {
            // FIXME. Need to get user-settable device name.
            return Identity ();
        }
        public string UserAgent () {
            // NOTE: The native client uses "Android/4.3-EAS-1.3".
            // The NitroDesk client uses "TouchDown(MSRPC)/8.1.00052/".
            return "Android/4.3-EAS-1.3";
        }
        public bool IsSimulator ()
        {
            return false;
        }
        public bool Wipe (string username, string password, string url, string protoVersion)
        {
            // FIXME.
            return false;
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

