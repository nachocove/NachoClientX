//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using MonoTouch.Foundation;
using ParseBinding;
using NachoPlatform;
using NachoClient.Build;

namespace NachoCore.Utils
{
    public class NSMutableDictionaryEx
    {
        private static bool EpochInitialized;
        private static ConcurrentDictionary<string, NSString> OneConstNSString = new ConcurrentDictionary<string, NSString> ();
        private static DateTime Epoch;

        private NSMutableDictionary Dict;

        public NSMutableDictionaryEx()
        {
            Dict = new NSMutableDictionary ();
        }

        // This is a really fugly solution to what seems to be a
        // MonoTouch.Foundation.NSString implementation issue. When creating
        // a NSString object initialized with a constant string. It seems that
        // it creates with a RetainCount of -1. It seems that NSString is
        // smart enough to know that it is initialized from a constant NSString.
        // In that case, it creates a NSString with a ref. count of MAX_UINT
        // which becomes -1 when casted into int.
        //
        // See: http://stackoverflow.com/questions/1390334/nsstring-retain-count
        //
        // This exposes Xamarin bug:
        // https://bugzilla.xamarin.com/show_bug.cgi?id=7723

        private NSString SafeNSString (string key)
        {
            NcAssert.NotNull (key);
            var reTries = 10;
            while (0 < reTries--) {
                try {
                    return OneConstNSString.GetOrAdd (key, new NSString (key));
                } catch (ArgumentNullException ex) {
                    Log.Warn (Log.LOG_SYS, "Xamarin NSString bug caught: {0}.", ex);
                    if (0 >= reTries) {
                        Log.Error (Log.LOG_SYS, "Xamarin NSString bug caught 10 times in a row.");
                        throw;
                    }
                }
            }
            return null;
        }

        public void AddString (string key, string value)
        {
            NcAssert.NotNull (key);
            NcAssert.NotNull (value);
            Add (SafeNSString (key), SafeNSString(value));
        }

        public void AddInteger (string key, long value)
        {
            Add (SafeNSString (key), NSNumber.FromInt64 (value));
        }

        public void AddData (string key, byte[] data)
        {
            Add (SafeNSString (key), NSData.FromArray (data));
        }

        public void Add (NSObject key, NSObject value)
        {
            NcAssert.True (null != key);
            NcAssert.True (null != value);
            Dict.Add (key, value);
        }

        public void AddDate (string key, DateTime date)
        {
            if (false == EpochInitialized) {
                EpochInitialized = true;
                Epoch = new DateTime (1970, 1, 1, 0, 0, 0);
            }
            double elapsedTime = date.Subtract (Epoch).TotalSeconds;
            NSString nsKey = SafeNSString (key);
            NSDate nsDate = NSDate.FromTimeIntervalSince1970 (elapsedTime);
            Add (nsKey, nsDate);
        }

        public NSDictionary GetDictionary () {
            return (NSDictionary)Dict;
        }
    }

    public class TelemetryBEParse : ITelemetryBE
    {
        private PFACL DefaultAcl;

        private PFUser CurrentUser;

        public TelemetryBEParse ()  : base()
        {
            Parse.Initialize (BuildInfo.ParseAppId, BuildInfo.ParseApiKey);

            PFUser.EnableAutomaticUser ();
            CurrentUser = PFUser.CurrentUser ();
            CurrentUser.IncrementKey ("RunCount");
            CurrentUser.Save ();

            DefaultAcl = PFACL.CreateACL ();
            DefaultAcl.SetReadAccessForRoleWithName (true, "Ops");
        }

        public bool IsUseable ()
        {
            return null != CurrentUser.Username;
        }

        public string GetUserName ()
        {
            return IsUseable () ? CurrentUser.Username : null;
        }

        public static string dummy = "";

        public bool SendEvent (TelemetryEvent tEvent)
        {
            // I think we are running in this problem:
            // http://forums.xamarin.com/discussion/6404/memory-leaks-and-nsautoreleasepool
            // Wrapping this around auto release pool does seem to help to reduce leak.
            using (NSAutoreleasePool autoreleasePool = new NSAutoreleasePool ()) {
                NSMutableDictionaryEx dict = new NSMutableDictionaryEx ();

                dict.AddString ("client", CurrentUser.Username);
                dict.AddDate ("timestamp", tEvent.Timestamp);
                dict.AddString ("os_type", Device.Instance.OsType ());
                dict.AddString ("os_version", Device.Instance.OsVersion ());
                dict.AddString ("device_model", Device.Instance.Model ());
                dict.AddString ("build_version",
                    String.Format("{0} (build {1})", BuildInfo.Version, BuildInfo.BuildNumber));

                if (tEvent.IsLogEvent ()) {
                    switch (tEvent.Type) {
                    case TelemetryEventType.ERROR:
                        dict.AddString ("event_type", "ERROR");
                        break;
                    case TelemetryEventType.WARN:
                        dict.AddString ("event_type", "WARN");
                        break;
                    case TelemetryEventType.INFO:
                        dict.AddString ("event_type", "INFO");
                        break;
                    case TelemetryEventType.DEBUG:
                        dict.AddString ("event_type", "DEBUG");
                        break;
                    default:
                        NcAssert.True (false);
                        break;
                    }
                    dict.AddString ("message", tEvent.Message);
                } else if (tEvent.IsWbxmlEvent ()) {
                    switch (tEvent.Type) {
                    case TelemetryEventType.WBXML_REQUEST:
                        dict.AddString ("event_type", "WBXML_REQUEST");
                        break;
                    case TelemetryEventType.WBXML_RESPONSE:
                        dict.AddString ("event_type", "WBXML_RESPONSE");
                        break;
                    default:
                        NcAssert.True (false);
                        break;
                    }
                    dict.AddData ("wbxml", tEvent.Wbxml);
                } else if (tEvent.IsCounterEvent ()) {
                    dict.AddString ("event_type", "COUNTER");
                    dict.AddString ("counter_name", tEvent.CounterName);
                    dict.AddInteger ("count", tEvent.Count);
                    dict.AddDate ("counter_start", tEvent.CounterStart);
                    dict.AddDate ("counter_end", tEvent.CounterEnd);
                } else if (tEvent.IsCaptureEvent ()) {
                    dict.AddString ("event_type", "CAPTURE");
                    dict.AddString ("capture_name", tEvent.CaptureName);
                    dict.AddInteger ("count", tEvent.Count);
                    dict.AddInteger ("average", tEvent.Average);
                    dict.AddInteger ("min", tEvent.Min);
                    dict.AddInteger ("max", tEvent.Max);
                    dict.AddInteger ("stddev", tEvent.StdDev);
                } else if (tEvent.IsUiEvent ()) {
                    dict.AddString ("event_type", "UI");
                    dict.AddString ("ui_type", tEvent.UiType);
                    if (null == tEvent.UiObject) {
                        dict.AddString ("ui_object", "(unknown)");
                    } else { 
                        dict.AddString ("ui_object", tEvent.UiObject);
                    }
                    switch (tEvent.UiType) {
                    case TelemetryEvent.UIDATEPICKER:
                        dict.AddString ("ui_string", tEvent.UiString);
                        break;
                    case TelemetryEvent.UIPAGECONTROL:
                        dict.AddInteger ("ui_integer", (int)tEvent.UiLong);
                        break;
                    case TelemetryEvent.UISEGMENTEDCONTROL:
                        dict.AddInteger ("ui_integer", (int)tEvent.UiLong);
                        break;
                    case TelemetryEvent.UISWITCH:
                        dict.AddString ("ui_string", tEvent.UiString);
                        break;
                    case TelemetryEvent.UIVIEWCONTROLER:
                        dict.AddString ("ui_string", tEvent.UiString);
                        break;
                    }
                } else if (tEvent.IsSupportEvent ()) {
                    dict.AddString ("event_type", "SUPPORT");
                    dict.AddString ("support", tEvent.Support);
                } else {
                    NcAssert.True (false);
                }
                PFObject anEvent = PFObject.ObjectWithClassName ("Events", dict.GetDictionary ());
                anEvent.ACL = DefaultAcl;
                bool succeed = anEvent.Save ();
                anEvent.Dispose ();

                return succeed;
            }
        }
    }
}

