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
                    // "Not fully initialized" is defined as less than 50% of the emails are fully scored.
                    if (level < 0.10) {
                        WorkCredits = 0;
                    } else if (level < 0.3) {
                        WorkCredits = 10;
                    } else if (level < 0.5) {
                        WorkCredits = 20;
                    } else {
                        WorkCredits = 30;
                    }
                } else {
                    if (level < 0.3) {
                        WorkCredits = 0;
                    } else if (level < 0.6) {
                        WorkCredits = 10;
                    } else {
                        WorkCredits = 20;
                    }
                }
                Log.Debug (Log.LOG_BRAIN, "On battery, power_level={0}, work_credit={1}", level, WorkCredits);
                Console.WriteLine ("On battery, power_level={0}, work_credit={1}", level, WorkCredits);
            }
        }
    }
}

