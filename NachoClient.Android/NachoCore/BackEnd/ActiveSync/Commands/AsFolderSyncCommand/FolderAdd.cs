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
                var account = McAccount.QueryById<McAccount> (AccountId);
                var protocolState = McProtocolState.QueryById<McProtocolState> (account.ProtocolStateId);
                var folderSyncEpoch = protocolState.AsFolderSyncEpoch;

                var folder = new McFolder () {
                    AccountId = AccountId,
                    ServerId = ServerId,
                    ParentId = ParentId,
                    DisplayName = DisplayName,
                    Type = FolderType,
                    AsSyncKey = McFolder.AsSyncKey_Initial,
                    AsFolderSyncEpoch = folderSyncEpoch,
                    AsSyncMetaToClientExpected = true,
                };

                var maybeSame = McFolder.QueryByServerId<McFolder> (AccountId, ServerId);
                if (null != maybeSame &&
                    maybeSame.DisplayName == DisplayName &&
                    maybeSame.Type == FolderType &&
                    maybeSame.AsFolderSyncEpoch < folderSyncEpoch) {
                    // The add is really the same as this old folder.
                    maybeSame.ParentId = ParentId;
                    // FIXME - see what happens when we use the prior sync key.
                    // maybeSame.AsSyncKey = McFolder.AsSyncKey_Initial;
                    maybeSame.AsFolderSyncEpoch = folderSyncEpoch;
                    maybeSame.AsSyncMetaToClientExpected = true;
                    maybeSame.Update ();
                } else {
                    folder.Insert ();
                }
            }
        }
    }
}

