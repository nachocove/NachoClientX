//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Security;
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

        private const string KDefaultAccount = "device";

        public bool HasKeychain ()
        {
            return true;
        }

        private SecRecord CreateQuery (int handle)
        {
            // http://stackoverflow.com/questions/4891562/ios-keychain-services-only-specific-values-allowed-for-ksecattrgeneric-key/5008417#5008417
            return new SecRecord (SecKind.GenericPassword) {
                Account = KDefaultAccount,
                Service = handle.ToString (),
            };
        }

        public string GetPassword (int handle)
        {
            SecStatusCode res;
            var match = SecKeyChain.QueryAsRecord (CreateQuery (handle), out res);
            if (SecStatusCode.Success == res) {
                return match.ValueData.ToString ();
            } else {
                Log.Error (Log.LOG_SYS, "GetPassword: SecKeyChain.QueryAsRecord returned {0}", res.ToString ());
                return null;
            }
        }

        public bool SetPassword (int handle, string password)
        {
            SecStatusCode res;
            var match = SecKeyChain.QueryAsRecord (CreateQuery (handle), out res);
            if (SecStatusCode.Success == res) {
                match.ValueData = NSData.FromString (password);
                res = SecKeyChain.Update (CreateQuery (handle), match);
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "SetPassword: SecKeyChain.Remove returned {0}", res.ToString ());
                    return false;
                }
            } else {
                var insert = CreateQuery (handle);
                insert.ValueData = NSData.FromString (password);
                insert.Accessible = SecAccessible.AfterFirstUnlockThisDeviceOnly;
                res = SecKeyChain.Add (insert);
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "SetPassword: SecKeyChain.Add returned {0}", res.ToString ());
                    return false;
                }
            }
            return true;
        }
    }
}

