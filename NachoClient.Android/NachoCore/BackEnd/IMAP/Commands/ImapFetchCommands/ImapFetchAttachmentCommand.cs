//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Model;
using NachoCore.Utils;
using System.IO;
using System.Linq;
using MailKit;
using System;
using System.Collections.Generic;
using MimeKit.IO;
using MimeKit.IO.Filters;

namespace NachoCore.IMAP
{
    public class ImapFetchAttachmentCommand : ImapFetchCommand
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

            // We don't really care which folder this email/attachment is in. If it's duplicated in multiple
            // folders, the email and attachment will be the same. So find the first map and from that the folder.
            var map = McMapFolderFolderEntry.QueryByFolderEntryIdClassCode (email.AccountId, email.Id, McAbstrFolderEntry.ClassCodeEnum.Email).FirstOrDefault ();
            if (null == map) {
                Log.Error (Log.LOG_IMAP, "Could not find folder from attachment Id {0}", pending.AttachmentId);
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed);
            }
            McFolder folder = McFolder.QueryById<McFolder> (map.FolderId);
            if (null == folder) {
                Log.Error (Log.LOG_IMAP, "Could not find folder for ImapUid {0}", email.ImapUid);
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed);
            }

            var mailKitFolder = GetOpenMailkitFolder (folder);
            var part = attachmentBodyPart (new UniqueId(email.ImapUid), mailKitFolder, attachment.FileReference);
            if (null == part) {
                Log.Error (Log.LOG_IMAP, "Could not find part with PartSpecifier {0} in summary", attachment.FileReference);
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed);
            }

            var tmp = NcModel.Instance.TmpPath (BEContext.Account.Id);
            mailKitFolder.SetStreamContext (new UniqueId (email.ImapUid), tmp);
            try {
                Stream st = mailKitFolder.GetStream (new UniqueId (email.ImapUid), attachment.FileReference, Cts.Token, this);
                var path = attachment.GetFilePath ();
                using (var attachFile = new FileStream (path, FileMode.OpenOrCreate, FileAccess.Write)) {
                    using (var filtered = new FilteredStream (attachFile)) {
                        filtered.Add (DecoderFilter.Create (part.ContentTransferEncoding));
                        st.CopyTo(filtered);
                    }
                }
                attachment.Truncated = false;
                attachment.UpdateSaveFinish ();
                return NcResult.Info (NcResult.SubKindEnum.Info_AttDownloadUpdate);
            } catch (Exception e) {
                Log.Error (Log.LOG_IMAP, "Could not GetBodyPart: {0}", e);
                attachment.DeleteFile ();
                mailKitFolder.UnsetStreamContext ();
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed);
            }
        }

        private BodyPartBasic attachmentBodyPart(UniqueId uid, IMailFolder mailKitFolder, string fileReference)
        {
            // TODO Perhaps we can store the content transfer encoding in McAttachment,
            // so we don't have to go to the server to get it again.
            var UidList = new List<UniqueId> ();
            UidList.Add (uid);
            MessageSummaryItems flags = MessageSummaryItems.BodyStructure | MessageSummaryItems.UniqueId;
            var isummary = mailKitFolder.Fetch (UidList, flags, Cts.Token);
            if (null == isummary || isummary.Count < 1) {
                Log.Error (Log.LOG_IMAP, "Could not get summary for uid {0}", uid);
            }
            var summary = isummary[0] as MessageSummary;
            return summary.BodyParts.Where (x => x.PartSpecifier == fileReference).FirstOrDefault ();
        }
    }
}
