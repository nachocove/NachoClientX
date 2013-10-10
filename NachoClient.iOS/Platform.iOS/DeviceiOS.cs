using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

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

        [DllImport("__Internal")]
        private static extern string nacho_sysctlbyname (string name);
        [DllImport("__Internal")]
        private static extern uint nacho_is_simulator ();

        private static string Platform () {
            return nacho_sysctlbyname ("hw.machine");
        }
        private static string Build () {
            return nacho_sysctlbyname ("kern.osversion");
        }


        public bool IsSimulator () {
            return (0 == nacho_is_simulator ()) ? false : true;
        }
        public string Model () {
            return Platform().Replace (',', 'C');
        }
        public string Type () {
            return Model ().Split (null)[0];
        }
        public string Identity() {
            // FIXME - hard wired for the moment. iOS Mail App uses 'Appl' + serial number: ApplF17K1P5BF8H4.
            // We can get the serial number, but it may not be kosher w/Apple. If we cant use serial number, 
            // then use either dev or adv uuid replacement.
            // https://github.com/erica/iOS-6-Cookbook
            return "ApplF17K1P5BF8H3";
        }
        public string Os () {
            if (IsSimulator ()) {
                return "iOS 6.1.4 10B350";
            }
            return "iOS " + UIDevice.CurrentDevice.SystemVersion + ' ' + Build ();
        }
        public string OsLanguage () {
            // FIXME - we will need to update (a) to handle locale, (b) to take into account our bundle (possibly).
            return "en-us";
            //return NSUserDefaults.StandardUserDefaults.StringForKey ("AppleLanguages");
        }
        public string FriendlyName () {
            return UIDevice.CurrentDevice.Name;
        }
        public string UserAgent () {
            if (IsSimulator ()) {
                return "Apple-iPhone5C2/1002.350";
            }
            var rawBuild = Build ();
            string letter = Regex.Match (rawBuild, "[A-Z]").Value;
            string[] sides = Regex.Split (rawBuild, "[A-Z]");
            var lhs = ((Convert.ToInt32 (sides [0]) * 100) + 
                       Encoding.ASCII.GetBytes (letter) [0] -
                       Encoding.ASCII.GetBytes ("A") [0] + 1).ToString ();
            return "Apple-" + Type () + "/" + lhs + "." + sides [1];
        }
	}
}

