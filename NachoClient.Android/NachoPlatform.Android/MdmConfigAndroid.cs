//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Content;
using NachoClient.AndroidClient;
using NachoCore;
using NachoCore.Utils;

namespace NachoPlatform
{
    public class MdmConfig : IPlatformMdmConfig
    {
        private static volatile MdmConfig instance;
        private static object syncRoot = new Object ();

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

        public void ExtractValues ()
        {
            if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.Lollipop) {
                return;
            }

            var myRestrictionsMgr = (RestrictionsManager)MainApplication.Context.GetSystemService (Context.RestrictionsService);
            ExtractValuesFromRestrictions (myRestrictionsMgr.ApplicationRestrictions);
        }

        public void ExtractValuesFromRestrictions (Android.OS.Bundle appRestrictions)
        {
            try {
                NcMdmConfig.Instance.SetValues ((mdmConfig) => {
                    mdmConfig.Host = appRestrictions.GetString ("AppServiceHost");
                    var port = (uint)appRestrictions.GetInt ("AppServicePort");
                    if (port != 0) {
                        mdmConfig.Port = port;
                    }
                    mdmConfig.Username = appRestrictions.GetString ("UserName");
                    mdmConfig.Domain = appRestrictions.GetString ("UserDomain");
                    mdmConfig.EmailAddr = appRestrictions.GetString ("UserEmail");
                    mdmConfig.BrandingName = appRestrictions.GetString ("BrandingName");
                    mdmConfig.BrandingLogoUrl = appRestrictions.GetString ("BrandingLogo");
                });
            } catch (ArgumentException ex) {
                Log.Error (Log.LOG_SYS, "Could not get app config: {0}", ex);
            }
        }

        [BroadcastReceiver (Enabled = true)]
        [Android.App.IntentFilter (new[] { Intent.ActionApplicationRestrictionsChanged })]
        class RestrictionsChangedBroadcastReceiver : BroadcastReceiver
        {
            public override void OnReceive (Context context, Intent intent)
            {
                MainApplication.OneTimeStartup ("RestrictionsChangedBroadcastReceiverNotificationActivity");
                if (intent.Action == Intent.ActionApplicationRestrictionsChanged) {
                    var myRestrictionsMgr = (RestrictionsManager)context.GetSystemService (Context.RestrictionsService);
                    MdmConfig.Instance.ExtractValuesFromRestrictions (myRestrictionsMgr.ApplicationRestrictions);
                }
            }
        }
    }
}
