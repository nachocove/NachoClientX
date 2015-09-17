//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoPlatform;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.Brain
{
    public partial class NcBrain
    {
        private bool Bootstrapped;

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

        private DateTime EvaluateRunTime (double periodSec)
        {
            double durationSec;
            if (IsWallPowered ()) {
                durationSec = 0.5 * periodSec;
            } else {
                double level = Power.Instance.BatteryLevel;
                double dutyCycle;
                if (!Bootstrapped) {
                    int numScored = McEmailMessage.CountByVersion (Scoring.Version);
                    int numTotal = McEmailMessage.Count ();
                    double percentageScored = 0.0;
                    if (0 < numTotal) {
                        percentageScored = Math.Min (1.0, (double)numScored / (double)numTotal);
                    }
                    if (0.75 <= percentageScored) {
                        Bootstrapped = true;
                    }
                }
                if (!Bootstrapped &&
                    (NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext)) {
                    // If brain is not fully initialized, we want to run brain more aggressively.
                    // "Not fully initialized" is defined as less than 75% of the emails are fully scored.
                    if (level < 0.10) {
                        dutyCycle = 0.0;
                    } else if (level < 0.3) {
                        dutyCycle = 0.2;
                    } else if (level < 0.5) {
                        dutyCycle = 0.3;
                    } else {
                        dutyCycle = 0.4;
                    }
                } else {
                    if (level < 0.3) {
                        dutyCycle = 0.0;
                    } else if (level < 0.6) {
                        dutyCycle = 0.15;
                    } else {
                        dutyCycle = 0.3;
                    }
                }
                durationSec = periodSec * dutyCycle;
                Log.Debug (Log.LOG_BRAIN, "On battery, power_level={0}, duration={1}", level, durationSec);
            }
            return DateTime.UtcNow.AddSeconds (durationSec);
        }
    }
}

