using Foundation;
using UIKit;
using ObjCRuntime;
using CoreFoundation;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SQLite;
using NachoCore.Utils;

namespace NachoPlatform
{
    public sealed class Device : IPlatformDevice
    {
        private static volatile Device instance;
        private static object syncRoot = new Object ();

        private Device ()
        {
        }

        public static Device Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new Device ();
                    }
                }
                return instance;
            }
        }

        [DllImport ("__Internal")]
        private static extern void nacho_sysctlbyname (StringBuilder dest, uint limit, string domain);

        [DllImport ("__Internal")]
        private static extern uint nacho_is_simulator ();

        [DllImport ("__Internal")]
        private static extern void nacho_macaddr (StringBuilder dest, uint limit);

        [DllImport ("__Internal")]
        private static extern bool nacho_set_handlers_and_boom (string home);

        [DllImport ("__Internal")]
        public static extern IntPtr NSHomeDirectory();

        private string Platform ()
        {
            if (IsSimulator ()) {
                return (IsPhone ()) ? "iPhone5,2" : "iPad3,1";
            } else {
                StringBuilder sb = new StringBuilder (256);
                nacho_sysctlbyname (sb, (uint)sb.Capacity, "hw.machine");
                return sb.ToString ();
            }
        }

        private static string Build ()
        {
            StringBuilder sb = new StringBuilder (256);
            nacho_sysctlbyname (sb, (uint)sb.Capacity, "kern.osversion");
            return sb.ToString ();
        }

        public bool IsSimulator ()
        {
            return (0 == nacho_is_simulator ()) ? false : true;
        }

        private static bool IsPhone ()
        {
            return UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone;
        }
            
        public string UserAgentModel ()
        {
            return Platform ().Replace (',', 'C');
        }

        public string Model ()
        {
            return Platform ();
        }

        public string Type ()
        {
            if (IsPhone ()) {
                return "iPhone";
            } else {
                return "iPad";
            }
        }

        private string UserAgentType ()
        {
            return UserAgentModel ().Split (null) [0];
        }

        static string _IdentityMemoize = null;

        public string Identity ()
        {
            if (null != _IdentityMemoize) {
                return _IdentityMemoize;
            }
            if (IsSimulator ()) {
                StringBuilder sb = new StringBuilder (256);
                nacho_macaddr (sb, (uint)sb.Capacity);
                _IdentityMemoize = "Ncho" + sb.ToString ();
                return _IdentityMemoize;
            }
            //NOTE: iOS Mail App uses 'Appl' + serial number. Eg: ApplF17K1P5BF8H4.
            // We can get the serial number, but it may not be kosher w/Apple. If we cant use serial number, 
            // then use either dev or adv uuid replacement.
            // https://github.com/erica/iOS-6-Cookbook
            // For now, use 'Ncho' + the identifierForVendor, which can change on delete & re-install.
            // We current truncate the string to 28 bytes, but we could by spec go for 32 bytes.
            var bunId = NSBundle.MainBundle.BundleIdentifier;
            var suffix = "";
            var IdForVendorChars = 24;
            if ("com.nachocove.nachomail.beta" != bunId) {
                var BunIdHashChars = 4;
                IdForVendorChars -= BunIdHashChars;
                suffix = BitConverter.ToString (new SHA256Managed ().ComputeHash (Encoding.UTF8.GetBytes (bunId))).Replace ("-", "").Substring (0, BunIdHashChars);
            }
            var ident = Keychain.Instance.GetIdentifierForVendor ();
            if (null == ident) {
                ident = UIDevice.CurrentDevice.IdentifierForVendor.AsString ();
            }
            NcAssert.NotNull (ident, "Could not create ident.");
            // Set this before any logs, since otherwise we loop/recurse forever
            _IdentityMemoize = "Ncho" + ident.Replace ('-', 'X').Substring (0, IdForVendorChars) + suffix;
            if (!Keychain.Instance.SetIdentifierForVendor (ident)) {
                Log.Error (Log.LOG_SYS, "Identity: unable to save IdentifierForVendor in KeyChain.");
            }
            return _IdentityMemoize;
        }

        public string OsType ()
        {
            return UIDevice.CurrentDevice.SystemName;
        }

        public string OsVersion ()
        {
            return UIDevice.CurrentDevice.SystemVersion;
        }

        public string Os ()
        {
            // MINOR TODO - we could make the OS rev match that which is being simulated.
            if (IsSimulator ()) {
                return "iOS 6.1.4 10B350";
            }
            return "iOS " + UIDevice.CurrentDevice.SystemVersion + ' ' + Build ();
        }

        public OsCode BaseOs ()
        {
            return OsCode.iOS;
        }

        public string OsLanguage ()
        {
            // TODO - we will need to update (a) to handle locale, (b) to take into account our bundle (possibly).
            return "en";
            //return NSUserDefaults.StandardUserDefaults.StringForKey ("AppleLanguages");
        }

        public string FriendlyName ()
        {
            return UIDevice.CurrentDevice.Name;
        }

        public string UserAgent ()
        {
            if (IsSimulator ()) {
                return string.Format ("Apple-{0}/1002.350", UserAgentModel ());
            }
            var rawBuild = Build ();
            string letter = Regex.Match (rawBuild, "[A-Z]").Value;
            string[] sides = Regex.Split (rawBuild, "[A-Z]");
            var lhs = ((Convert.ToInt32 (sides [0]) * 100) +
                      Encoding.ASCII.GetBytes (letter) [0] -
                      Encoding.ASCII.GetBytes ("A") [0] + 1).ToString ();
            return "Apple-" + UserAgentType () + "/" + lhs + "." + sides [1];
        }

        [MonoPInvokeCallback (typeof (SQLite3.ErrorLogCallback))]
        public static void SQLite3ErrorCallback (IntPtr pArg, int iErrCode, string zMsg)
        {
            if (!zMsg.Contains ("frames from WAL file")) {
                ReverseSQLite3ErrorCallback (iErrCode, zMsg);
            }
        }

        private static Action<int, string> ReverseSQLite3ErrorCallback;

        public SQLite3.ErrorLogCallback GetSQLite3ErrorCallback (Action<int, string> action)
        {
            ReverseSQLite3ErrorCallback = action;
            return SQLite3ErrorCallback;
        }
    }
}
