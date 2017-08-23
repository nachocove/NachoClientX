// # Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Runtime.InteropServices;
using ObjCRuntime;

using NachoCore.Utils;

namespace NachoPlatform
{
    public sealed class NetStatus : IPlatformNetStatus
    {
        private static volatile NetStatus instance;
        private static object syncRoot = new Object ();

        private void Fire ()
        {
            NetStatusStatusEnum status;
            NetStatusSpeedEnum speed;
            Log.Info (Log.LOG_SYS, "Fire called by ReachableBlock/UnreachableBlock.");
            GetCurrentStatus (out status, out speed);
            if (null != NetStatusEvent) {
                NetStatusEvent (this, new NetStatusEventArgs (status, speed));
            }
        }

        [MonoPInvokeCallback (typeof (ReachabilityCallback))]
        private static void NachoInternetCallback ()
        {
            Instance.Fire ();
        }
            
        private NetStatus ()
        {
            nacho_internet_reachability_init (NachoInternetCallback);
            nacho_internet_reachability_start_notifier ();
        }

        public static NetStatus Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new NetStatus ();
                    }
                }
                return instance;
            }
        }

        public event NetStatusEventHandler NetStatusEvent;
             
        public void GetCurrentStatus (out NetStatusStatusEnum status, out NetStatusSpeedEnum speed)
        {
            lock (syncRoot) {
                var isUp = nacho_internet_reachability_is_reachable () != 0;
                status = (isUp) ? NetStatusStatusEnum.Up : NetStatusStatusEnum.Down;
                if (NetStatusStatusEnum.Up == status) {
                    speed = (nacho_internet_reachability_is_reachable_via_wifi() != 0) ? NetStatusSpeedEnum.WiFi_0 : NetStatusSpeedEnum.CellFast_1;
                } else {
                    speed = NetStatusSpeedEnum.CellSlow_2;
                }
            }
		}

        [UnmanagedFunctionPointer (CallingConvention.Cdecl)]
        delegate void ReachabilityCallback();

        [DllImport ("__Internal")]
        static extern void nacho_internet_reachability_init (ReachabilityCallback callback);

        [DllImport ("__Internal")]
		static extern void nacho_internet_reachability_start_notifier ();

        [DllImport ("__Internal")]
		static extern void nacho_internet_reachability_stop_notifier ();

        [DllImport ("__Internal")]
		static extern int nacho_internet_reachability_is_reachable ();

        [DllImport ("__Internal")]
		static extern int nacho_internet_reachability_is_reachable_via_wifi ();
    }
}
