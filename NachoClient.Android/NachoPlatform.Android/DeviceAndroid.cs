using System;
using Android.App;
using Android.Nfc;
using Android.Content;
using Android.Provider;
using Android.Runtime;
using Android.OS;
using Android.Text.Format;
using Android.Views;

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
        public string Type () {
            // NOTE: The native email client uses "Android". The NitroDesk client uses "Touchdown".
            return "Android";
        }
        public string Identity() {
            // NOTE: The native email client uses "android1325419235512".
            // The NitroDesk client uses "49515649525250545154575557495751".
            return "android" + Settings.Secure.AndroidId;
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
    }
}

