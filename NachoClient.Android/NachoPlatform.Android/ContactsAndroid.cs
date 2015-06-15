//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoPlatform
{
    public sealed class Contacts : IPlatformContacts
    {
        private const int SchemaRev = 0;
        private static volatile Contacts instance;
        private static object syncRoot = new Object ();

        public event EventHandler ChangeIndicator;

        private Contacts ()
        {
        }

        public static Contacts Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new Contacts ();
                    }
                }
                return instance;
            }
        }

        public void AskForPermission (Action<bool> result)
        {
            // Should never be called on Android.
        }

        public IEnumerable<PlatformContactRecord> GetContacts ()
        {
            return null;
        }

        public NcResult Add (McContact contact)
        {
            return NcResult.Error ("Android Contacts.Add not yet implemented.");
        }

        public NcResult Delete (string serverId)
        {
            return NcResult.Error ("Android Contacts.Delete not yet implemented.");
        }

        public NcResult Change (McContact contact)
        {
            return NcResult.Error ("Android Contacts.Change not yet implement.");
        }

        public bool AuthorizationStatus {
            get {
                // TODO Not yet implemented.
                return false;
            }
        }
    }
}

