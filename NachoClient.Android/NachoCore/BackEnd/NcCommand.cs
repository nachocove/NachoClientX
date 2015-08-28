//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Threading;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore
{
    public class NcCommand : INcCommand
    {
        protected const int KLockTimeout = 10000;

        protected IBEContext BEContext;
        protected CancellationTokenSource Cts { get; set; }
        // PendingSingle is for commands that process 1-at-a-time. Pending list is for N-at-a-time commands.
        // Both get loaded-up in the class initalizer. During loading, each gets marked as dispatched.
        // The sublass is responsible for re-writing each from dispatched to something else.
        // This base class has a "diaper" to catch any dispached left behind by the subclass. This base class
        // is responsible for clearing PendingSingle/PendingList. 
        // Because of threading, the PendingResolveLockObj must be locked before resolving.
        // Any resolved pending objects must be removed from PendingSingle/PendingList before unlock.
        protected object PendingResolveLockObj;
        protected McPending PendingSingle;
        protected List<McPending> PendingList;
        protected NcResult SuccessInd;
        protected NcResult FailureInd;
        protected Object LockObj = new Object ();

        public enum AutoDFailureReason : uint
        {
            CannotConnectToServer,
            CannotFindServer
        };

        protected enum ResolveAction {
            None,
            DeferAll,
            FailAll,
        }

        public NcCommand (IBEContext beContext)
        {
            Cts = new CancellationTokenSource ();
            BEContext = beContext;
            PendingList = new List<McPending> ();
            PendingResolveLockObj = new object ();
        }

        public virtual void Execute (NcStateMachine sm)
        {
        }

        public virtual void Cancel ()
        {
            Cts.Cancel ();
        }

        // TODO - should these be in the interface?
        public virtual void ResolveAllFailed (NcResult.WhyEnum why)
        {
            lock (PendingResolveLockObj) {
                ConsolidatePending ();
                foreach (var pending in PendingList) {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, why);
                }
                PendingList.Clear ();
            }
        }

        public virtual void ResolveAllDeferred ()
        {
            lock (PendingResolveLockObj) {
                ConsolidatePending ();
                foreach (var pending in PendingList) {
                    pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                }
                PendingList.Clear ();
            }
        }

        protected void ConsolidatePending ()
        {
            if (null != PendingSingle) {
                PendingList.Add (PendingSingle);
                PendingSingle = null;
            }
        }

        protected delegate void PendingAction (McPending pending);

        protected void PendingResolveApply (PendingAction action)
        {
            lock (PendingResolveLockObj) {
                ConsolidatePending ();
                foreach (var pending in PendingList) {
                    action (pending);
                }
                PendingList.Clear ();
            }
        }

        public virtual void StatusInd (NcResult result)
        {
            BEContext.Owner.StatusInd (BEContext.ProtoControl, result);
        }

        public virtual void StatusInd (bool didSucceed)
        {
            if (didSucceed) {
                if (null != SuccessInd) {
                    BEContext.Owner.StatusInd (BEContext.ProtoControl, SuccessInd);
                }
            } else {
                if (null != FailureInd) {
                    BEContext.Owner.StatusInd (BEContext.ProtoControl, FailureInd);
                }
            }
        }

        public class CommandLockTimeOutException : Exception
        {
            public CommandLockTimeOutException (string message) : base(message)
            {

            }
        }

        public static Event TryLock (object lockObj, int timeout, Func<Event> func = null)
        {
            if (Monitor.TryEnter(lockObj, timeout))
            {
                try
                {
                    if (null != func) {
                        return func();
                    } else {
                        return null;
                    }
                }
                finally
                {
                    Monitor.Exit(lockObj);
                }
            } else {
                throw new CommandLockTimeOutException (string.Format ("Could not acquire lock object after {0:n0}ms", timeout));
            }
        }
    }

    // We don't involve the base class other than for is-a.
    public class NcWaitCommand : NcCommand
    {
        private NcStateMachine Sm;
        private int Duration;
        private bool EarlyOnECChange;
        private NcTimer WaitTimer;
        private bool HasCompleted = false;

        public NcWaitCommand (IBEContext dataSource, int duration, bool earlyOnECChange) : base(dataSource)
        {
            NcAssert.True (0 < duration);
            Duration = duration;
            EarlyOnECChange = earlyOnECChange;
        }

        public override void Execute (NcStateMachine sm)
        {
            Sm = sm;

            WaitTimer = new NcTimer ("NcWaitCommand:WaitTimer",
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
