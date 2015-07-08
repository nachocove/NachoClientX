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
        private const string KIdentifierForVendor = "IdentifierForVendor";
        private const string KAccessToken = "AccessToken";
        private const string KRefreshToken = "RefreshToken";
        private const string KUserId = "UserId";

        /*
         * For better or worse...
         * Key chain is searched using account and service. We keep account constant as KDefaultAccount.
         * We store passwords using the account id, eg "5" as the service.
         * We store subsequent account-specific values using the account id and a string, eg "5:RefreshToken".
         * Global (non-account specific) values use unformatted strings that don't start with a number, 
         * eg "IdentifierForVendor".
         */
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

        private SecRecord CreateQuery (int handle, string subKey)
        {
            return CreateQuery (handle.ToString () + ":" + subKey);
        }

        private SecRecord CreateQuery (int handle)
        {
            return CreateQuery (handle.ToString ());
        }

        public string GetPassword (int handle)
        {
            var data = Getter (CreateQuery (handle));
            // XAMMIT. 
            // Sometimes NSData.ToString would return System.Runtime.Remoting.Messaging.AsyncResult.
            return null == data ? null : System.Text.Encoding.UTF8.GetString (data.ToArray ());
        }

        public bool SetPassword (int handle, string password)
        {
            return Setter (CreateQuery (handle), NSData.FromString (password));
        }

        public bool DeletePassword (int handle)
        {
            return Deleter (CreateQuery (handle));
        }

        public string GetStringForKey (string key)
        {
            var data = Getter (CreateQuery (key));
            // XAMMIT. 
            // Sometimes NSData.ToString would return System.Runtime.Remoting.Messaging.AsyncResult.
            return null == data ? null : System.Text.Encoding.UTF8.GetString (data.ToArray ());
        }

        public bool SetStringForKey (string key, string value)
        {
            return Setter (CreateQuery (key), NSData.FromString (value));
        }

        public string GetIdentifierForVendor ()
        {
            return GetStringForKey (KIdentifierForVendor);
        }

        public bool SetIdentifierForVendor (string ident)
        {
            return SetStringForKey (KIdentifierForVendor, ident);
        }

        public string GetUserId ()
        {
            return GetStringForKey (KUserId);
        }

        public bool SetUserId (string userId)
        {
            return SetStringForKey (KUserId, userId);
        }

        public bool SetAccessToken (int handle, string token)
        {
            return Setter (CreateQuery (handle, KAccessToken), NSData.FromString (token));
        }

        public string GetAccessToken (int handle)
        {
            var data = Getter (CreateQuery (handle, KAccessToken));
            // XAMMIT.
            // Sometimes NSData.ToString would return System.Runtime.Remoting.Messaging.AsyncResult.
            return null == data ? null : System.Text.Encoding.UTF8.GetString (data.ToArray ());
        }

        public bool DeleteAccessToken (int handle)
        {
            return Deleter (CreateQuery (handle, KAccessToken));
        }

        public bool SetRefreshToken (int handle, string token)
        {
            return Setter (CreateQuery (handle, KRefreshToken), NSData.FromString (token));
        }

        public string GetRefreshToken (int handle)
        {
            var data = Getter (CreateQuery (handle, KRefreshToken));
            // XAMMIT. 
            // Sometimes NSData.ToString would return System.Runtime.Remoting.Messaging.AsyncResult.
            return null == data ? null : System.Text.Encoding.UTF8.GetString (data.ToArray ());
        }

        public bool DeleteRefreshToken (int handle)
        {
            return Deleter (CreateQuery (handle, KRefreshToken));
        }

        // Shared implementations below.
        private NSData Getter (SecRecord query)
        {
            SecStatusCode res;
            var match = SecKeyChain.QueryAsRecord (query, out res);
            if (SecStatusCode.Success == res) {
                if (null == match.ValueData || 0 == match.ValueData.Length) {
                    Log.Error (Log.LOG_SYS, "Getter: SecKeyChain.QueryAsRecord returned ValueData of null/(length==0)");
                }
                return match.ValueData;
            } else {
                if (SecStatusCode.ItemNotFound != res) {
                    Log.Error (Log.LOG_SYS, "Getter: SecKeyChain.QueryAsRecord returned {0}", res.ToString ());
                }
                return null;
            }
        }

        private bool Setter (SecRecord query, NSData value)
        {
            SecStatusCode res;
            var match = SecKeyChain.QueryAsRecord (query, out res);
            if (SecStatusCode.Success == res) {
                match.ValueData = value;
                res = SecKeyChain.Update (query, match);
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "Setter: SecKeyChain.Remove returned {0}", res.ToString ());
                    return false;
                }
            } else {
                query.ValueData = value;
                query.Accessible = SecAccessible.AlwaysThisDeviceOnly;
                res = SecKeyChain.Add (query);
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "Setter: SecKeyChain.Add returned {0}", res.ToString ());
                    return false;
                }
            }
            return true;
        }

        private bool Deleter (SecRecord query)
        {
            SecStatusCode res;
            SecKeyChain.QueryAsRecord (query, out res);
            if (SecStatusCode.Success == res) {
                res = SecKeyChain.Remove (query);
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "Deleter: SecKeyChain.Remove returned {0}", res.ToString ());
                    return false;
                }
            } else { 
                Log.Error (Log.LOG_SYS, "Deleter: SecKeyChain.Delete returned {0}", res.ToString ());
                return false;
            }
            return true;        
        }
    }
}
