//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public partial class AsFolderSyncCommand : AsCommand
    {
        private class ApplyFolderUpdate : NcApplyServerCommand
        {
            public string ServerId { set; get; }

            public string ParentId { set; get; }

            public string DisplayName { set; get; }

            public uint FolderType { set; get; }

            public ApplyFolderUpdate (int accountId)
                : base (accountId)
            {
            }

            protected override List<McPending.ReWrite> ApplyCommandToPending (McPending pending, 
                out McPending.DbActionEnum action,
                out bool cancelCommand)
            {
                switch (pending.Operation) {
                case McPending.Operations.FolderDelete:
                    cancelCommand = pending.ServerIdDominatesCommand (ServerId);
                    action = McPending.DbActionEnum.DoNothing;
                    return null;

                case McPending.Operations.FolderUpdate:
                    action = (pending.ParentId == pending.DestParentId && pending.ServerId == ServerId) ?
                        McPending.DbActionEnum.Delete : McPending.DbActionEnum.DoNothing;
                    cancelCommand = false;
                    return null;

                default:
                    action = McPending.DbActionEnum.DoNothing;
                    cancelCommand = false;
                    return null;
                }
            }

            protected override void ApplyCommandToModel ()
            {
                var folder = McAbstrItem.QueryByServerId<McFolder> (AccountId, ServerId);
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ParentId = ParentId;
                    target.DisplayName = DisplayName;
                    return true;
                });
            }
        }
    }
}

