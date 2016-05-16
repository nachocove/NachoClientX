//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore
{
    public abstract class NcApplyServerCommand
    {
        protected int AccountId;
        protected List<McPending.ReWrite> ReWrites;
        protected string CmdNameWithAccount;

        public NcApplyServerCommand (int accountId)
        {
            AccountId = accountId;
            ReWrites = new List<McPending.ReWrite> ();
            CmdNameWithAccount = string.Format ("{0}({1})", GetType ().Name, accountId);
        }

        // TODO per-account caching.
        private static int LastAccountId = -1;
        private static int LastVersion = -1;
        private static List<McPending> LastQueryNonFailedNonDeleted = null;

        public void ProcessServerCommand ()
        {
            // TODO consider grouping in a transaction.
            NcModel.Instance.RunInLock (() => {
                if (LastVersion != McPending.Version || null == LastQueryNonFailedNonDeleted || AccountId != LastAccountId) {
                    LastAccountId = AccountId;
                    LastVersion = McPending.Version;
                    // TODO: evaluate whether we can drop OrderBy.
                    LastQueryNonFailedNonDeleted = McPending.QueryNonFailedNonDeleted (AccountId).OrderBy (x => x.Id).ToList ();
                }
                foreach (var iter in LastQueryNonFailedNonDeleted) {
                    var pending = iter;
                    if (McPending.StateEnum.Dispatched == pending.State) {
                        // TODO: possibly apply changes to pending after the server rejects them rather
                        // than letting them possibly HardFail.
                        continue;
                    }

                    // Apply all existing re-writes to the pending.
                    switch (pending.ApplyReWrites (ReWrites)) {
                    case McPending.DbActionEnum.DoNothing:
                        break;
                    case McPending.DbActionEnum.Update:
                        pending = pending.UpdateWithOCApply<McPending> ((record) => {
                            var target = (McPending)record;
                            target.ApplyReWrites (ReWrites);
                            return true;
                        });
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
                        pending = pending.UpdateWithOCApply<McPending> ((record) => {
                            var target = (McPending)record;
                            // newReWrites already captured above.
                            ApplyCommandToPending (target, out action, out cancelDelta);
                            return true;
                        });
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
            });
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

