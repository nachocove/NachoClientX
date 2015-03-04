// Copyright (C) 2013, Nacho Cove, Inc.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using SQLite;
using NachoCore;
using NachoCore.Utils;
using MimeKit;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Brain;
using NachoCore.Index;

namespace NachoCore.Model
{
    public enum MessageDeferralType
    {
        None,
        OneHour,
        TwoHours,
        Later,
        EndOfDay,
        Tonight,
        Tomorrow,
        NextWeek,
        MonthEnd,
        NextMonth,
        Forever,
        Custom,
        Weekend,
        ThisWeek,
    };

    public enum NcImportance
    {
        Low_0 = 0,
        Normal_1 = 1,
        High_2 = 2,
    };

    /// From MS-ASEMAIL.pdf, Section 2.2.2.36
    public enum AsLastVerbExecutedType
    {
        UNKNOWN = 0,
        REPLYTOSENDER = 1,
        REPLYTOALL = 2,
        FORWARD = 3,
    }

    public class NcEmailMessageIndex
    {
        public int Id { set; get; }

        public string ThreadId { set; get; }

        public McEmailMessage GetMessage ()
        {
            return McEmailMessage.QueryById<McEmailMessage> (Id);
        }
    }

    public class NcEmailMessageIndexComparer : IEqualityComparer<NcEmailMessageIndex>
    {
        public bool Equals (NcEmailMessageIndex a, NcEmailMessageIndex b)
        {
            return a.Id == b.Id;
        }

        public int GetHashCode (NcEmailMessageIndex i)
        {
            return i.Id;
        }
    }

    public partial class McEmailMessage : McAbstrItem
    {
        private const string CrLf = "\r\n";
        private const string ColonSpace = ": ";

        /// All To addresses, comma separated (optional)
        public string To { set; get; }

        /// Indexes of To in McEmailAddress table
        protected List<int> dbToEmailAddressId { set; get; }

        /// All Cc addresses, comma separated (optional)
        public string Cc { set; get; }

        /// Indexes of Cc in McEmailAddress table
        protected List<int> dbCcEmailAddressId { set; get; }

        /// Email address of the sender (optional)
        public string From { set; get; }

        /// Index of Email in McEmailAddress table
        public int FromEmailAddressId { set; get; }

        public int cachedFromColor { set; get; }

        public int cachedPortraitId { set; get; }

        public string cachedFromLetters { set; get; }

        /// Subject of the message (optional)
        public string Subject { set; get; }

        public enum IntentType
        {
            None = 0,
            FYI = 1,
            PleaseRead = 2,
            ResponseRequired = 3,
            Urgent = 4,
            Important = 5,
        }

        // Intent of the message (default None (0))
        public IntentType Intent { set; get; }

        // Due date of selected intent (optional)
        public DateTime IntentDate { set; get; }

        /// Email addresses for replies, semi-colon separated (optional)
        public string ReplyTo { set; get; }

        /// When the message was received by the current recipient (optional)
        public DateTime DateReceived { set; get; }

        /// List of display names, semi-colon separated (optional)
        public string DisplayTo { set; get; }

        [Indexed]
        /// The topic is used for conversation threading (optional)
        public string ThreadTopic { set; get; }

        /// 0..2, increasing priority (optional)
        public NcImportance Importance { set; get; }

        [Indexed]
        /// Has the message been read? (optional)
        public bool IsRead { set; get; }

        /// A hint from the server (optional)
        public string MessageClass { set; get; }

        /// Sender, maybe not the same as From (optional)
        public string Sender { set; get; }

        /// McEmailAddress Index of Sender
        public int SenderEmailAddressId { set; get; }

        /// The user is on the bcc list (optional)
        public bool ReceivedAsBcc { set; get; }

        /// Conversation id, from Exchange
        public string ConversationId { set; get; }

        /// MIME header Message-ID: unique message identifier (optional)
        public string MessageID { set; get; }

        /// MIME header In-Reply-To: message ids, crlf separated (optional)
        public string InReplyTo { set; get; }

        /// MIME header References: message ids, crlf separated (optional)
        public string References { set; get; }

        /// Specifies how the e-mail is stored on the server (optional)
        public byte NativeBodyType { set; get; }

        /// MIME original code page ID
        public string InternetCPID { set; get; }

        /// Set of timestamps used to generation conversation tree
        public byte[] ConversationIndex { set; get; }

        /// Specifies the content class of the data (optional) - Must be 'urn:content-classes:message' for email
        public string ContentClass { set; get; }

        // The following four fields are used when composing a forward or reply.
        // When composing a new message, ReferencedEmailId should be zero, and the other three are ignored.
        // After the message has been successfully sent, all four fields are ignored.

        /// The DB Id of the message being forwarded or replied to.
        [Indexed]
        public int ReferencedEmailId { set; get; }

        /// Whether or not the body of the referenced message is explicitly included in the body of the outgoing message.
        public bool ReferencedBodyIsIncluded { set; get; }

        /// Whether or not this outgoing message is a forward.
        public bool ReferencedIsForward { set; get; }

        /// Indicates that a forwarded message is waiting for attachments to be downloaded so they can
        /// be included in the outgoing message.
        public bool WaitingForAttachmentsToDownload { set; get; }

        [Ignore]
        /// Internal list of category elements
        protected List<McEmailMessageCategory> _Categories{ get; set; }

        [Ignore]
        /// Internal copy of McMeetingRequest
        protected McMeetingRequest _MeetingRequest { get; set; }

        [Ignore]
        /// List of xml attachments for the email
        public IEnumerable<XElement> xmlAttachments { get; set; }

        /// Cache a bit that says we have attachments
        public bool cachedHasAttachments { get; set; }

        /// Last action (fwd, rply, etc.) that was taken on the message- Used to display an icon (optional)
        public int LastVerbExecuted { set; get; }

        /// Date and time when the action specified by the LastVerbExecuted element was performed on the msg (optional)
        public DateTime LastVerbExecutionTime { set; get; }

        ///
        /// <Flag> STUFF.
        ///

        /// Kind of delay being applied
        /// FIXME - this should go away and be a function of the Sync-ed
        /// data in the model. Otherwise, it won't work right in another 
        /// NachoClient.
        public MessageDeferralType DeferralType { set; get; }

        /// NOTE: These values ARE the AS values.
        public enum FlagStatusValue : uint
        {
            Cleared = 0,
            Complete = 1,
            Active = 2,
        };

        public uint FlagStatus { set; get; }

        /// This is the string associated with the flag.
        public string FlagType { set; get; }

        /// User has asked to hide the message for a while
        [Indexed]
        public DateTime FlagUtcStartDate { set; get; }

        public DateTime FlagStartDate { set; get; }

        // User must complete task by.
        public DateTime FlagUtcDue { set; get; }

        public DateTime FlagDue { set; get; }

        public DateTime FlagDateCompleted { set; get; }

        public DateTime FlagCompleteTime { set; get; }

        public bool FlagReminderSet { set; get; }

        public DateTime FlagReminderTime { set; get; }

        public DateTime FlagOrdinalDate { set; get; }

        public DateTime FlagSubOrdinalDate { set; get; }

        public bool IsIndexed { set; get; }

        ///
        /// </Flag> STUFF.
        ///

        /// Attachments are separate

        //This is strictly used for testing purposes
        public List<McEmailMessageCategory> getInternalCategoriesList ()
        {
            return _Categories;
        }

        /// TODO: Support other types besides mime!
        public Stream ToMime (out long length)
        {
            length = 0;
            var bodyPath = MimePath ();
            if (null == bodyPath) {
                return null;
            }
            var fileStream = new FileStream (bodyPath, FileMode.Open, FileAccess.Read);
            if (null == fileStream) {
                Log.Error (Log.LOG_EMAIL, "BodyPath {0} doesn't find a file.", bodyPath);
                return null;
            }
            length = new FileInfo (bodyPath).Length;
            return fileStream;
        }

        public string MimePath ()
        {
            if (WaitingForAttachmentsToDownload) {
                // TODO This is not the right place to make this call. Move this call to a more
                // appropriate place, once one is found.
                AddMissingAttachmentsToBody ();
            }
            var body = McBody.QueryById<McBody> (BodyId);
            return body.GetFilePath ();
        }

        public void AddMissingAttachmentsToBody ()
        {
            if (!WaitingForAttachmentsToDownload) {
                // Attachments, if any, are already taken care of.
                return;
            }
            var originalAttachments = McAttachment.QueryByItemId (AccountId, ReferencedEmailId, this.GetClassCode ());
            var body = McBody.QueryById<McBody> (BodyId);
            MimeMessage mime = MimeHelpers.LoadMessage (body);
            MimeHelpers.AddAttachments (mime, originalAttachments);
            body.UpdateData ((FileStream stream) => {
                mime.WriteTo (stream);
            });
            WaitingForAttachmentsToDownload = false;
            this.Update ();
        }

        public void ConvertToRegularSend ()
        {
            if (ReferencedEmailId == 0 ||
                (ReferencedBodyIsIncluded && (!ReferencedIsForward || !WaitingForAttachmentsToDownload))) {
                // No conversion necessary.
                return;
            }
            var originalMessage = McEmailMessage.QueryById<McEmailMessage> (ReferencedEmailId);
            if (null == originalMessage) {
                // Original message no longer exists.  There is nothing we can do.
                return;
            }
            var body = McBody.QueryById<McBody> (BodyId);
            var outgoingMime = MimeHelpers.LoadMessage (body);
            if (!ReferencedBodyIsIncluded) {
                // Append the body of the original message to the outgoing message.
                // TODO Be smart about formatting.  Right now everything is forced to plain text.
                string originalBodyText = MimeHelpers.ExtractTextPart (originalMessage);
                string outgoingBodyText = MimeHelpers.ExtractTextPart (outgoingMime);
                MimeHelpers.SetPlainText (outgoingMime, outgoingBodyText + originalBodyText);
            }
            if (ReferencedIsForward && (!ReferencedBodyIsIncluded || WaitingForAttachmentsToDownload)) {
                // Add all the attachments from the original message.
                var originalAttachments = McAttachment.QueryByItemId (originalMessage);
                MimeHelpers.AddAttachments (outgoingMime, originalAttachments);
            }
            body.UpdateData ((FileStream stream) => {
                outgoingMime.WriteTo (stream);
            });
            ReferencedEmailId = 0;
            ReferencedBodyIsIncluded = false;
            ReferencedIsForward = false;
            WaitingForAttachmentsToDownload = false;
            this.Update ();
        }

        public void DeleteAttachments ()
        {
            var atts = McAttachment.QueryByItemId (this);
            foreach (var toNix in atts) {
                toNix.Delete ();
            }
        }

        public static List<NcEmailMessageIndex> QueryInteractions (int accountId, McContact contact)
        {
            if (String.IsNullOrEmpty (contact.GetPrimaryCanonicalEmailAddress ())) {
                return new List<NcEmailMessageIndex> ();
            }

            string emailWildcard = "%" + contact.GetPrimaryCanonicalEmailAddress () + "%";

            // Not all accounts have deleted folder (e.g. Device). Using '0' is a trick.
            McFolder deletedFolder = McFolder.GetDefaultDeletedFolder (accountId);
            var deletedFolderId = ((null == deletedFolder) ? 0 : deletedFolder.Id);

            return NcModel.Instance.Db.Query<NcEmailMessageIndex> (
                "SELECT DISTINCT e.Id as Id, e.ConversationId as ThreadId FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " JOIN McFolder AS f ON m.FolderId = f.Id " +
                " WHERE " +
                " e.AccountId = ? AND " +
                " e.IsAwaitingDelete = 0 AND " +
                " f.IsClientOwned != 1 AND " +
                " m.ClassCode = ? AND " +
                " m.AccountId = ? AND " +
                " m.FolderId != ? AND " +
                " e.[From] LIKE ? OR " +
                " e.[To] Like ? ORDER BY e.DateReceived DESC",
                accountId, accountId, McAbstrFolderEntry.ClassCodeEnum.Email, deletedFolderId, emailWildcard, emailWildcard);
        }

        public static List<NcEmailMessageIndex> QueryActiveMessageItems (int accountId, int folderId)
        {
            return NcModel.Instance.Db.Query<NcEmailMessageIndex> (
                "SELECT e.Id as Id, e.ConversationId as ThreadId FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                " e.AccountId = ? AND " +
                " e.IsAwaitingDelete = 0 AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? AND " +
                " e.FlagUtcStartDate < ? " +
                " ORDER BY e.DateReceived DESC",
                accountId, accountId, McAbstrFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow);
        }

        public static List<NcEmailMessageIndex> QueryActiveMessageItemsByThreadId (int accountId, int folderId, string threadId)
        {
            return NcModel.Instance.Db.Query<NcEmailMessageIndex> (
                "SELECT e.Id as Id, e.ConversationId as ThreadId FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                " e.ConversationId = ? AND " +
                " e.AccountId = ? AND " +
                " e.IsAwaitingDelete = 0 AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? AND " +
                " e.FlagUtcStartDate < ? " +
                " ORDER BY e.DateReceived DESC",
                threadId, accountId, accountId, McAbstrFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow);
        }

        public static int CountOfUnreadMessageItems (int accountId, int folderId)
        {
            return NcModel.Instance.Db.ExecuteScalar<int> (
                "SELECT COUNT(*) FROM McEmailMessage AS e " +
                "JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                "WHERE " +
                "e.AccountId = ?  AND " +
                " e.IsAwaitingDelete = 0 AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? AND " +
                " e.FlagUtcStartDate < ? AND " +
                "e.IsRead = 0", 
                accountId, accountId, McAbstrFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow);
        }

        public static IEnumerable<McEmailMessage> QueryNeedsFetch (int accountId, int limit, double minScore)
        {
            return NcModel.Instance.Db.Query<McEmailMessage> (
                "SELECT e.* FROM McEmailMessage AS e " +
                " LEFT OUTER JOIN McBody AS b ON b.Id = e.BodyId" +
                " WHERE " +
                " e.AccountId = ? AND " +
                " e.IsAwaitingDelete = 0 AND " +
                " e.FlagUtcStartDate < ? AND " +
                " e.UserAction > -1 AND " +
                " (e.Score > ? OR e.UserAction = 1) AND " +
                " ((b.FilePresence != ? AND " +
                "   b.FilePresence != ? AND " +
                "   b.FilePresence != ?) OR " +
                "  e.BodyId = 0) " +
                "UNION " +
                "SELECT e.* FROM McEmailMessage AS e " +
                " LEFT OUTER JOIN McBody AS b ON b.Id = e.BodyId" +
                " JOIN McEmailMessageDependency AS d ON e.Id = d.EmailMessageId " +
                " WHERE " +
                " e.AccountId = ? AND " +
                " e.IsAwaitingDelete = 0 AND " +
                " d.EmailAddressId IN (SELECT a.Id FROM McEmailAddress AS a WHERE a.IsVip != 0) AND " +
                " ((b.FilePresence != ? AND " +
                "   b.FilePresence != ? AND " +
                "   b.FilePresence != ?) OR " +
                "  e.BodyId = 0) " +
                " ORDER BY e.DateReceived DESC LIMIT ?",
                accountId, DateTime.UtcNow, minScore,
                (int)McAbstrFileDesc.FilePresenceEnum.Complete,
                (int)McAbstrFileDesc.FilePresenceEnum.Partial,
                (int)McAbstrFileDesc.FilePresenceEnum.Error,
                accountId,
                (int)McAbstrFileDesc.FilePresenceEnum.Complete,
                (int)McAbstrFileDesc.FilePresenceEnum.Partial,
                (int)McAbstrFileDesc.FilePresenceEnum.Error,
                limit);
        }

        public static List<NcEmailMessageIndex> QueryActiveMessageItemsByScore (int accountId, int folderId, double hotScore)
        {
            return NcModel.Instance.Db.Query<NcEmailMessageIndex> (
                "SELECT e.Id as Id, e.ConversationId as ThreadId, e.DateReceived FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                " e.AccountId = ? AND " +
                " e.IsAwaitingDelete = 0 AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? AND " +
                " e.FlagUtcStartDate < ? AND " +
                " e.UserAction > -1 AND " +
                " (e.Score >= ? OR e.UserAction = 1) " +
                "UNION " +
                "SELECT e.Id as Id, e.ConversationId as ThreadId, e.DateReceived FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " JOIN McEmailMessageDependency AS d ON e.Id = d.EmailMessageId " +
                " WHERE " +
                " e.AccountId = ? AND " +
                " e.IsAwaitingDelete = 0 AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? AND " +
                " d.EmailAddressId IN (SELECT a.Id FROM McEmailAddress AS a WHERE a.IsVip != 0) " +
                " ORDER BY e.DateReceived DESC",
                accountId, accountId, McAbstrFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow, hotScore,
                accountId, accountId, McAbstrFolderEntry.ClassCodeEnum.Email, folderId);
        }

        /// TODO: Need account id
        /// TODO: Delete needs to clean up deferred
        public static List<NcEmailMessageIndex> QueryDeferredMessageItemsAllAccounts ()
        {
            return NcModel.Instance.Db.Query<NcEmailMessageIndex> (
                "SELECT e.Id as Id, e.ConversationId as ThreadId FROM McEmailMessage AS e " +
                " WHERE " +
                " e.IsAwaitingDelete = 0 AND " +
                " e.FlagStatus <> 0 AND " +
                " e.FlagUtcStartDate > ? " +
                " ORDER BY e.DateReceived DESC",
                DateTime.UtcNow);
        }

        /// TODO: Need account id
        /// TODO: Delete needs to clean up deferred
        public static List<NcEmailMessageIndex> QueryDeferredMessageItemsAllAccountsByThreadId (string threadId)
        {
            return NcModel.Instance.Db.Query<NcEmailMessageIndex> (
                "SELECT e.Id as Id, e.ConversationId as ThreadId FROM McEmailMessage AS e " +
                " WHERE " +
                " e.ConversationId = ? AND " +
                " e.IsAwaitingDelete = 0 AND " +
                " e.FlagStatus <> 0 AND " +
                " e.FlagUtcStartDate > ? " +
                " ORDER BY e.DateReceived DESC",
                threadId, DateTime.UtcNow);
        }

        public static List<NcEmailMessageIndex> QueryDueDateMessageItemsAllAccounts ()
        {
            return NcModel.Instance.Db.Query<NcEmailMessageIndex> (
                "SELECT e.Id as Id, e.ConversationId as ThreadId FROM McEmailMessage AS e " +
                " WHERE " +
                " e.IsAwaitingDelete = 0 AND" +
                " e.FlagStatus <> 0 AND" +
                " e.FlagType <> ?", 
                "Defer until");
        }

        public static List<NcEmailMessageIndex> QueryDueDateMessageItemsAllAccountsByThreadId (string threadId)
        {
            return NcModel.Instance.Db.Query<NcEmailMessageIndex> (
                "SELECT e.Id as Id, e.ConversationId as ThreadId FROM McEmailMessage AS e " +
                " WHERE " +
                " e.ConversationId = ? AND" +
                " e.IsAwaitingDelete = 0 AND" +
                " e.FlagStatus <> 0 AND" +
                " e.FlagType <> ?", 
                threadId, "Defer until");
        }

        public static List<McEmailMessage> QueryNeedsIndexing (int maxMessages)
        {
            return NcModel.Instance.Db.Query<McEmailMessage> (
                "SELECT e.* FROM McEmailMessage as e " +
                " JOIN McBody as b ON b.Id == e.BodyId " +
                " WHERE e.IsIndexed = 0 AND " +
                " e.BodyId != 0 AND " +
                " b.FilePresence = ? AND " +
                " b.BodyType = ? " +
                " ORDER BY e.DateReceived DESC LIMIT ?",
                McAbstrFileDesc.FilePresenceEnum.Complete, McAbstrFileDesc.BodyTypeEnum.MIME_4, maxMessages
            );
        }

        public static List<McEmailMessage> QueryByThreadTopic (int accountId, string topic)
        {
            return NcModel.Instance.Db.Table<McEmailMessage> ().Where (
                x => x.AccountId == accountId &&
                x.IsAwaitingDelete == false &&
                x.ThreadTopic == topic).ToList ();
        }

        public static IEnumerable<McEmailMessage>  QueryUnreadAndHotAfter (DateTime since)
        {
            var retardedSince = since.AddDays (-1.0);
            return NcModel.Instance.Db.Table<McEmailMessage> ().Where (x => 
                false == x.IsRead && since < x.CreatedAt && retardedSince < x.DateReceived).OrderByDescending (x => x.CreatedAt);
        }

        public static List<NcEmailMessageIndex> QueryByDateReceivedAndFrom (int accountId, DateTime dateRecv, string from)
        {
            return NcModel.Instance.Db.Query<NcEmailMessageIndex> (
                "SELECT e.Id AS Id, e.ConversationId as ThreadId FROM McEmailMessage AS e WHERE " +
                " e.AccountId = ? AND " +
                " e.IsAwaitingDelete = 0 AND " +
                " e.DateReceived = ? AND " +
                " e.[From] = ? ",
                accountId, dateRecv, from);
        }

        public override ClassCodeEnum GetClassCode ()
        {
            return McAbstrFolderEntry.ClassCodeEnum.Email;
        }

        public const double minHotScore = 0.5;

        /// <summary>
        /// Returns true if this message is hot or if the user has said it is hot.
        /// The UserAction comes into play after the user sets hot/not but before
        /// the brain re-calculates hotness.
        /// </summary>
        /// <returns><c>true</c>, if the message is hot, <c>false</c> otherwise.</returns>
        public bool isHot ()
        {
            if (0 < UserAction) {
                return true;
            }
            if (0 > UserAction) {
                return false;
            }
            return (minHotScore <= this.Score);
        }

        public bool IsDeferred ()
        {
            if ((DateTime.MinValue == FlagStartDate) && (DateTime.MinValue == FlagUtcStartDate)) {
                return false;
            }
            if (DateTime.MinValue != FlagStartDate) {
                return DateTime.Now < FlagStartDate;
            }
            if (DateTime.MinValue != FlagUtcStartDate) {
                return DateTime.UtcNow < FlagUtcStartDate;
            }
            NcAssert.CaseError ();
            return false;
        }

        public bool HasDueDate ()
        {
            if ((DateTime.MinValue == FlagDue) && (DateTime.MinValue == FlagUtcDue)) {
                return false;
            }
            if ((FlagDue == FlagStartDate) && (FlagUtcDue == FlagUtcStartDate)) {
                return false;
            }
            return true;
        }

        public bool IsOverdue ()
        {
            if (HasDueDate ()) {
                if (DateTime.MinValue != FlagDue) {
                    return DateTime.Now > FlagDue;
                }
                if (DateTime.MinValue != FlagUtcDue) {
                    return DateTime.UtcNow > FlagUtcDue;
                }
                NcAssert.CaseError ();
            }
            return false;
        }

        public DateTime FlagDueAsUtc ()
        {
            if (DateTime.MinValue != FlagDue) {
                return FlagDue.ToUniversalTime ();
            }
            if (DateTime.MinValue != FlagUtcDue) {
                return FlagUtcDue.ToUniversalTime ();
            }
            return DateTime.MinValue;
        }
    }

    public class McEmailMessageThread
    {
        List<NcEmailMessageIndex> thread;

        public McEmailMessageThread ()
        {
            thread = new List<NcEmailMessageIndex> ();
        }

        public void Add (NcEmailMessageIndex index)
        {
            if (!thread.Contains (index, new NcEmailMessageIndexComparer ())) {
                thread.Add (index);
            }
        }

        public NcEmailMessageIndex GetEmailMessageIndex (int i)
        {
            return thread.ElementAt (i);
        }

        public McEmailMessage GetEmailMessage (int i)
        {
            return thread.ElementAt (i).GetMessage ();
        }

        /// <summary>
        /// Applies to only the first message of a thread
        /// </summary>
        public McEmailMessage FirstMessageSpecialCase ()
        {
            var message = GetEmailMessage (0);
            return message;
        }

        public int FirstMessageSpecialCaseIndex ()
        {
            return GetEmailMessageIndex (0).Id;
        }

        /// <summary>
        /// Implies that the thread has only a single messages
        /// </summary>
        public McEmailMessage SingleMessageSpecialCase ()
        {
            NcAssert.True (1 == thread.Count);
            var message = GetEmailMessage (0);
            return message;
        }

        public int SingleMessageSpecialCaseIndex ()
        {
            NcAssert.True (1 == thread.Count);
            return GetEmailMessageIndex (0).Id;
        }

        public int Count {
            get {
                return thread.Count;
            }
        }

        public bool HasMultipleMessages ()
        {
            return (1 < Count);
        }

        public string GetThreadId ()
        {
            return thread.ElementAt (0).ThreadId;
        }

        public string GetSubject ()
        {
            return FirstMessageSpecialCase ().Subject;
        }

        public IEnumerator<McEmailMessage> GetEnumerator ()
        {
            using (IEnumerator<NcEmailMessageIndex> ie = thread.GetEnumerator ()) {
                while (ie.MoveNext ()) {
                    var message = ie.Current.GetMessage ();
                    if (null != message) {
                        yield return message;
                    }
                }
            }
        }
    }

    public partial class McEmailMessage
    {
        protected Boolean isAncillaryInMemory;

        public McEmailMessage () : base ()
        {
            _Categories = new List<McEmailMessageCategory> ();
            _MeetingRequest = null;
            isAncillaryInMemory = false;
            dbToEmailAddressId = new List<int> ();
            dbCcEmailAddressId = new List<int> ();
        }

        [Ignore]
        public List<McEmailMessageCategory> Categories {
            get {
                ReadAncillaryData ();
                return _Categories;
            }
            set {
                ReadAncillaryData ();
                _Categories = value;
            }
        }

        [Ignore]
        public McMeetingRequest MeetingRequest {
            get {
                ReadAncillaryData ();
                return _MeetingRequest;
            }
            set {
                ReadAncillaryData ();
                _MeetingRequest = value;
            }
        }

        [Ignore]
        public List<int> ToEmailAddressId {
            get {
                ReadAncillaryData ();
                return dbToEmailAddressId;
            }
            set {
                ReadAncillaryData ();
                dbToEmailAddressId = value;
            }
        }

        [Ignore]
        public List<int> CcEmailAddressId {
            get {
                ReadAncillaryData ();
                return dbCcEmailAddressId;
            }
            set {
                ReadAncillaryData ();
                dbCcEmailAddressId = value;
            }
        }

        protected NcResult ReadAncillaryData ()
        {
            NcAssert.True (!isDeleted);
            if (isAncillaryInMemory) {
                return NcResult.OK ();
            }
            if (0 == Id) {
                isAncillaryInMemory = true;
                return NcResult.OK ();
            }
            _Categories = NcModel.Instance.Db.Table<McEmailMessageCategory> ().Where (x => x.ParentId == Id).ToList ();
            _MeetingRequest = NcModel.Instance.Db.Table<McMeetingRequest> ().Where (x => x.EmailMessageId == Id).SingleOrDefault ();
            isAncillaryInMemory = true;
            dbToEmailAddressId = McMapEmailAddressEntry.QueryMessageToAddressIds (AccountId, Id);
            dbCcEmailAddressId = McMapEmailAddressEntry.QueryMessageCcAddressIds (AccountId, Id);

            return NcResult.OK ();
        }

        protected NcResult InsertAncillaryData ()
        {
            NcAssert.True (0 != Id);

            InsertCategories ();

            if (null != _MeetingRequest) {
                _MeetingRequest.Id = 0;
                _MeetingRequest.EmailMessageId = Id;
                _MeetingRequest.AccountId = AccountId;
                _MeetingRequest.Insert ();
            }

            InsertAddressMaps ();

            return NcResult.OK ();
        }

        protected NcResult InsertCategories ()
        {
            foreach (var c in _Categories) {
                c.Id = 0;
                c.SetParent (this);
                c.Insert ();
            }
            return NcResult.OK ();
        }

        protected void DeleteAncillaryData ()
        {
            NcAssert.True (0 != Id);
            NcModel.Instance.RunInTransaction (() => {
                NcModel.Instance.Db.Query<McEmailMessageCategory> ("DELETE FROM McEmailMessageCategory WHERE ParentID=?", Id);
                NcModel.Instance.Db.Query<McMeetingRequest> ("DELETE FROM McMeetingRequest WHERE EmailMessageId=?", Id);
                DeleteAddressMaps ();
            });
        }

        private void InsertAddressList (List<int> addressIdList, NcEmailAddress.Kind kind)
        {
            foreach (var addressId in addressIdList) {
                var map = CreateAddressMap ();
                map.EmailAddressId = addressId;
                map.AddressType = kind;
                map.Insert ();
            }
        }

        private void InsertAddressMaps ()
        {
            var map = CreateAddressMap ();
            map.EmailAddressId = FromEmailAddressId;
            map.AddressType = NcEmailAddress.Kind.From;
            map.Insert ();
            if (0 < SenderEmailAddressId) {
                map = CreateAddressMap ();
                map.EmailAddressId = SenderEmailAddressId;
                map.AddressType = NcEmailAddress.Kind.Sender;
                map.Insert ();
            }
            InsertAddressList (dbToEmailAddressId, NcEmailAddress.Kind.To);
            InsertAddressList (dbCcEmailAddressId, NcEmailAddress.Kind.Cc);
        }

        private void DeleteAddressMaps ()
        {
            McMapEmailAddressEntry.DeleteMessageMapEntries (AccountId, Id);
        }

        public override int Insert ()
        {
            int returnVal = -1; 

            if (0 == ScoreVersion) {
                // Try to use the contact score for initial email message score
                McEmailAddress emailAddress = GetFromAddress ();
                if (null != emailAddress) {
                    Score = emailAddress.Score;
                }
            }

            NcModel.Instance.RunInTransaction (() => {
                returnVal = base.Insert ();
                InsertAncillaryData ();
            });
              
            return returnVal;
        }

        public override int Update ()
        {
            int returnVal = -1;  

            NcModel.Instance.RunInTransaction (() => {
                returnVal = base.Update ();
                ReadAncillaryData ();
                DeleteAncillaryData ();
                InsertAncillaryData ();
            });

            return returnVal;
        }

        public override void DeleteAncillary ()
        {
            NcAssert.True (0 != Id);
            NcAssert.True (NcModel.Instance.IsInTransaction ());
            if (!IsRead) {
                McEmailAddress emailAddress;
                if (null != From) {
                    bool found = McEmailAddress.Get (AccountId, From, out emailAddress);
                    if (found) {
                        emailAddress.IncrementEmailsDeleted ();
                        emailAddress.UpdateByBrain ();
                    }
                }
            }
            DeleteAttachments ();
            DeleteAncillaryData ();
        }

        public override int Delete ()
        {
            int returnVal = 0;
            NcModel.Instance.RunInTransaction (() => {
                returnVal = base.Delete ();
            });
            NcBrain.SharedInstance.UnindexEmailMessage (this);
            return returnVal;
        }

        public McEmailAddress GetFromAddress ()
        {
            McEmailAddress emailAddress = null;
            var address = NcEmailAddress.ParseMailboxAddressString (From);
            if (null == address) {
                Log.Warn (Log.LOG_BRAIN, "[McEmailMessage:{0}] Cannot parse email address {1}", Id, From);
            } else {
                bool found = McEmailAddress.Get (AccountId, address.Address, out emailAddress);
                if (!found) {
                    Log.Warn (Log.LOG_BRAIN, "[McEmailMessage:{0}] Unknown email address {1}", Id, From);
                }
            }
            return emailAddress;
        }

        public bool IsMeetingInvite (out DateTime endTime)
        {
            endTime = DateTime.MinValue;
            if (null == MeetingRequest) {
                return false; // not a meeting invite
            }
            NcAssert.True (0 < MeetingRequest.EmailMessageId);

            if (MeetingRequest.EndTime > DateTime.MinValue) {
                endTime = MeetingRequest.EndTime;
                return true;
            }
            return false;
        }
    }
}
