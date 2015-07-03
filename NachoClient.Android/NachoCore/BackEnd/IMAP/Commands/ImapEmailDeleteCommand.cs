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
            // Nothing additional to redact. The command just has UID's.
            //L00000059 OK [READ-WRITE] REDACTED selected. (Success)
            //2015-06-22T17:18:23.971Z: IMAP C: L00000060 UID STORE 8642 FLAGS.SILENT (\Deleted)
            //2015-06-22T17:18:25.468Z: IMAP S: * 12 FETCH (UID 8642 MODSEQ (953602) FLAGS (\Deleted))
            //L00000060 OK Success
            //2015-06-22T17:18:25.470Z: IMAP C: L00000061 UID EXPUNGE 8642
            //2015-06-22T17:18:27.717Z: IMAP S: * 12 EXPUNGE
            //* 16 EXISTS
            //L00000061 OK Success
            return logData;
        }

        protected override Event ExecuteCommand ()
        {
            var folderGuid = ImapProtoControl.ImapMessageFolderGuid (PendingSingle.ServerId);
            McFolder folder = McFolder.QueryByServerId (BEContext.Account.Id, PendingSingle.ParentId);
            if (folderGuid != folder.ImapGuid) {
                Log.Error (Log.LOG_IMAP, "Folder GUID no longer matches.");
                throw new NcImapCommandRetryException (Event.Create ((uint)ImapProtoControl.ImapEvt.E.ReFSync, "IMAPEMDELUIDVAL"));
            }
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
            UpdateImapSetting (mailKitFolder, ref folder);
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, 
                    NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageDeleteSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPMSGDELSUC");
        }
    }
}

