//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;

namespace NachoCore.IMAP
{
    public class ImapEmailMarkAnsweredCommand : ImapCommand
    {
        public ImapEmailMarkAnsweredCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            PendingSingle = pending;
            pending.MarkDispatched ();
        }

        protected override Event ExecuteCommand ()
        {
            McFolder folder = McFolder.QueryByServerId (AccountId, PendingSingle.ParentId);
            IMailFolder mailKitFolder = GetOpenMailkitFolder (folder, FolderAccess.ReadWrite);
            if (null == mailKitFolder) {
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMARKANSWOPEN");
            }
            UpdateImapSetting (mailKitFolder, ref folder);
            McEmailMessage email = McEmailMessage.QueryByServerId<McEmailMessage> (AccountId, PendingSingle.ServerId);
            try {
                if (PendingSingle.EmailSetFlag_FlagType == McPending.MarkAnsweredFlag) {
                    mailKitFolder.AddFlags (new UniqueId (email.ImapUid), MessageFlags.Answered, true, Cts.Token);
                } else {
                    mailKitFolder.RemoveFlags (new UniqueId (email.ImapUid), MessageFlags.Answered, true, Cts.Token);
                }
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, 
                        NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMarkedAnsweredSucceeded));
                });
                return Event.Create ((uint)SmEvt.E.Success, "IMAPMARKANSWSUC");
            } catch (MessageNotFoundException) {
                email.Delete ();
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageMarkedAnsweredFailed, NcResult.WhyEnum.MissingOnServer));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMARKANSWMISS");
            }
        }
    }
}

