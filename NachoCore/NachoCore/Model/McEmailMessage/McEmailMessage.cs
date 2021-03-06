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
using NachoCore.ActiveSync;

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
        DueDate,
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

    public class NcEmailMessageIndex : IComparable
    {
        public int Id { set; get; }

        public NcEmailMessageIndex ()
        {
        }

        public NcEmailMessageIndex (int Id)
        {
            this.Id = Id;
        }

        public McEmailMessage GetMessage ()
        {
            return McEmailMessage.QueryById<McEmailMessage> (Id);
        }

        #region IComparable implementation

        public int CompareTo (object obj)
        {
            var other = obj as NcEmailMessageIndex;
            NcAssert.NotNull (other, string.Format ("CompareTo object is not NcEmailMessageIndex: {0}", obj.GetType ().Name));
            return other.Id.CompareTo (Id);
        }

        #endregion
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

        /// All Cc addresses, comma separated (optional)
        public string Cc { set; get; }

        /// All Bcc addresses, comma separated (optional, for drafts)
        public string Bcc { set; get; }

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

        // Type of the due date for the selected intent (optional)
        public MessageDeferralType IntentDateType { set; get; }

        // QRType of the message (optional, for drafts)
        public NcQuickResponse.QRTypeEnum QRType { set; get; }

        /// Email addresses for replies, semi-colon separated (optional)
        public string ReplyTo { set; get; }

        /// When the message was received by the current recipient (optional)
        [Indexed]
        public DateTime DateReceived { set; get; }

        /// List of display names, semi-colon separated (optional)
        public string DisplayTo { set; get; }

        [Indexed]
        /// The topic is used for conversation threading (optional)
        public string ThreadTopic { set; get; }

        /// 0..2, increasing priority (optional)
        public NcImportance Importance { set; get; }

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
        [Indexed]
        public string ConversationId { set; get; }

        /// MIME header Message-ID: unique message identifier (optional)
        [Indexed]
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
        public byte [] ConversationIndex { set; get; }

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
        /// List of xml attachments for the email
        public IEnumerable<XElement> xmlAttachments { get; set; }

        /// Cache a bit that says we have attachments
        public bool cachedHasAttachments { get; set; }

        /// Last action (fwd, rply, etc.) that was taken on the message- Used to display an icon (optional)
        public int LastVerbExecuted { set; get; }

        /// Date and time when the action specified by the LastVerbExecuted element was performed on the msg (optional)
        public DateTime LastVerbExecutionTime { set; get; }

        /// Must be set when Insert()ing a to-be-send message into the DB.
        public bool ClientIsSender { set; get; }

        /// IMAP Stuff
        [Indexed]
        public uint ImapUid { get; set; }

        /// <summary>
        /// Email headers only. Used for Brain to help with scoring, etc.
        /// </summary>
        /// <value>The headers.</value>
        public string Headers { get; set; }

        /// If true, the message headers match a disqualifying
        /// pattern and this message will be disqualified by brain.
        public bool HeadersFiltered { get; set; }

        /// If true, this message is in a junk folder or has been
        /// classified as junk and will be disqualified by brain.
        public bool IsJunk { get; set; }

        /// <summary>
        /// The Imap BODYSTRUCTURE.
        /// </summary>
        /// <value>The imap body structure.</value>
        public string ImapBodyStructure { get; set; }

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

        [Indexed]
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

        // This field was original a boolean. But in order to support versioning and partial indexing
        // (i.e. indexing of an email message without its body downloaded), it is changed to an int.
        // I didn't want to rename it to IndexVersion (like McContact) in order to avoid a migration.
        //
        // For header indexed messages, the field has the value of EmailMessageDocument.Version-1.
        // For header+body indexed messages, EmailMessageDocument.Version. If a new indexing schema is needed,
        // just increment the version # and implement the new version of EmailMessageDocument.
        // Brain will unindex all old version documents and re-index them using the new schema.
        public int IsIndexed { set; get; }

        ///
        /// </Flag> STUFF.
        ///

        // Whether the email should be notified subjected to the current notification settings.
        // Note that this boolean is only meaningful within the context of a background duration;
        // as the settings can change during foreground.
        public bool ShouldNotify { get; set; }

        /// True if its InReplyTo matches the MessageID of another McEmailMessage whose From
        /// address matches one of the McAccount. Set by brain.
        public bool IsReply { get; set; }

        public bool IsChat { get; set; }

        public bool IsAction { get; set; }

        /// <summary>
        /// Access the <see cref="From"/> property as a list of structured mailboxes.  While it's not common,
        /// it's possible for an email message to have more than one from address.  In such a scenario, the
        /// <see cref="SenderMailbox"/> should specify the actual sender of the message.
        /// </summary>
        /// <value>From mailboxes.</value>
        [Ignore]
        public Mailbox [] FromMailboxes {
            get {
                if (Mailbox.TryParseArray (From, out var mailboxes)) {
                    return mailboxes;
                }
                return new Mailbox [0];
            }
            set {
                From = value?.ToAddressString ();
            }
        }

        /// <summary>
        /// Access the <see cref="Sender"/> property as a structured mailbox.  The sender is used when
        /// a message is sent on behalf of someone else, or when the <see cref="FromMailboxes"/> field contains
        /// multiple mailboxes.
        /// </summary>
        /// <value>The sender mailbox.</value>
        [Ignore]
        public Mailbox? SenderMailbox {
            get {
                if (Mailbox.TryParse (Sender, out var mailbox)) {
                    return mailbox;
                }
                return null;
            }
            set {
                Sender = value.ToString ();
            }
        }

        /// <summary>
        /// Access the <see cref="ReplyTo"/> property as a list of structured mailboxes.  While the reply to
        /// field often has zero on one entries, it's possible to have more than one.
        /// </summary>
        /// <value>The reply to mailboxes.</value>
        [Ignore]
        public Mailbox [] ReplyToMailboxes {
            get {
                if (Mailbox.TryParseArray (ReplyTo, out var mailboxes)) {
                    return mailboxes;
                }
                return new Mailbox [0];
            }
            set {
                ReplyTo = value?.ToAddressString ();
            }
        }

        /// <summary>
        /// Access the <see cref="To"/> property as a list of structured mailboxes.
        /// </summary>
        /// <value>To mailboxes.</value>
        [Ignore]
        public Mailbox [] ToMailboxes {
            get {
                if (Mailbox.TryParseArray (To, out var mailboxes)) {
                    return mailboxes;
                }
                return new Mailbox [0];
            }
            set {
                To = value?.ToAddressString ();
            }
        }

        /// <summary>
        /// Access the <see cref="Cc"/> property as a list of structured mailboxes.
        /// </summary>
        /// <value>To mailboxes.</value>
        [Ignore]
        public Mailbox [] CcMailboxes {
            get {
                if (Mailbox.TryParseArray (Cc, out var mailboxes)) {
                    return mailboxes;
                }
                return new Mailbox [0];
            }
            set {
                Cc = value?.ToAddressString ();
            }
        }

        /// <summary>
        /// Access the <see cref="Bcc"/> property as a list of structured mailboxes.
        /// </summary>
        /// <value>To mailboxes.</value>
        [Ignore]
        public Mailbox [] BccMailboxes {
            get {
                if (Mailbox.TryParseArray (Bcc, out var mailboxes)) {
                    return mailboxes;
                }
                return new Mailbox [0];
            }
            set {
                Bcc = value?.ToAddressString ();
            }
        }

        /// Attachments are separate

        [Ignore]
        public bool IsMeetingRelated {
            get {
                return null != MessageClass && MessageClass.StartsWith ("IPM.Schedule.Meeting.");
            }
        }

        [Ignore]
        public bool IsMeetingRequest {
            get {
                return "IPM.Schedule.Meeting.Request" == MessageClass;
            }
        }

        [Ignore]
        public bool IsMeetingCancelation {
            get {
                return "IPM.Schedule.Meeting.Canceled" == MessageClass;
            }
        }

        [Ignore]
        public bool IsMeetingResponse {
            get {
                return null != MessageClass && MessageClass.StartsWith ("IPM.Schedule.Meeting.Resp.");
            }
        }

        [Ignore]
        public NcResponseType MeetingResponseValue {
            get {
                if (IsMeetingResponse) {
                    switch (MessageClass) {
                    case "IPM.Schedule.Meeting.Resp.Pos":
                        return NcResponseType.Accepted;
                    case "IPM.Schedule.Meeting.Resp.Tent":
                        return NcResponseType.Tentative;
                    case "IPM.Schedule.Meeting.Resp.Neg":
                        return NcResponseType.Declined;
                    }
                }
                return NcResponseType.None;
            }
        }

        /// TODO: Support other types besides mime!
        public FileStream ToMime (out long length)
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
            var pendingAttachments = new List<McAttachment> ();
            var attachments = McAttachment.QueryByItem (this);
            foreach (var attachment in attachments) {
                var mapItem = McMapAttachmentItem.QueryByAttachmentIdItemIdClassCode (AccountId, attachment.Id, Id, GetClassCode ());
                if (!mapItem.IncludedInBody) {
                    mapItem.IncludedInBody = true;
                    mapItem.Update ();
                    pendingAttachments.Add (attachment);
                }
            }
            var body = McBody.QueryById<McBody> (BodyId);
            MimeMessage mime = MimeHelpers.LoadMessage (body);
            MimeHelpers.AddAttachments (mime, pendingAttachments);
            body.UpdateData ((FileStream stream) => {
                mime.WriteTo (stream);
            });
            UpdateWithOCApply<McEmailMessage> ((record) => {
                var target = (McEmailMessage)record;
                target.WaitingForAttachmentsToDownload = false;
                return true;
            });
        }

        public McEmailMessage ConvertToRegularSend ()
        {
            if (ReferencedEmailId == 0 ||
                (ReferencedBodyIsIncluded && (!ReferencedIsForward || !WaitingForAttachmentsToDownload))) {
                // No conversion necessary.
                return this;
            }
            var originalMessage = McEmailMessage.QueryById<McEmailMessage> (ReferencedEmailId);
            if (null == originalMessage) {
                // Original message no longer exists.  There is nothing we can do.
                return this;
            }
            var body = McBody.QueryById<McBody> (BodyId);
            var outgoingMime = MimeHelpers.LoadMessage (body);
            if (!ReferencedBodyIsIncluded) {
                // Append the body of the original message to the outgoing message.
                // TODO Be smart about formatting.  Right now everything is forced to plain text.
                string originalBodyText = MimeHelpers.ExtractTextPart (originalMessage);
                string outgoingBodyText = MimeHelpers.ExtractTextPart (outgoingMime);
                string originalHeaderText = EmailHelper.FormatBasicHeaders (originalMessage);
                MimeHelpers.SetPlainText (outgoingMime, outgoingBodyText + "\n\n" + originalHeaderText + originalBodyText);
            }
            if (ReferencedIsForward && (!ReferencedBodyIsIncluded || WaitingForAttachmentsToDownload)) {
                // Add all the attachments from the original message.
                var originalAttachments = McAttachment.QueryByItem (originalMessage);
                MimeHelpers.AddAttachments (outgoingMime, originalAttachments);
            }
            body.UpdateData ((FileStream stream) => {
                outgoingMime.WriteTo (stream);
            });
            return UpdateWithOCApply<McEmailMessage> ((record) => {
                var target = (McEmailMessage)record;
                target.ReferencedEmailId = 0;
                target.ReferencedBodyIsIncluded = false;
                target.ReferencedIsForward = false;
                target.WaitingForAttachmentsToDownload = false;
                return true;
            });
        }

        public void DeleteAttachments ()
        {
            var atts = McAttachment.QueryByItem (this);
            foreach (var toNix in atts) {
                NcModel.Instance.RunInTransaction (() => {
                    toNix.Unlink (this);
                    if (0 == McMapAttachmentItem.QueryItemCount (toNix.Id)) {
                        toNix.Delete ();
                    }
                });
            }
        }

        public bool StillExists ()
        {
            return McEmailMessage.QueryById<McEmailMessage> (Id) != null;
        }

        public static McEmailMessage MessageWithSubject (McAccount account, string subject)
        {
            var message = new McEmailMessage () {
                ClientIsSender = true,
            };
            message.AccountId = account.Id;
            message.Subject = subject;
            return message;
        }

        public static string SingleAccountString (string formatString, int accountId)
        {
            if (McAccount.GetUnifiedAccount ().Id == accountId) {
                return String.Empty;
            } else {
                return String.Format (formatString, accountId);
            }
        }

        public static List<McEmailMessageThread> QueryInteractions (McContact contact)
        {
            var sql = "SELECT DISTINCT e.Id as FirstMessageId, 1 as MessageCount FROM McContactEmailAddressAttribute a " +
                "JOIN McMapEmailAddressEntry am ON am.EmailAddressId = a.EmailAddress " +
                "JOIN McEmailMessage e ON e.Id = am.ObjectId " +
                "JOIN McMapFolderFolderEntry fm ON fm.FolderEntryId = e.Id " +
                "JOIN McFolder f ON f.Id = fm.FolderId " +
                "WHERE a.ContactId = ? " +
                "AND likelihood (e.IsAwaitingDelete = 0, 1.0) " +
                "AND likelihood (fm.ClassCode = ?, 0.2) " +
                "AND likelihood (e.IsChat = 0, 0.8) " +
                "AND likelihood (f.IsClientOwned != 1, 0.9) " +
                "AND likelihood (f.Type != ?, 0.5) " +
                "ORDER BY e.DateReceived DESC";

            return NcModel.Instance.Db.Query<McEmailMessageThread> (sql, contact.Id, ClassCodeEnum.Email, Xml.FolderHierarchy.TypeCode.DefaultDeleted_4);
        }

        public static List<McEmailMessageThread> QueryActiveMessageItems (int accountId, int folderId, bool groupBy = true)
        {
            var queryFormat =
                "SELECT e.Id as FirstMessageId, " +
                (groupBy ? " MAX(e.DateReceived), Count(e.Id)" : "1") +
                " as MessageCount FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                "{0}" +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.IsChat = 0, 0.8) AND " +
                "{1}" +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (m.FolderId = ?, 0.5) " +
                (groupBy ? " GROUP BY e.ConversationId " : "") +
                " ORDER BY e.DateReceived DESC ";

            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 0.2) AND ", accountId);
            var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 0.2) AND ", accountId);

            var query = String.Format (queryFormat, account0, account1);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, McAbstrFolderEntry.ClassCodeEnum.Email, folderId);
        }

        public static List<McEmailMessageThread> QueryUnreadMessageItems (int accountId, int folderId, bool groupBy = true)
        {
            var queryFormat =
                "SELECT e.Id as FirstMessageId, " +
                (groupBy ? " MAX(e.DateReceived), Count(e.Id)" : "1") +
                " as MessageCount FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                "{0}" +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.IsChat = 0, 0.8) AND " +
                " likelihood (e.IsRead = 0, 0.05) AND " +
                "{1}" +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (m.FolderId = ?, 0.5) " +
                (groupBy ? " GROUP BY e.ConversationId " : "") +
                " ORDER BY e.DateReceived DESC ";

            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 0.2) AND ", accountId);
            var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 0.2) AND ", accountId);

            var query = String.Format (queryFormat, account0, account1);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, McAbstrFolderEntry.ClassCodeEnum.Email, folderId);
        }

        public static List<McEmailMessageThread> QueryUnifiedInboxItems (bool groupBy = true)
        {
            var query =
                "SELECT e.Id as FirstMessageId, " +
                (groupBy ? " MAX(e.DateReceived), Count(e.Id)" : "1") +
                " as MessageCount FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " JOIN McFolder AS f ON f.Id = m.FolderId " +
                " WHERE " +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.IsChat = 0, 0.8) AND " +
                " likelihood (f.Type = ?, 0.2) AND " +
                " likelihood (m.ClassCode = ?, 0.2) " +
                (groupBy ? " GROUP BY e.ConversationId " : "") +
                " ORDER BY e.DateReceived DESC ";

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, Xml.FolderHierarchy.TypeCode.DefaultInbox_2, McAbstrFolderEntry.ClassCodeEnum.Email);
        }

        public static List<McEmailMessageThread> QueryUnreadUnifiedInboxItems (bool groupBy = true)
        {
            var query =
                "SELECT e.Id as FirstMessageId, " +
                (groupBy ? " MAX(e.DateReceived), Count(e.Id)" : "1") +
                " as MessageCount FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " JOIN McFolder AS f ON f.Id = m.FolderId " +
                " WHERE " +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.IsChat = 0, 0.8) AND " +
                " likelihood (e.IsRead = 0, 0.05) AND " +
                " likelihood (f.Type = ?, 0.2) AND " +
                " likelihood (m.ClassCode = ?, 0.2) " +
                (groupBy ? " GROUP BY e.ConversationId " : "") +
                " ORDER BY e.DateReceived DESC ";

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, Xml.FolderHierarchy.TypeCode.DefaultInbox_2, McAbstrFolderEntry.ClassCodeEnum.Email);
        }

        public static List<McEmailMessageThread> QueryActiveMessageItemsByThreadId (int accountId, int folderId, string threadId)
        {
            var queryFormat =
                "SELECT e.Id as FirstMessageId, 1 as MessageCount FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                " likelihood( e.ConversationId = ?, 0.01 ) AND " +
                "{0}" +
                " e.IsAwaitingDelete = 0 AND " +
                "{1}" +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? " +
                " ORDER BY e.DateReceived DESC";

            var account0 = SingleAccountString (" e.AccountId = {0} AND ", accountId);
            var account1 = SingleAccountString (" m.AccountId = {0} AND ", accountId);

            var query = String.Format (queryFormat, account0, account1);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, threadId, McAbstrFolderEntry.ClassCodeEnum.Email, folderId);
        }

        public static List<McEmailMessageThread> QueryActiveMessageItemsInAllFoldersByThreadId (int accountId, string threadId)
        {
            var queryFormat =
                "SELECT MAX(e.Id) as FirstMessageId, 1 as MessageCount FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                " likelihood( e.ConversationId = ?, 0.01 ) AND " +
                "{0}" +
                " e.IsAwaitingDelete = 0 AND " +
                "{1}" +
                " m.ClassCode = ? " +
                " GROUP BY e.MessageId " +
                " ORDER BY e.DateReceived DESC";

            var account0 = SingleAccountString (" e.AccountId = {0} AND ", accountId);
            var account1 = SingleAccountString (" m.AccountId = {0} AND ", accountId);

            var query = String.Format (queryFormat, account0, account1);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (query, threadId, McAbstrFolderEntry.ClassCodeEnum.Email);
        }

        public static int CountOfUnreadMessageItems (int accountId, int folderId, DateTime newSince)
        {
            var queryFormat =
                "SELECT COUNT(*) FROM McEmailMessage AS e " +
                "JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                "WHERE " +
                "{0}" +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.IsChat = 0, 0.8) AND " +
                "{1}" +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (m.FolderId = ?, 0.05) AND " +
                " e.DateReceived >= ? AND " +
                "e.IsRead = 0";

            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 0.2) AND ", accountId);
            var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 0.2) AND ", accountId);

            var query = String.Format (queryFormat, account0, account1);

            return NcModel.Instance.Db.ExecuteScalar<int> (
                query, McAbstrFolderEntry.ClassCodeEnum.Email, folderId, newSince);
        }

        public static IEnumerable<McEmailMessage> QueryNeedsFetch (int accountId, int limit, double minScore)
        {
            var queryFormat =
                "SELECT e.* FROM McEmailMessage AS e " +
                "LEFT OUTER JOIN McBody AS b ON b.Id = e.BodyId " +
                "WHERE {0} " +
                "  likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                "  likelihood (e.UserAction > -1, 0.05) AND " +
                "  (e.Score > ? OR e.UserAction = 1) AND " +
                "  ((b.FilePresence != ? AND b.FilePresence != ? AND b.FilePresence != ?) OR e.BodyId = 0) " +
                "ORDER BY e.DateReceived DESC LIMIT ?";

            var accountString = SingleAccountString (" likelihood (e.AccountId = {0}, 0.2) AND ", accountId);

            var query = String.Format (queryFormat, accountString);

            return NcModel.Instance.Db.Query<McEmailMessage> (
                query, minScore,
                (int)McAbstrFileDesc.FilePresenceEnum.Complete,
                (int)McAbstrFileDesc.FilePresenceEnum.Partial,
                (int)McAbstrFileDesc.FilePresenceEnum.Error,
                limit);
        }

        public static List<McEmailMessageThread> QueryActiveMessageItemsByScore (int accountId, int folderId, double hotScore, bool includeActions = true)
        {
            var queryFormat =
                "SELECT FirstMessageId, Count(FirstMessageId) as MessageCount, DateReceived, ConversationId FROM " +
                " ( " +
                " SELECT e.Id as FirstMessageId, e.DateReceived as DateReceived, e.ConversationId as ConversationId FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                "{0}" +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.IsChat = 0, 0.8) AND " +
                (includeActions ? "" : " likelihood (e.IsAction = 0, 0.8) AND ") +
                "{1}" +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (m.FolderId = ?, 0.05) AND " +
                " likelihood (e.UserAction > -1, 0.99) AND " +
                " (likelihood (e.Score >= ?, 0.1) OR likelihood (e.UserAction = 1, 0.01)) " +
                " ) " +
                " GROUP BY ConversationId " +
                " ORDER BY DateReceived DESC";

            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 0.2) AND ", accountId);
            var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 0.2) AND ", accountId);

            var query = String.Format (queryFormat, account0, account1);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query,
                McAbstrFolderEntry.ClassCodeEnum.Email, folderId, hotScore);
        }


        public static List<McEmailMessageThread> QueryUnifiedInboxItemsByScore (double hotScore, bool includeActions = true)
        {
            var queryFormat =
                "SELECT FirstMessageId, Count(FirstMessageId) as MessageCount, DateReceived, ConversationId FROM " +
                " ( " +
                " SELECT e.Id as FirstMessageId, e.DateReceived as DateReceived, e.ConversationId as ConversationId FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " JOIN McFolder AS f ON f.Id = m.FolderId " +
                " WHERE " +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.IsChat = 0, 0.8) AND " +
                (includeActions ? "" : " likelihood (e.IsAction = 0, 0.8) AND ") +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (f.Type = ?, 0.05) AND " +
                " likelihood (e.UserAction > -1, 0.99) AND " +
                " (likelihood (e.Score >= ?, 0.1) OR likelihood (e.UserAction = 1, 0.01)) " +
                " ) " +
                " GROUP BY ConversationId " +
                " ORDER BY DateReceived DESC";

            var query = String.Format (queryFormat);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query,
                McAbstrFolderEntry.ClassCodeEnum.Email, Xml.FolderHierarchy.TypeCode.DefaultInbox_2, hotScore);
        }

        public static List<McEmailMessageThread> QueryActiveMessageItemsByScore2 (int accountId, int folderId, double hotScore, double ltrScore)
        {
            var queryFormat =
                "SELECT FirstMessageId, Count(FirstMessageId) as MessageCount, DateReceived, ConversationId FROM " +
                " ( " +
                " SELECT e.Id as FirstMessageId, e.DateReceived as DateReceived, e.ConversationId as ConversationId FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                "{0}" +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.IsChat = 0, 0.8) AND " +
                "{1}" +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (m.FolderId = ?, 0.05) AND " +
                " likelihood (e.Score < ? AND e.Score2 >= ?, 0.1) AND " +
                " likelihood (e.UserAction <= 0, 0.99) " +
                " ) " +
                " GROUP BY ConversationId " +
                " ORDER BY DateReceived DESC";

            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 0.2) AND ", accountId);
            var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 0.2) AND ", accountId);

            var query = String.Format (queryFormat, account0, account1);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, McAbstrFolderEntry.ClassCodeEnum.Email, folderId, hotScore, ltrScore);
        }


        public static List<McEmailMessageThread> QueryUnifiedItemsByScore2 (double hotScore, double ltrScore)
        {
            var queryFormat =
                "SELECT FirstMessageId, Count(FirstMessageId) as MessageCount, DateReceived, ConversationId FROM " +
                " ( " +
                " SELECT e.Id as FirstMessageId, e.DateReceived as DateReceived, e.ConversationId as ConversationId FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " JOIN McFolder AS f ON f.Id = m.FolderId " +
                " WHERE " +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.IsChat = 0, 0.8) AND " +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (f.Type = ?, 0.2) AND " +
                " likelihood (e.Score < ? AND e.Score2 >= ?, 0.1) AND " +
                " likelihood (e.UserAction <= 0, 0.99) " +
                " ) " +
                " GROUP BY ConversationId " +
                " ORDER BY DateReceived DESC";

            var query = String.Format (queryFormat);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, McAbstrFolderEntry.ClassCodeEnum.Email, Xml.FolderHierarchy.TypeCode.DefaultInbox_2, hotScore, ltrScore);
        }

        public static List<McEmailMessage> QueryNeedsIndexing (int maxMessages)
        {
            return NcModel.Instance.Db.Query<McEmailMessage> (
                "SELECT e.* FROM McEmailMessage as e " +
                " LEFT JOIN McBody as b ON b.Id == e.BodyId " +
                " WHERE likelihood (e.IsIndexed < ?, 0.5) OR " +
                "  (likelihood (e.IsIndexed < ?, 0.5) AND " +
                "   likelihood (e.BodyId > 0, 0.2) AND " +
                "   likelihood (b.FilePresence = ?, 0.5))" +
                " ORDER BY e.DateReceived DESC " +
                " LIMIT ?",
                EmailMessageDocument.Version - 1, EmailMessageDocument.Version,
                McAbstrFileDesc.FilePresenceEnum.Complete, maxMessages
            );
        }

        public static List<object> QueryNeedsIndexingObjects (int count)
        {
            return new List<object> (QueryNeedsIndexing (count));
        }

        public static List<McEmailMessage> QueryForSet (List<int> indexList)
        {
            var set = String.Format ("( {0} )", String.Join (",", indexList.ToArray<int> ()));
            var cmd = String.Format ("SELECT e.* FROM McEmailMessage as e WHERE e.ID IN {0}", set);
            return NcModel.Instance.Db.Query<McEmailMessage> (cmd);
        }

        public static List<McEmailMessage> QueryUnreadAndHotAfter (DateTime since)
        {
            var retardedSince = since.AddDays (-1.0);
            return NcModel.Instance.Db.Query<McEmailMessage> ("SELECT * FROM McEmailMessage WHERE " +
            " (HasBeenNotified = 0 OR ShouldNotify = 1) AND " +
            " likelihood (IsRead = 0, 0.5) AND " +
            " likelihood (IsChat = 0, 0.8) AND " +
            " CreatedAt > ? AND " +
            " likelihood (DateReceived > ?, 0.01) " +
            " ORDER BY DateReceived ASC ",
                since, retardedSince);
        }

        public static List<NcEmailMessageIndex> QueryByDateReceivedAndFrom (int accountId, DateTime dateRecv, string from)
        {
            var queryFormat =
                "SELECT e.Id as Id FROM McEmailMessage AS e WHERE " +
                "{0}" +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.DateReceived = ?, 0.01) AND " +
                " likelihood (e.[From] = ?, 0.01) ";

            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 0.2) AND ", accountId);

            var query = String.Format (queryFormat, account0);

            return NcModel.Instance.Db.Query<NcEmailMessageIndex> (query, dateRecv, from);
        }

        public static List<NcEmailMessageIndex> QueryByServerIdList (int accountId, List<string> serverIds)
        {
            var queryFormat =
                "SELECT f.Id FROM McEmailMessage AS f WHERE " +
                "{0}" +
                " likelihood (f.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (f.ServerId IN ('{1}'), 0.001) ";

            var account0 = SingleAccountString (" likelihood (f.AccountId = {0}, 0.2) AND ", accountId);

            var query = String.Format (queryFormat, account0, String.Join ("','", serverIds));

            return NcModel.Instance.Db.Query<NcEmailMessageIndex> (query);
        }


        public static List<McEmailMessage> QueryUnnotified (int accountId = 0)
        {
            var emailMessageList = NcModel.Instance.Db.Table<McEmailMessage> ().Where (x => false == x.HasBeenNotified);
            if ((0 != accountId) && (McAccount.GetUnifiedAccount ().Id != accountId)) {
                emailMessageList = emailMessageList.Where (x => x.AccountId == accountId);
            }
            return emailMessageList.ToList ();
        }

        const string KCapQueryByImapUidRange = "NcModel.McEmailMessage.QueryByImapUidRange";

        public static List<NcEmailMessageIndex> QueryByImapUidRange (int accountId, int folderId, uint min, uint max, uint limit)
        {
            NcCapture.AddKind (KCapQueryByImapUidRange);
            using (var cap = NcCapture.CreateAndStart (KCapQueryByImapUidRange)) {
                // We'll just reuse NcEmailMessageIndex instead of making a new fake class to fetch the Uid's. It would
                // look identical except for the Id argument, so what the heck.
                var queryFormat =
                    "SELECT e.ImapUid as Id FROM McEmailMessage as e " +
                    " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                    " JOIN McFolder AS f ON m.FolderId = f.Id " +
                    " WHERE " +
                    "{0}" +
                    " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                    "{1}" +
                    " likelihood (m.ClassCode = ?, 0.2) AND " +
                    " likelihood (e.ImapUid <> 0 AND e.ImapUid >= ? AND e.ImapUid < ?, 0.1) AND " +
                    " likelihood (m.FolderId = ?, 0.5) " +
                    " ORDER BY e.ImapUid DESC LIMIT ?";

                var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 0.2) AND ", accountId);
                var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 0.2) AND ", accountId);

                var query = String.Format (queryFormat, account0, account1);

                return NcModel.Instance.Db.Query<NcEmailMessageIndex> (
                    query, (int)McAbstrFolderEntry.ClassCodeEnum.Email,
                    min, max, folderId, limit);
            }
        }

        const string KCapQueryImapMessagesToSend = "NcModel.McEmailMessage.QueryImapMessagesToSend";

        public static List<NcEmailMessageIndex> QueryImapMessagesToSend (int accountId, int folderId, uint limit)
        {
            NcCapture.AddKind (KCapQueryImapMessagesToSend);
            using (var cap = NcCapture.CreateAndStart (KCapQueryImapMessagesToSend)) {
                var queryFormat =
                    "SELECT e.Id FROM McEmailMessage as e " +
                    " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                    " JOIN McFolder AS f ON m.FolderId = f.Id " +
                    " WHERE " +
                    "{0}" +
                    " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                    "{1}" +
                    " likelihood (m.ClassCode = ?, 0.2) AND " +
                    " likelihood (e.ImapUid = 0, 0.1) AND " +
                    " likelihood (m.FolderId = ?, 0.5) " +
                    " LIMIT ?";

                var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 0.2) AND ", accountId);
                var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 0.2) AND ", accountId);

                var query = String.Format (queryFormat, account0, account1);

                return NcModel.Instance.Db.Query<NcEmailMessageIndex> (
                    query, (int)McAbstrFolderEntry.ClassCodeEnum.Email,
                    folderId, limit);
            }
        }

        public override ClassCodeEnum GetClassCode ()
        {
            return McAbstrFolderEntry.ClassCodeEnum.Email;
        }

        public const double minHotScore = 0.5;
        public const double minLikelyToReadScore = 0.3;

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
            if (((int)FlagStatusValue.Cleared) == FlagStatus) {
                return false;
            }
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
            if (((int)FlagStatusValue.Cleared) == FlagStatus) {
                return false;
            }
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

        public static bool TryImportanceFromString (string priority, out NcImportance importance)
        {
            // see https://msdn.microsoft.com/en-us/library/Gg671973(v=EXCHG.80).aspx
            int prio;
            if (Int32.TryParse (priority, out prio)) {
                if (prio > 3) {
                    importance = NcImportance.Low_0;
                    return true;
                } else if (prio < 3) {
                    importance = NcImportance.High_2;
                    return true;
                } else {
                    importance = NcImportance.Normal_1;
                    return true;
                }
            }

            // according to https://tools.ietf.org/html/rfc2156
            //       importance      = "low" / "normal" / "high"
            // But apparently I need to make sure to account for case (i.e. Normal and Low, etc).
            switch (priority.ToLowerInvariant ()) {
            case "low":
            case "non-urgent":
                importance = NcImportance.Low_0;
                return true;

            case "medium":
            case "normal":
                importance = NcImportance.Normal_1;
                return true;

            case "high":
            case "urgent":
                importance = NcImportance.High_2;
                return true;

            default:
                // cover the case where we have something like "3 (Normal)" (seriously I saw this one)
                // ignore the number and go with the letters. If they don't match (say some idiot makes
                // it "1 (Normal)", then tough cookies.
                if (priority.ToLowerInvariant ().Contains ("normal") || priority.ToLowerInvariant ().Contains ("medium")) {
                    importance = NcImportance.Normal_1;
                    return true;
                } else if (priority.ToLowerInvariant ().Contains ("low")) {
                    importance = NcImportance.Low_0;
                    return true;
                } else if (priority.ToLowerInvariant ().Contains ("high")) {
                    importance = NcImportance.High_2;
                    return true;
                }
                Log.Error (Log.LOG_EMAIL, "Unknown Importance/Priority string {0}", priority);
                importance = NcImportance.Normal_1; // gotta set something or the compiler complains.
                return false;
            }
        }

        public void DeleteMatchingOutboxMessage ()
        {
            if (IsChat && !String.IsNullOrEmpty (MessageID)) {
                var outbox = McFolder.GetClientOwnedOutboxFolder (AccountId);
                var outboxMessages = NcModel.Instance.Db.Query<McEmailMessage> (
                    "SELECT m.* FROM McEmailMessage m JOIN McMapFolderFolderEntry e ON m.Id = e.FolderEntryId " +
                    "WHERE m.AccountId = ? AND likelihood(m.MessageID = ?, 0.01) AND m.Id != ? AND e.FolderId = ?",
                    AccountId, MessageID, Id, outbox.Id);
                foreach (var message in outboxMessages) {
                    message.Delete ();
                }
            }
        }

        public void ProcessAfterReceipt ()
        {
            GleanContactsIfNeeded (GleanPhaseEnum.GLEAN_PHASE2);
            Indexer.Instance.Add (this);
        }

        #region Contact Gleaning

        static NcDisqualifier<McEmailMessage> [] GleaningDisqualifiers = new NcDisqualifier<McEmailMessage> []{
            new NcMarketingEmailDisqualifier (),
            new NcYahooBulkEmailDisqualifier (),
        };

        bool NeedsContactsGleaned (GleanPhaseEnum phase)
        {
            if (HasBeenGleaned >= (int)phase) {
                return false;
            }
            if (IsJunk) {
                return false;
            }
            foreach (var disqualifier in GleaningDisqualifiers) {
                if (disqualifier.Analyze (this)) {
                    return false;
                }
            }
            return true;
        }

        public void GleanContactsIfNeeded (GleanPhaseEnum phase)
        {
            if (!NeedsContactsGleaned (phase)) {
                return;
            }
            var folder = McFolder.GetGleanedFolder (AccountId);
            McContact contact;
            if (phase >= GleanPhaseEnum.GLEAN_PHASE1 && HasBeenGleaned < (int)GleanPhaseEnum.GLEAN_PHASE1) {
                foreach (var mailbox in FromMailboxes) {
                    McContact.CreateFromMailboxIfNeeded (folder, mailbox, out contact);
                }
                foreach (var mailbox in ToMailboxes) {
                    McContact.CreateFromMailboxIfNeeded (folder, mailbox, out contact);
                }
            }
            if (phase >= GleanPhaseEnum.GLEAN_PHASE2 && HasBeenGleaned < (int)GleanPhaseEnum.GLEAN_PHASE2) {
                var sender = SenderMailbox;
                if (sender.HasValue) {
                    McContact.CreateFromMailboxIfNeeded (folder, sender.Value, out contact);
                }
                foreach (var mailbox in CcMailboxes) {
                    McContact.CreateFromMailboxIfNeeded (folder, mailbox, out contact);
                }
                foreach (var mailbox in ReplyToMailboxes) {
                    McContact.CreateFromMailboxIfNeeded (folder, mailbox, out contact);
                }
                foreach (var mailbox in BccMailboxes) {
                    McContact.CreateFromMailboxIfNeeded (folder, mailbox, out contact);
                }
            }
            MarkAsGleaned (phase);
        }

        public void MarkAsGleaned (GleanPhaseEnum phase)
        {
            if (Id != 0) {
                UpdateWithOCApply<McEmailMessage> ((item) => {
                    var message = (McEmailMessage)item;
                    message.HasBeenGleaned = (int)phase;
                    return true;
                });
            } else {
                HasBeenGleaned = (int)phase;
            }
        }

        #endregion
    }

    public class McEmailMessageThread
    {
        public int MessageCount { set; get; }

        public int FirstMessageId { set; get; }

        public NachoEmailMessages Source;

        // Filled on demand
        List<McEmailMessageThread> thread;

        public McEmailMessage FirstMessage ()
        {
            return McEmailMessage.QueryById<McEmailMessage> (FirstMessageId);
        }

        public McEmailMessage FirstMessageSpecialCase ()
        {
            return McEmailMessage.QueryById<McEmailMessage> (FirstMessageId);
        }

        public int FirstMessageSpecialCaseIndex ()
        {
            return FirstMessageId;
        }

        /// <summary>
        /// Implies that the thread has only a single messages
        /// </summary>
        public McEmailMessage SingleMessageSpecialCase ()
        {
            //            NcAssert.True (1 == thread.Count);
            return FirstMessageSpecialCase ();
        }

        public int Count {
            get {
                return MessageCount;
            }
        }

        public bool HasMultipleMessages ()
        {
            return (1 < Count);
        }

        public string GetThreadId ()
        {
            var message = FirstMessageSpecialCase ();
            return (null == message) ? null : message.ConversationId;
        }

        public string GetSubject ()
        {
            var message = FirstMessageSpecialCase ();
            return (null == message) ? null : message.Subject;
        }

        public IEnumerator<McEmailMessage> GetEnumerator ()
        {
            if (null == thread) {
                NcAssert.NotNull (Source);
                thread = Source.GetEmailThreadMessages (FirstMessageId);
                if (null == thread) {
                    yield break; // thread is gone. Maybe backend removed it asynchronously
                }
            }
            using (IEnumerator<McEmailMessageThread> ie = thread.GetEnumerator ()) {
                while (ie.MoveNext ()) {
                    var message = ie.Current.SingleMessageSpecialCase ();
                    if (null != message) {
                        yield return message;
                    }
                }
            }
        }
    }

    public class McEmailMessageThreadIndexComparer : IEqualityComparer<McEmailMessageThread>
    {
        public bool Equals (McEmailMessageThread a, McEmailMessageThread b)
        {
            return a.FirstMessageId == b.FirstMessageId;
        }

        public int GetHashCode (McEmailMessageThread i)
        {
            return i.FirstMessageId;
        }
    }

    public partial class McEmailMessage
    {

        private List<McEmailMessageCategory> dbCategories = null;
        private IList<McEmailMessageCategory> appCategories = null;

        [Ignore]
        public IList<McEmailMessageCategory> Categories {
            get {
                return GetAncillaryCollection (appCategories, ref dbCategories, ReadDbCategories);
            }
            set {
                NcAssert.NotNull (value, "To clear the categories, use an empty list instead of null.");
                appCategories = value;
            }
        }

        private List<McEmailMessageCategory> ReadDbCategories ()
        {
            return NcModel.Instance.Db.Table<McEmailMessageCategory> ().Where (x => x.ParentId == Id).ToList ();
        }

        private void DeleteDbCategories ()
        {
            DeleteAncillaryCollection (ref dbCategories, ReadDbCategories);
        }

        private void SaveCategories ()
        {
            SaveAncillaryCollection (ref appCategories, ref dbCategories, ReadDbCategories, (McEmailMessageCategory category) => {
                category.SetParent (this);
            }, (McEmailMessageCategory category) => {
                return category.ParentId == this.Id;
            });
        }

        private void InsertCategories ()
        {
            InsertAncillaryCollection (ref appCategories, ref dbCategories, (McEmailMessageCategory category) => {
                category.SetParent (this);
            });
        }

        private McMeetingRequest dbMeetingRequest = null;
        private McMeetingRequest appMeetingRequest = null;
        private bool dbMeetingRequestRead = false;
        private bool appMeetingRequestSet = false;

        [Ignore]
        public McMeetingRequest MeetingRequest {
            get {
                if (appMeetingRequestSet) {
                    return appMeetingRequest;
                }
                ReadDbMeetingRequest ();
                return dbMeetingRequest;
            }
            set {
                appMeetingRequest = value;
                appMeetingRequestSet = true;
            }
        }

        private void ReadDbMeetingRequest ()
        {
            if (!dbMeetingRequestRead) {
                if (0 != this.Id) {
                    dbMeetingRequest = NcModel.Instance.Db.Table<McMeetingRequest> ().Where (x => x.EmailMessageId == Id).SingleOrDefault ();
                }
                dbMeetingRequestRead = true;
            }
        }

        private void DeleteDbMeetingRequest ()
        {
            ReadDbMeetingRequest ();
            if (null != dbMeetingRequest) {
                dbMeetingRequest.Delete ();
                dbMeetingRequest = null;
            }
        }

        private void InsertMeetingRequest ()
        {
            NcAssert.True (null == dbMeetingRequest);
            if (!appMeetingRequestSet) {
                dbMeetingRequestRead = true;
                return;
            }
            if (null != appMeetingRequest) {
                NcAssert.True (0 == appMeetingRequest.Id);
                appMeetingRequest.AccountId = this.AccountId;
                appMeetingRequest.EmailMessageId = this.Id;
                appMeetingRequest.Insert ();
            }
            dbMeetingRequest = appMeetingRequest;
            dbMeetingRequestRead = true;
            appMeetingRequest = null;
            appMeetingRequestSet = false;
        }

        private void SaveMeetingRequest ()
        {
            if (!appMeetingRequestSet) {
                return;
            }
            ReadDbMeetingRequest ();
            if (null == appMeetingRequest) {
                DeleteDbMeetingRequest ();
            } else if (0 == appMeetingRequest.Id) {
                DeleteDbMeetingRequest ();
                appMeetingRequest.AccountId = this.AccountId;
                appMeetingRequest.EmailMessageId = this.Id;
                appMeetingRequest.Insert ();
            } else {
                NcAssert.True (appMeetingRequest.EmailMessageId == this.Id);
                appMeetingRequest.Update ();
            }
            dbMeetingRequest = appMeetingRequest;
            dbMeetingRequestRead = true;
            appMeetingRequest = null;
            appMeetingRequestSet = false;
        }

        private void InsertAddressList (List<int> addressIdList, EmailMessageAddressType kind)
        {
            if (null != addressIdList) {
                foreach (var addressId in addressIdList) {
                    var map = CreateAddressMap ();
                    map.EmailAddressId = addressId;
                    map.AddressType = kind;
                    map.Insert ();
                }
            }
        }

        public McMapEmailAddressEntry CreateAddressMap ()
        {
            var map = new McMapEmailAddressEntry ();
            map.AccountId = AccountId;
            map.ObjectId = Id;
            return map;
        }

        public void InsertAddressMaps ()
        {
            var sender = SenderMailbox;
            if (sender.HasValue && McEmailAddress.GetOrCreate (AccountId, sender.Value, out var senderAddress)) {
                var map = CreateAddressMap ();
                map.EmailAddressId = senderAddress.Id;
                map.AddressType = EmailMessageAddressType.Sender;
                map.Insert ();
            }
            foreach (var mailbox in FromMailboxes) {
                if (McEmailAddress.GetOrCreate (AccountId, mailbox, out var address)) {
                    var map = CreateAddressMap ();
                    map.EmailAddressId = address.Id;
                    map.AddressType = EmailMessageAddressType.From;
                    map.Insert ();
                }
            }
            foreach (var mailbox in ReplyToMailboxes) {
                if (McEmailAddress.GetOrCreate (AccountId, mailbox, out var address)) {
                    var map = CreateAddressMap ();
                    map.EmailAddressId = address.Id;
                    map.AddressType = EmailMessageAddressType.ReplyTo;
                    map.Insert ();
                }
            }
            foreach (var mailbox in ToMailboxes) {
                if (McEmailAddress.GetOrCreate (AccountId, mailbox, out var address)) {
                    var map = CreateAddressMap ();
                    map.EmailAddressId = address.Id;
                    map.AddressType = EmailMessageAddressType.To;
                    map.Insert ();
                }
            }
            foreach (var mailbox in CcMailboxes) {
                if (McEmailAddress.GetOrCreate (AccountId, mailbox, out var address)) {
                    var map = CreateAddressMap ();
                    map.EmailAddressId = address.Id;
                    map.AddressType = EmailMessageAddressType.Cc;
                    map.Insert ();
                }
            }
            foreach (var mailbox in BccMailboxes) {
                if (McEmailAddress.GetOrCreate (AccountId, mailbox, out var address)) {
                    var map = CreateAddressMap ();
                    map.EmailAddressId = address.Id;
                    map.AddressType = EmailMessageAddressType.Bcc;
                    map.Insert ();
                }
            }
        }

        private void DeleteAddressMaps ()
        {
            McMapEmailAddressEntry.DeleteMessageMapEntries (AccountId, Id);
        }

        void SetInitialScore ()
        {
            if (0 == ScoreVersion) {
                // Try to use the address score for initial email message score
                // TODO - Should refactor IScorable to include a quick score function in Brain 2.0
                McEmailAddress emailAddress = GetFromAddress ();
                if (null != emailAddress) {
                    if (emailAddress.IsVip || (0 < UserAction)) {
                        Score = minHotScore;
                    } else if (0 > UserAction) {
                        Score = minHotScore - 0.1;
                    } else if (0 < emailAddress.ScoreVersion) {
                        Score = emailAddress.Score;
                    } else {
                        Score = 0.0;
                    }
                }
            }
        }

        public bool PopulateCachedFields ()
        {
            var originalFromId = FromEmailAddressId;
            var originalSenderId = SenderEmailAddressId;
            var originalFromLetters = cachedFromLetters;
            var originalFromColor = cachedFromColor;
            var fromMailboxes = FromMailboxes;
            if (fromMailboxes.Length > 0 && McEmailAddress.GetOrCreate (AccountId, fromMailboxes [0], out var fromAddress)) {
                FromEmailAddressId = fromAddress.Id;
                cachedFromLetters = fromMailboxes [0].Initials;
                cachedFromColor = fromAddress.ColorIndex;
            } else {
                FromEmailAddressId = 0;
                cachedFromLetters = "";
                cachedFromColor = 1;
            }

            var sender = SenderMailbox;
            if (sender.HasValue && McEmailAddress.GetOrCreate (AccountId, sender.Value, out var senderAddress)) {
                SenderEmailAddressId = senderAddress.Id;
            } else {
                SenderEmailAddressId = 0;
            }

            return originalFromId != FromEmailAddressId || originalSenderId != SenderEmailAddressId || originalFromColor != cachedFromColor || originalFromLetters != cachedFromLetters;
        }

        public override int Insert ()
        {
            using (var capture = CaptureWithStart ("Insert")) {
                int returnVal = -1;

                SetInitialScore ();
                PopulateCachedFields ();
                HasBeenNotified = (NcApplication.Instance.IsForeground || IsRead);

                NcModel.Instance.RunInTransaction (() => {
                    returnVal = base.Insert ();
                    InsertAddressMaps ();
                    InsertMeetingRequest ();
                    InsertCategories ();
                    InsertScoreStates ();
                    McEmailMessageNeedsUpdate.Insert (this, 0);
                });

                return returnVal;
            }
        }

        public override T UpdateWithOCApply<T> (Mutator mutator, out int count, int tries = 100)
        {
            int myCount = 0;
            T retval = null;
            NcModel.Instance.RunInTransaction (() => {
                retval = base.UpdateWithOCApply<T> ((record) => {
                    var target = (McEmailMessage)record;
                    if (!target.HasBeenNotified) {
                        target.HasBeenNotified = (NcApplication.Instance.IsForeground || target.IsRead);
                    }
                    return mutator (record);
                }, out myCount, tries);
                if (null == retval) {
                    // We were not able to update the record.
                    return;
                }
                SaveMeetingRequest ();
                SaveCategories ();
                // Score states are only affected by brain which uses the score states Update() method.
                // So, no need to update score states here
            });
            count = myCount;
            return retval;
        }

        public override T UpdateWithOCApply<T> (Mutator mutator, int tries = 100)
        {
            int rc = 0;
            return UpdateWithOCApply<T> (mutator, out rc, tries);
        }

        public override int Update ()
        {
            NcAssert.True (false, "Must use UpdateWithOCApply.");
            return 0;
        }

        public void UpdateIsIndex (int version)
        {
            IsIndexed = version;
            NcModel.Instance.BusyProtect (() => {
                return NcModel.Instance.Db.Execute ("UPDATE McEmailMessage SET IsIndexed = ?, RowVersion = RowVersion + 1 WHERE Id = ?",
                    version, Id);
            });
        }

        void DeleteChatMessages ()
        {
            var messages = McChatMessage.QueryByMessageId (Id);
            foreach (var message in messages) {
                message.Delete ();
                message.UpdateLatestDuplicate ();
            }
        }

        void DeleteAction ()
        {
            var action = McAction.ActionForMessage (this);
            if (action != null) {
                action.Delete ();
            }
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
            DeleteDbMeetingRequest ();
            DeleteDbCategories ();
            DeleteAttachments ();
            DeleteAddressMaps ();
            DeleteChatMessages ();
            DeleteAction ();
        }

        public override int Delete ()
        {
            using (var capture = CaptureWithStart ("Delete")) {
                int returnVal = 0;
                NcModel.Instance.RunInTransaction (() => {
                    returnVal = base.Delete ();
                    // FIXME: Do we need to delete associated records like Attachments?
                    Indexer.Instance.Remove (this);
                    DeleteScoreStates ();
                    McEmailMessageNeedsUpdate.Delete (this);
                });
                return returnVal;
            }
        }

        public McEmailAddress GetFromAddress ()
        {
            McEmailAddress emailAddress = null;
            var address = NcEmailAddress.ParseMailboxAddressString (From);
            if (null == address) {
                Log.Warn (Log.LOG_BRAIN, "[McEmailMessage:{0}] Cannot parse email address", Id);
            } else {
                bool found = McEmailAddress.Get (AccountId, address.Address, out emailAddress);
                if (!found) {
                    Log.Warn (Log.LOG_BRAIN, "[McEmailMessage:{0}] Unknown email address", Id);
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

        public int GetIndexVersion ()
        {
            if (!IsJunk && GetBodyIfComplete () == null) {
                return EmailMessageDocument.Version - 1;
            }
            return EmailMessageDocument.Version;
        }

        public static McEmailMessage QueryByMessageId (int accountId, string messageId)
        {
            var queryFormat = "SELECT * FROM McEmailMessage WHERE {0} likelihood(MessageID = ?, 0.01)";

            var account0 = SingleAccountString (" AccountId = {0} AND ", accountId);

            var query = String.Format (queryFormat, account0);

            return NcModel.Instance.Db.Query<McEmailMessage> (query, messageId).FirstOrDefault ();
        }

        public void DetermineIfIsChat ()
        {
            if (!String.IsNullOrEmpty (MessageID)) {
                IsChat = MessageID.StartsWith ("NachoChat.");
            }
            if (!IsChat && !String.IsNullOrEmpty (Subject)) {
                IsChat = Subject.EndsWith ("[Nacho Chat]");
            }
            if (!IsChat && !String.IsNullOrEmpty (ConversationId)) {
                var query = "SELECT COUNT(*) FROM McEmailMessage WHERE AccountId = ? AND likelihood(ConversationId = ?, 0.01) AND IsChat = 1";
                IsChat = NcModel.Instance.Db.ExecuteScalar<int> (query, AccountId, ConversationId) > 0;
            }
        }

        public void ParseIntentFromSubject ()
        {
            if (!string.IsNullOrEmpty (Subject)) {
                string subject;
                IntentType intent;
                MessageDeferralType intentDateType;
                DateTime intentDate;
                EmailHelper.ParseSubject (Subject, DateReceived, out subject, out intent, out intentDateType, out intentDate);
                Subject = subject;
                Intent = intent;
                IntentDateType = intentDateType;
                IntentDate = intentDate;
            }
        }

        public void DetermineIfIsAction (McFolder inFolder)
        {
            if (inFolder.Type != Xml.FolderHierarchy.TypeCode.DefaultSent_5 && inFolder.Type != Xml.FolderHierarchy.TypeCode.DefaultDeleted_4 && !IsJunk) {
                if (Intent == IntentType.PleaseRead || Intent == IntentType.ResponseRequired || Intent == IntentType.Urgent) {
                    IsAction = true;
                } else if (Intent == IntentType.Important && IntentDateType != MessageDeferralType.None) {
                    IsAction = true;
                }
                if (IsAction) {
                    var account = McAccount.QueryById<McAccount> (AccountId);
                    // Make sure if the message is from the account and not to the account, that it's not considered an action
                    // We see this is gmail, where a sent message is duplicated in the All Mail folder and therefore not caught by the previous Sent folder check
                    if (EmailHelper.AddressIsInList (account.Id, account.EmailAddr, EmailHelper.AddressList (NcEmailAddress.Kind.Unknown, null, From)) && !EmailHelper.AddressIsInList (account.Id, account.EmailAddr, EmailHelper.AddressList (NcEmailAddress.Kind.Unknown, null, To, Cc))) {
                        IsAction = false;
                    }
                }
            }
        }

        public McEmailMessage MarkHasBeenNotified (bool shouldNotify)
        {
            if (shouldNotify) {
                //NcBrain.MessageNotificationStatusUpdated (this, DateTime.UtcNow, 0.1);
            }
            return UpdateWithOCApply<McEmailMessage> ((record) => {
                var target = (McEmailMessage)record;
                target.HasBeenNotified = true;
                target.ShouldNotify = shouldNotify;
                return true;
            });
        }
    }
}
