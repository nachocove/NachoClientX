//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    // We don't involve the base class other than for is-a.
    public class AsWaitCommand : AsCommand
    {
        private NcStateMachine Sm;
        private int Duration;
        private bool EarlyOnECChange;
        private NcTimer WaitTimer;
        private bool HasCompleted = false;

        public AsWaitCommand (IBEContext dataSource, int duration, bool earlyOnECChange) :
            base ("AsWaitCommand", Xml.AirSyncBase.Ns, dataSource)
        {
            NcAssert.True (0 < duration);
            Duration = duration;
            EarlyOnECChange = earlyOnECChange;
        }

        public override void Execute (NcStateMachine sm)
        {
            Sm = sm;

            WaitTimer = new NcTimer ("AsWaitCommand:WaitTimer",
                (state) => {
                    Complete (true);
                }, 
                null,
                new TimeSpan (0, 0, Duration), 
                System.Threading.Timeout.InfiniteTimeSpan);

            if (EarlyOnECChange) {
                NcApplication.Instance.StatusIndEvent += Detector;
            }
        }

        private void Detector (object sender, EventArgs ea)
        {
            StatusIndEventArgs siea = (StatusIndEventArgs)ea;
            if (NcResult.SubKindEnum.Info_ExecutionContextChanged == siea.Status.SubKind) {
                Complete (false);
            }
        }

        private void Complete (bool fromTimer)
        {
            lock (LockObj) {
                if (EarlyOnECChange) {
                    EarlyOnECChange = false;
                    NcApplication.Instance.StatusIndEvent -= Detector;
                }
                if (!fromTimer) {
                    if (null != WaitTimer) {
                        WaitTimer.Dispose ();
                        WaitTimer = null;
                    }
                }
                if (!HasCompleted) {
                    HasCompleted = true;
                    Sm.PostEvent ((uint)SmEvt.E.Success, "ASWAITS");
                }
            }
        }

        public override void Cancel ()
        {
            lock (LockObj) {
                HasCompleted = true;
                Complete (false);
            }
        }
    }
}
