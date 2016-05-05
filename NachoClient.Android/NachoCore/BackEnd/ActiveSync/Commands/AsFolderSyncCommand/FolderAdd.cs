//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public partial class AsFolderSyncCommand : AsCommand
    {
        private class ApplyFolderAdd : NcApplyServerCommand
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
                        if (pending.Folder_Type != FolderType) {
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
                        pending.Folder_Type == FolderType) {
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
                var protocolState = McProtocolState.QueryByAccountId<McProtocolState> (account.Id).SingleOrDefault ();
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
                    maybeSame = maybeSame.UpdateWithOCApply<McFolder> ((record) => {
                        var target = (McFolder)record;
                        target.ParentId = ParentId;
                        // We tried leaving the AsSyncKey untouched, and the server took it.
                        // We are going to rely on the server resetting the AsSyncKey if it wants it reset.
                        target.AsFolderSyncEpoch = folderSyncEpoch;
                        target.AsSyncMetaToClientExpected = true;
                        return true;
                    });
                } else {
                    if (null != maybeSame) {
                        // We aren't confident that the folder is the same, but we can't keep executing 
                        // with two folders having the same ServerId. The folder will be moved to LAF later,
                        // assuming it was from a prior epoch.
                        if (maybeSame.AsFolderSyncEpoch == folderSyncEpoch) {
                            Log.Error (Log.LOG_AS, "{0}: ApplyFolderAdd Clobber: new: {1}, existing {2}.", 
                                CmdNameWithAccount, maybeSame.ToString (), folder.ToString ());
                        }
                        var newServerId = Guid.NewGuid ().ToString ("N");
                        NcModel.Instance.RunInTransaction (() => {
                            maybeSame = maybeSame.UpdateSet_ServerId (newServerId);
                            McAbstrFolderEntry.GloballyReWriteServerId (AccountId, ServerId, newServerId);
                        });
                    }
                    folder.Insert ();
                }
            }
        }
    }
}

