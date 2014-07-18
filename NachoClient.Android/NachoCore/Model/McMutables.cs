//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Concurrent;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McMutables : McAbstrObject
    {
        /* These are key-value mutable settings to be used by any module/class.
         * For some settings, there will be distinct objects in the db (e.g. McServer).
         * This is for the random stuff - like sendmail-timeout-seconds.
         * Expect that a settings UI or telemetry may modify (some of) these values down the line.
         * You should read from the DB each time you need the value.
         * 
         * These values are NOT account-specific.
         */

        // Use a class name as the Module name to avoid conflict.
        public string Module { set; get; }
        public string Key { set; get; }
        public string Value { set; get; }

        public static event EventHandler ResetYourValues;

        public static void Reset ()
        {
            if (null != ResetYourValues) {
                ResetYourValues (null, EventArgs.Empty);
            } else {
                Log.Error (Log.LOG_SYS, "Nobody registered to receive McMutables Reset event.");
            }
        }

        public static void Set (string module, string key, string value)
        {
            var exists = NcModel.Instance.Db.Table<McMutables> ().Where (x => x.Key == key && 
                x.Module == module).SingleOrDefault ();
            if (null == exists) {
                exists = new McMutables ();
                exists.Module = module;
                exists.Key = key;
                exists.Value = value;
                exists.Insert ();
            } else {
                exists.Value = value;
                exists.Update ();
            }
        }

        public static string GetOrCreate (string module, string key, string defaultValue)
        {
            var exists = Get (module, key);
            if (null != exists) {
                return exists;
            } else {
                Set (module, key, defaultValue);
                return defaultValue;
            }
        }

        public static string Get (string module, string key)
        {
            var exists = NcModel.Instance.Db.Table<McMutables> ().Where (x => x.Key == key && 
                x.Module == module).SingleOrDefault ();
            if (null == exists) {
                return null;
            }
            return exists.Value;
        }

        public static void Delete (string module, string key)
        {
            var exists = NcModel.Instance.Db.Table<McMutables> ().Where (x => x.Key == key && 
                x.Module == module).SingleOrDefault ();
            if (null != exists) {
                exists.Delete ();
            }
        }
    }
}

