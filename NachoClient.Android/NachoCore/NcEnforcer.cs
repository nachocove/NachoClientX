//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
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
            // FIXME - this is unimplemented.
            return Xml.Provision.PolicyReqStatusCode.Success_1;
        }

        public bool Wipe (McAccount account)
        {
            // FIXME - this is unimplemented.
            return true;
        }
    }
}

