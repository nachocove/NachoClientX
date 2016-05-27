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
            NcAssert.NotNull (PendingSingle, "PendingSingle is null");
            NcAssert.NotNull (Cts, "Cts is null");
            McFolder folder = McFolder.QueryByServerId (AccountId, PendingSingle.ParentId);
            if (null == folder) {
                Log.Warn (Log.LOG_IMAP, "{0}: folder {1} seems to have disappeared", CmdNameWithAccount, PendingSingle.ParentId);
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMARKANSNOFOLDER");
            }
            IMailFolder mailKitFolder = GetOpenMailkitFolder (folder, FolderAccess.ReadWrite);
            if (null == mailKitFolder) {
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMARKANSWOPEN");
            }
            UpdateImapSetting (mailKitFolder, ref folder);
            McEmailMessage email = McEmailMessage.QueryByServerId<McEmailMessage> (AccountId, PendingSingle.ServerId);
            if (null == email) {
                Log.Warn (Log.LOG_IMAP, "{0}: Email {1} seems to have disappeared", CmdNameWithAccount, PendingSingle.ServerId);
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMARKANSWNOEMAIL");
            }
            try {
                if (PendingSingle.EmailSetFlag_FlagType == McPending.MarkAnsweredFlag) {
                    mailKitFolder.AddFlags (email.GetImapUid (folder), MessageFlags.Answered, true, Cts.Token);
                } else {
                    mailKitFolder.RemoveFlags (email.GetImapUid (folder), MessageFlags.Answered, true, Cts.Token);
                }
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, 
                        NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMarkedAnsweredSucceeded));
                });
                return Event.Create ((uint)SmEvt.E.Success, "IMAPMARKANSWSUC");
            } catch (MessageNotFoundException) {
                email.Delete ();
                var protoControl = BEContext.ProtoControl;
                NcAssert.NotNull (protoControl, "protoControl is null");
                PendingResolveApply ((pending) => pending.ResolveAsHardFail (protoControl, NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageMarkedAnsweredFailed, NcResult.WhyEnum.MissingOnServer)));
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPMARKANSWMISS");
            }
        }
    }
}

