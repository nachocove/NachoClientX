//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.ActiveSync;
using NachoCore.Model;

namespace NachoCore
{
    public class NcEnforcer
    {
        private static volatile NcEnforcer instance;
        private static object syncRoot = new Object();

        private NcEnforcer () {}

        public static NcEnforcer Instance
        {
            get 
            {
                if (instance == null) 
                {
                    lock (syncRoot)
                    {
                        if (instance == null) 
                            instance = new NcEnforcer ();
                    }
                }
                return instance;
            }
        }

        public Xml.Provision.PolicyReqStatusCode Compliance (McAccount account)
        {
            // By design, we always say we are compliant.
            return Xml.Provision.PolicyReqStatusCode.Success_1;
        }

        public bool Wipe (McAccount account, string url, string protoVersion, bool testing = false)
        {
            NcAccountHandler.Instance.RemoveAccount (stopStartServices : !testing);
            // we have initiated remove account. This will go thru even if the app is stopped/restarted.
            return true;
        }
    }
}

