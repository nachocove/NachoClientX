//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    public class NcMdmConfig
    {
        private static volatile NcMdmConfig instance;
        private static object syncRoot = new object ();

        public bool IsPopulated {
            get {
                return !String.IsNullOrEmpty(Host) || Port.HasValue || !String.IsNullOrEmpty(Username) || !String.IsNullOrEmpty(Domain) || !String.IsNullOrEmpty(EmailAddr) || !String.IsNullOrEmpty(BrandingName) || !String.IsNullOrEmpty(BrandingLogoUrl);
            }
        }
        public bool IsValid { get; private set; }
        // Begin MDM Values. All types must be nullable (null => not set).
        public string Host { get; set; }
        public uint? Port { get; set; }
        public string Username;
        public string Domain;
        public string EmailAddr;
        public string BrandingName;
        public string BrandingLogoUrl;
        // End MDM Values

        public static NcMdmConfig Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new NcMdmConfig ();
                        }
                    }
                }
                return instance; 
            }
        }

        private NcMdmConfig ()
        {
        }

        /// <summary>
        /// Sets the values for the singleton.
        /// </summary>
        /// <param name="setter">caller's function to set the relevant values.</param>
        public void SetValues (Action<NcMdmConfig> setter)
        {
            setter (this);
            Validate ();
            NcApplication.Instance.InvokeStatusIndEventInfo (ConstMcAccount.NotAccountSpecific, 
                NcResult.SubKindEnum.Info_MdmConfigMayHaveChanged);
        }

        /// <summary>
        /// Resets the values.
        /// </summary>
        public void ResetValues ()
        {
            Host = null;
            Port = null;
            Username = null;
            Domain = null;
            EmailAddr = null;
            BrandingName = null;
            IsValid = false;
            BrandingLogoUrl = null;
            NcApplication.Instance.InvokeStatusIndEventInfo (ConstMcAccount.NotAccountSpecific, 
                NcResult.SubKindEnum.Info_MdmConfigMayHaveChanged);
        }

        private void Validate ()
        {
            IsValid = true;
            if (String.IsNullOrEmpty (Host) && Port.HasValue) {
                IsValid = false;
                Log.Info (Log.LOG_UTILS, "NcMdmConfig invalid config: port without server");
            }
            if (!String.IsNullOrEmpty (EmailAddr) && !EmailHelper.IsValidEmail (EmailAddr)) {
                IsValid = false;
                Log.Info (Log.LOG_UTILS, "NcMdmConfig invalid config: email address does not validate: {0}", EmailAddr);
            }
            if (!String.IsNullOrEmpty (Host)){
                var result = EmailHelper.IsValidServer (Host);
                if (result != EmailHelper.ParseServerWhyEnum.Success_0) {
                    IsValid = false;
                    Log.Info (Log.LOG_UTILS, "NcMdmConfig invalid config: server does not validate: {0} {1}", result, Host);
                }
            }
            if (!string.IsNullOrEmpty (BrandingLogoUrl)) {
                try {
                    // Analysis disable once ObjectCreationAsStatement
                    new Uri (BrandingLogoUrl);
                } catch (UriFormatException ex) {
                    IsValid = false;
                    Log.Info (Log.LOG_UTILS, "NcMdmConfig invalid config: BrandingLogoUrl: {0} {1}", BrandingLogoUrl, ex.Message);
                }
            }
        }
    }
}
