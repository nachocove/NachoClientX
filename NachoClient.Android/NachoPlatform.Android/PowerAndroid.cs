﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoPlatform
{
    public sealed class Power : IPlatformPower
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
                            instance = new Power ();
                        }
                    }
                }
                return instance;
            }
        }

        public double BatteryLevel { 
            get {
//                NcAssert.True (false);
                return 0.0;
            }
        }

        public PowerStateEnum PowerState {
            get {
//                NcAssert.True (false);
                return PowerStateEnum.Unknown;
            }
        }

        public bool PowerStateIsPlugged ()
        {
            switch (PowerState) {
            case PowerStateEnum.Plugged:
            case PowerStateEnum.PluggedAC:
            case PowerStateEnum.PluggedUSB:
                return true;
            default:
                return false;
            }
        }
    }
}

