//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public abstract class AsApplyServerDelta
    {
        protected int AccountId;
        protected List<McPending.ReWrite> ReWrites;
        protected int PriorPendingId;

        public AsApplyServerDelta (int accountId)
        {
            AccountId = accountId;
            ReWrites = new List<McPending.ReWrite> ();
            PriorPendingId = 0;
        }

        public void ProcessDelta ()
        {
            McPending pending;
            for (pending = McPending.GetOldestYoungerThanId (AccountId, PriorPendingId);
                null != pending;
                pending = McPending.GetOldestYoungerThanId (AccountId, PriorPendingId)) {
                PriorPendingId = pending.Id;

                if (McPending.StateEnum.Dispatched == pending.State) {
                    continue;
                }

                // Apply all existing re-writes to the pending.
                switch (pending.ApplyReWrites (ReWrites)) {
                case McPending.DbActionEnum.DoNothing:
                    break;
                case McPending.DbActionEnum.Update:
                    pending.Update ();
                    break;
                case McPending.DbActionEnum.Delete:
                    pending.Delete ();
                    continue; // Not break! No need to apply delta to a just-deleted pending!
                }

                // Apply this specific to-client delta to the pending,
                // possibly generating new re-writes.
                McPending.DbActionEnum action;
                bool cancelDelta;
                var newReWrites = ApplyDeltaToPending (pending, out action, out cancelDelta);
                if (null != newReWrites) {
                    ReWrites.AddRange (newReWrites);
                }
                switch (action) {
                case McPending.DbActionEnum.DoNothing:
                    break;
                case McPending.DbActionEnum.Update:
                    pending.Update ();
                    break;
                case McPending.DbActionEnum.Delete:
                    pending.Delete ();
                    break;
                }
                if (cancelDelta) {
                    // There is no need to keep processing the delta, and no need to apply it to the DB.
                    return;
                }
            }
            ApplyReWritesToModel ();
            ApplyDeltaToModel ();
        }

        protected abstract List<McPending.ReWrite> ApplyDeltaToPending (McPending pending, 
            out McPending.DbActionEnum action,
            out bool cancelDelta
        );

        private void ApplyReWritesToModel ()
        {
            // FIXME.
        }

        protected abstract void ApplyDeltaToModel ();
    }
}

