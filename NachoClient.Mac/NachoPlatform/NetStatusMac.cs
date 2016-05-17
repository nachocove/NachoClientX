// # Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
//using NachoPlatformBinding;
using NachoCore.Utils;

namespace NachoPlatform
{
    public sealed class NetStatus : IPlatformNetStatus
    {
//        private NachoPlatformBinding.Reachability ReachInternet;
        private static volatile NetStatus instance;
        private static object syncRoot = new Object ();

//        private void Fire (NachoPlatformBinding.Reachability reachability)
//        {
//            NetStatusStatusEnum status;
//            NetStatusSpeedEnum speed;
//            Log.Info (Log.LOG_SYS, "Fire called by ReachableBlock/UnreachableBlock.");
//            GetCurrentStatus (out status, out speed);
//            if (null != NetStatusEvent) {
//                NetStatusEvent (this, new NetStatusEventArgs (status, speed));
//            }
//        }
            
        private NetStatus ()
        {
//            ReachInternet = NachoPlatformBinding.Reachability.ReachabilityForInternetConnection ();
//            // NOTE: these DON'T get called from the UI thread, so no need to NcTask.Run them.
//            ReachInternet.ReachableBlock = Fire;
//            ReachInternet.UnreachableBlock = Fire;
//			ReachInternet.StartNotifier ();
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
            // FIXME:
            status = NetStatusStatusEnum.Up;
            speed = NetStatusSpeedEnum.WiFi_0;
//            lock (syncRoot) {
//                var isUp = ReachInternet.IsReachable ();
//                status = (isUp) ? NetStatusStatusEnum.Up : NetStatusStatusEnum.Down;
//                if (NetStatusStatusEnum.Up == status) {
//                    speed = (ReachInternet.IsReachableViaWiFi ()) ? NetStatusSpeedEnum.WiFi_0 : NetStatusSpeedEnum.CellFast_1;
//                } else {
//                    speed = NetStatusSpeedEnum.CellSlow_2;
//                }
//            }
        }
    }
}
