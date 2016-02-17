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
    public partial class ImapFetchCommand
    {
        NcResult FetchAttachments (List<FetchKit.FetchAttachment> fetchAttachments)
        {
            NcResult result = null;
            foreach (var fetchAttach in fetchAttachments) {
                var emails = from item in McAttachment.QueryItems (fetchAttach.Attachment.AccountId, fetchAttach.Attachment.Id)
                                         where item is McEmailMessage
                                         select (McEmailMessage)item;
                var email = emails.First (x => !x.ClientIsSender);
                McFolder folder = FolderFromEmail (email);
                if (null == folder) {
                    return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed);
                }
                Log.Info (Log.LOG_IMAP, "Processing DnldAttCmd({0}) for email {1} attachment {2}", AccountId, email.Id, fetchAttach.Attachment.Id);
                var fetchResult = FetchAttachmentInternal (folder, fetchAttach.Attachment, email);
                if (fetchResult.isError ()) {
                    Log.Error (Log.LOG_IMAP, "FetchAttachments: {0}", fetchResult);
                    fetchAttach.Pending.ResolveAsHardFail (BEContext.ProtoControl, result);
                    result = fetchResult;
                } else {
                    fetchAttach.Pending.ResolveAsSuccess (BEContext.ProtoControl, result);
                }
            }
            if (null != result) {
                return result;
            }
            return NcResult.OK ();
        }

        NcResult FetchAttachmentInternal (McFolder folder, McAttachment attachment, McEmailMessage email)
        {
            var mailKitFolder = GetOpenMailkitFolder (folder);
            UpdateImapSetting (mailKitFolder, ref folder);
            BodyPartBasic part;
            try {
                part = attachmentBodyPart (new UniqueId(email.ImapUid), mailKitFolder, attachment.FileReference);
            } catch (MessageNotFoundException) {
                part = null;
            }
            if (null == part) {
                Log.Error (Log.LOG_IMAP, "Could not find part with PartSpecifier {0} in summary", attachment.FileReference);
                email.Delete ();
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed, NcResult.WhyEnum.MissingOnServer);
            }

            var tmp = NcModel.Instance.TmpPath (AccountId, "attach");
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
                if (null != part.ContentType) {
                    attachment.ContentType = part.ContentType.MimeType;
                }
                attachment.Truncated = false;
                attachment.UpdateSaveFinish ();
                return NcResult.Info (NcResult.SubKindEnum.Info_AttDownloadUpdate);
            } catch (MessageNotFoundException) {
                attachment.DeleteFile ();
                email.Delete ();
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed, NcResult.WhyEnum.MissingOnServer);
            } finally {
                mailKitFolder.UnsetStreamContext ();
            }
        }

        BodyPartBasic attachmentBodyPart(UniqueId uid, IMailFolder mailKitFolder, string fileReference)
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

        McFolder FolderFromEmail (McEmailMessage email)
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
