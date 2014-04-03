//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public partial class AsFolderSyncCommand : AsCommand
    {
        private class ApplyFolderAdd : AsApplyServerDelta
        {
            public string ServerId { set; get; }

            public string ParentId { set; get; }

            public string DisplayName { set; get; }

            public Xml.FolderHierarchy.TypeCode FolderType { set; get; }

            public ApplyFolderAdd (int accountId)
                : base (accountId)
            {
            }

            protected override List<McPending.ReWrite> ApplyChangeToPending (McPending pending, 
                                                                             out McPending.DbActionEnum action)
            {
                action = McPending.DbActionEnum.DoNothing;
                switch (pending.Operation) {
                case McPending.Operations.FolderCreate:
                    if (pending.DisplayName == DisplayName &&
                        pending.ParentId == ParentId) {
                        action = McPending.DbActionEnum.Delete;
                        var guid = pending.ServerId;
                        return new List<McPending.ReWrite> () {
                            new McPending.ReWrite () {
                                Action = McPending.ReWrite.LocalActionEnum.Replace,
                                Field = McPending.ReWrite.FieldEnum.ServerId,
                                Match = guid,
                                ReplaceWith = ServerId,
                            }
                        };
                    }
                    break;
                }
                return null;
            }

            protected override void ApplyChangeToModel ()
            {
                var folder = new McFolder () {
                    AccountId = AccountId,
                    ServerId = ServerId,
                    ParentId = ParentId,
                    DisplayName = DisplayName,
                    Type = FolderType,
                    AsSyncKey = McFolder.AsSyncKey_Initial,
                    AsSyncMetaToClientExpected = true,
                };
                folder.Insert ();
            }
        }
    }
}

