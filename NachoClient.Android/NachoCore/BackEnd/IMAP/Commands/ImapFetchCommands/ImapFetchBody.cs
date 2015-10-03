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

            McFolder folder = McFolder.QueryByServerId (AccountId, ParentId);
            var mailKitFolder = GetOpenMailkitFolder (folder);

            BodyPart imapBody = null;
            HeaderList headers;
            McAbstrFileDesc.BodyTypeEnum bodyType = McAbstrFileDesc.BodyTypeEnum.None;
            if (!string.IsNullOrEmpty (email.ImapBodyStructure)) {
                if (!BodyPart.TryParse (email.ImapBodyStructure, out imapBody)) {
                    Log.Error (Log.LOG_IMAP, "Couldn't reconstitute ImapBodyStructure");
                } else {
//                    var debugStr = new MemoryStream ();
//                    DumpToStream (debugStr, imapBody);
//                    debugStr.Flush ();
//                    Log.Info (Log.LOG_IMAP, "Imap Body: {0}", Encoding.ASCII.GetString (debugStr.GetBuffer ()));
                    bodyType = BodyTypeFromBodyPart (imapBody);
                }
            }
            if (bodyType == McAbstrFileDesc.BodyTypeEnum.None &&
                !string.IsNullOrEmpty (email.Headers)) {
                headers = ParseHeaders (email.Headers);
                if (null != headers && headers.Any ()) {
                    bodyType = BodyTypeFromHeaders (headers);
                }
            }

            NcResult result;
            var uid = new UniqueId (email.ImapUid);
            if (null == imapBody || McAbstrFileDesc.BodyTypeEnum.None == bodyType) {
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

            List<DownloadPart> Parts;
            if (CanDownloadAll (imapBody, out Parts)) {
                result = DownloadEntireMessage (ref body, mailKitFolder, uid, imapBody);
            } else {
                result = DownloadIndividualParts (email, ref body, mailKitFolder, uid, Parts, imapBody.ContentType.Boundary);
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

        private uint DownloadAttachTotalSizeWithCommStatus ()
        {
            return (uint)(NetStatusSpeedEnum.CellSlow_2 == NcCommStatus.Instance.Speed ? 50 * 1024 : 100 * 1024);
        }

        private uint DownloadAttachSizeWithCommStatus ()
        {
            return (uint)(NetStatusSpeedEnum.CellSlow_2 == NcCommStatus.Instance.Speed ? 1 * 1024 : 5 * 1024);
        }

        private uint MaxPartsWithCommStatus ()
        {
            return (uint)(NetStatusSpeedEnum.CellSlow_2 == NcCommStatus.Instance.Speed ? 10 : 50);
        }

        private bool CanDownloadAll (BodyPart body, out List<DownloadPart> Parts)
        {
            Parts = new List<DownloadPart> ();
            uint attachSize = 0;
            uint attachCount = CountAttachments (body, ref attachSize);
            bool downloadAll = false;
            uint partCount = CountDownloadableParts (body, Parts);
            if (attachCount == 0) {
                // no attachments Just download it all.
                downloadAll = true;
            } else if (attachSize < DownloadAttachTotalSizeWithCommStatus ()) {
                // total attachment size is within acceptable range. Download all.
                downloadAll = true;
            } else if (partCount == 1 && attachCount == 0) {
                // There's only one part (the main one). Download it whole.
                downloadAll = true;
            }

            if (!downloadAll && partCount > MaxPartsWithCommStatus ()) {
                // there's too many individual parts. Don't download them separately.
                // TODO Should probably also check the size.
                downloadAll = true;
            }
            return downloadAll;
        }

        class ImapFetchDnldInvalidPartException: Exception
        {
            public ImapFetchDnldInvalidPartException (string message) : base (message)
            {
                
            }
        }
        class DownloadPart
        {
            int AccountId;
            public string PartSpecifier { get; protected set; }
            public string MimeType { get; protected set; }
            public List<DownloadPart> Parts { get; set; }
            public string Boundary { get; protected set; }

            public bool HeadersOnly { get; protected set; }
            public int Length { get; protected set; }
            public int Offset { get; protected set; }
            public bool DownloadAll {
                get {
                    return (Offset == 0 && Length == -1);
                }
                set {
                    HeadersOnly = false;
                    Offset = 0;
                    Length = -1;
                }
            }
            public ITransferProgress ProgressOwner;

            public DownloadPart (int accountId, BodyPart part, bool headersOnly, ITransferProgress Owner = null)
            {
                PartSpecifier = part.PartSpecifier;
                HeadersOnly = headersOnly;
                MimeType = part.ContentType.MimeType;
                Boundary = part.ContentType.Boundary;

                if (string.IsNullOrEmpty (PartSpecifier)) {
                    throw new ImapFetchDnldInvalidPartException ("PartSpecifier can not be empty");
                }
                ProgressOwner = Owner;
                AccountId = accountId;
                DownloadAll = true;
            }

            public override string ToString ()
            {
                string me = string.Format ("{0} {1}:{2}", this.GetType ().Name, PartSpecifier, MimeType);
                if (!string.IsNullOrEmpty (Boundary)) {
                    me += string.Format (" Boundary={0}", Boundary);
                }
                if (null != Parts) {
                    me += string.Format (" SubParts={0}", Parts.Count);
                }
                if (!DownloadAll) {
                    me += string.Format (" <{0}..{1}", Offset, Length);
                }
                return me;
            }

            public void Truncate ()
            {
                HeadersOnly = true;
                Length = 0;
                Offset = 0;
            }

            public void Subset (int offset, int length)
            {
                NcAssert.True (length >= 0 && offset >= 0);
                if (offset == 0 && length == 0) {
                    Truncate ();
                }
                HeadersOnly = false;
                Offset = offset;
                Length = length;
            }

            public void Download (NcImapFolder mailKitFolder, UniqueId uid, Stream stream, CancellationToken Token)
            {
                DownloadMimeHeaders (mailKitFolder, uid, stream, Token);
                if (!HeadersOnly) {
                    DownloadPartData (mailKitFolder, uid, stream, Token);
                }
            }

            public void DownloadMimeHeaders (NcImapFolder mailKitFolder, UniqueId uid, Stream stream, CancellationToken Token)
            {
                var tmp = NcModel.Instance.TmpPath (AccountId);
                try {
                    mailKitFolder.SetStreamContext (uid, tmp);
                    NcCapture.AddKind (KImapFetchPartCommandFetch);
                    using (var cap = NcCapture.CreateAndStart (KImapFetchPartCommandFetch)) {
                        Log.Info (Log.LOG_IMAP, "Fetching HEADERS {0}", this.ToString ());
                        using (Stream st = mailKitFolder.GetStream (uid, PartSpecifier + ".MIME", Token, ProgressOwner)) {
                            st.CopyTo (stream);
                        }
                    }
                } finally {
                    mailKitFolder.UnsetStreamContext ();
                    File.Delete (tmp);
                }
            }

            public void DownloadPartData (NcImapFolder mailKitFolder, UniqueId uid, Stream stream, CancellationToken Token)
            {
                var tmp = NcModel.Instance.TmpPath (AccountId);
                try {
                    mailKitFolder.SetStreamContext (uid, tmp);
                    NcCapture.AddKind (KImapFetchPartCommandFetch);
                    using (var cap = NcCapture.CreateAndStart (KImapFetchPartCommandFetch)) {
                        if (DownloadAll) {
                            Log.Info (Log.LOG_IMAP, "Fetching Entire Part {0}", this.ToString ());
                            using (Stream st = mailKitFolder.GetStream (uid, PartSpecifier, Token, ProgressOwner)) {
                                st.CopyTo (stream);
                            }
                        } else {
                            Log.Info (Log.LOG_IMAP, "Fetching Part {0}", this.ToString ());
                            using (Stream st = mailKitFolder.GetStream (uid, PartSpecifier, Offset, Length, Token, ProgressOwner)) {
                                st.CopyTo (stream);
                            }
                        }
                    }
                } finally {
                    mailKitFolder.UnsetStreamContext ();
                    File.Delete (tmp);
                }
            }
        }

        private uint CountDownloadableParts (BodyPart body, List<DownloadPart> Parts, uint depth = 0)
        {
            if (depth > 10) {
                throw new Exception ("CountDownloadableParts: Recursion excceeds max of 10");
            }
            uint count = 0;
            var multi = body as BodyPartMultipart;
            if (null != multi) {
                DownloadPart d = null;
                if (!string.IsNullOrEmpty (multi.PartSpecifier)) {
                    d = new DownloadPart (AccountId, multi, true, this);
                }
                var newParts = new List<DownloadPart> ();
                foreach (var part in multi.BodyParts) {
                    count += CountDownloadableParts (part, newParts, depth + 1);
                }
                if (null != d) {
                    d.Parts = newParts;
                    Parts.Add (d);
                } else {
                    Parts.AddRange (newParts);
                }
                return count;
            }
            var basic = body as BodyPartBasic;
            if (null != basic) {
                if (!string.IsNullOrEmpty (basic.PartSpecifier)) {
                    DownloadPart d = new DownloadPart (AccountId, basic, false, this);
                    bool isAttachment = basic.IsAttachment;
                    if (isAttachment && isExchangeATTAttachment(basic)) {
                        isAttachment = false;
                    }
                    if (isAttachment && basic.Octets > DownloadAttachSizeWithCommStatus ()) {
                        d.Truncate ();
                    }
                    Parts.Add (d);
                }
            } else {
                Log.Error (Log.LOG_IMAP, "Unhandled BodyPart {0}. Downloading it whole.", body.GetType ().Name);
                if (!string.IsNullOrEmpty (multi.PartSpecifier)) {
                    Parts.Add (new DownloadPart (AccountId, body, false, this));
                }
            }
            return 1;
        }

        private static bool isExchangeATTAttachment (BodyPartBasic basic)
        {
            if (basic.IsAttachment && basic.ContentType.Matches ("text", "*")) {
                var regex = new Regex (@"^ATT\d{5,}\.(txt|html?)$");
                if (regex.IsMatch (basic.ContentDisposition.FileName)) {
                    return true;
                }
            }
            return false ;      
        }

        private uint CountAttachments (BodyPart body, ref uint Size, uint depth = 0)
        {
            if (depth > 10) {
                throw new Exception ("CountAttachments: Recursion excceeds max of 10");
            }
            uint count = 0;
            var multi = body as BodyPartMultipart;
            if (null != multi) {
                foreach (var part in multi.BodyParts) {
                    count += CountAttachments (part, ref Size, depth + 1);
                }
                return count;
            }

            var basic = body as BodyPartBasic;
            if (null != basic) {
                if (basic.IsAttachment) {
                    Size += basic.Octets;
                    return 1;
                }
            }
            return 0;
        }

        private void DumpToStream (Stream stream, BodyPart body, int depth = 0)
        {
            if (depth > 10) {
                throw new Exception ("DumpToStream: Recursion excceeds max of 10");
            }
            var multi = body as BodyPartMultipart;
            if (null != multi) {
                WriteString (stream, string.Format ("{0}{1}:{2}\n", String.Concat(Enumerable.Repeat("  ", depth)),
                    multi.PartSpecifier.Length > 0 ? multi.PartSpecifier : "MAINBODY", multi.ContentType.MimeType));
                foreach (var part in multi.BodyParts) {
                    DumpToStream (stream, part, depth + 1);
                }
            }

            var basic = body as BodyPartBasic;
            if (null != basic) {
                WriteString (stream, string.Format ("{0}{1}:{2}\n", String.Concat(Enumerable.Repeat("  ", depth)), basic.PartSpecifier, basic.ContentType.MimeType));
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
                    Log.Info (Log.LOG_IMAP, "Fetching part {0}:{1}", imapBody.PartSpecifier, imapBody.ContentType.MimeType);
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
        private NcResult DownloadIndividualParts (McEmailMessage email, ref McBody body, NcImapFolder mailKitFolder, UniqueId uid, List<DownloadPart> Parts, string boundary)
        {
            NcAssert.True (null != Parts && Parts.Any ());
            var tmp = NcModel.Instance.TmpPath (AccountId);
            try {
                using (FileStream stream = new FileStream (tmp, FileMode.CreateNew)) {
                    WriteString (stream, email.Headers);
                    foreach (var part in Parts) {
                        WriteBoundary (stream, boundary, false);
                        DownloadIndividualPart(stream, mailKitFolder, uid, part);
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

        private void DownloadIndividualPart (Stream stream, NcImapFolder mailKitFolder, UniqueId uid, DownloadPart Part, int depth = 0)
        {
            if (depth > 10) {
                throw new Exception ("DownloadIndividualPart: Recursion excceeds max of 10");
            }
            Part.Download (mailKitFolder, uid, stream, Cts.Token);
            if (null != Part.Parts) {
                foreach (var part in Part.Parts) {
                    WriteBoundary (stream, Part.Boundary, false);
                    DownloadIndividualPart (stream, mailKitFolder, uid, part, depth + 1);
                }
                WriteBoundary (stream, Part.Boundary, true);
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

        private HeaderList ParseHeaders (string headers)
        {
            var stream = new MemoryStream (Encoding.ASCII.GetBytes (headers));
            var parser = new MimeParser (ParserOptions.Default, stream);
            return parser.ParseHeaders (Cts.Token);
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

            McAbstrFileDesc.BodyTypeEnum bodyType = BodyTypeFromBodyPart (summary.Body);
            if (bodyType == McAbstrFileDesc.BodyTypeEnum.None) {
                bodyType = BodyTypeFromHeaders (summary.Headers);
            }

            if (bodyType == McAbstrFileDesc.BodyTypeEnum.None) {
                return NcResult.Error (string.Format ("No Body Type found"));
            }

            NcResult result = NcResult.OK ();
            result.Value = bodyType;
            return result;
        }

        public static McAbstrFileDesc.BodyTypeEnum BodyTypeFromHeaders (HeaderList headers)
        {
            McAbstrFileDesc.BodyTypeEnum bodyType = McAbstrFileDesc.BodyTypeEnum.None;
            if (null == headers) {
                Log.Warn (Log.LOG_IMAP, "No headers.");
            } else {
                if (headers.Contains (HeaderId.MimeVersion)) {
                    bodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
                }
            }
            return bodyType;
        }

        public static McAbstrFileDesc.BodyTypeEnum BodyTypeFromBodyPart (BodyPart body)
        {
            McAbstrFileDesc.BodyTypeEnum bodyType = McAbstrFileDesc.BodyTypeEnum.None;
            if (null != body && null == body.ContentType) {
                Log.Warn (Log.LOG_IMAP, "No ContentType found in body.");
            }

            if (null != body && null != body.ContentType) {
                // If we have a body and a content type, get the body type from that.
                if (body.ContentType.Matches ("multipart", "*")) {
                    bodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
                } else if (body.ContentType.Matches ("text", "*")) {
                    if (body.ContentType.Matches ("text", "html")) {
                        bodyType = McAbstrFileDesc.BodyTypeEnum.HTML_2;
                    } else if (body.ContentType.Matches ("text", "plain")) {
                        bodyType = McAbstrFileDesc.BodyTypeEnum.PlainText_1;
                    } else {
                        Log.Warn (Log.LOG_IMAP, "Unhandled text subtype {0}", body.ContentType.MediaSubtype);
                    }
                } else {
                    Log.Warn (Log.LOG_IMAP, "Unhandled contenttype {0}:{1}", body.ContentType.MediaType, body.ContentType.MediaSubtype);
                }
            } 

            return bodyType;
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
