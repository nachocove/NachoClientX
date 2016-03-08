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
        public ImapEmailMarkReadCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            PendingSingle = pending;
            pending.MarkDispatched ();
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
            McFolder folder = McFolder.QueryByServerId (AccountId, PendingSingle.ParentId);
            McEmailMessage email = McEmailMessage.QueryByServerId<McEmailMessage> (AccountId, PendingSingle.ServerId);
            IMailFolder mailKitFolder = GetOpenMailkitFolder (folder, FolderAccess.ReadWrite);
            if (null == mailKitFolder) {
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMARKREADOPEN");
            }
            UpdateImapSetting (mailKitFolder, ref folder);
            try {
                if (PendingSingle.EmailSetFlag_FlagType == McPending.MarkReadFlag) {
		    mailKitFolder.SetFlags (email.GetImapUid (folder), MessageFlags.Seen, true, Cts.Token);
                } else {
		    mailKitFolder.RemoveFlags (email.GetImapUid (folder), MessageFlags.Seen, true, Cts.Token);
                }
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, 
                        NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMarkedReadSucceeded));
                });
                return Event.Create ((uint)SmEvt.E.Success, "IMAPMARKREADSUC");
            } catch (MessageNotFoundException) {
                email.Delete ();
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageMarkedReadFailed, NcResult.WhyEnum.MissingOnServer));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMARKREADMISS");
            }
        }
    }
}

