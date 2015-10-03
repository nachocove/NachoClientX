//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System.Collections.Generic;
using MailKit;
using NachoCore.Model;
using System;
using MailKit.Search;
using MimeKit;
using NachoCore.Utils;
using System.Linq;
using NachoCore.ActiveSync;

namespace NachoCore.IMAP
{
    public class SyncKit
    {

        // TODO Strategy Optimizations:
        // 1) Instead of passing a simple list (or set) of uniqueId's, send a set, where each element is UID+Flags+headers.
        //    Reason: We only need the full NewMessageFlags for NEW messages. When we're trying to determine Deleted or Flags-change,
        //    we only need MessageSummaryItems.Flags and MessageSummaryItems.UniqueId. This will give us a much faster
        //    server-response.
        //    Perhaps instead UID+Flags_headers, have a set of "check for new" and a set for "check for changes"?

        public enum MethodEnum
        {
            Sync,
            OpenOnly,
            QuickSync,
        };

        /// <summary>
        /// The type of synckit
        /// </summary>
        public MethodEnum Method;

        /// <summary>
        /// The folder to sync.
        /// </summary>
        public McFolder Folder;

        /// <summary>
        /// Message Summary Flags. Tunes IMAP fetch behavior
        /// </summary>
        public MessageSummaryItems Flags;

        /// <summary>
        /// PendingSingle is null if Strategy decided to Sync.
        /// </summary>
        public McPending PendingSingle;

        /// <summary>
        /// A list of Uid's to sync.
        /// </summary>
        public IList<UniqueId> SyncSet;

        /// <summary>
        /// The headers to fetch for the summary fetch.
        /// </summary>
        public HashSet<HeaderId> Headers;

        /// <summary>
        /// Whether to get Previews during fetching of the summary.
        /// </summary>
        public bool GetPreviews;

        /// <summary>
        /// List of message Id's of messages we need to upload/sync to the server.
        /// </summary>
        public List<NcEmailMessageIndex> UploadMessages;

        /// <summary>
        /// Whether to fetch the RAW headers when getting the message summary.
        /// This is not related to the Headers list above.
        /// </summary>
        public bool GetHeaders;

        /// <summary>
        /// The Sync Span
        /// </summary>
        public uint Span;

        public SyncKit (McFolder folder)
        {
            Method = MethodEnum.OpenOnly;
            Folder = folder;
        }

        public SyncKit (McFolder folder, uint span, McPending pending, MessageSummaryItems flags, HashSet<HeaderId> headers)
        {
            Method = MethodEnum.QuickSync;
            Folder = folder;
            Span = span;
            Flags = flags;
            Headers = headers;
            GetPreviews = true;
            GetHeaders = true;
            PendingSingle = pending;
        }

        public SyncKit (McFolder folder, IList<UniqueId> uidset, MessageSummaryItems flags, HashSet<HeaderId> headers)
        {
            Method = MethodEnum.Sync;
            Folder = folder;
            SyncSet = uidset;
            Flags = flags;
            Headers = headers;
            GetPreviews = true;
            GetHeaders = true;
        }

        public static UniqueIdSet MustUniqueIdSet (IList<UniqueId> uids)
        {
            if (uids is UniqueIdSet) {
                return uids as UniqueIdSet;
            } else {
                return null != uids ? new UniqueIdSet (uids) : new UniqueIdSet ();
            }
        }

        public override string ToString ()
        {
            string me = string.Format ("SyncKit {0} (Type {{{1}}}", Folder.ImapFolderNameRedacted (), Method.ToString ());
            switch (Method) {
            case MethodEnum.Sync:
                me += string.Format (" Flags {{{0}}}", Flags);
                me += string.Format (" SyncSet {{{0}}}", SyncSet.ToString ());
                me += string.Format (" UploadMessages {{{0}}}", null != UploadMessages ? UploadMessages.Count : 0);
                break;

            default:
                break;
            }
            if (null != PendingSingle) {
                me += " pending=true";
            }
            me += ")";
            return me;
        }
    }

    public class FetchKit
    {
        public class FetchBody
        {
            public string ParentId { get; set; }
            public string ServerId { get; set; }
            public List<DownloadPart>Parts { get; set; }
        }

        public List<FetchBody> FetchBodies { get; set; }
        public List<McAttachment> FetchAttachments { get; set; }

        public class DownloadPart
        {
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

            public class ImapFetchDnldInvalidPartException: Exception
            {
                public ImapFetchDnldInvalidPartException (string message) : base (message)
                {

                }
            }

            public DownloadPart (BodyPart part, bool headersOnly)
            {
                PartSpecifier = part.PartSpecifier;
                HeadersOnly = headersOnly;
                MimeType = part.ContentType.MimeType;
                Boundary = part.ContentType.Boundary;

                if (string.IsNullOrEmpty (PartSpecifier)) {
                    throw new ImapFetchDnldInvalidPartException ("PartSpecifier can not be empty");
                }
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
        }
    }


    public interface IImapStrategy
    {
        SyncKit GenSyncKit (ref McProtocolState protocolState, NcApplication.ExecutionContextEnum exeCtxt, McPending pending);
        SyncKit GenSyncKit (McProtocolState protocolState, McPending pending);
        SyncKit GenSyncKit (ref McProtocolState protocolState, McFolder folder, McPending pending, bool quickSync);

        FetchKit GenFetchKit ();
        FetchKit GenFetchKitHints ();
    }
}
