//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
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
        public string Module { set; get; }
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

        public static void Set (int accountId, string module, string key, string value)
        {
            var exists = NcModel.Instance.Db.Table<McMutables> ().Where (x =>
                x.AccountId == accountId &&
                x.Key == key && 
                x.Module == module).SingleOrDefault ();
            if (null == exists) {
                exists = new McMutables (accountId);
                exists.Module = module;
                exists.Key = key;
                exists.Value = value;
                exists.Insert ();
            } else {
                exists.Value = value;
                exists.Update ();
            }
        }

        // TODO: Eliminate "Bool" suffix and just use overloads.
        // TODO: Put 0/1 <=> true/false in one place.
        public static bool GetOrCreateBool (int accountId, string module, string key, bool defaultValue)
        {
            string boolString = (defaultValue) ? "1" : "0";
            var stringRetval = GetOrCreate (accountId, module, key, boolString);
            return ("1" == stringRetval) ? true : false;
        }

        public static string GetOrCreate (int accountId, string module, string key, string defaultValue)
        {
            var exists = Get (accountId, module, key);
            if (null != exists) {
                return exists;
            } else {
                Set (accountId, module, key, defaultValue);
                return defaultValue;
            }
        }

        public static string Get (int accountId, string module, string key)
        {
            var exists = NcModel.Instance.Db.Table<McMutables> ().Where (x =>
                x.AccountId == accountId &&
                x.Key == key && 
                x.Module == module).SingleOrDefault ();
            if (null == exists) {
                return null;
            }
            return exists.Value;
        }

        public static void Delete (int accountId, string module, string key)
        {
            var exists = NcModel.Instance.Db.Table<McMutables> ().Where (x =>
                x.AccountId == accountId &&
                x.Key == key && 
                x.Module == module).SingleOrDefault ();
            if (null != exists) {
                exists.Delete ();
            }
        }

        public static bool GetBool (int accountId, string module, string key)
        {
            var value = Get (accountId, module, key);
            if (value == "1") {
                return true;
            } else {
                return false;
            }
        }

        public static void SetBool (int accountId, string module, string key, bool value){
            if (true == value) {
                Set (accountId, module, key, "1");
            } else {
                Set (accountId, module, key, "0");
            }
        }
    }
}

