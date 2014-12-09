//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public abstract class AsApplyServerCommand
    {
        protected int AccountId;
        protected List<McPending.ReWrite> ReWrites;

        public AsApplyServerCommand (int accountId)
        {
            AccountId = accountId;
            ReWrites = new List<McPending.ReWrite> ();
        }

        public void ProcessServerCommand ()
        {
            foreach (var pending in McPending.QueryNonFailedNonDeleted (AccountId).OrderBy (x => x.Id)) {
                if (McPending.StateEnum.Dispatched == pending.State) {
                    // TODO: apply changes to pending after the server rejects them. Or should we mark them as maybe-gonna-fail?
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
                var newReWrites = ApplyCommandToPending (pending, out action, out cancelDelta);
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
            ApplyCommandToModel ();
        }

        protected abstract List<McPending.ReWrite> ApplyCommandToPending (McPending pending, 
            out McPending.DbActionEnum action,
            out bool cancelCommand
        );

        private void ApplyReWritesToModel ()
        {
            foreach (var rw in ReWrites) {
                switch (rw.ObjAction) {
                case McPending.ReWrite.ObjActionEnum.ReWriteServerParentIdString:
                    McAbstrFolderEntry.GloballyReWriteServerId (AccountId, rw.MatchString, rw.ReplaceString);
                    break;
                }
            }
        }

        protected abstract void ApplyCommandToModel ();
    }
}

