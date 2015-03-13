//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoPlatform
{
    public class Keychain : IPlatformKeychain
    {
        private static volatile Keychain instance;
        private static object syncRoot = new Object ();

        public static Keychain Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new Keychain ();
                    }
                }
                return instance;
            }
        }
            
        public bool HasKeychain ()
        {
            return false;
        }

        public string GetPassword (int handle)
        {
            NcAssert.True (false);
            return null;
        }

        public bool SetPassword (int handle, string password)
        {
            NcAssert.True (false);
            return false;
        }

        public bool DeletePassword (int handle)
        {
            NcAssert.True (false);
            return false;
        }
    }
}
