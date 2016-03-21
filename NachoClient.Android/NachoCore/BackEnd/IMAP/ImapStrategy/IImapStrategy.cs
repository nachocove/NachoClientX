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
    public class SyncInstruction
    {
        /// <summary>
        /// Message Summary Flags. Tunes IMAP fetch behavior
        /// </summary>
        public MessageSummaryItems Flags;

        /// <summary>
        /// A list of Uid's to sync.
        /// </summary>
        public IList<UniqueId> UidSet;

        /// <summary>
        /// The headers to fetch for the summary fetch.
        /// </summary>
        public HashSet<string> Headers;

        /// <summary>
        /// Whether to get Previews during fetching of the summary.
        /// </summary>
        public bool GetPreviews;

        /// <summary>
        /// Whether to fetch the RAW headers when getting the message summary.
        /// This is not related to the Headers list above.
        /// </summary>
        public bool GetHeaders;

        public SyncInstruction (IList<UniqueId> uidSet, MessageSummaryItems flags, HashSet<string> headers, bool getPreviews, bool getHeaders)
        {
            UidSet = uidSet;
            Flags = flags;
            Headers = headers;
            GetPreviews = getPreviews;
            GetHeaders = getHeaders;
        }

        public override string ToString ()
        {
            string me = string.Format ("SyncInstruction Flags {{{0}}}", Flags);
            me += string.Format (" UidSet({0})", UidSet.Count);
            if (UidSet.Any ()) {
                me += string.Format (" {{{0}..{1}}}", UidSet.Min (), UidSet.Max ());
            }
            return me;
        }
    }

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
        /// PendingSingle is null if Strategy decided to Sync.
        /// </summary>
        public McPending PendingSingle;

        /// <summary>
        /// List of message Id's of messages we need to upload/sync to the server.
        /// </summary>
        public List<NcEmailMessageIndex> UploadMessages;

        /// <summary>
        /// The sync set.
        /// </summary>
        public List<SyncInstruction> SyncInstructions;

        public SyncKit (McFolder folder, McPending pending)
        {
            Method = MethodEnum.QuickSync;
            Folder = folder;
            SyncInstructions = new List<SyncInstruction> ();
            PendingSingle = pending;
        }

        public SyncKit (McFolder folder, List<SyncInstruction> syncInstructions)
        {
            Method = MethodEnum.Sync;
            Folder = folder;
            SyncInstructions = syncInstructions ?? new List<SyncInstruction> ();
        }

        public uint? MaxSynced {
            get {
                if (!_MaxSynced.HasValue) {
                    MaxMinOfUidSets(SyncInstructions);
                }
                return _MaxSynced;
            }
        }

        public uint? MinSynced {
            get {
                if (!_MinSynced.HasValue) {
                    MaxMinOfUidSets(SyncInstructions);
                }
                return _MinSynced;
            }
        }

        public UniqueIdSet CombinedUidSet {
            get {
                if (null == _CombinedUidSet) {
                    MaxMinOfUidSets(SyncInstructions);
                }
                return _CombinedUidSet;
            }
        }

        uint? _MaxSynced;
        uint? _MinSynced;
        UniqueIdSet _CombinedUidSet;
        void MaxMinOfUidSets (List<SyncInstruction> syncInstructions)
        {
            _CombinedUidSet = new UniqueIdSet ();
            foreach (var syncInst in syncInstructions) {
                if (null != syncInst.UidSet && syncInst.UidSet.Any ()) {
                    _CombinedUidSet.AddRange (syncInst.UidSet);
                    _MaxSynced = Math.Max (syncInst.UidSet.Max ().Id, _MaxSynced.HasValue ? _MaxSynced.Value : uint.MinValue);
                    _MinSynced = Math.Min (syncInst.UidSet.Min ().Id, _MinSynced.HasValue ? _MinSynced.Value : uint.MaxValue);
                }
            }
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
            string me = string.Format ("SyncKit {0} (Type {{{1}}}", Folder.ImapFolderNameRedacted (), Method);
            switch (Method) {
            case MethodEnum.Sync:
                if (SyncInstructions.Any ()) {
                    me += " SyncInstructions {";
                    foreach (var inst in SyncInstructions) {
                        me += string.Format (" {{{0}}}", inst);;
                    }
                    me += "}";
                }
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

            public bool IsAttachment { get; protected set; }

            public List<DownloadPart> Parts { get; set; }

            public string Boundary { get; protected set; }

            public bool HeadersOnly { get; protected set; }

            /// <summary>
            /// Gets or sets the length. A length of -1 means 'ALL'
            /// </summary>
            /// <value>The length.</value>
            public int Length { get; protected set; }

            const int All = -1;

            public int Offset { get; protected set; }

            public bool IsTruncated {
                get {
                    // TODO Using length > All is possibly prone to error, if the caller sets Length to the
                    // actual length of the body part. In that case, IsTruncated will erroneously say true.
                    return Offset != 0 || Length > All;
                }
            }

            public bool DownloadAll {
                get {
                    return (!IsTruncated && HeadersOnly == false);
                }
                set {
                    if (value) {
                        HeadersOnly = false;
                        Offset = 0;
                        Length = All;
                    } else {
                        HeadersOnly = true;
                        Offset = 0;
                        Length = 0;
                    }
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
                if (string.IsNullOrEmpty (part.PartSpecifier)) {
                    throw new ImapFetchDnldInvalidPartException ("PartSpecifier can not be empty");
                }
                PartSpecifier = part.PartSpecifier;
                HeadersOnly = headersOnly;
                MimeType = part.ContentType.MimeType;
                Boundary = part.ContentType.Boundary;
                var basic = part as BodyPartBasic;
                if (null != basic) {
                    IsAttachment = basic.IsAttachment;
                } else {
                    IsAttachment = false;
                }
                Offset = 0;
                Length = All;
                Parts = new List<DownloadPart>();
            }

            public override string ToString ()
            {
                string me = string.Format ("{0} {1}:{2}", this.GetType ().Name, PartSpecifier, MimeType);
                if (!string.IsNullOrEmpty (Boundary)) {
                    me += string.Format (" Boundary={0}", Boundary);
                }
                if (Parts.Any ()) {
                    me += string.Format (" SubParts={0}", Parts.Count);
                }
                if (IsTruncated) {
                    me += string.Format (" <{0}..{1}>", Offset, Length);
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

            public string ToQuery ()
            {
                string query = string.Format ("BODY[{0}.MIME]", PartSpecifier);
                if (!HeadersOnly) {
                    query += string.Format (" BODY[{0}]", PartSpecifier);
                    if (IsTruncated) {
                        query += string.Format ("<{0}..{1}>", Offset, Length);
                    }
                }
                if (Parts.Any ()) {
                    foreach (var dp in Parts) {
                        query += " " + dp.ToQuery ();
                    }
                }
                return query;
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
