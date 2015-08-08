//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using UIKit;
using Security;
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
        private const string KLogSalt = "LogSalt";

        public bool HasKeychain ()
        {
            return true;
        }

        private SecRecord CreateQuery (string service)
        {
            // http://stackoverflow.com/questions/4891562/ios-keychain-services-only-specific-values-allowed-for-ksecattrgeneric-key/5008417#5008417
            return new SecRecord (SecKind.GenericPassword) {
                Account = KDefaultAccount,
                Service = service,
            };
        }

        private SecRecord CreateQuery (int handle)
        {
            return CreateQuery (handle.ToString ());
        }

        public string GetPassword (int handle)
        {
            SecStatusCode res;
            var match = SecKeyChain.QueryAsRecord (CreateQuery (handle), out res);
            if (SecStatusCode.Success == res) {
                var iData = match.ValueData;
                if ((null == iData) || (iData.Length == 0)) {
                    Log.Error (Log.LOG_SYS, "GetPassword: SecKeyChain.QueryAsRecord returned ValueData of null/(length==0) for handle {0}", handle);
                    return null;
                }
                var bytes = iData.ToArray ();
                var password = System.Text.Encoding.UTF8.GetString (bytes);
                return password;
                // XAMMIT. 
                // Sometimes NSData.ToString would return System.Runtime.Remoting.Messaging.AsyncResult.
                // return match.ValueData.ToString ();
            } else {
                Log.Error (Log.LOG_SYS, "GetPassword: SecKeyChain.QueryAsRecord returned status {0} for handle {1}", res.ToString (), handle);
                // TODO : remove this before Appstore
                if (match != null) {
                    Log.Error (Log.LOG_SYS, "GetPassword: SecKeyChain.QueryAsRecord returned value {0} for handle {1}", match.ValueData.ToString (), handle);
                }
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
                    Log.Error (Log.LOG_SYS, "SetPassword: SecKeyChain.Update returned {0} for handle {1}", res.ToString (), handle);
                    return false;
                }
            } else {
                var insert = CreateQuery (handle);
                insert.ValueData = NSData.FromString (password);
                insert.Accessible = SecAccessible.AfterFirstUnlockThisDeviceOnly;
                res = SecKeyChain.Add (insert);
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "SetPassword: SecKeyChain.Add returned {0} for handle {1}", res.ToString (), handle);
                    return false;
                }
            }
            return true;
        }

        public bool DeletePassword (int handle)
        {
            SecStatusCode res;
            SecKeyChain.QueryAsRecord (CreateQuery (handle), out res);
            if (SecStatusCode.Success == res) {
                res = SecKeyChain.Remove (CreateQuery (handle));
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "DeletePassword: SecKeyChain.Remove returned {0} for handle {1}", res.ToString (), handle);
                    return false;
                }
            } else { 
                Log.Error (Log.LOG_SYS, "DeletePassword: SecKeyChain.Delete returned {0} for handle {1}", res.ToString (), handle);
                return false;
            }
            return true;        
        }


        private SecRecord CreateQuery (int handle, string subKey)
        {
            return CreateQuery (handle.ToString () + ":" + subKey);
        }

        public string GetLogSalt (int handle)
        {
            SecStatusCode res;
            var match = SecKeyChain.QueryAsRecord (CreateQuery (handle, KLogSalt), out res);
            if (SecStatusCode.Success == res) {
                var iData = match.ValueData;
                if ((null == iData) || (iData.Length == 0)) {
                    Log.Error (Log.LOG_SYS, "GetLogSalt: SecKeyChain.QueryAsRecord returned ValueData of null/(length==0) for handle {0}", handle);
                    return null;
                }
                var bytes = iData.ToArray ();
                var logSalt = System.Text.Encoding.UTF8.GetString (bytes);
                return logSalt;
                // XAMMIT. 
                // Sometimes NSData.ToString would return System.Runtime.Remoting.Messaging.AsyncResult.
                // return match.ValueData.ToString ();
            } else {
                Log.Error (Log.LOG_SYS, "GetLogSalt: SecKeyChain.QueryAsRecord returned status {0} for handle {1}", res.ToString (), handle);
                // TODO : remove this before Appstore
                if (match != null) {
                    Log.Error (Log.LOG_SYS, "GetLogSalt: SecKeyChain.QueryAsRecord returned value {0} for handle {1}", match.ValueData.ToString (), handle);
                }
                return null;
            }
        }

        public bool SetLogSalt (int handle, string logSalt)
        {
            SecStatusCode res;
            var match = SecKeyChain.QueryAsRecord (CreateQuery (handle, KLogSalt), out res);
            if (SecStatusCode.Success == res) {
                match.ValueData = NSData.FromString (logSalt);
                res = SecKeyChain.Update (CreateQuery (handle, KLogSalt), match);
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "SetLogSalt: SecKeyChain.Update returned {0} for handle {1}", res.ToString (), handle);
                    return false;
                }
            } else {
                var insert = CreateQuery (handle, KLogSalt);
                insert.ValueData = NSData.FromString (logSalt);
                insert.Accessible = SecAccessible.AfterFirstUnlockThisDeviceOnly;
                res = SecKeyChain.Add (insert);
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "SetLogSalt: SecKeyChain.Add returned {0} for handle {1}", res.ToString (), handle);
                    return false;
                }
            }
            return true;
        }

        public bool DeleteLogSalt (int handle)
        {
            SecStatusCode res;
            SecKeyChain.QueryAsRecord (CreateQuery (handle, KLogSalt), out res);
            if (SecStatusCode.Success == res) {
                res = SecKeyChain.Remove (CreateQuery (handle, KLogSalt));
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "DeleteLogSalt: SecKeyChain.Remove returned {0} for handle {1}", res.ToString (), handle);
                    return false;
                }
            } else { 
                Log.Error (Log.LOG_SYS, "DeleteLogSalt: SecKeyChain.Delete returned {0} for handle {1}", res.ToString (), handle);
                return false;
            }
            return true;        
        }

        private const string KIdentifierForVendor = "IdentifierForVendor";

        public string GetIdentifierForVendor ()
        {
            SecStatusCode res;
            var match = SecKeyChain.QueryAsRecord (CreateQuery (KIdentifierForVendor), out res);
            if (SecStatusCode.Success == res) {
                var iData = match.ValueData;
                var bytes = iData.ToArray ();
                var ident = System.Text.Encoding.UTF8.GetString (bytes);
                return ident;
                // XAMMIT. 
                // Sometimes NSData.ToString would return System.Runtime.Remoting.Messaging.AsyncResult.
                // return match.ValueData.ToString ();
            } else {
                if (SecStatusCode.ItemNotFound != res) {
                    Log.Error (Log.LOG_SYS, "GetIdentifierForVendor: SecKeyChain.QueryAsRecord returned {0}", res.ToString ());
                }
                return null;
            }
        }

        public bool SetIdentifierForVendor (string ident)
        {
            SecStatusCode res;
            var match = SecKeyChain.QueryAsRecord (CreateQuery (KIdentifierForVendor), out res);
            if (SecStatusCode.Success == res) {
                match.ValueData = NSData.FromString (ident);
                res = SecKeyChain.Update (CreateQuery (KIdentifierForVendor), match);
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "SetIdentifierForVendor: SecKeyChain.Remove returned {0}", res.ToString ());
                    return false;
                }
            } else {
                var insert = CreateQuery (KIdentifierForVendor);
                insert.ValueData = NSData.FromString (ident);
                insert.Accessible = SecAccessible.AlwaysThisDeviceOnly;
                res = SecKeyChain.Add (insert);
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "SetIdentifierForVendor: SecKeyChain.Add returned {0}", res.ToString ());
                    return false;
                }
            }
            return true;
        }
    }
}

