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

        public bool IsPopulated { get; private set; }
        public bool IsValid { get; private set; }
        // Begin MDM Values. All types must be nullable (null => not set).
        public string Host { get; set; }
        public uint? Port { get; set; }
        public string Username;
        public string Domain;
        public string EmailAddr;
        public string BrandingName;
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
            IsPopulated = !String.IsNullOrEmpty(Host) || Port.HasValue || !String.IsNullOrEmpty(Username) || !String.IsNullOrEmpty(Domain) || !String.IsNullOrEmpty(EmailAddr) || !String.IsNullOrEmpty(BrandingName);
            IsValid = true;
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
            IsPopulated = false;
            IsValid = false;
            NcApplication.Instance.InvokeStatusIndEventInfo (ConstMcAccount.NotAccountSpecific, 
                NcResult.SubKindEnum.Info_MdmConfigMayHaveChanged);
        }
    }
}
