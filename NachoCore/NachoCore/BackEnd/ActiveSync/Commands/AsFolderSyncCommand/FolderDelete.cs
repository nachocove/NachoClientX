//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public partial class AsFolderSyncCommand : AsCommand
    {
        private class ApplyFolderDelete : NcApplyServerCommand
        {
            public string ServerId { set; get; }

            public ApplyFolderDelete (int accountId)
                : base (accountId)
            {
            }

            protected override List<McPending.ReWrite> ApplyCommandToPending (McPending pending,
                                                                              out McPending.DbActionEnum action,
                                                                              out bool cancelCommand)
            {
                switch (pending.Operation) {
                case McPending.Operations.FolderCreate:
                    action = (pending.ParentId == ServerId || pending.CommandDominatesParentId (ServerId)) ? 
                        McPending.DbActionEnum.Delete : McPending.DbActionEnum.DoNothing;
                    cancelCommand = false;
                    return null;

                case McPending.Operations.FolderDelete:
                    if (pending.ServerId == ServerId) {
                        action = McPending.DbActionEnum.Delete;
                        cancelCommand = true;
                    } else if (pending.CommandDominatesServerId (ServerId)) {
                        action = McPending.DbActionEnum.Delete;
                        cancelCommand = false;
                    } else if (pending.ServerIdDominatesCommand (ServerId)) {
                        action = McPending.DbActionEnum.DoNothing;
                        cancelCommand = true;
                    } else {
                        action = McPending.DbActionEnum.DoNothing;
                        cancelCommand = false;
                    }
                    return null;

                case McPending.Operations.FolderUpdate:
                    if (pending.ParentId == pending.DestParentId) {
                        // FolderUpdate:Rename.
                        if (pending.CommandDominatesServerId (ServerId)) {
                            action = McPending.DbActionEnum.Delete;
                            cancelCommand = false;
                            return null;
                        }
                    } else {
                        // FolderUpdate:Move.
                        if (pending.DestParentId == ServerId || pending.CommandDominatesDestParentId (ServerId)) {
                            McFolder.ServerEndMoveToClientOwned (AccountId, pending.ServerId, McFolder.ClientOwned_LostAndFound);
                            action = McPending.DbActionEnum.Delete;
                            cancelCommand = false;
                            return null;
                        } else if (pending.CommandDominatesParentId (ServerId)) {
                            // TODO - convert into SyncAdds (in-place). This means injecting new McPendings.
                            action = McPending.DbActionEnum.Delete;
                            cancelCommand = false;
                            return null;
                        }
                    }
                    action = McPending.DbActionEnum.DoNothing;
                    cancelCommand = false;
                    return null;

                case McPending.Operations.AttachmentDownload:
                case McPending.Operations.CalRespond:
                    action = (pending.CommandDominatesServerId (ServerId)) ? 
                        McPending.DbActionEnum.Delete : McPending.DbActionEnum.DoNothing;
                    cancelCommand = false;
                    return null;

                case McPending.Operations.EmailMove:
                case McPending.Operations.CalMove:
                case McPending.Operations.ContactMove:
                case McPending.Operations.TaskMove:
                    cancelCommand = false;
                    var item = pending.QueryItemUsingServerId ();
                    if (pending.CommandDominatesDestParentId (ServerId)) {
                        McFolder.UnlinkAll (item);
                        var laf = McFolder.GetLostAndFoundFolder (AccountId);
                        laf.Link (item);
                        action = McPending.DbActionEnum.Delete;
                        return null;
                    } else if (pending.CommandDominatesParentId (ServerId)) {
                        // TODO - convert into SyncAdds (in-place).
                        action = McPending.DbActionEnum.Delete;
                        return null;
                    }
                    action = McPending.DbActionEnum.DoNothing;
                    return null;

                case McPending.Operations.EmailForward:
                case McPending.Operations.EmailReply:
                    cancelCommand = false;
                    if (pending.CommandDominatesItem (ServerId)) {
                        pending.ConvertToEmailSend ();
                        action = McPending.DbActionEnum.Update;
                    } else {
                        action = McPending.DbActionEnum.DoNothing;
                    }
                    return null;

                case McPending.Operations.CalForward:
                    cancelCommand = false;
                    if (pending.CommandDominatesItem (ServerId)) {
                        action = McPending.DbActionEnum.Delete;
                    } else {
                        action = McPending.DbActionEnum.DoNothing;
                    }
                    return null;

                case McPending.Operations.CalCreate:
                case McPending.Operations.ContactCreate:
                case McPending.Operations.TaskCreate:
                case McPending.Operations.CalUpdate:
                case McPending.Operations.ContactUpdate:
                case McPending.Operations.TaskUpdate:
                    cancelCommand = false;
                    if (ServerId == pending.ParentId || pending.CommandDominatesParentId (ServerId)) {
                        item = pending.GetItem ();
                        McFolder.UnlinkAll (item);
                        var laf = McFolder.GetLostAndFoundFolder (AccountId);
                        laf.Link (item);
                        action = McPending.DbActionEnum.Delete;
                    } else {
                        action = McPending.DbActionEnum.DoNothing;
                    }
                    return null;

                case McPending.Operations.EmailDelete:
                case McPending.Operations.CalDelete:
                case McPending.Operations.ContactDelete:
                case McPending.Operations.TaskDelete:
                    cancelCommand = false;
                    if (ServerId == pending.ParentId || pending.CommandDominatesParentId (ServerId)) {
                        action = McPending.DbActionEnum.Delete;
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
                // Remove the folder and anything subordinate.
                var folder = McAbstrFolderEntry.QueryByServerId<McFolder> (AccountId, ServerId);
                if (null != folder) {
                    folder.Delete ();
                } else {
                    Log.Error (Log.LOG_AS, "{0}: ApplyFolderDelete:ApplyCommandToModel: ServerId missing in DB.", CmdNameWithAccount);
                }
            }
        }
    }
}
