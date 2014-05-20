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

        public void AddString (string key, string value)
        {
            Add (new NSString (key), new NSString (value));
        }

        public void AddInteger (string key, int value)
        {
            Add (new NSString (key), NSNumber.FromInt32 (value));
        }

        public void AddData (string key, byte[] data)
        {
            Add (new NSString (key), NSData.FromArray (data));
        }

        public void Add (NSObject key, NSObject value)
        {
            NachoAssert.True (null != key);
            NachoAssert.True (null != value);
            Dict.Add (key, value);
        }

        public void AddDate (string key, DateTime date)
        {
            if (false == EpochInitialized) {
                EpochInitialized = true;
                Epoch = new DateTime (1970, 1, 1, 0, 0, 0);
            }
            double elapsedTime = date.Subtract (Epoch).TotalSeconds;
            NSString nsKey = new NSString (key);
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
        }
            
        public void SendEvent (TelemetryEvent tEvent)
        {
            NSMutableDictionaryEx dict = new NSMutableDictionaryEx ();

            dict.AddString ("client", CurrentUser.Username);
            dict.AddDate ("timestamp", tEvent.Timestamp);
            dict.AddString ("os_type", Device.Instance.OsType ());
            dict.AddString ("os_version", Device.Instance.OsVersion ());
            dict.AddString ("device_model", Device.Instance.Model ());
            dict.AddString ("build_version", BuildInfo.Version);

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
                    NachoAssert.True (false);
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
                    NachoAssert.True (false);
                    break;
                }
                dict.AddData ("wbxml", tEvent.Wbxml);
            }
            PFObject anEvent = PFObject.ObjectWithClassName ("Events", dict.GetDictionary ());
            anEvent.ACL = DefaultAcl;
            anEvent.Save ();
        }
    }
}

