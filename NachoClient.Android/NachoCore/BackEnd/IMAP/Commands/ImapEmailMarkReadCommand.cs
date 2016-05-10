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
        }

        protected override Event ExecuteCommand ()
        {
            McFolder folder = McFolder.QueryByServerId (AccountId, PendingSingle.ParentId);
            IMailFolder mailKitFolder = GetOpenMailkitFolder (folder, FolderAccess.ReadWrite);
            if (null == mailKitFolder) {
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMARKREADOPEN");
            }
            UpdateImapSetting (mailKitFolder, ref folder);
            McEmailMessage email = McEmailMessage.QueryByServerId<McEmailMessage> (AccountId, PendingSingle.ServerId);
            try {
                if (PendingSingle.EmailSetFlag_FlagType == McPending.MarkReadFlag) {
                    mailKitFolder.AddFlags (email.GetImapUid (folder), MessageFlags.Seen, true, Cts.Token);
                } else {
                    mailKitFolder.RemoveFlags (email.GetImapUid (folder), MessageFlags.Seen, true, Cts.Token);
                }
                if (email.IsRead != (PendingSingle.EmailSetFlag_FlagType == McPending.MarkReadFlag)) {
                    Log.Warn (Log.LOG_IMAP, "{0}: Setting IsRead={0} because DB doesn't have the right value", CmdNameWithAccount, PendingSingle.EmailSetFlag_FlagType == McPending.MarkReadFlag);
                    email = email.UpdateWithOCApply<McEmailMessage> (((record) => {
                        var target = (McEmailMessage)record;
                        target.IsRead = (PendingSingle.EmailSetFlag_FlagType == McPending.MarkReadFlag);
                        return true;
                    }));
                    var result = NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageChanged);
                    result.Value = email.Id;
                    BEContext.ProtoControl.StatusInd (result);
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

