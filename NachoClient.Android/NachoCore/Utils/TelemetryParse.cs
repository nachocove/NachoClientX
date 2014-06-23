//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
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
        // The solution is to reate a NSMutableString and set the constant
        // string to that object. NSMutableString cannot use that optimization
        // because it is mutable. This way we can create a NSString that has
        // a positive ref. count.
        private NSString SafeNSString (string key)
        {
            NSMutableString nsString = new NSMutableString ();
            nsString.SetString (new NSString (key));
            return nsString;
        }

        public void AddString (string key, string value)
        {
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
            NcAssert.True (0 < key.RetainCount);
            NcAssert.True (0 < value.RetainCount);
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
            Parse.Initialize ("krLhpUrvcoKXTNx8LWG8ESR1zQGRzei6vttCmwFm",
                              "224PvsNa7ABberuxpthahr0YIp4742VZqRgw6zT1");

            PFUser.EnableAutomaticUser ();
            CurrentUser = PFUser.CurrentUser ();
            CurrentUser.IncrementKey ("RunCount");
            CurrentUser.Save ();

            DefaultAcl = PFACL.ACL ();
            DefaultAcl.SetReadAccessForRoleWithName (true, "Ops");

            while (null == CurrentUser.Username) {
                Console.WriteLine ("waiting for Parse...");
                Thread.Sleep (5000);
            }
        }
           
        public static string dummy = "";
        private string XammitNonConstIfy (string consty)
        {
            return string.Format ("{0}{1}", consty, dummy);
        }

        public void SendEvent (TelemetryEvent tEvent)
        {
            NSMutableDictionaryEx dict = new NSMutableDictionaryEx ();

            dict.AddString (XammitNonConstIfy ("client"), CurrentUser.Username);
            dict.AddDate (XammitNonConstIfy ("timestamp"), tEvent.Timestamp);
            dict.AddString (XammitNonConstIfy ("os_type"), Device.Instance.OsType ());
            dict.AddString (XammitNonConstIfy ("os_version"), Device.Instance.OsVersion ());
            dict.AddString (XammitNonConstIfy ("device_model"), Device.Instance.Model ());
            dict.AddString (XammitNonConstIfy ("build_version"), BuildInfo.Version);

            if (tEvent.IsLogEvent ()) {
                switch (tEvent.Type) {
                case TelemetryEventType.ERROR:
                    dict.AddString (XammitNonConstIfy ("event_type"), XammitNonConstIfy ("ERROR"));
                    break;
                case TelemetryEventType.WARN:
                    dict.AddString (XammitNonConstIfy ("event_type"), XammitNonConstIfy ("WARN"));
                    break;
                case TelemetryEventType.INFO:
                    dict.AddString (XammitNonConstIfy ("event_type"), XammitNonConstIfy ("INFO"));
                    break;
                case TelemetryEventType.DEBUG:
                    dict.AddString (XammitNonConstIfy ("event_type"), XammitNonConstIfy ("DEBUG"));
                    break;
                default:
                    NcAssert.True (false);
                    break;
                }
                dict.AddString (XammitNonConstIfy ("message"), tEvent.Message);
            } else if (tEvent.IsWbxmlEvent ()) {
                switch (tEvent.Type) {
                case TelemetryEventType.WBXML_REQUEST:
                    dict.AddString (XammitNonConstIfy ("event_type"), XammitNonConstIfy ("WBXML_REQUEST"));
                    break;
                case TelemetryEventType.WBXML_RESPONSE:
                    dict.AddString (XammitNonConstIfy ("event_type"), XammitNonConstIfy ("WBXML_RESPONSE"));
                    break;
                default:
                    NcAssert.True (false);
                    break;
                }
                dict.AddData (XammitNonConstIfy ("wbxml"), tEvent.Wbxml);
            } else if (tEvent.IsCounterEvent ()) {
                dict.AddString (XammitNonConstIfy ("event_type"), XammitNonConstIfy ("COUNTER"));
                dict.AddString (XammitNonConstIfy ("counter_name"), tEvent.CounterName);
                dict.AddInteger (XammitNonConstIfy ("count") , tEvent.Count);
                dict.AddDate (XammitNonConstIfy ("counter_start"), tEvent.CounterStart);
                dict.AddDate (XammitNonConstIfy ("counter_end"), tEvent.CounterEnd);
            } else if (tEvent.IsCaptureEvent()) {
                dict.AddString (XammitNonConstIfy ("event_type"), XammitNonConstIfy ("CAPTURE"));
                dict.AddString (XammitNonConstIfy ("capture_name"), tEvent.CaptureName);
                dict.AddInteger (XammitNonConstIfy ("count"), tEvent.Count);
                dict.AddInteger (XammitNonConstIfy ("average"), tEvent.Average);
                dict.AddInteger (XammitNonConstIfy ("min"), tEvent.Min);
                dict.AddInteger (XammitNonConstIfy ("max"), tEvent.Max);
                dict.AddInteger (XammitNonConstIfy ("stddev"), tEvent.StdDev);
            } else {
                NcAssert.True (false);
            }
            PFObject anEvent = PFObject.ObjectWithClassName ("Events", dict.GetDictionary ());
            anEvent.ACL = DefaultAcl;
            anEvent.Save ();
        }
    }
}

