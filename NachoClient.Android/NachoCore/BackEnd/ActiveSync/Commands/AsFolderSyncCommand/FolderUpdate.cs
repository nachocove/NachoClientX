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
        private class ApplyFolderUpdate : AsApplyServerDelta
        {
            public string ServerId { set; get; }

            public string ParentId { set; get; }

            public string DisplayName { set; get; }

            public uint FolderType { set; get; }

            public ApplyFolderUpdate (int accountId)
                : base (accountId)
            {
            }

            protected override List<McPending.ReWrite> ApplyDeltaToPending (McPending pending, 
                out McPending.DbActionEnum action,
                out bool cancelDelta)
            {
                action = McPending.DbActionEnum.DoNothing;
                cancelDelta = false;
                switch (pending.Operation) {
                default:
                    break;
                }
                return null;
            }

            protected override void ApplyDeltaToModel ()
            {
                var folder = McItem.QueryByServerId<McFolder> (AccountId, ServerId);
                folder.ParentId = ParentId;
                folder.DisplayName = DisplayName;
                folder.Update ();
            }
        }
    }
}

