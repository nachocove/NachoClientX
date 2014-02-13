//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public partial class AsFolderSyncCommand : AsCommand
    {
        private class ApplyFolderDelete : AsApplyServerDelta
        {
            public string ServerId { set; get; }

            public ApplyFolderDelete (int accountId)
                    : base (accountId)
            {
            }

            protected override List<McPending.ReWrite> ApplyChangeToPending (McPending pending, 
                                                                        out McPending.ActionEnum action)
            {
                action = McPending.ActionEnum.DoNothing;
                switch (pending.Operation) {
                case McPending.Operations.FolderCreate:
                    // FIXME - need to handle the indirect subordinate cases.
                    if (pending.ParentId == ServerId) {
                        action = McPending.ActionEnum.Delete;
                        return new List<McPending.ReWrite> () {
                            new McPending.ReWrite () {
                                Action = McPending.ReWrite.ActionEnum.Delete,
                                Field = McPending.ReWrite.FieldEnum.ParentId,
                                Match = ServerId,
                            }
                        };
                    }
                    break;
                case McPending.Operations.FolderDelete:
                    break;
                case McPending.Operations.FolderUpdate:
                    break;
                }
                return null;
            }

            protected override void ApplyChangeToModel ()
            {
                // Remove the folder and anything subordinate.
                var folder = McFolder.QueryByServerId (AccountId, ServerId);
                folder.Delete ();
            }
        }
    }
}
