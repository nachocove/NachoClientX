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
        private const string KUserId = "UserId2";
        private const string KDeviceId = "DeviceId";
        private const string KLogSalt = "LogSalt";

        /*
         * For better or worse...
         * Key chain is searched using account and service. We keep account constant as KDefaultAccount.
         * We store passwords using the account id, eg "5" as the service.
         * We store subsequent account-specific values using the account id and a string, eg "5:RefreshToken".
         * Global (non-account specific) values use unformatted strings that don't start with a number, 
         * eg "IdentifierForVendor".
         */
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
            return CreateQuery (string.Format ("{0}:{1}", handle, subKey));
        }

        private SecRecord CreateQuery (int handle)
        {
            return CreateQuery (handle.ToString ());
        }

        public string GetPassword (int handle)
        {
            var data = Getter (CreateQuery (handle), errorIfMissing: true);
            return StringFromNSData (data);
        }

        public bool SetPassword (int handle, string password)
        {
            return Setter (CreateQuery (handle), NSData.FromString (password));
        }

        public bool DeletePassword (int handle)
        {
            return Deleter (CreateQuery (handle));
        }

        private string GetStringForKey (string key)
        {
            var data = Getter (CreateQuery (key));
            return StringFromNSData (data);
        }

        private bool SetStringForKey (string key, string value,
                                     SecAccessible accessible = SecAccessible.AfterFirstUnlockThisDeviceOnly)
        {
            return Setter (CreateQuery (key), NSData.FromString (value), accessible);
        }

        public string GetLogSalt (int handle)
        {
            var data = Getter (CreateQuery (handle, KLogSalt), errorIfMissing: true);
            return StringFromNSData (data);
        }

        public bool SetLogSalt (int handle, string logSalt)
        {
            return Setter (CreateQuery (handle, KLogSalt), NSData.FromString (logSalt));
        }

        public bool DeleteLogSalt (int handle)
        {
            return Deleter (CreateQuery (handle, KLogSalt));
        }

        public string GetIdentifierForVendor ()
        {
            return GetStringForKey (KIdentifierForVendor);
        }

        public bool SetIdentifierForVendor (string ident)
        {
            return SetStringForKey (KIdentifierForVendor, ident, accessible: SecAccessible.AlwaysThisDeviceOnly);
        }

        public string GetUserId ()
        {
            return GetStringForKey (KUserId);
        }

        public bool SetUserId (string userId)
        {
            return SetStringForKey (KUserId, userId);
        }

        public string GetDeviceId ()
        {
            return GetStringForKey (KDeviceId);

        }

        public bool SetDeviceId (string deviceId)
        {
            return SetStringForKey (KDeviceId, deviceId);
        }

        public bool SetAccessToken (int handle, string token)
        {
            return Setter (CreateQuery (handle, KAccessToken), NSData.FromString (token));
        }

        public string GetAccessToken (int handle)
        {
            var data = Getter (CreateQuery (handle, KAccessToken), errorIfMissing: true);
            return StringFromNSData (data);
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
            var data = Getter (CreateQuery (handle, KRefreshToken), errorIfMissing: true);
            return StringFromNSData (data);
        }

        public bool DeleteRefreshToken (int handle)
        {
            return Deleter (CreateQuery (handle, KRefreshToken));
        }

        // Shared implementations below.
        private string StringFromNSData (NSData data)
        {
            try {
                // XAMMIT. 
                // Sometimes NSData.ToString would return System.Runtime.Remoting.Messaging.AsyncResult.
                return null == data ? null : System.Text.Encoding.UTF8.GetString (data.ToArray ());
            } catch (ArgumentNullException) {
                // XAMMIT. 
                // Sometimes NSData.ToString throws ArgumentNullException.
                Log.Error (Log.LOG_SYS, "StringFromNSData: ArgumentNullException");
                return null;
            }
        }

        /// <summary>
        /// The number of times we'll retry getting the value from the keychain in case of a failure.
        /// </summary>
        const int KSecKeyChainGetFailRetry = 5;

        private NSData Getter (SecRecord query, bool errorIfMissing = false)
        {
            SecStatusCode res = default (SecStatusCode);
            for (var i = 0; i < KSecKeyChainGetFailRetry; i++) {
                var match = SecKeyChain.QueryAsRecord (query, out res);
                if (SecStatusCode.Success == res) {
                    if (null == match.ValueData || 0 == match.ValueData.Length) {
                        // TODO should this also throw KeychainItemNotFoundException?
                        Log.Error (Log.LOG_SYS, "Getter: query={{{0}}} returned ValueData of null/(length==0)", DumpQuery (query));
                    }
                    return match.ValueData;
                } else {
                    if (errorIfMissing || SecStatusCode.ItemNotFound != res) {
                        Log.Error (Log.LOG_SYS, "Getter: Error: {0} query={{{1}}} iter={2}", res, DumpQuery (query), i);
                    }
                    if (!errorIfMissing) {
                        break;
                    }
                }
            }
            if (errorIfMissing) {
                throw new KeychainItemNotFoundException (string.Format ("{0}: {1}", DumpQuery (query), res));
            }
            return null;
        }

        /// <summary>
        /// Turns a SecRecord into a string for logging.
        /// 
        /// NOTE: This will dump ALL FIELDS. Should only be used for queries, not responses, as
        /// responses may contain passwords
        /// </summary>
        /// <returns>The query.</returns>
        /// <param name="query">Query.</param>
        private string DumpQuery (SecRecord query)
        {
            // make sure we don't ever use this dumper for SecKeyClass.Private or SecKeyClass.Symmetric keys.
            // If we need this, we'll want to write a more restrictive dumper. This one
            // dumps EVERYTHING.
            NcAssert.True (query.KeyClass == SecKeyClass.Invalid || query.KeyClass == SecKeyClass.Public);
            string str = string.Format ("{0} ", query);
            var qDict = query.ToDictionary ();
            foreach (var x in qDict) {
                str += string.Format ("{0}={1} ", x.Key, x.Value);
            }
            return str;
        }

        private bool Setter (SecRecord query, NSData value,
                             SecAccessible accessible = SecAccessible.AfterFirstUnlockThisDeviceOnly)
        {
            SecStatusCode res;
            var match = SecKeyChain.QueryAsRecord (query, out res);
            if (SecStatusCode.Success == res) {
                match.ValueData = value;
                res = SecKeyChain.Update (query, match);
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "Setter: SecKeyChain.Remove returned {0}:{1}", res.ToString (), DumpQuery (query));
                    return false;
                }
            } else {
                query.ValueData = value;
                query.Accessible = accessible;
                res = SecKeyChain.Add (query);
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "Setter: SecKeyChain.Add returned {0}:{1}", res.ToString (), DumpQuery (query));
                    return false;
                }
            }
            return true;
        }

        private bool Deleter (SecRecord query, bool errorIfMissing = false)
        {
            SecStatusCode res;
            SecKeyChain.QueryAsRecord (query, out res);
            if (SecStatusCode.Success == res) {
                res = SecKeyChain.Remove (query);
                if (SecStatusCode.Success != res) {
                    Log.Error (Log.LOG_SYS, "Deleter: SecKeyChain.Remove returned {0}:{1}", res.ToString (), DumpQuery (query));
                    return false;
                }
            } else if (errorIfMissing) {
                Log.Error (Log.LOG_SYS, "Deleter: SecKeyChain.Delete returned {0}:{1}", res.ToString (), DumpQuery (query));
                return false;
            }
            return true;
        }
    }
}
