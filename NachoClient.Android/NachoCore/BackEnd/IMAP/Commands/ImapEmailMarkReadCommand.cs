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
            return logData;
        }

        protected override Event ExecuteCommand ()
        {
            var folderGuid = ImapProtoControl.ImapMessageFolderGuid (PendingSingle.ServerId);
            McFolder folder = McFolder.QueryByServerId (BEContext.Account.Id, PendingSingle.ParentId);
            NcAssert.Equals (folderGuid, folder.ImapGuid);
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
            return Event.Create ((uint)SmEvt.E.Success, "IMAPMARKREADSUC");
        }
    }
}

