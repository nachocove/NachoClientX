//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;

namespace NachoCore.IMAP
{
    public class ImapEmailDeleteCommand : ImapCommand
    {
        public ImapEmailDeleteCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
        }

        protected override Event ExecuteCommand ()
        {
            var folderGuid = ImapProtoControl.ImapMessageFolderGuid (PendingSingle.ServerId);
            McFolder folder = McFolder.QueryByServerId (BEContext.Account.Id, PendingSingle.ParentId);
            NcAssert.Equals (folderGuid, folder.ImapGuid);
            var uid = ImapProtoControl.ImapMessageUid (PendingSingle.ServerId);
            lock (Client.SyncRoot) {
                IMailFolder mailKitFolder = GetOpenMailkitFolder (folder, FolderAccess.ReadWrite);
                if (null == mailKitFolder) {
                    return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMSGDELOPEN");
                }
                // FIXME Need to also copy the message to the Trash folder?
                mailKitFolder.SetFlags (uid, MessageFlags.Deleted, true, Cts.Token);
            }
            PendingResolveApply ((PendingSingle) => {
                PendingSingle.ResolveAsSuccess (BEContext.ProtoControl, 
                    NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageDeleteSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPMSGDELSUC");
        }
    }
}

