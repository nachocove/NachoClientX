//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using NachoCore;

namespace NachoPlatform
{
    public class MdmConfig : IPlatformMdmConfig
    {
        private static volatile MdmConfig instance;
        private static object syncRoot = new Object ();
        private NSDictionary Defaults;

        public static MdmConfig Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new MdmConfig ();
                    }
                }
                return instance;
            }
        }

        private MdmConfig ()
        {
        }

        private string GetStringAsString (string key)
        {
            var stringValue = Defaults.ObjectForKey (new NSString (key)) as NSString;
            return (null == stringValue) ? null : stringValue.ToString ();
        }

        private uint? GetUintAsUint (string key)
        {
            var uintValue = Defaults.ObjectForKey (new NSString (key)) as NSNumber;
            return (null == uintValue) ? null : (uint?)uintValue.UInt32Value;
        }

        private uint? GetStringAsUint (string key)
        {
            var stringValue = GetStringAsString (key);
            if (null == stringValue) {
                var uintValue = GetUintAsUint (key);
                if (null == uintValue) {
                    return null;
                }
                return uintValue;
            }
            try {
                return uint.Parse (stringValue);
            } catch {
                return null;
            }
        }

        public void ExtractValues ()
        {
            Defaults = NSUserDefaults.StandardUserDefaults.DictionaryForKey ("com.apple.configuration.managed");
            if (null == Defaults) {
                NcMdmConfig.Instance.ResetValues ();
                return;
            }
            NcMdmConfig.Instance.SetValues ((mdmConfig) => {
                mdmConfig.Host = GetStringAsString ("AppServiceHost");
                mdmConfig.Port = GetStringAsUint ("AppServicePort");
                mdmConfig.Username = GetStringAsString ("UserName");
                mdmConfig.Domain = GetStringAsString ("UserDomain");
                mdmConfig.EmailAddr = GetStringAsString ("UserEmail");
                mdmConfig.BrandingName = GetStringAsString ("BrandingName");
                mdmConfig.BrandingLogoUrl = GetStringAsString ("BrandingLogo");
            });
        }
    }
}
