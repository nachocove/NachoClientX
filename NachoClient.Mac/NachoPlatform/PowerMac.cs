//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using NachoCore.Utils;
using ObjCRuntime;

namespace NachoPlatform
{
    public class Power : IPlatformPower
    {
        private static volatile Power instance;
        private static object syncRoot = new Object ();

        private Power ()
        {
        }

        public static Power Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
//                            UIDevice.CurrentDevice.BatteryMonitoringEnabled = true;
                            instance = new Power ();
                        }
                    }
                }
                return instance;
            }
        }

        public double BatteryLevel { 
            get {
                return 0.99;
                // FIXME:
//                if (Device.Instance.IsSimulator ()) {
//                    return 0.99;
//                }
//                var iosLevel = UIDevice.CurrentDevice.BatteryLevel;
//                if (0.0 > iosLevel) {
//                    return 0.0;
//                }
//                NcAssert.True (0.0 <= iosLevel && 1.0 >= iosLevel);
//                return iosLevel;
            }
        }

        public PowerStateEnum PowerState {
            get {
                return PowerStateEnum.Plugged;
                // FIXME:
//                if (Device.Instance.IsSimulator ()) {
//                    return PowerStateEnum.Plugged;
//                }
//                switch (UIDevice.CurrentDevice.BatteryState) {
//                case UIDeviceBatteryState.Charging:
//                case UIDeviceBatteryState.Full:
//                    return PowerStateEnum.Plugged;
//
//                case UIDeviceBatteryState.Unplugged:
//                    return PowerStateEnum.Unplugged;
//
//                case UIDeviceBatteryState.Unknown:
//                    return PowerStateEnum.Unknown;
//
//                default:
//                    Log.Error (Log.LOG_SYS, "Unknown batteryState value: {0}", UIDevice.CurrentDevice.BatteryState);
//                    return PowerStateEnum.Unknown;
//                }
            }
        }

        public bool PowerStateIsPlugged ()
        {
            return PowerStateEnum.Plugged == PowerState;
        }
    }
}

