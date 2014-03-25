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
        protected List<McPending> PendingQ;

        public AsApplyServerDelta (int accountId)
        {
            AccountId = accountId;
            ReWrites = new List<McPending.ReWrite> ();
            PendingQ = McPending.Query (AccountId);
        }

        public void ProcessDelta ()
        {
            foreach (var pending in PendingQ) {
                switch (pending.ApplyReWrites (ReWrites)) {
                case McPending.DbActionEnum.DoNothing:
                    break;
                case McPending.DbActionEnum.Update:
                    pending.Update ();
                    break;
                case McPending.DbActionEnum.Delete:
                    pending.Delete ();
                    continue;
                }

                McPending.DbActionEnum action = McPending.DbActionEnum.DoNothing;
                var newReWrites = ApplyChangeToPending (pending, out action);
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
                    break; // Note not the same as 'continue;' above!
                }
            }
            ApplyChangeToModel ();
        }

        protected abstract List<McPending.ReWrite> ApplyChangeToPending (McPending pending, 
                                                                         out McPending.DbActionEnum action);

        protected abstract void ApplyChangeToModel ();
    }
}

