//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoPlatform;

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
            var cred = McCred.QueryByAccountId<McCred> (account.Id).SingleOrDefault ();
            if (Keychain.Instance.HasKeychain ()) {
                Keychain.Instance.DeletePassword (cred.Id);
            }
            NcAccountHandler.Instance.RemoveAccount (stopStartServices : !testing);
            // TODO: need to remove the testing flag check for tests
            if (!testing) {
                return Device.Instance.Wipe (cred.Username, cred.GetPassword (), url, protoVersion);
            } else {
                return true;
            }
        }
    }
}

