//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public partial class AsFolderSyncCommand : AsCommand
    {
        private class ApplyFolderAdd : AsApplyServerCommand
        {
            public string ServerId { set; get; }

            public string ParentId { set; get; }

            public string DisplayName { set; get; }

            public Xml.FolderHierarchy.TypeCode FolderType { set; get; }

            public ApplyFolderAdd (int accountId)
                : base (accountId)
            {
            }

            protected override List<McPending.ReWrite> ApplyCommandToPending (McPending pending, 
                out McPending.DbActionEnum action,
                out bool cancelCommand)
            {
                switch (pending.Operation) {
                case McPending.Operations.FolderCreate:
                    cancelCommand = false;
                    if (pending.DisplayName == DisplayName &&
                        pending.ParentId == ParentId) {
                        if (pending.FolderCreate_Type != FolderType) {
                            pending.DisplayName = pending.DisplayName + " Client-Created";
                            action = McPending.DbActionEnum.Update;
                            return null;
                        } else {
                            action = McPending.DbActionEnum.Delete;
                            return new List<McPending.ReWrite> () {
                                new McPending.ReWrite () {
                                    ObjAction = McPending.ReWrite.ObjActionEnum.ReWriteServerParentIdString,
                                    MatchString = pending.ServerId, // The GUID.
                                    ReplaceString = ServerId,
                                },
                            };
                        }
                    }
                    action = McPending.DbActionEnum.DoNothing;
                    return null;

                case McPending.Operations.FolderDelete:
                    action = McPending.DbActionEnum.DoNothing;
                    cancelCommand = pending.ServerIdDominatesCommand (ServerId);
                    return null;

                case McPending.Operations.FolderUpdate:
                    cancelCommand = false;
                    if (pending.DestParentId == ParentId &&
                        pending.DisplayName == DisplayName &&
                        pending.FolderCreate_Type == FolderType) {
                        pending.DisplayName = pending.DisplayName + " Client-Moved";
                        action = McPending.DbActionEnum.Update;
                    } else {
                        action = McPending.DbActionEnum.DoNothing;
                    }
                    return null;

                default:
                    action = McPending.DbActionEnum.DoNothing;
                    cancelCommand = false;
                    return null;
                }
            }

            protected override void ApplyCommandToModel ()
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
                    // The FolderSync:Add is really the same as this old folder.
                    maybeSame.ParentId = ParentId;
                    // We tried leaving the AsSyncKey untouched, and the server took it.
                    // We are going to rely on the server resetting the AsSyncKey if it wants it.
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

