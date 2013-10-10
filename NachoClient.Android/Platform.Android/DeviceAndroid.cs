using System;

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

        public bool IsSimulator () {
            return true;
        }
        public string Model () {
            return "iPhone5C2";
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
            return "iOS 6.1.4 10B350";
        }
        public string OsLanguage () {
            // FIXME - we will need to update (a) to handle locale, (b) to take into account our bundle (possibly).
            return "en-us";
            //return NSUserDefaults.StandardUserDefaults.StringForKey ("AppleLanguages");
        }
        public string FriendlyName () {
            return "Moi";
        }
        public string UserAgent () {
            return "Apple-iPhone5C2/1002.350";
        }
    }
}

