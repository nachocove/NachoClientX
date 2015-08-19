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
        const string KImapFetchBodyCommandFetch = "ImapFetchBodyCommand.Fetch";

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
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand failed: {0}", result.Message);
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
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: Could not find email for {0}", pending.ServerId);
                return NcResult.Error ("Unknown email ServerId");
            }
            Log.Info (Log.LOG_IMAP, "ImapFetchBodyCommand: fetching body for email {0}:{1}", email.Id, email.ServerId);

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

            McBody body;
            if (0 == email.BodyId) {
                if (McAbstrFileDesc.BodyTypeEnum.None == bodyType) {
                    Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: unknown body type {0}. Using Default Mime", bodyType);
                    bodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
                }
                body = new McBody () {
                    AccountId = BEContext.Account.Id,
                    BodyType = bodyType,
                };
                body.Insert ();
            } else {
                body = McBody.QueryById<McBody> (email.BodyId);
                if (McAbstrFileDesc.BodyTypeEnum.None == body.BodyType) {
                    Log.Info (Log.LOG_IMAP, "ImapFetchBodyCommand: Existing body for {0} was saved with unknown body type {1}.", email.ServerId, body.BodyType);
                    body = body.UpdateWithOCApply<McBody> ((record) => {
                        var target = (McBody)record;
                        target.BodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
                        return true;
                    });
                }
            }

            try {
                var tmp = NcModel.Instance.TmpPath (BEContext.Account.Id);
                mailKitFolder.SetStreamContext (uid, tmp);
                NcCapture.AddKind (KImapFetchBodyCommandFetch);
                long bytes;
                using (var cap = NcCapture.CreateAndStart (KImapFetchBodyCommandFetch)) {
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
                        bytes = st.Length;
                    }
                    cap.Pause ();
                    float kBytes = (float)bytes/(float)1024.0;
                    Log.Info (Log.LOG_IMAP, "ImapFetchBodyCommand: Body download of size {0}k took {1}ms ({2}k/sec; {3})",
                        bytes, cap.ElapsedMilliseconds,
                        kBytes/((float)cap.ElapsedMilliseconds/(float)1000.0), NcCommStatus.Instance.Speed);
                }
                body.Truncated = false;
                body.UpdateSaveFinish ();
                if (McAbstrFileDesc.BodyTypeEnum.None == body.BodyType) {
                    Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: Body for {0} has unknown body type {1}. Using Default Mime.", email.ServerId, body.BodyType);
                    body = body.UpdateWithOCApply<McBody> ((record) => {
                        var target = (McBody)record;
                        target.BodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
                        return true;
                    });
                }

                email = email.UpdateWithOCApply<McEmailMessage> ((record) => {
                    var target = (McEmailMessage)record;
                    target.BodyId = body.Id;
                    return true;
                });

                if (string.IsNullOrEmpty (email.BodyPreview)) {
                    // The Sync didn't create a preview. Do it now.
                    var preview = BodyToPreview (body);
                    if (!string.IsNullOrEmpty (preview)) {
                        email = email.UpdateWithOCApply<McEmailMessage> ((record) => {
                            var target = (McEmailMessage)record;
                            target.BodyPreview = preview;
                            return true;
                        });
                        var status = NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageChanged);
                        status.Value = email.Id;
                        StatusInd (status);
                    }
                }
                Log.Info (Log.LOG_IMAP, "ImapFetchBodyCommand: Fetched body for email {0}:{1} type {2}", email.Id, email.ServerId, body.BodyType);
                result = NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded);
            } catch (ImapCommandException ex) {
                Log.Warn (Log.LOG_IMAP, "ImapFetchBodyCommand ImapCommandException: {0}", ex.Message);
                // TODO Probably want to narrow this down. Pull in latest MailKit and make it compile.
                // The message doesn't exist. Delete it locally.
                Log.Warn (Log.LOG_IMAP, "ImapFetchBodyCommand: no message found. Deleting local copy");
                body.DeleteFile ();
                body.Delete ();
                email.Delete ();
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                result = NcResult.Error ("No Body found");
            } finally {
                mailKitFolder.UnsetStreamContext ();
            }
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
            var part = summary.Body;
            if (null == part) {
                // No body fetched.
                return NcResult.Error ("messageBodyPart: no body");
            }

            result = BodyTypeFromSummary (summary);
            if (!result.isOK ()) {
                // we couldn't find the content type. Try to continue assuming MIME.
                Log.Error (Log.LOG_IMAP, "BodyTypeFromSummary error: {0}", result.GetMessage ());
                bodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
            } else {
                bodyType = result.GetValue<McAbstrFileDesc.BodyTypeEnum> ();
            }
            if (McAbstrFileDesc.BodyTypeEnum.None == bodyType) {
                // We don't like this, but keep going. The UI will try its best to figure it out.
                Log.Error (Log.LOG_IMAP, "messageBodyPart: unknown body type {0}", bodyType);
            }
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
        public static NcResult BodyTypeFromSummary (MessageSummary summary)
        {
            McAbstrFileDesc.BodyTypeEnum bodyType;

            if (null == summary.Headers && null == summary.Body) {
                return NcResult.Error (string.Format ("No headers nor body."));
            }

            // check headers first, because it's nice and easy.
            if (null != summary.Headers && summary.Headers.Contains (HeaderId.MimeVersion)) {
                NcResult result = NcResult.OK ();
                result.Value = McAbstrFileDesc.BodyTypeEnum.MIME_4;
                return result;
            }

            if (null != summary.Body) {
                var part = summary.Body;
                if (null == part.ContentType) {
                    return NcResult.Error (string.Format ("No ContentType found in body."));
                } else {
                    // If we have a body and a content type, get the body type from that.
                    if (part.ContentType.Matches ("multipart", "*")) {
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
                        return NcResult.Error (string.Format ("Unhandled contenttype {0}:{1}", part.ContentType.MediaType, part.ContentType.MediaSubtype));
                    }
                    NcResult result = NcResult.OK ();
                    result.Value = bodyType;
                    return result;
                }
            } else {
                return NcResult.Error (string.Format ("No Body found"));
            }
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
