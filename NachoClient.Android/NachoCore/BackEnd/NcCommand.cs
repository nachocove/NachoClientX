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
    }
}
