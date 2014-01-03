//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.ActiveSync;
using NachoCore.Model;

namespace NachoCore
{
    public class Enforcer
    {
        private static volatile Enforcer instance;
        private static object syncRoot = new Object();

        private Enforcer () {}

        public static Enforcer Instance
        {
            get 
            {
                if (instance == null) 
                {
                    lock (syncRoot)
                    {
                        if (instance == null) 
                            instance = new Enforcer ();
                    }
                }
                return instance;
            }
        }

        public Xml.Provision.PolicyReqStatusCode Compliance (McAccount account)
        {
            // FIXME - this is unimplemented.
            return Xml.Provision.PolicyReqStatusCode.Success;
        }

        public bool Wipe (McAccount account)
        {
            // FIXME - this is unimplemented.
            return true;
        }
    }
}

