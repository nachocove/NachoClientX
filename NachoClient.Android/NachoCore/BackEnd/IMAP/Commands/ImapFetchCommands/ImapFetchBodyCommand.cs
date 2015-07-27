//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using MimeKit;
using MailKit;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Model;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace NachoCore.IMAP
{
    public class ImapFetchBodyCommand : ImapFetchCommand
    {
        public ImapFetchBodyCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            pending.MarkDispached ();
            PendingSingle = pending;
        }

        protected override Event ExecuteCommand ()
        {
            NcResult result = null;
            result = ProcessPending (PendingSingle);
            if (result.isInfo ()) {
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, result);
                });
                return Event.Create ((uint)SmEvt.E.Success, "IMAPBDYSUCC");
            } else {
                NcAssert.True (result.isError ());
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, result);
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPBDYHRD0");
            }
        }

        private NcResult ProcessPending (McPending pending)
        {
            McPending.Operations op;
            if (null == pending) {
                // If there is no pending, then we are doing an email prefetch.
                op = McPending.Operations.EmailBodyDownload;
            } else {
                op = pending.Operation;
            }
            switch (op) {
            case McPending.Operations.EmailBodyDownload:
                return FetchOneBody (pending);

            default:
                NcAssert.True (false, string.Format ("ItemOperations: inappropriate McPending Operation {0}", pending.Operation));
                break;
            }
            return NcResult.Error ("Unknown operation");
        }

        private NcResult FetchOneBody (McPending pending)
        {
            McEmailMessage email = McAbstrItem.QueryByServerId<McEmailMessage> (BEContext.Account.Id, pending.ServerId);
            if (null == email) {
                Log.Error (Log.LOG_IMAP, "Could not find email for {0}", pending.ServerId);
                return NcResult.Error ("Unknown email ServerId");
            }

            NcResult result;
            McFolder folder = McFolder.QueryByServerId (BEContext.Account.Id, pending.ParentId);
            var mailKitFolder = GetOpenMailkitFolder (folder);

            UniqueId uid = new UniqueId (email.ImapUid);
            McAbstrFileDesc.BodyTypeEnum bodyType;
            result = messageBodyPart (uid, mailKitFolder, out bodyType);
            if (!result.isOK ()) {
                return result;
            }
            BodyPart part = result.GetValue<BodyPart> ();

            var tmp = NcModel.Instance.TmpPath (BEContext.Account.Id);
            mailKitFolder.SetStreamContext (uid, tmp);
            McBody body;
            if (0 == email.BodyId) {
                body = new McBody () {
                    AccountId = BEContext.Account.Id,
                    BodyType = bodyType,
                };
                body.Insert ();
            } else {
                body = McBody.QueryById<McBody> (email.BodyId);
            }

            try {
                Stream st = mailKitFolder.GetStream (uid, part.PartSpecifier, Cts.Token, this);
                var path = body.GetFilePath ();
                using (var bodyFile = new FileStream (path, FileMode.OpenOrCreate, FileAccess.Write)) {
                    // TODO Do we need to filter by Content-Transfer-Encoding?
                    switch (body.BodyType) {
                    default:
                        // Mime is good for us. Just copy over to the proper file
                        // FIXME: We don't just use the body.GetFilePath(), because MailKit has a bug
                        // where it doesn't release the stream handles properly, and not using a temp file
                        // leads to Sharing violations. This is fixed in more recent versions of MailKit.
                        st.CopyTo(bodyFile);
                        break;

                    case McAbstrFileDesc.BodyTypeEnum.HTML_2:
                    case McAbstrFileDesc.BodyTypeEnum.PlainText_1:
                        // Text and Mime get downloaded with the RFC822 mail headers. Copy the stream
                        // to the proper place and remove the headers while we're doing so.
                        CopyBody(st, bodyFile);
                        break;
                    }
                }
                body.Truncated = false;
                body.UpdateSaveFinish ();

                email.BodyId = body.Id;
                email.Update ();

                if (string.IsNullOrEmpty (email.BodyPreview)) {
                    // The Sync didn't create a preview. Do it now.
                    var preview = BodyToPreview (body);
                    if (!string.IsNullOrEmpty (preview)) {
                        email = email.UpdateWithOCApply<McEmailMessage> ((record) => {
                            var target = (McEmailMessage)record;
                            target.BodyPreview = preview;
                            return true;
                        });
                        StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                    }
                }

                result = NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded);
            } catch (ImapCommandException ex) {
                Log.Warn (Log.LOG_IMAP, "ImapCommandException: {0}", ex.Message);
                // TODO Probably want to narrow this down. Pull in latest MailKit and make it compile.
                // The message doesn't exist. Delete it locally.
                Log.Warn (Log.LOG_IMAP, "ImapFetchBodyCommand: no message found. Deleting local copy");
                body.DeleteFile ();
                body.Delete ();
                email.Delete ();
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                result = NcResult.Error ("No Body found");
            }
            mailKitFolder.UnsetStreamContext ();
            return result;
        }

        /// <summary>
        /// Copies a downloaded email from one stream to another, skipping the rfc822 mail headers.
        /// The headers are separated from the body by an empty line, so look for that, and write everything after.
        /// </summary>
        /// <param name="src">Source stream</param>
        /// <param name="dst">Dst stream</param>
        /// <param name="terminator">Terminator (default "\r\n")</param>
        private void CopyBody (Stream src, Stream dst, string terminator = "\r\n")
        {
            // TODO Need to pass in the encoding, instead of assuming UTF8?
            Encoding enc = Encoding.UTF8;
            int bufsize = 1024;
            string line;
            bool skip = true;
            using (var streamReader = new StreamReader (src, enc, true, bufsize)) {
                using (var streamWriter = new StreamWriter (dst, enc, bufsize, true)) {
                    streamWriter.NewLine = terminator;
                    while ((line = streamReader.ReadLine ()) != null) {
                        if (skip && line.Equals (string.Empty)) {
                            skip = false;
                            continue; // skip the empty line. Next iteration will start writing.
                        }
                        if (!skip) {
                            streamWriter.WriteLine (line);
                        }
                    }
                }
            }
        }   

        /// <summary>
        /// Find the message part of for a give UID. This makes a FETCH query to the server, similar to what sync
        /// does (i.e. fetching BODYSTRUCTURE and some other things), but doesn't do the full fetch that sync does.
        /// It then analyzes the BODYSTRUCTURE to find the relevant part we want to download.
        /// </summary>
        /// <returns>The body part.</returns>
        /// <param name="uid">Uid.</param>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// <param name="bodyType">Body type.</param>
        private NcResult messageBodyPart(UniqueId uid, IMailFolder mailKitFolder, out McAbstrFileDesc.BodyTypeEnum bodyType)
        {
            bodyType = McAbstrFileDesc.BodyTypeEnum.None;
            NcResult result;
            var UidList = new List<UniqueId> ();
            UidList.Add (uid);
            MessageSummaryItems flags = MessageSummaryItems.BodyStructure | MessageSummaryItems.UniqueId;
            HashSet<HeaderId> headers = new HashSet<HeaderId> ();
            headers.Add (HeaderId.MimeVersion);
            headers.Add (HeaderId.ContentType);
            headers.Add (HeaderId.ContentTransferEncoding);

            var isummary = mailKitFolder.Fetch (UidList, flags, headers, Cts.Token);
            if (null == isummary || isummary.Count < 1) {
                return NcResult.Error (string.Format ("Could not get summary for uid {0}", uid));
            }

            var summary = isummary [0] as MessageSummary;
            if (null == summary) {
                return NcResult.Error ("Could not convert summary to MessageSummary");
            }

            result = BodyTypeFromSummary (summary);
            if (!result.isOK ()) {
                return result;
            }
            bodyType = result.GetValue<McAbstrFileDesc.BodyTypeEnum> ();

            var part = summary.Body;
            result = NcResult.OK ();
            result.Value = part;
            return result;
        }

        /// <summary>
        /// Given an IMAP summary, figure out the ContentType, and return the 
        /// McAbstrFileDesc.BodyTypeEnum that corresponds to it.
        /// </summary>
        /// <returns>The type from summary.</returns>
        /// <param name="summary">Summary.</param>
        private NcResult BodyTypeFromSummary (MessageSummary summary)
        {
            McAbstrFileDesc.BodyTypeEnum bodyType;
            var part = summary.Body;

            if (summary.Headers.Contains (HeaderId.MimeVersion) || part.ContentType.Matches ("multipart", "*")) {
                bodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
            } else if (part.ContentType.Matches ("text", "*")) {
                if (part.ContentType.Matches ("text", "html")) {
                    bodyType = McAbstrFileDesc.BodyTypeEnum.HTML_2;
                } else if (part.ContentType.Matches ("text", "plain")) {
                    bodyType = McAbstrFileDesc.BodyTypeEnum.PlainText_1;
                } else {
                    return NcResult.Error (string.Format ("Unhandled text subtype {0}", part.ContentType.MediaSubtype));
                }
            } else {
                return NcResult.Error (string.Format ("Unhandled mime subtype {0}", part.ContentType.MediaSubtype));
            }
            NcResult result = NcResult.OK ();
            result.Value = bodyType;
            return result;
        }

        /// <summary>
        /// Given a McBody, generate the preview from it.
        /// Note: Similar functionality exists in MimeHelpers, but this is attempting to do everything it can
        /// with files instead of memory. More work needed and we should combine this with the MimeHelpers.
        /// </summary>
        /// <returns>The to preview.</returns>
        /// <param name="body">Body.</param>
        /// <param name="previewLength">Preview length.</param>
        private static string BodyToPreview (McBody body, int previewLength = 500)
        {
            string preview = string.Empty;
            using (var bodyFile = new FileStream (body.GetFilePath (), FileMode.Open, FileAccess.Read)) {
                switch (body.BodyType) {
                case McAbstrFileDesc.BodyTypeEnum.HTML_2:
                    // TODO This reads the entire file into memory. Perhaps not the best idea?
                    preview = Html2Text (bodyFile);
                    break;

                case McAbstrFileDesc.BodyTypeEnum.PlainText_1:
                    byte[] pbytes = new byte[previewLength];
                    bodyFile.Read (pbytes, 0, previewLength);
                    preview = Encoding.UTF8.GetString (pbytes);
                    break;

                case McAbstrFileDesc.BodyTypeEnum.MIME_4:
                    // TODO This reads the entire file into memory. Perhaps not the best idea?
                    MimeMessage mime = MimeHelpers.LoadMessage (body);
                    string html;
                    string text;
                    if (MimeHelpers.FindText (mime, out html, out text)) {
                        if (!string.IsNullOrEmpty (text)) {
                            preview = text;
                        } else if (!string.IsNullOrEmpty (html)) {
                            preview = Html2Text (html);
                        }
                    }
                    break;
                }
            }
            if (preview.Length > previewLength) {
                preview = preview.Substring (0, previewLength);
            }
            return preview;
        }
    }
}
