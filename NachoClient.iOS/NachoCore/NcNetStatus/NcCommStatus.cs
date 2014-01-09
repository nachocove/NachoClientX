//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    public class NcNetStatus
    {
        public enum NetStatusEnum
        {
            Up,
            Down,
        };

        public enum NetQualityEnum
        {
            OK,
            Degraded,
            Unusable,
        };

        public enum NetSpeedEnum
        {
            WiFi,
            CellFast,
            CellSlow,
        };

        private static volatile NcNetStatus instance;
        private static object syncRoot = new Object ();

        private NetStatusEnum Status;
        private NetQualityEnum Quality;
        private NetSpeedEnum Speed;

        private NcNetStatus ()
        {
        }

        public static NcNetStatus Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new NcNetStatus ();
                            // FIXME - init values.
                        }
                    }
                }
                return instance;
            }
        }

        public event EventHandler NetStatusEvent;

        // FIXME - compute quality based on Fail %age in last window.
        public void NetFailInd ()
        {
        }
        public void NetSuccessInd ()
        {
        }
        public void ForceNetQualityOK ()
        {
            // clear counters.
            // fire event.
        }
        // FIXME - use basic "reachability" indicators.
    }
}

