//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Content;
using NachoClient.AndroidClient;
using Android.OS;

namespace NachoPlatform
{
    public sealed class Power : IPlatformPower
    {
        private static volatile Power instance;
        private static object syncRoot = new Object ();

        private NcAndroidBatteryInformation BatteryInfo;

        private Power ()
        {
            BatteryInfo = new NcAndroidBatteryInformation ();
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
                if (BatteryInfo.Level >= 0 && BatteryInfo.Scale >= 1) {
                    return (double)BatteryInfo.Level / (double)BatteryInfo.Scale;
                } else {
                    return 0.0; // unknown
                }
            }
        }

        public PowerStateEnum PowerState {
            get {
                switch (BatteryInfo.Plugged) {
                case -1:
                    return PowerStateEnum.Unknown;

                case (int)BatteryPlugged.Ac:
                    return PowerStateEnum.PluggedAC;

                case (int)BatteryPlugged.Usb:
                    return PowerStateEnum.PluggedUSB;

                case (int)BatteryPlugged.Wireless:
                    return PowerStateEnum.Plugged;

                default:
                    return PowerStateEnum.Unplugged;
                }
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

    public class NcAndroidBatteryInformation : BroadcastReceiver
    {
        public int Level { get; protected set; }

        public int Scale { get; protected set; }

        public int Plugged { get; protected set; }

        public NcAndroidBatteryInformation ()
        {
            Level = -1;
            Scale = -1;
            Plugged = -1;

            IntentFilter filter = new IntentFilter (Intent.ActionBatteryChanged);
            Intent intent = MainApplication.Instance.ApplicationContext.RegisterReceiver (this, filter);
            SetFromIntent (intent);
        }

        public override void OnReceive (Context context, Intent intent)
        {
            SetFromIntent (intent);
        }

        private void SetFromIntent (Intent intent)
        {
            Level = intent.GetIntExtra (BatteryManager.ExtraLevel, 0);
            Scale = intent.GetIntExtra (BatteryManager.ExtraScale, -1);
            Plugged = intent.GetIntExtra (BatteryManager.ExtraPlugged, -1);
        }
    }
}

