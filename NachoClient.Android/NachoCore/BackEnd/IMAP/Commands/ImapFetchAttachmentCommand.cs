//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Model;
using MailKit.Net.Imap;
using NachoCore.Utils;
using System.IO;
using System.Linq;

namespace NachoCore.IMAP
{
    public class ImapFetchAttachmentCommand : ImapCommand
    {
        public ImapFetchAttachmentCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            PendingSingle = pending;
            pending.MarkDispached ();
        }

        protected override Event ExecuteCommand ()
        {
            NcResult result = null;
            result = FetchAttachment (PendingSingle);
            if (result.isInfo ()) {
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, result);
                });
                return Event.Create ((uint)SmEvt.E.Success, "IMAPATTSUCC");
            } else {
                NcAssert.True (result.isError ());
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, result);
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSUCCHARD0");
            }
        }

        private NcResult FetchAttachment (McPending pending)
        {
            McEmailMessage email = McEmailMessage.QueryByServerId<McEmailMessage> (BEContext.Account.Id, pending.ServerId);
            if (null == email) {
                Log.Error (Log.LOG_IMAP, "Could not find email for ServerId {0}", pending.ServerId);
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed);
            }
            var attachment = McAttachment.QueryById<McAttachment> (pending.AttachmentId);
            if (null == attachment) {
                Log.Error (Log.LOG_IMAP, "Could not find attachment for Id {0}", pending.AttachmentId);
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed);
            }
            var folder = McFolder.QueryByImapGuid (BEContext.Account.Id, ImapProtoControl.ImapMessageFolderGuid (email.ServerId)).FirstOrDefault();
            if (null == folder) {
                Log.Error (Log.LOG_IMAP, "Could not find folder with ImapGuid {0}", ImapProtoControl.ImapMessageFolderGuid (email.ServerId));
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed);
            }
            var mailKitFolder = GetOpenMailkitFolder (folder);
            // TODO Use GetStream with subclassed routines for direct-to-disk downloads
            var st = mailKitFolder.GetStream (ImapProtoControl.ImapMessageUid (email.ServerId), attachment.FileReference, Cts.Token);
            var path = attachment.GetFilePath ();
            using (var fileStream = new FileStream (path, FileMode.OpenOrCreate, FileAccess.ReadWrite)) {
                st.CopyTo(fileStream);
            }
            st.Close ();
            attachment.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.Complete);
            attachment.Truncated = false;
            attachment.Update ();
            return NcResult.Info (NcResult.SubKindEnum.Info_AttDownloadUpdate);
        }
    }
}
