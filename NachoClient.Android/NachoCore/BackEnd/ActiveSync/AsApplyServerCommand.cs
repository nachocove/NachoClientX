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
            foreach (var pending in McPending.Query (AccountId).OrderBy (x => x.Id)) {
                if (McPending.StateEnum.Dispatched == pending.State) {
                    // FIXME - what if there is an impact? will it cause this pending to fail?
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
                    var maybes = new List<McAbstrFolderEntry> ();
                    maybes.Add (McAbstrFolderEntry.QueryByServerId<McFolder> (AccountId, rw.MatchString));
                    maybes.Add (McAbstrFolderEntry.QueryByServerId<McEmailMessage> (AccountId, rw.MatchString));
                    maybes.Add (McAbstrFolderEntry.QueryByServerId<McContact> (AccountId, rw.MatchString));
                    maybes.Add (McAbstrFolderEntry.QueryByServerId<McCalendar> (AccountId, rw.MatchString));
                    maybes.Add (McAbstrFolderEntry.QueryByServerId<McTask> (AccountId, rw.MatchString));
                    foreach (var entry in maybes) {
                        if (null != entry) {
                            entry.ServerId = rw.ReplaceString;
                            entry.Update ();
                        }
                    }
                    var folders = McFolder.QueryByParentId (AccountId, rw.MatchString);
                    foreach (var folder in folders) {
                        folder.ParentId = rw.ReplaceString;
                        folder.Update ();
                    }
                    break;
                }
            }
        }

        protected abstract void ApplyCommandToModel ();
    }
}

