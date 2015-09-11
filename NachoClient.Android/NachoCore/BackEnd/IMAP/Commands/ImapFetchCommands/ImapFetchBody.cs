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
using MimeKit.IO;
using MimeKit.IO.Filters;
using System;
using System.Threading;

namespace NachoCore.IMAP
{
    public partial class ImapFetchCommand
    {
        const string KImapFetchBodyCommandFetch = "ImapFetchBodyCommand.Fetch";

        private NcResult FetchBodies (FetchKit fetchkit)
        {
            NcResult result = null;
            foreach (var body in fetchkit.FetchBodies) {
                var fetchResult = FetchOneBody (body.ServerId, body.ParentId);
                if (fetchResult.isError ()) {
                    Log.Error (Log.LOG_IMAP, "FetchBodies: {0}", fetchResult);
                    // TODO perhaps we should accumulate all errors into one, instead of
                    // just returning the last one. But since we log them here, it should
                    // be ok.
                    result = fetchResult;
                }
            }
            if (null != result) {
                return result;
            }
            return NcResult.OK ();
        }

        private NcResult FetchOneBody (McPending pending)
        {
            return FetchOneBody (pending.ServerId, pending.ParentId);
        }

        private NcResult FetchOneBody (string ServerId, string ParentId)
        {
            McEmailMessage email = McAbstrItem.QueryByServerId<McEmailMessage> (AccountId, ServerId);
            if (null == email) {
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: Could not find email for {0}", ServerId);
                return NcResult.Error ("Unknown email ServerId");
            }
            Log.Info (Log.LOG_IMAP, "ImapFetchBodyCommand: fetching body for email {0}:{1}", email.Id, email.ServerId);

            NcResult result;
            McFolder folder = McFolder.QueryByServerId (AccountId, ParentId);
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
                    AccountId = AccountId,
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
                var tmp = NcModel.Instance.TmpPath (AccountId);
                mailKitFolder.SetStreamContext (uid, tmp);
                NcCapture.AddKind (KImapFetchBodyCommandFetch);
                long bytes;
                using (var cap = NcCapture.CreateAndStart (KImapFetchBodyCommandFetch)) {
                    using (Stream st = mailKitFolder.GetStream (uid, part.PartSpecifier, Cts.Token, this)) {
                        bytes = st.Length;
                        var path = body.GetFilePath ();
                        using (var bodyFile = new FileStream (path, FileMode.OpenOrCreate, FileAccess.Write)) {
                            switch (body.BodyType) {
                            default:
                                // Mime is good for us. Just copy over to the proper file
                                // FIXME: We don't just use the body.GetFilePath(), because MailKit has a bug
                                // where it doesn't release the stream handles properly, and not using a temp file
                                // leads to Sharing violations. This is fixed in more recent versions of MailKit.
                                st.CopyTo (bodyFile);
                                break;

                            case McAbstrFileDesc.BodyTypeEnum.HTML_2:
                            case McAbstrFileDesc.BodyTypeEnum.PlainText_1:
                                var basic = part as BodyPartBasic;
                                string TransferEncoding = string.Empty;
                                if (null != basic) {
                                    TransferEncoding = basic.ContentTransferEncoding;
                                }
                                // Text and Mime get downloaded with the RFC822 mail headers. Copy the stream
                                // to the proper place and remove the headers while we're doing so.
                                CopyFilteredStream(st, bodyFile, basic.ContentType.Charset, TransferEncoding, CopyBodyWithoutHeaderAction, Cts.Token);
                                break;
                            }
                        }
                    }
                    cap.Pause ();
                    float kBytes = (float)bytes / (float)1024.0;
                    Log.Info (Log.LOG_IMAP, "ImapFetchBodyCommand: Body download of size {0}k took {1}ms ({2}k/sec; {3})",
                        bytes, cap.ElapsedMilliseconds,
                        kBytes / ((float)cap.ElapsedMilliseconds / (float)1000.0), NcCommStatus.Instance.Speed);
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
                result = NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded);
                BackEnd.Instance.BodyFetchHints.RemoveHint (AccountId, email.Id);
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
        /// <param name = "Token">CancellationToken</param>
        private void CopyBodyWithoutHeaderAction (Stream src, Stream dst, CancellationToken Token)
        {
            string terminator = "\r\n";
            string line;
            bool skip = true;
            int bytes_written = 0;
            try {
                using (var streamReader = new StreamReader (src)) {
                    using (var streamWriter = new StreamWriter (dst)) {
                        streamWriter.NewLine = terminator;
                        while ((line = streamReader.ReadLine ()) != null) {
                            Token.ThrowIfCancellationRequested ();
                            if (skip && line.Equals (string.Empty)) {
                                skip = false;
                                continue; // skip the empty line. Next iteration will start writing.
                            }
                            if (!skip) {
                                streamWriter.WriteLine (line);
                                bytes_written += line.Length;
                            }
                        }
                    }
                }
            } catch (ArgumentException) {
                // happens if there was nothing written to the stream, or MimeKit tries to convert with an empty buffer
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
        private NcResult messageBodyPart (UniqueId uid, IMailFolder mailKitFolder, out McAbstrFileDesc.BodyTypeEnum bodyType)
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
                Log.Info (Log.LOG_IMAP, "Could not get summary for uid {0}", uid);
                return NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed,
                    NcResult.WhyEnum.MissingOnServer);
            }

            var summary = isummary [0] as MessageSummary;
            if (null == summary) {
                Log.Error (Log.LOG_IMAP, "Could not convert summary to MessageSummary");
                return NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed,
                    NcResult.WhyEnum.Unknown);

            }
            var part = summary.Body;
            if (null == part) {
                // No body fetched.
                Log.Error (Log.LOG_IMAP, "messageBodyPart: no body");
                return NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed,
                    NcResult.WhyEnum.MissingOnServer);
            }

            result = BodyTypeFromSummary (summary);
            if (!result.isOK ()) {
                // we couldn't find the content type. Try to continue assuming MIME.
                Log.Error (Log.LOG_IMAP, "BodyTypeFromSummary error: {0}. Defaulting to Mime.", result.GetMessage ());
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
        /// <description>
        /// Using the values from the body-summary is much more accurate. If we don't have
        /// any body-summary, then the headers are a good hint. If the header contains
        /// the Mime-version header, then we just default to mime. But that masks some of the
        /// plain parts with funky encodings (transfer encoding).
        /// </description>
        /// <returns>The type from summary.</returns>
        /// <param name="summary">Summary.</param>
        public static NcResult BodyTypeFromSummary (MessageSummary summary)
        {
            McAbstrFileDesc.BodyTypeEnum bodyType = McAbstrFileDesc.BodyTypeEnum.None;

            if (null == summary.Headers && null == summary.Body) {
                return NcResult.Error (string.Format ("No headers nor body."));
            }

            if (null != summary.Body && null == summary.Body.ContentType) {
                Log.Warn (Log.LOG_IMAP, "No ContentType found in body.");
            }

            if (null != summary.Body && null != summary.Body.ContentType) {
                // If we have a body and a content type, get the body type from that.
                if (summary.Body.ContentType.Matches ("multipart", "*")) {
                    bodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
                } else if (summary.Body.ContentType.Matches ("text", "*")) {
                    if (summary.Body.ContentType.Matches ("text", "html")) {
                        bodyType = McAbstrFileDesc.BodyTypeEnum.HTML_2;
                    } else if (summary.Body.ContentType.Matches ("text", "plain")) {
                        bodyType = McAbstrFileDesc.BodyTypeEnum.PlainText_1;
                    } else {
                        Log.Warn (Log.LOG_IMAP, "Unhandled text subtype {0}", summary.Body.ContentType.MediaSubtype);
                    }
                } else {
                    Log.Warn (Log.LOG_IMAP, "Unhandled contenttype {0}:{1}", summary.Body.ContentType.MediaType, summary.Body.ContentType.MediaSubtype);
                }
            } 

            if (bodyType == McAbstrFileDesc.BodyTypeEnum.None && 
                summary.Headers.Contains (HeaderId.MimeVersion)) {
                bodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
            }

            if (bodyType != McAbstrFileDesc.BodyTypeEnum.None) {
                NcResult result = NcResult.OK ();
                result.Value = bodyType;
                return result;
            } else {
                return NcResult.Error (string.Format ("No Body Type found"));
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
