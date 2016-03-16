//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using NachoCore.Model;
using System.Collections.Generic;
using NachoPlatform;
using System.Linq;
using NachoCore.ActiveSync;
using MailKit;
using System.Text.RegularExpressions;
using System;
using MimeKit;
using System.IO;
using System.Text;
using System.Threading;

namespace NachoCore.IMAP
{
    public partial class ImapStrategy
    {
        public const int KBaseFetchSize = 5;

        /// <summary>
        /// The max total size of attachments we're willing to download, depending on Comm Status. Value returned is in bytes.
        /// </summary>
        /// <returns>The attach total size with comm status.</returns>
        private static uint DownloadAttachTotalSizeWithCommStatus ()
        {
            return (uint)(NetStatusSpeedEnum.CellSlow_2 == NcCommStatus.Instance.Speed ? 50 * 1024 : 100 * 1024);
        }

        /// <summary>
        /// The max size we're willing to download per attachment, depending on Comm Status. Value returned is in bytes.
        /// </summary>
        /// <returns>The attach size with comm status.</returns>
        private static uint DownloadAttachSizeWithCommStatus ()
        {
            return (uint)(NetStatusSpeedEnum.CellSlow_2 == NcCommStatus.Instance.Speed ? 1 * 1024 : 5 * 1024);
        }

        /// <summary>
        /// The maximum number of parts we'll download individually. Anything over this would incur more
        /// cost (time, especially) and won't be worth it, i.e. we'll download all at once, instead of one
        /// at a time.
        /// </summary>
        /// <returns>The parts with comm status.</returns>
        private static uint MaxPartsWithCommStatus ()
        {
            return (uint)(NetStatusSpeedEnum.CellSlow_2 == NcCommStatus.Instance.Speed ? 5 : 25);
        }

        // Returns null if nothing to do.
        public FetchKit GenFetchKit ()
        {
            var remaining = KBaseFetchSize;
            var fetchBodies = FetchBodiesFromEmailList (FetchBodyHintList (remaining));
            remaining -= fetchBodies.Count;

            fetchBodies.AddRange (FetchBodiesFromEmailList (McEmailMessage.QueryNeedsFetch (AccountId, remaining, McEmailMessage.minHotScore).ToList ()));
            remaining -= fetchBodies.Count;

            List<McAttachment> fetchAtts = new List<McAttachment> ();
            if (0 < remaining) {
                fetchAtts = McAttachment.QueryNeedsFetch (AccountId, remaining, 0.9, (int)MaxAttachmentSize ()).ToList ();
            }
            if (fetchBodies.Any () || fetchAtts.Any ()) {
                Log.Info (Log.LOG_IMAP, "GenFetchKit: {0} emails, {1} attachments.", fetchBodies.Count, fetchAtts.Count);
                return new FetchKit () {
                    FetchBodies = fetchBodies,
                    FetchAttachments = fetchAtts,
                };
            }
            Log.Info (Log.LOG_IMAP, "GenFetchKit: nothing to do.");
            return null;
        }

        public FetchKit GenFetchKitHints ()
        {
            var fetchBodies = FetchBodiesFromEmailList (FetchBodyHintList (KBaseFetchSize));
            if (fetchBodies.Any ()) {
                Log.Info (Log.LOG_IMAP, "GenFetchKitHints: {0} emails", fetchBodies.Count);
                return new FetchKit () {
                    FetchBodies = fetchBodies,
                    FetchAttachments = new List<McAttachment> (),
                };
            } else {
                return null;
            }
        }

        private List<FetchKit.FetchBody> FetchBodiesFromEmailList (List<McEmailMessage> emails)
        {
            var fetchBodies = new List<FetchKit.FetchBody> ();
            foreach (var email in emails) {
                var body = FetchBodyFromEmail (email);
                if (null != body) {
                    fetchBodies.Add (body);
                }
            }
            return fetchBodies;
        }

        public static FetchKit.FetchBody FetchBodyFromEmail (McEmailMessage email)
        {
            // TODO: all this can be one SQL JOIN.
            var folders = McFolder.QueryByFolderEntryId<McEmailMessage> (email.AccountId, email.Id);
            if (0 == folders.Count) {
                // This can happen - we score a message, and then it gets moved to a client-owned folder.
                return null;
            }
            return new FetchKit.FetchBody () {
                ServerId = email.ServerId,
                ParentId = folders [0].ServerId,
                Parts = DownloadBodyParts (email),
            };
        }

        public static McAbstrFileDesc.BodyTypeEnum BodyTypeFromBodyPart (BodyPart body)
        {
            McAbstrFileDesc.BodyTypeEnum bodyType = McAbstrFileDesc.BodyTypeEnum.None;
            if (null != body && null == body.ContentType) {
                Log.Warn (Log.LOG_IMAP, "No ContentType found in body.");
            }

            if (null != body && null != body.ContentType) {
                // If we have a body and a content type, get the body type from that.
                if (body.ContentType.IsMimeType ("multipart", "*")) {
                    bodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
                } else if (body.ContentType.IsMimeType ("text", "*")) {
                    if (body.ContentType.IsMimeType ("text", "html")) {
                        bodyType = McAbstrFileDesc.BodyTypeEnum.HTML_2;
                    } else if (body.ContentType.IsMimeType ("text", "plain")) {
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

        public static HeaderList ParseHeaders (string headers, CancellationToken Token)
        {
            var stream = new MemoryStream (Encoding.ASCII.GetBytes (headers));
            var parser = new MimeParser (ParserOptions.Default, stream);
            return parser.ParseHeaders (Token);
        }

        /// <summary>
        /// Determine the individual body parts (if any) to download. Returns null if the message as a whole needs to be downloaded.
        /// </summary>
        /// <returns>The body parts or null.</returns>
        /// <param name="email">Email.</param>
        private static List<FetchKit.DownloadPart> DownloadBodyParts (McEmailMessage email)
        {
            BodyPart imapBody = null;
            if (!string.IsNullOrEmpty (email.ImapBodyStructure)) {
                if (!BodyPart.TryParse (email.ImapBodyStructure, out imapBody)) {
                    Log.Error (Log.LOG_IMAP, "Couldn't reconstitute ImapBodyStructure");
                    return null;
                }
            }
            List<FetchKit.DownloadPart> Parts = null;
            if (null != imapBody) {
                if (CanDownloadAll (imapBody, out Parts)) {
                    // make sure whatever is returned is ignored.
                    Parts = null;
                }
            }
            return Parts;
        }

        /// <summary>
        /// Determines if can download all Parts in one query, or if we want to download parts individually.
        /// </summary>
        /// <returns><c>true</c> if can download all the specified body Parts; otherwise, <c>false</c>.</returns>
        /// <param name="body">Body.</param>
        /// <param name="Parts">Parts.</param>
        private static bool CanDownloadAll (BodyPart body, out List<FetchKit.DownloadPart> Parts)
        {
            Parts = new List<FetchKit.DownloadPart> ();
            uint attachSize = 0;
            uint attachCount = CountAttachments (body, ref attachSize);
            bool downloadAll = false;
            uint allDownloadSize = 0;
            GetDownloadableParts (body, Parts, ref allDownloadSize);
            if (attachCount == 0) {
                // no attachments Just download it all.
                downloadAll = true;
            } else if (attachSize < DownloadAttachTotalSizeWithCommStatus ()) {
                // total attachment size is within acceptable range. Download all.
                downloadAll = true;
            } else if (Parts.Count == 1 && attachCount == 0) {
                // There's only one part (the main one). Download it whole.
                downloadAll = true;
            }

            if (!downloadAll && Parts.Count > MaxPartsWithCommStatus ()) {
                // there's too many individual parts. Don't download them separately.
                // TODO Should probably also check the size, but what's a good measure of a total message size?
                downloadAll = true;
            }
            return downloadAll;
        }

        /// <summary>
        /// Get the downloadable parts.
        /// The count of downloadable parts is important in so far as each separate download incurs extra time to ask the
        /// IMAP server for the data.
        /// </summary>
        /// <returns>The downloadable parts.</returns>
        /// <param name="body">Body.</param>
        /// <param name="Parts">Parts.</param>
        /// <param name = "AllPartSize"></param>
        /// <param name="depth">Depth.</param>
        private static void GetDownloadableParts (BodyPart body, List<FetchKit.DownloadPart> Parts, ref uint AllPartSize, uint depth = 0)
        {
            if (depth > 10) {
                throw new Exception ("CountDownloadableParts: Recursion excceeds max of 10");
            }
            var multi = body as BodyPartMultipart;
            if (null != multi) {
                FetchKit.DownloadPart d = null;
                if (!string.IsNullOrEmpty (multi.PartSpecifier)) {
                    d = new FetchKit.DownloadPart (multi, headersOnly: true);
                }
                var newParts = new List<FetchKit.DownloadPart> ();
                if (!multi.ContentType.IsMimeType ("multipart", "alternative")) {
                    foreach (var part in multi.BodyParts) {
                        GetDownloadableParts (part, newParts, ref AllPartSize, depth + 1);
                    }
                }
                if (null != d) {
                    if (!newParts.Any ()) {
                        d.DownloadAll = true;
                    }
                    d.Parts.AddRange (newParts);
                    Parts.Add (d);
                } else {
                    Parts.AddRange (newParts);
                }
                return;
            }

            var basic = body as BodyPartBasic;
            if (null != basic) {
                if (!string.IsNullOrEmpty (basic.PartSpecifier)) {
                    FetchKit.DownloadPart d = new FetchKit.DownloadPart (basic, headersOnly: false);
                    bool isAttachment = basic.IsAttachment;
                    if (isAttachment && isExchangeATTAttachment (basic)) {
                        isAttachment = false;
                    }
                    if (isAttachment && basic.Octets > DownloadAttachSizeWithCommStatus ()) {
                        d.Truncate ();
                    }
                    Parts.Add (d);
                    AllPartSize += basic.Octets;
                }
                return;
            } else {
                Log.Error (Log.LOG_IMAP, "Unhandled BodyPart {0}. Downloading it whole.", body.GetType ().Name);
                if (!string.IsNullOrEmpty (multi.PartSpecifier)) {
                    Parts.Add (new FetchKit.DownloadPart (body, headersOnly: false));
                }
                return;
            }
        }

        private static bool isExchangeATTAttachment (BodyPartBasic basic)
        {
            if (basic.IsAttachment && basic.ContentType.IsMimeType ("text", "*")) {
                if (null == basic.ContentDisposition || string.IsNullOrEmpty (basic.ContentDisposition.FileName)) {
                    return false;
                }
                return MimeHelpers.isExchangeATTFilename (basic.ContentDisposition.FileName);
            }
            return false;      
        }

        private static uint CountAttachments (BodyPart body, ref uint Size, uint depth = 0)
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

    }
}

