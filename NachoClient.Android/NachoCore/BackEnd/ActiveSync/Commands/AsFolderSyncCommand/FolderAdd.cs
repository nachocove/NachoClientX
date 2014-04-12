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

            protected override List<McPending.ReWrite> ApplyDeltaToPending (McPending pending, 
                out McPending.DbActionEnum action,
                out bool cancelDelta)
            {
                action = McPending.DbActionEnum.DoNothing;
                cancelDelta = false;
                switch (pending.Operation) {
                case McPending.Operations.FolderCreate:
                    if (pending.DisplayName == DisplayName &&
                        pending.ParentId == ParentId) {
                        if (pending.FolderType == FolderType) {
                            // Delete the pending folder create.
                            action = McPending.DbActionEnum.Delete;
                            // Add a re-write to replace the FolderCreate's GUID with the real ServerId henceforth.
                            var guid = pending.ServerId;
                            return new List<McPending.ReWrite> () {
                                new McPending.ReWrite () {
                                    // FIXME Search TBA Path too.
                                    IsMatch = (subject) => subject.ServerId == guid,
                                    PerformReWrite = (subject) => {
                                        // FIXME be able to update the Path too.
                                        subject.ServerId = guid;
                                        return McPending.DbActionEnum.Update;
                                    },
                                }
                            };

                        } else {
                            // Just alter the display name to alert the user.
                            pending.DisplayName = pending.DisplayName + " Client-Created";
                            pending.Update ();
                        }
                    }
                    break;

                default:
                    // No other operations are affected by FolderSync:Add.
                    break;
                }
                return null;
            }

            protected override void ApplyDeltaToModel ()
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

