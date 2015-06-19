//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;
using System.Collections.Generic;
using MailKit.Net.Imap;

namespace NachoCore.IMAP
{
    public class ImapEmailDeleteCommand : ImapCommand
    {
        public ImapEmailDeleteCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
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
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMSGDELOPEN");
            }
            mailKitFolder.SetFlags (uid, MessageFlags.Deleted, true, Cts.Token);
            if (Client.Capabilities.HasFlag (ImapCapabilities.UidPlus)) {
                var list = new List<UniqueId> ();
                list.Add (uid);
                mailKitFolder.Expunge (list, Cts.Token);
            }
            // TODO The set flags reply contains information we can use (S: * 5 FETCH (UID 8631 MODSEQ (948373) FLAGS (\Deleted))).
            // save it. That being said, if we increment the MODSEQ, then that will basically
            // force a resync (or at least a sync), which we don't want. Might want to check the modseq before our delete.
            UpdateImapSetting (mailKitFolder, folder);
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, 
                    NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageDeleteSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPMSGDELSUC");
        }
    }
}

