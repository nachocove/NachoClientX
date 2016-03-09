//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McMutables : McAbstrObjectPerAcc
    {
        /* These are key-value mutable settings to be used by any module/class.
         * For some settings, there will be distinct objects in the db (e.g. McServer).
         * This is for the random stuff - like sendmail-timeout-seconds.
         * Expect that a settings UI or telemetry may modify (some of) these values down the line.
         * You should read from the DB each time you need the value.
         * 
         * Account-independent values must use the device account Id. WARNING: these are not subject to wipe.
         */

        // Use a class name as the Module name to avoid conflict.
        [Indexed]
        public string Module { set; get; }

        [Indexed]
        public string Key { set; get; }

        public string Value { set; get; }

        public McMutables ()
        {
            // For LINQ.
        }

        public McMutables (int accountId) : this ()
        {
            AccountId = accountId;
        }

        public static event EventHandler ResetYourValues;

        public static void Reset ()
        {
            if (null != ResetYourValues) {
                ResetYourValues (null, EventArgs.Empty);
            } else {
                Log.Error (Log.LOG_SYS, "Nobody registered to receive McMutables Reset event.");
            }
        }

        private static McMutables GetExistingItem (int accountId, string module, string key)
        {
            try {

                return NcModel.Instance.Db.Table<McMutables> ().Where (
                    x => x.AccountId == accountId && x.Key == key && x.Module == module
                ).SingleOrDefault ();

            } catch (InvalidOperationException) {
                McMutables result = null;
                NcModel.Instance.RunInTransaction (() => {
                    var allMatching = NcModel.Instance.Db.Table<McMutables> ().Where (
                                          x => x.AccountId == accountId && x.Key == key && x.Module == module
                                      );
                    Log.Error (Log.LOG_DB, "The database has {3} McMutables items for [{0}, {1}, {2}]. One will be picked at random and the others will be deleted.",
                        accountId, module, key, allMatching.Count ());
                    foreach (var item in allMatching) {
                        if (null == result) {
                            result = item;
                        } else {
                            item.Delete ();
                        }
                    }
                });
                return result;
            }
        }

        public static void Set (int accountId, string module, string key, string value)
        {
            NcModel.Instance.RunInTransaction (() => {
                var existing = GetExistingItem (accountId, module, key);
                if (null == existing) {
                    new McMutables (accountId) {
                        Module = module,
                        Key = key,
                        Value = value,
                    }.Insert ();
                } else {
                    existing.Value = value;
                    existing.Update ();
                }
            });
        }

        // TODO: Eliminate "Bool" suffix and just use overloads.
        // TODO: Put 0/1 <=> true/false in one place.
        public static bool GetOrCreateBool (int accountId, string module, string key, bool defaultValue)
        {
            string boolString = (defaultValue) ? "1" : "0";
            var stringRetval = GetOrCreate (accountId, module, key, boolString);
            return "1" == stringRetval;
        }

        public static string GetOrCreate (int accountId, string module, string key, string defaultValue)
        {
            string result = null;
            var existing = GetExistingItem (accountId, module, key);
            if (null != existing) {
                result = existing.Value;
            } else {
                NcModel.Instance.RunInTransaction (() => {
                    existing = GetExistingItem (accountId, module, key);
                    if (null != existing) {
                        result = existing.Value;
                    } else {
                        new McMutables (accountId) {
                            Module = module,
                            Key = key,
                            Value = defaultValue,
                        }.Insert ();
                        result = defaultValue;
                    }
                });
            }
            return result;
        }

        public static string Get (int accountId, string module, string key)
        {
            var existing = GetExistingItem (accountId, module, key);
            if (null == existing) {
                return null;
            }
            return existing.Value;
        }

        public static List<McMutables> Get (int accountId, string module)
        {
            return NcModel.Instance.Db.Table<McMutables> ().Where (
                x => x.AccountId == accountId && x.Module == module
            ).ToList ();
        }

        public static void Delete (int accountId, string module, string key)
        {
            var existing = GetExistingItem (accountId, module, key);
            if (null != existing) {
                existing.Delete ();
            }
        }

        public static bool GetBool (int accountId, string module, string key)
        {
            var value = Get (accountId, module, key);
            return "1" == value;
        }

        public static bool GetBoolDefault (int accountId, string module, string key, bool defaultValue)
        {
            var value = Get (accountId, module, key);
            if (null == value) {
                return defaultValue;
            } else {
                return "1" == value;
            }
        }

        public static void SetBool (int accountId, string module, string key, bool value)
        {
            Set (accountId, module, key, value ? "1" : "0");
        }

        public static int GetInt (int accountId, string module, string key, int defaultValue)
        {
            int value;
            if (int.TryParse (Get (accountId, module, key), out value)) {
                return value;
            } else {
                return defaultValue;
            }
        }

        public static int GetOrCreateInt (int accountId, string module, string key, int defaultValue)
        {
            string valueString = GetOrCreate (accountId, module, key, defaultValue.ToString ());
            int value;
            if (int.TryParse (valueString, out value)) {
                return value;
            } else {
                // There is an existing value for this key, but it is not a number.
                // The correct behavior in this case is unclear.
                return defaultValue;
            }
        }

        public static void SetInt (int accountId, string module, string key, int value)
        {
            Set (accountId, module, key, value.ToString ());
        }
    }
}

