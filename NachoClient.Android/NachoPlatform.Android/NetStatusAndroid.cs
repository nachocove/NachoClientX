// # Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading.Tasks;
using Android.Content;
using Android.Net;
using Android.App;
using Android.OS;
using Android.Telephony;
using NachoClient.AndroidClient;
using NachoCore.Utils;

namespace NachoPlatform
{
    public sealed class NetStatus : BroadcastReceiver, IPlatformNetStatus
    {
        private static volatile NetStatus instance;
        private static object syncRoot = new Object ();

        private NetStatus ()
        {
            IntentFilter filter = new IntentFilter ("android.net.conn.CONNECTIVITY_CHANGE");
            MainApplication.Instance.ApplicationContext.RegisterReceiver (this, filter);
        }

        public static NetStatus Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new NetStatus ();
                            NcAssert.NotNull (instance);
                        }
                    }
                }
                return instance;
            }
        }

        public event NetStatusEventHandler NetStatusEvent;
      
        public void GetCurrentStatus (out NetStatusStatusEnum status, out NetStatusSpeedEnum speed)
        {
            lock (syncRoot) {
                var context = MainApplication.Context;
                var connectivityManager = (ConnectivityManager)context.GetSystemService (Context.ConnectivityService);
                var netInfo = connectivityManager.ActiveNetworkInfo;
                status = (null != netInfo && netInfo.IsConnectedOrConnecting) ? NetStatusStatusEnum.Up : NetStatusStatusEnum.Down;
                if (null != netInfo) {
                    switch (netInfo.Type) {
                    case ConnectivityType.Ethernet:
                    case ConnectivityType.Wifi:
                        speed = NetStatusSpeedEnum.WiFi_0;
                        break;
                    case ConnectivityType.Wimax:
                        speed = NetStatusSpeedEnum.CellFast_1;
                        break;
                    default:
                        var telephonyManager = (TelephonyManager)
                            MainApplication.Context.GetSystemService (Context.TelephonyService);
                        if (NetworkType.Lte == telephonyManager.NetworkType) {
                            speed = NetStatusSpeedEnum.CellFast_1;
                        } else {
                            speed = NetStatusSpeedEnum.CellSlow_2;
                        }
                        break;
                    }
                } else {
                    speed = NetStatusSpeedEnum.CellSlow_2;
                }
            }
        }

        public void Fire ()
        {
            NetStatusStatusEnum status;
            NetStatusSpeedEnum speed;
            GetCurrentStatus (out status, out speed);
            if (null != NetStatusEvent) {
                NetStatusEvent (this, new NetStatusEventArgs (status, speed));
            }
        }

        public override void OnReceive (Context context, Intent intent)
        {
            // NOTE: This is called using the UI thread, so need to Task.Run here.
            NcTask.Run (() => {
                var netInst = NetStatus.Instance;
                if (null != netInst) {
                    netInst.Fire ();
                } else {
                    Log.Error (Log.LOG_SYS, "NetStatusBroadcastReceiver:OnReceive: No NetStatus.Instance");
                }
            }, "NetStatusBroadcastReceiver:OnReceive");
        }
    }
}
