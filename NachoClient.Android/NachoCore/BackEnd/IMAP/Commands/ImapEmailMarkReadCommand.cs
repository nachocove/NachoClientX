//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;

namespace NachoCore.IMAP
{
    public class ImapEmailMarkReadCommand : ImapCommand
    {
        public ImapEmailMarkReadCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            PendingSingle = pending;
            pending.MarkDispached ();
        }

        protected override Event ExecuteCommand ()
        {
            var folderGuid = ImapProtoControl.ImapMessageFolderGuid (PendingSingle.ServerId);
            McFolder folder = McFolder.QueryByServerId (BEContext.Account.Id, PendingSingle.ParentId);
            if (folderGuid != folder.ImapGuid) {
                Log.Error (Log.LOG_IMAP, "Folder GUID no longer matches.");
                throw new NcImapCommandRetryException (Event.Create ((uint)ImapProtoControl.ImapEvt.E.FolderSync, "IMAPEMREADUID"));
            }
            var uid = ImapProtoControl.ImapMessageUid (PendingSingle.ServerId);
            lock (Client.SyncRoot) {
                IMailFolder mailKitFolder = GetOpenMailkitFolder (folder.ServerId, FolderAccess.ReadWrite);
                if (null == mailKitFolder) {
                    return Event.Create ((uint)SmEvt.E.HardFail, "IMAPEMREADOPEN");
                }
                mailKitFolder.SetFlags (uid, MessageFlags.Seen, true, Cts.Token);
            }
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, 
                    NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMarkedReadSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPEMREADSUC");
        }
    }
}

