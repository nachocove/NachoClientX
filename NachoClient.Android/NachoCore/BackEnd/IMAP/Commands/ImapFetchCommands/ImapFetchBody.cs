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
using System;
using System.Linq;
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
            McEmailMessage email = McAbstrItem.QueryByServerId<McEmailMessage> (AccountId, fetchBody.ServerId);
            if (null == email) {
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: Could not find email for {0}", fetchBody.ServerId);
                return NcResult.Error ("Unknown email ServerId");
            }
            NcResult result = FetchOneBodyInternal (fetchBody, email);
            if (result.isError ()) {
                if (result.Why == NcResult.WhyEnum.MissingOnServer) {
                    // The message doesn't exist. Delete it locally.
                    email.Delete ();
                    BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                }
            }
            return result;
        }

        private NcResult FetchOneBodyInternal (FetchKit.FetchBody fetchBody, McEmailMessage email)
        {
            NcResult result;
            Log.Info (Log.LOG_IMAP, "ImapFetchBodyCommand: fetching body for email {0}:{1}", email.Id, email.ServerId);

            McFolder folder = McFolder.QueryByServerId (AccountId, fetchBody.ParentId);
            var mailKitFolder = GetOpenMailkitFolder (folder);
            UpdateImapSetting (mailKitFolder, ref folder);
            var uid = new UniqueId (email.ImapUid);

            Cts.Token.ThrowIfCancellationRequested ();

            if (string.IsNullOrEmpty (email.ImapBodyStructure)) {
                // backwards compatibility: Current code fills this in, but older code didn't. So fetch it now.
                try {
                    email = FillInBodyStructure (email, mailKitFolder, Cts.Token);
                } catch (MessageNotFoundException ex) {
                    Log.Info (Log.LOG_IMAP, "FillInBodyStructure error: {0}", ex);
                    email = null;
                }
                if (email == null) {
                    return NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed,
                        NcResult.WhyEnum.MissingOnServer);
                }
            }

            Cts.Token.ThrowIfCancellationRequested ();
            if (string.IsNullOrEmpty (email.Headers)) {
                try {
                    // sync didn't fetch them for us. Do it now.
                    email = ImapSyncCommand.FetchHeaders (email, mailKitFolder, Cts.Token);
                } catch (MessageNotFoundException ex) {
                    Log.Info (Log.LOG_IMAP, "FillInBodyStructure error: {0}", ex);
                    return NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed,
                        NcResult.WhyEnum.MissingOnServer);
                }
            }

            BodyPart imapBody;
            if (!BodyPart.TryParse (email.ImapBodyStructure, out imapBody)) {
                Log.Error (Log.LOG_IMAP, "Couldn't reconstitute ImapBodyStructure: {0}", email.ImapBodyStructure);
                return NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed,
                    NcResult.WhyEnum.BadOrMalformed);
            }

            McAbstrFileDesc.BodyTypeEnum bodyType = ImapStrategy.BodyTypeFromBodyPart (imapBody);
            if (bodyType == McAbstrFileDesc.BodyTypeEnum.None &&
                !string.IsNullOrEmpty (email.Headers)) {
                HeaderList headers = ImapStrategy.ParseHeaders (email.Headers, Cts.Token);
                if (null != headers && headers.Any ()) {
                    bodyType = ImapStrategy.BodyTypeFromHeaders (headers);
                }
            }

            if (McAbstrFileDesc.BodyTypeEnum.None == bodyType) {
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: unknown body type {0}. Using Default Mime", bodyType);
                bodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
            }

            McBody body;
            if (0 == email.BodyId) {
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
                        target.BodyType = bodyType;
                        return true;
                    });
                }
            }

            Cts.Token.ThrowIfCancellationRequested ();
            try {
                if (null == fetchBody.Parts || !fetchBody.Parts.Any ()) {
                    result = DownloadEntireMessage (ref body, mailKitFolder, uid, imapBody);
                } else {
                    result = DownloadIndividualParts (email, ref body, mailKitFolder, uid, fetchBody.Parts, imapBody.ContentType.Boundary);
                }
            } catch (MessageNotFoundException) {
                result = NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed, NcResult.WhyEnum.MissingOnServer);
            }

            if (!result.isOK ()) {
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

            Cts.Token.ThrowIfCancellationRequested ();

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
            Cts.Token.ThrowIfCancellationRequested ();

            BackEnd.Instance.BodyFetchHints.RemoveHint (AccountId, email.Id);
            MimeHelpers.PossiblyExtractAttachmentsFromBody (body, email, Cts.Token);

            return NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded);
        }

        /// <summary>
        /// Recursively dump the PartSpecifier+MimeTypeString to the strem for debugging.
        /// Useful for debugging, but not currently used.
        /// </summary>
        /// <param name="stream">Stream.</param>
        /// <param name="body">Body.</param>
        /// <param name="depth">Depth (used to limit recursion).</param>
        private void DumpToStream (Stream stream, BodyPart body, int depth = 0)
        {
            if (depth > 10) {
                throw new Exception ("DumpToStream: Recursion excceeds max of 10");
            }
            var multi = body as BodyPartMultipart;
            if (null != multi) {
                WriteASCIIString (stream, string.Format ("{0}{1}:{2}\n", String.Concat (Enumerable.Repeat ("  ", depth)),
                    multi.PartSpecifier.Length > 0 ? multi.PartSpecifier : "MAINBODY", multi.ContentType.MimeType));
                foreach (var part in multi.BodyParts) {
                    DumpToStream (stream, part, depth + 1);
                }
            }

            var basic = body as BodyPartBasic;
            if (null != basic) {
                WriteASCIIString (stream, string.Format ("{0}{1}:{2}\n", String.Concat (Enumerable.Repeat ("  ", depth)), basic.PartSpecifier, basic.ContentType.MimeType));
            }
            return;
        }

        #region DownloadData

        /// <summary>
        /// Downloads the entire message in one query to the IMAP server.
        /// </summary>
        /// <returns>The entire message.</returns>
        /// <param name="body">Body.</param>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// <param name="uid">Uid.</param>
        /// <param name="imapBody">Imap body.</param>
        private NcResult DownloadEntireMessage (ref McBody body, NcImapFolder mailKitFolder, UniqueId uid, BodyPart imapBody)
        {
            var tmp = NcModel.Instance.TmpPath (AccountId, "msg");
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
                    Log.Info (Log.LOG_IMAP, "ImapFetchBodyCommand: Body download of size {0} took {1}ms ({2}k/sec; {3})",
                        bytes, cap.ElapsedMilliseconds,
                        kBytes / ((float)cap.ElapsedMilliseconds / (float)1000.0), NcCommStatus.Instance.Speed);
                }
                body.UpdateSaveFinish ();
                body.Truncated = false;
            } catch {
                body.DeleteFile ();
                body.Delete ();
                throw;
            } finally {
                mailKitFolder.UnsetStreamContext ();
            }
            return NcResult.OK ();
        }

        /// <summary>
        /// Downloads the individual BodyParts, i.e. walk the List<FetchKit.DownloadPart> Parts and write them to the Stream,
        /// which writes to a tmp file.
        /// </summary>
        /// <description>
        /// This function mimics reading the entire MIME message, but it does so selectively, as dictated by the Parts. The
        /// Parts are assembled by strategy, and include things like "get only the mime header" (e.g. for large attachments)
        /// or "download the entire "multipart/alternative" part as one (instead of its components).
        /// TODO: See if the function can use MimeEntities, so we don't have to write out the mime boundaries, etc, by hand.
        /// </description>
        /// <returns>The individual parts.</returns>
        /// <param name="email">McEmailMessage</param>
        /// <param name="body">McBody</param>
        /// <param name="mailKitFolder">MailKit folder.</param>
        /// <param name="uid">Imap Uid.</param>
        /// <param name="Parts">Parts</param>
        /// <param name="boundary">Top level Boundary (usually from the rfc822 email header</param>
        private NcResult DownloadIndividualParts (McEmailMessage email, ref McBody body, NcImapFolder mailKitFolder, UniqueId uid, List<FetchKit.DownloadPart> Parts, string boundary)
        {
            NcAssert.True (null != Parts && Parts.Any ());
            var tmp = NcModel.Instance.TmpPath (AccountId, "part");
            try {
                using (FileStream stream = new FileStream (tmp, FileMode.CreateNew)) {
                    WriteUTF8String (stream, email.Headers);
                    foreach (var part in Parts) {
                        WriteBoundary (stream, boundary, false);
                        DownloadIndividualPart (stream, mailKitFolder, uid, part);
                    }
                    WriteBoundary (stream, boundary, true);
                }
                body.Truncated = false;
                body.UpdateFileMove (tmp);
                tmp = null;
            } catch {
                body.DeleteFile ();
                body.Delete ();
                throw;
            } finally {
                if (!string.IsNullOrEmpty (tmp)) {
                    File.Delete (tmp);
                }
            }
            return NcResult.OK ();
        }

        /// <summary>
        /// Download a BodyPart. If the BodyPart has children, recursively fetch those as well, adding them to stream.
        /// </summary>
        /// <param name="stream">Stream to write the downloaded part(s) to.</param>
        /// <param name="mailKitFolder">MailKit folder.</param>
        /// <param name="uid">Uid.</param>
        /// <param name="dp">DownloadPart</param>
        /// <param name="depth">Depth (used to limit recursion).</param>
        private void DownloadIndividualPart (Stream stream, NcImapFolder mailKitFolder, UniqueId uid, FetchKit.DownloadPart dp, int depth = 0)
        {
            if (depth > 10) {
                throw new Exception ("DownloadIndividualPart: Recursion excceeds max of 10");
            }
            NcCapture.AddKind (KImapFetchPartCommandFetch);
            using (var cap = NcCapture.CreateAndStart (KImapFetchPartCommandFetch)) {
                if (dp.DownloadAll) {
                    GetBodyPart (stream, mailKitFolder, uid, dp);
                } else {
                    GetBodyPartHeader (stream, mailKitFolder, uid, dp);
                    if (!dp.HeadersOnly) {
                        GetBodyPartData (stream, mailKitFolder, uid, dp);
                    }
                }
            }

            if (dp.Parts.Any ()) {
                foreach (var part in dp.Parts) {
                    WriteBoundary (stream, dp.Boundary, false);
                    DownloadIndividualPart (stream, mailKitFolder, uid, part, depth + 1);
                }
                WriteBoundary (stream, dp.Boundary, true);
            }
        }

        /// <summary>
        /// Gets the entire BodyPart, including the data and the MIME headers.
        /// </summary>
        /// <param name="stream">Stream.</param>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// <param name="uid">Uid.</param>
        /// <param name="dp">DownloadPart.</param>
        private void GetBodyPart (Stream stream, NcImapFolder mailKitFolder, UniqueId uid, FetchKit.DownloadPart dp)
        {
            var tmp = NcModel.Instance.TmpPath (AccountId, "bodypart");
            try {
                mailKitFolder.SetStreamContext (uid, tmp);
                var mime = mailKitFolder.GetBodyPart (uid, dp.PartSpecifier, Cts.Token, this);
                mime.WriteTo (stream);
            } finally {
                mailKitFolder.UnsetStreamContext ();
            }
        }

        /// <summary>
        /// Gets the BodyPart MIME header.
        /// If we aren't going to download the entire data, then adjust the ContentDisposition.Size of the downloaded header.
        /// This is used to omit large attachments from the download.
        /// </summary>
        /// <param name="stream">Stream.</param>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// <param name="uid">Uid.</param>
        /// <param name="dp">DownloadPart.</param>
        private void GetBodyPartHeader (Stream stream, NcImapFolder mailKitFolder, UniqueId uid, FetchKit.DownloadPart dp)
        {
            var tmp = NcModel.Instance.TmpPath (AccountId, "header");
            try {
                mailKitFolder.SetStreamContext (uid, tmp);
                var mime = mailKitFolder.GetBodyPart (uid, dp.PartSpecifier, true, Cts.Token, this);
                if (dp.IsTruncated) {
                    mime.ContentDisposition.Size = dp.Length > 0 ? dp.Length : 0;
                }
                mime.WriteTo (stream);
            } finally {
                mailKitFolder.UnsetStreamContext ();
            }
        }

        /// <summary>
        /// Get only the actual data for the BodyPart, possibly truncated.
        /// </summary>
        /// <param name="stream">Stream.</param>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// <param name="uid">Uid.</param>
        /// <param name="dp">DownloadPart.</param>
        private void GetBodyPartData (Stream stream, NcImapFolder mailKitFolder, UniqueId uid, FetchKit.DownloadPart dp)
        {
            var tmp = NcModel.Instance.TmpPath (AccountId, "data");
            try {
                mailKitFolder.SetStreamContext (uid, tmp);
                if (dp.IsTruncated) {
                    using (Stream st = mailKitFolder.GetStream (uid, dp.PartSpecifier, dp.Offset, dp.Length, Cts.Token, this)) {
                        st.CopyTo (stream);
                    }
                } else {
                    using (Stream st = mailKitFolder.GetStream (uid, dp.PartSpecifier, Cts.Token, this)) {
                        st.CopyTo (stream);
                    }
                }
            } finally {
                mailKitFolder.UnsetStreamContext ();
            }
        }

        /// <summary>
        /// Writes the string to the stream. Assumes ASCII encoding, so the caller should be sure that's OK for the given string.
        /// </summary>
        /// <param name="stream">Stream.</param>
        /// <param name="s">S.</param>
        private void WriteASCIIString (Stream stream, string s)
        {
            byte[] x = Encoding.ASCII.GetBytes (s);
            stream.Write (x, 0, x.Length);
        }

        private void WriteUTF8String (Stream stream, string s)
        {
            byte[] x = Encoding.UTF8.GetBytes (s);
            stream.Write (x, 0, x.Length);
        }

        /// <summary>
        /// Writes the MIME boundary.
        /// </summary>
        /// <param name="stream">Stream.</param>
        /// <param name="boundary">Boundary string</param>
        /// <param name="final">If true, the boundary is the final MIME boundary.</param>
        private void WriteBoundary (Stream stream, string boundary, bool final)
        {
            WriteASCIIString (stream, string.Format ("--{0}{1}\n", boundary, final ? "--" : "")); 
        }

        #endregion

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

        public static McEmailMessage FillInBodyStructure (McEmailMessage email, NcImapFolder mailKitFolder, CancellationToken Token)
        {
            var UidList = new List<UniqueId> ();
            var uid = new UniqueId (email.ImapUid);
            UidList.Add (uid);
            MessageSummaryItems flags = MessageSummaryItems.BodyStructure | MessageSummaryItems.UniqueId;
            var isummary = mailKitFolder.Fetch (UidList, flags, Token);
            if (null == isummary || isummary.Count < 1) {
                Log.Info (Log.LOG_IMAP, "Could not get summary for uid {0}", uid);
                return null;
            }
            var summary = isummary [0] as MessageSummary;
            if (null == summary) {
                Log.Error (Log.LOG_IMAP, "Could not convert summary to MessageSummary");
                return null;
            }

            if (string.IsNullOrEmpty (email.ImapBodyStructure)) {
                // save it for next time we might need it.
                email = email.UpdateWithOCApply<McEmailMessage> ((record) => {
                    var target = (McEmailMessage)record;
                    target.ImapBodyStructure = summary.Body.ToString ();
                    return true;
                });
            }
            return email;
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
