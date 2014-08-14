//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoPlatform;
using NachoCore.Utils;

namespace NachoCore.Brain
{
    public partial class NcBrain
    {
        // Amount of "thinking" per background invocation
        private int WorkCredits;

        private bool IsWallPowered ()
        {
            switch (Power.Instance.PowerState) {
            case PowerStateEnum.Plugged:
            case PowerStateEnum.PluggedAC:
            case PowerStateEnum.PluggedUSB:
                return true;
            }
            return false;
        }

        private void EvaluateRunRate ()
        {
            if (IsWallPowered ()) {
                WorkCredits = 30;
                Log.Debug (Log.LOG_BRAIN, "Plugged in, work_credit={0}", WorkCredits);
                Console.WriteLine ("Plugged in, work_credit={0}", WorkCredits);
            } else {
                double level = Power.Instance.BatteryLevel;
                if (level < 0.3) {
                    WorkCredits = 0;
                } else if (level < 0.6) {
                    WorkCredits = 10;
                } else {
                    WorkCredits = 20;
                }
                Log.Debug (Log.LOG_BRAIN, "On battery, power_level={0}, work_credit={1}", level, WorkCredits);
                Console.WriteLine ("On battery, power_level={0}, work_credit={1}", level, WorkCredits);
            }
        }
    }
}

