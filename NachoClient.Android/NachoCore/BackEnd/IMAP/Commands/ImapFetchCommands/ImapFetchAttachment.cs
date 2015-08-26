﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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
    public partial class ImapFetchCommand
    {
        private NcResult FetchAttachments (FetchKit fetchkit)
        {
            NcResult result = null;
            foreach (var attachment in fetchkit.FetchAttachments) {
                McEmailMessage email = McEmailMessage.QueryById<McEmailMessage> (attachment.ItemId);
                McFolder folder = FolderFromEmail (email);
                if (null == folder) {
                    return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed);
                }
                var fetchResult = FetchAttachment (folder, attachment, email);
                if (!fetchResult.isOK ()) {
                    Log.Error (Log.LOG_IMAP, "FetchAttachments: {0}", fetchResult.GetMessage ());
                    result = fetchResult;
                }
            }
            if (null != result) {
                return result;
            }
            return NcResult.OK ();
        }

        private NcResult FetchAttachment (McPending pending)
        {
            McEmailMessage email = McEmailMessage.QueryByServerId<McEmailMessage> (BEContext.Account.Id, pending.ServerId);
            if (null == email) {
                Log.Error (Log.LOG_IMAP, "Could not find email for ServerId {0}", pending.ServerId);
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed, NcResult.WhyEnum.BadOrMalformed);
            }
            var attachment = McAttachment.QueryById<McAttachment> (pending.AttachmentId);
            if (null == attachment) {
                Log.Error (Log.LOG_IMAP, "Could not find attachment for Id {0}", pending.AttachmentId);
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed, NcResult.WhyEnum.BadOrMalformed);
            }
            McFolder folder = FolderFromEmail (email);
            if (null == folder) {
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed, NcResult.WhyEnum.ConflictWithServer);
            }
            return FetchAttachment (folder, attachment, email);
        }

        private NcResult FetchAttachment (McFolder folder, McAttachment attachment, McEmailMessage email)
        {
            var mailKitFolder = GetOpenMailkitFolder (folder);
            var part = attachmentBodyPart (new UniqueId(email.ImapUid), mailKitFolder, attachment.FileReference);
            if (null == part) {
                Log.Error (Log.LOG_IMAP, "Could not find part with PartSpecifier {0} in summary", attachment.FileReference);
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed, NcResult.WhyEnum.MissingOnServer);
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
                throw;
            } finally {
                mailKitFolder.UnsetStreamContext ();
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
                return null;
            }
            var summary = isummary[0] as MessageSummary;
            return summary.BodyParts.Where (x => x.PartSpecifier == fileReference).FirstOrDefault ();
        }

        private McFolder FolderFromEmail (McEmailMessage email)
        {
            // We don't really care which folder this email/attachment is in. If it's duplicated in multiple
            // folders, the email and attachment will be the same. So find the first map and from that the folder.
            var map = McMapFolderFolderEntry.QueryByFolderEntryIdClassCode (email.AccountId, email.Id, McAbstrFolderEntry.ClassCodeEnum.Email).FirstOrDefault ();
            if (null == map) {
                Log.Error (Log.LOG_IMAP, "Could not find folder for EmailId {0}", email.Id);
                return null;
            }
            return McFolder.QueryById<McFolder> (map.FolderId); // could be null!
        }
    }
}
