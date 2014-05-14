//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Threading;
using MonoTouch.Foundation;
using ParseBinding;
using NachoPlatform;

namespace NachoCore.Utils
{
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

        public class KeyValueList
        {
            private List<string> Keys;
            private List<object> Values;

            private static bool EpochInitialized;

            private static DateTime Epoch;

            public KeyValueList() 
            {
                Keys = new List<string> ();
                Values = new List<object> ();
            }

            public void AddString (string key, string value)
            {
                Keys.Add (key);
                Values.Add (new NSString (value));
            }

            public void AddInteger (string key, int value)
            {
                Keys.Add (key);
                Values.Add (NSNumber.FromInt32 (value));
            }

            public void AddData (string key, byte[] data)
            {
                Keys.Add (key);
                Values.Add (NSData.FromArray (data));
            }

            public void AddDate (string key, DateTime date)
            {
                if (false == EpochInitialized) {
                    EpochInitialized = true;
                    Epoch = new DateTime (1970, 1, 1, 0, 0, 0);
                }
                Keys.Add (key);
                double elapsedTime = date.Subtract (Epoch).TotalSeconds;
                Values.Add (NSDate.FromTimeIntervalSince1970 (elapsedTime));
            }

            public NSDictionary GetDictionary ()
            {
                return NSDictionary.FromObjectsAndKeys (Values.ToArray (), Keys.ToArray ());
            }
        }

        public void SendEvent (TelemetryEvent tEvent)
        {
            KeyValueList kvList = new KeyValueList ();

            kvList.AddString ("client", CurrentUser.Username);
            kvList.AddDate ("timestamp", tEvent.Timestamp);
            kvList.AddString ("os_type", Device.Instance.OsType ());
            kvList.AddString ("os_version", Device.Instance.OsVersion ());
            kvList.AddString ("device_model", Device.Instance.Model ());

            if (tEvent.IsLogEvent ()) {
                switch (tEvent.Type) {
                case TelemetryEventType.ERROR:
                    kvList.AddString ("event_type", "ERROR");
                    break;
                case TelemetryEventType.WARN:
                    kvList.AddString ("event_type", "WARN");
                    break;
                case TelemetryEventType.INFO:
                    kvList.AddString ("event_type", "INFO");
                    break;
                case TelemetryEventType.DEBUG:
                    kvList.AddString ("event_type", "DEBUG");
                    break;
                default:
                    NachoAssert.True (false);
                    break;
                }
                kvList.AddString ("message", tEvent.Message);
            } else if (tEvent.IsWbxmlEvent ()) {
                switch (tEvent.Type) {
                case TelemetryEventType.WBXML_REQUEST:
                    kvList.AddString ("event_type", "WBXML_REQUEST");
                    break;
                case TelemetryEventType.WBXML_RESPONSE:
                    kvList.AddString ("event_type", "WBXML_RESPONSE");
                    break;
                default:
                    NachoAssert.True (false);
                    break;
                }
                kvList.AddData ("wbxml", tEvent.Wbxml);
            }
            PFObject anEvent = PFObject.ObjectWithClassName ("Events", kvList.GetDictionary ());
            anEvent.ACL = DefaultAcl;
            anEvent.Save ();
        }
    }
}

