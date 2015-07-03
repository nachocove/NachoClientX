//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;
using MailKit.Net.Imap;

namespace NachoCore.IMAP
{
    public class ImapEmailMarkReadCommand : ImapCommand
    {
        public ImapEmailMarkReadCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            PendingSingle = pending;
            pending.MarkDispached ();
            RedactProtocolLogFunc = RedactProtocolLog;
        }

        public string RedactProtocolLog (bool isRequest, string logData)
        {
            // No additional redaction necessary
            //2015-06-22T17:26:07.601Z: IMAP C: A00000062 UID STORE 8728 FLAGS.SILENT (\Seen)
            //2015-06-22T17:26:08.028Z: IMAP S: * 60 FETCH (UID 8728 MODSEQ (953644) FLAGS (\Seen))
            //A00000062 OK Success
            return logData;
        }

        protected override Event ExecuteCommand ()
        {
            var folderGuid = ImapProtoControl.ImapMessageFolderGuid (PendingSingle.ServerId);
            McFolder folder = McFolder.QueryByServerId (BEContext.Account.Id, PendingSingle.ParentId);
            if (folderGuid != folder.ImapGuid) {
                Log.Error (Log.LOG_IMAP, "Folder GUID no longer matches.");
                throw new NcImapCommandRetryException (Event.Create ((uint)ImapProtoControl.ImapEvt.E.ReFSync, "IMAPEMREADUID"));
            }
            var uid = ImapProtoControl.ImapMessageUid (PendingSingle.ServerId);
            IMailFolder mailKitFolder = GetOpenMailkitFolder (folder, FolderAccess.ReadWrite);
            if (null == mailKitFolder) {
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMARKREADOPEN");
            }
            mailKitFolder.SetFlags (uid, MessageFlags.Seen, true, Cts.Token);
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, 
                    NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMarkedReadSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPEMREADSUC");
        }
    }
}

