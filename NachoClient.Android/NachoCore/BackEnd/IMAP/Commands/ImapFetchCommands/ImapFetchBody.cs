﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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
using System.Linq;
using System.Text.RegularExpressions;
using NachoPlatform;
using System.Threading;

namespace NachoCore.IMAP
{
    public partial class ImapFetchCommand
    {
        const string KImapFetchBodyCommandFetch = "ImapFetchBodyCommand.FetchBody";
        const string KImapFetchPartCommandFetch = "ImapFetchBodyCommand.FetchPart";

        private NcResult FetchBodies (FetchKit fetchkit)
        {
            NcResult result = null;
            foreach (var body in fetchkit.FetchBodies) {
                var fetchResult = FetchOneBody (body);
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
            McEmailMessage email = McAbstrItem.QueryByServerId<McEmailMessage> (AccountId, pending.ServerId);
            if (null == email) {
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: Could not find email for {0}", pending.ServerId);
                return NcResult.Error ("Unknown email ServerId");
            }
            var fetchBody = ImapStrategy.FetchBodyFromEmail (email);
            return FetchOneBody (fetchBody);
        }

        private NcResult FetchOneBody (FetchKit.FetchBody fetchBody)
        {
            NcResult result;
            McEmailMessage email = McAbstrItem.QueryByServerId<McEmailMessage> (AccountId, fetchBody.ServerId);
            if (null == email) {
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: Could not find email for {0}", fetchBody.ServerId);
                return NcResult.Error ("Unknown email ServerId");
            }
            Log.Info (Log.LOG_IMAP, "ImapFetchBodyCommand: fetching body for email {0}:{1}", email.Id, email.ServerId);

            McFolder folder = McFolder.QueryByServerId (AccountId, fetchBody.ParentId);
            var mailKitFolder = GetOpenMailkitFolder (folder);
            var uid = new UniqueId (email.ImapUid);

            BodyPart imapBody;
            McAbstrFileDesc.BodyTypeEnum bodyType = ImapStrategy.BodyTypeFromEmail (email, out imapBody, Cts.Token);
            if (McAbstrFileDesc.BodyTypeEnum.None == bodyType) {
                // couldn't determine it from the email message. See if we can get it from the server
                result = messageBodyPart (uid, mailKitFolder, out bodyType);
                if (!result.isOK ()) {
                    return result;
                }
                imapBody = result.GetValue<BodyPart> ();
            }

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

            if (null == fetchBody.Parts) {
                result = DownloadEntireMessage (ref body, mailKitFolder, uid, imapBody);
            } else {
                result = DownloadIndividualParts (email, ref body, mailKitFolder, uid, fetchBody.Parts, imapBody.ContentType.Boundary);
            }
            if (!result.isOK ()) {
                // The message doesn't exist. Delete it locally.
                email.Delete ();
                return result;
            }

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
            MimeHelpers.PossiblyExtractAttachmentsFromBody (body, email);
            return result;
        }

        private void DumpToStream (Stream stream, BodyPart body, int depth = 0)
        {
            if (depth > 10) {
                throw new Exception ("DumpToStream: Recursion excceeds max of 10");
            }
            var multi = body as BodyPartMultipart;
            if (null != multi) {
                WriteString (stream, string.Format ("{0}{1}:{2}\n", String.Concat (Enumerable.Repeat ("  ", depth)),
                    multi.PartSpecifier.Length > 0 ? multi.PartSpecifier : "MAINBODY", multi.ContentType.MimeType));
                foreach (var part in multi.BodyParts) {
                    DumpToStream (stream, part, depth + 1);
                }
            }

            var basic = body as BodyPartBasic;
            if (null != basic) {
                WriteString (stream, string.Format ("{0}{1}:{2}\n", String.Concat (Enumerable.Repeat ("  ", depth)), basic.PartSpecifier, basic.ContentType.MimeType));
            }
            return;
        }

        private NcResult DownloadEntireMessage (ref McBody body, NcImapFolder mailKitFolder, UniqueId uid, BodyPart imapBody)
        {
            var tmp = NcModel.Instance.TmpPath (AccountId);
            try {
                mailKitFolder.SetStreamContext (uid, tmp);
                NcCapture.AddKind (KImapFetchBodyCommandFetch);
                long bytes;
                using (var cap = NcCapture.CreateAndStart (KImapFetchBodyCommandFetch)) {
                    using (Stream st = mailKitFolder.GetStream (uid, imapBody.PartSpecifier, Cts.Token, this)) {
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
                                var basic = imapBody as BodyPartBasic;
                                string TransferEncoding = string.Empty;
                                if (null != basic) {
                                    TransferEncoding = basic.ContentTransferEncoding;
                                }
                                // Text and Mime get downloaded with the RFC822 mail headers. Copy the stream
                                // to the proper place and remove the headers while we're doing so.
                                CopyFilteredStream (st, bodyFile, basic.ContentType.Charset, TransferEncoding, CopyBodyWithoutHeaderAction);
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
                body.UpdateSaveFinish ();
                body.Truncated = false;
            } catch (ImapCommandException ex) {
                Log.Warn (Log.LOG_IMAP, "ImapFetchBodyCommand ImapCommandException: {0}", ex.Message);
                // TODO Probably want to narrow this down. Pull in latest MailKit and make it compile.
                Log.Warn (Log.LOG_IMAP, "ImapFetchBodyCommand: no message found. Deleting local copy");
                if (!string.IsNullOrEmpty (tmp)) {
                    File.Delete (tmp);
                }
                body.DeleteFile ();
                body.Delete ();
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                return NcResult.Error ("No Body found");
            } finally {
                mailKitFolder.UnsetStreamContext ();
            }
            return NcResult.OK ();
        }

        private void WriteBoundary (Stream stream, string boundary, bool final)
        {
            WriteString (stream, string.Format ("--{0}{1}\n", boundary, final ? "--" : "")); 
        }

        private NcResult DownloadIndividualParts (McEmailMessage email, ref McBody body, NcImapFolder mailKitFolder, UniqueId uid, List<FetchKit.DownloadPart> Parts, string boundary)
        {
            NcAssert.True (null != Parts && Parts.Any ());
            var tmp = NcModel.Instance.TmpPath (AccountId);
            try {
                using (FileStream stream = new FileStream (tmp, FileMode.CreateNew)) {
                    WriteString (stream, email.Headers);
                    foreach (var part in Parts) {
                        WriteBoundary (stream, boundary, false);
                        DownloadIndividualPart (stream, mailKitFolder, uid, part);
                    }
                    WriteBoundary (stream, boundary, true);
                }
                body.UpdateFileMove (tmp);
                tmp = null;
                body.Truncated = false;
            } catch (ImapCommandException ex) {
                Log.Warn (Log.LOG_IMAP, "ImapFetchBodyCommand ImapCommandException: {0}", ex.Message);
                // TODO Probably want to narrow this down. Pull in latest MailKit and make it compile.
                Log.Warn (Log.LOG_IMAP, "ImapFetchBodyCommand: no message found. Deleting local copy");
                if (!string.IsNullOrEmpty (tmp)) {
                    File.Delete (tmp);
                }
                body.DeleteFile ();
                body.Delete ();
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                return NcResult.Error ("No Body found");
            } finally {
                if (null != tmp) {
                    File.Delete (tmp);
                }
            }
            return NcResult.OK ();
        }

        private void DownloadIndividualPart (Stream stream, NcImapFolder mailKitFolder, UniqueId uid, FetchKit.DownloadPart dp, int depth = 0)
        {
            if (depth > 10) {
                throw new Exception ("DownloadIndividualPart: Recursion excceeds max of 10");
            }
            NcCapture.AddKind (KImapFetchPartCommandFetch);
            using (var cap = NcCapture.CreateAndStart (KImapFetchPartCommandFetch)) {
                if (dp.DownloadAll) {
                    GetBodyPartFull (stream, mailKitFolder, uid, dp);
                } else {
                    GetBodyPartMimeHeader (stream, mailKitFolder, uid, dp);
                    if (!dp.HeadersOnly) {
                        GetBodyPartData (stream, mailKitFolder, uid, dp);
                    }
                }
            }
            if (null != dp.Parts) {
                foreach (var part in dp.Parts) {
                    WriteBoundary (stream, dp.Boundary, false);
                    DownloadIndividualPart (stream, mailKitFolder, uid, part, depth + 1);
                }
                WriteBoundary (stream, dp.Boundary, true);
            }
        }

        private void GetBodyPartFull (Stream stream, NcImapFolder mailKitFolder, UniqueId uid, FetchKit.DownloadPart dp)
        {
            var tmp = NcModel.Instance.TmpPath (AccountId);
            try {
                mailKitFolder.SetStreamContext (uid, tmp);
                var mime = mailKitFolder.GetBodyPart (uid, dp.PartSpecifier, Cts.Token, this);
                mime.WriteTo (stream);
            } finally {
                mailKitFolder.UnsetStreamContext ();
                File.Delete (tmp);
            }
        }

        private void GetBodyPartMimeHeader (Stream stream, NcImapFolder mailKitFolder, UniqueId uid, FetchKit.DownloadPart dp)
        {
            var tmp = NcModel.Instance.TmpPath (AccountId);
            try {
                mailKitFolder.SetStreamContext (uid, tmp);
                using (Stream st = mailKitFolder.GetStream (uid, dp.PartSpecifier + ".MIME", Cts.Token, this)) {
                    st.CopyTo (stream);
                }
            } finally {
                mailKitFolder.UnsetStreamContext ();
                File.Delete (tmp);
            }
        }

        private void GetBodyPartData (Stream stream, NcImapFolder mailKitFolder, UniqueId uid, FetchKit.DownloadPart dp)
        {
            var tmp = NcModel.Instance.TmpPath (AccountId);
            try {
                mailKitFolder.SetStreamContext (uid, tmp);
                using (Stream st = mailKitFolder.GetStream (uid, dp.PartSpecifier, dp.Offset, dp.Length, Cts.Token, this)) {
                    st.CopyTo (stream);
                }
            } finally {
                mailKitFolder.UnsetStreamContext ();
                File.Delete (tmp);
            }
        }

        /// <summary>
        /// Copies a downloaded email from one stream to another, skipping the rfc822 mail headers.
        /// The headers are separated from the body by an empty line, so look for that, and write everything after.
        /// </summary>
        /// <param name="src">Source stream</param>
        /// <param name="dst">Dst stream</param>
        private void CopyBodyWithoutHeaderAction (Stream src, Stream dst)
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

        private void WriteString (Stream stream, string s)
        {
            byte[] x = Encoding.ASCII.GetBytes (s);
            stream.Write (x, 0, x.Length);
            stream.Flush ();
        }

        /// <summary>
        /// Find the message part of for a give UID. This makes a FETCH query to the server, similar to what sync
        /// does (i.e. fetching BODYSTRUCTURE and some other things), but doesn't do the full fetch that sync does.
        /// It then analyzes the BODYSTRUCTURE to find the relevant part we want to download.
        /// backwards compatibitlity. Older clients will definitely not have the BodyStructure,
        /// and possibly not the Headers (depending on how old they are)
        /// </summary>
        /// <returns>The body part.</returns>
        /// <param name="uid">Uid.</param>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// <param name="bodyType">Body type.</param>
        private NcResult messageBodyPart (UniqueId uid, IMailFolder mailKitFolder, out McAbstrFileDesc.BodyTypeEnum bodyType)
        {
            bodyType = McAbstrFileDesc.BodyTypeEnum.None;
            NcResult result;

            Log.Info (Log.LOG_IMAP, "Fetching summary again");
            var UidList = new List<UniqueId> ();
            UidList.Add (uid);
            MessageSummaryItems flags = MessageSummaryItems.BodyStructure | MessageSummaryItems.UniqueId;
            HashSet<HeaderId> fetchHeaders = new HashSet<HeaderId> ();
            fetchHeaders.Add (HeaderId.MimeVersion);
            fetchHeaders.Add (HeaderId.ContentType);
            fetchHeaders.Add (HeaderId.ContentTransferEncoding);

            var isummary = mailKitFolder.Fetch (UidList, flags, fetchHeaders, Cts.Token);
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
            result.Value = summary.Body;
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
            if (null == summary.Headers && null == summary.Body) {
                return NcResult.Error (string.Format ("No headers nor body."));
            }

            McAbstrFileDesc.BodyTypeEnum bodyType = ImapStrategy.BodyTypeFromBodyPart (summary.Body);
            if (bodyType == McAbstrFileDesc.BodyTypeEnum.None) {
                bodyType = ImapStrategy.BodyTypeFromHeaders (summary.Headers);
            }

            if (bodyType == McAbstrFileDesc.BodyTypeEnum.None) {
                return NcResult.Error (string.Format ("No Body Type found"));
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
