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
        // For header indexed messages, the field has the value of EmailMessageIndexDocument.Version-1.
        // For header+body indexed messages, EmailMessageIndexDocument.Version. If a new indexing schema is needed,
        // just increment the version # and implement the new version of EmailMessageIndexDocument.
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

        public static List<McEmailMessageThread> QueryInteractions (int accountId, McContact contact)
        {
            if (String.IsNullOrEmpty (contact.GetPrimaryCanonicalEmailAddress ())) {
                return new List<McEmailMessageThread> ();
            }

            string emailWildcard = "%" + contact.GetPrimaryCanonicalEmailAddress () + "%";

            // Not all accounts have deleted folder (e.g. Device). Using '0' is a trick.
            McFolder deletedFolder = McFolder.GetDefaultDeletedFolder (accountId);
            var deletedFolderId = ((null == deletedFolder) ? 0 : deletedFolder.Id);

            var queryFormat = 
                "SELECT DISTINCT e.Id as FirstMessageId, 1 as MessageCount FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " JOIN McFolder AS f ON m.FolderId = f.Id " +
                " WHERE " +
                "{0}" +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.IsChat = 0, 0.8) AND " +
                " likelihood (f.IsClientOwned != 1, 0.9) AND " +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                "{1}" +
                " likelihood (m.FolderId != ?, 0.5) AND " +
                " likelihood (e.[From] LIKE ?, 0.05) OR " +
                " likelihood (e.[To] Like ?, 0.05) " +
                " ORDER BY e.DateReceived DESC";

            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 1.0) AND ", accountId);
            var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 1.0) AND ", accountId);

            var query = String.Format (queryFormat, account0, account1);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, McAbstrFolderEntry.ClassCodeEnum.Email, deletedFolderId, emailWildcard, emailWildcard);
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
                " likelihood (m.FolderId = ?, 0.5) AND " +
                " e.FlagUtcStartDate < ? " +
                (groupBy ? " GROUP BY e.ConversationId " : "") +
                " ORDER BY e.DateReceived DESC ";

            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 1.0) AND ", accountId);
            var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 1.0) AND ", accountId);

            var query = String.Format (queryFormat, account0, account1);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, McAbstrFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow);
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
                " likelihood (m.FolderId = ?, 0.5) AND " +
                " e.FlagUtcStartDate < ? " +
                (groupBy ? " GROUP BY e.ConversationId " : "") +
                " ORDER BY e.DateReceived DESC ";

            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 1.0) AND ", accountId);
            var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 1.0) AND ", accountId);

            var query = String.Format (queryFormat, account0, account1);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, McAbstrFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow);
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
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " e.FlagUtcStartDate < ? " +
                (groupBy ? " GROUP BY e.ConversationId " : "") +
                " ORDER BY e.DateReceived DESC ";

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, Xml.FolderHierarchy.TypeCode.DefaultInbox_2, McAbstrFolderEntry.ClassCodeEnum.Email, DateTime.UtcNow);
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
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " e.FlagUtcStartDate < ? " +
                (groupBy ? " GROUP BY e.ConversationId " : "") +
                " ORDER BY e.DateReceived DESC ";

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, Xml.FolderHierarchy.TypeCode.DefaultInbox_2, McAbstrFolderEntry.ClassCodeEnum.Email, DateTime.UtcNow);
        }

        public static List<McEmailMessageThread> QueryActiveMessageItemsByThreadId (int accountId, int folderId, string threadId)
        {
            var queryFormat =
                "SELECT e.Id as FirstMessageId, 1 as MessageCount FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                " e.ConversationId = ? AND " +
                "{0}" +
                " e.IsAwaitingDelete = 0 AND " +
                "{1}" +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? AND " +
                " e.FlagUtcStartDate < ? " +
                " ORDER BY e.DateReceived DESC";

            var account0 = SingleAccountString (" e.AccountId = {0} AND ", accountId);
            var account1 = SingleAccountString (" m.AccountId = {0} AND ", accountId);

            var query = String.Format (queryFormat, account0, account1);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, threadId, McAbstrFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow);
        }

        public static int CountOfUnreadMessageItems (int accountId, int folderId)
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
                " e.FlagUtcStartDate < ? AND " +
                "e.IsRead = 0";

            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 1.0) AND ", accountId);
            var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 1.0) AND ", accountId);

            var query = String.Format (queryFormat, account0, account1);

            return NcModel.Instance.Db.ExecuteScalar<int> (
                query, McAbstrFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow);
        }

        public static IEnumerable<McEmailMessage> QueryNeedsFetch (int accountId, int limit, double minScore)
        {
            var queryFormat =
                "SELECT e.* FROM McEmailMessage AS e " +
                " LEFT OUTER JOIN McBody AS b ON b.Id = e.BodyId" +
                " WHERE " +
                "{0}" +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
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
                "{1}" +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " d.EmailAddressId IN (SELECT a.Id FROM McEmailAddress AS a WHERE a.IsVip != 0) AND " +
                " ((b.FilePresence != ? AND " +
                "   b.FilePresence != ? AND " +
                "   b.FilePresence != ?) OR " +
                "  e.BodyId = 0) " +
                " ORDER BY e.DateReceived DESC LIMIT ?";

            var accountString = SingleAccountString (" likelihood (e.AccountId = {0}, 1.0) AND ", accountId);

            var query = String.Format (queryFormat, accountString, accountString);

            return NcModel.Instance.Db.Query<McEmailMessage> (
                query,
                DateTime.UtcNow, minScore,
                (int)McAbstrFileDesc.FilePresenceEnum.Complete,
                (int)McAbstrFileDesc.FilePresenceEnum.Partial,
                (int)McAbstrFileDesc.FilePresenceEnum.Error,
                (int)McAbstrFileDesc.FilePresenceEnum.Complete,
                (int)McAbstrFileDesc.FilePresenceEnum.Partial,
                (int)McAbstrFileDesc.FilePresenceEnum.Error,
                limit);
        }

        public static List<McEmailMessageThread> QueryActiveMessageItemsByScore (int accountId, int folderId, double hotScore)
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
                " likelihood (e.FlagUtcStartDate < ?, 0.99) AND " +
                " likelihood (e.UserAction > -1, 0.99) AND " +
                " (likelihood (e.Score >= ?, 0.1) OR likelihood (e.UserAction = 1, 0.01)) " +
                "UNION " +
                "SELECT e.Id as FirstMessageId, e.DateReceived as DateReceived, e.ConversationId as ConversationId FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " JOIN McEmailMessageDependency AS d ON e.Id = d.EmailMessageId " +
                " WHERE " +
                "{2}" +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.IsChat = 0, 0.8) AND " +
                "{3}" +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (m.FolderId = ?, 0.05) AND " +
                " d.EmailAddressId IN (SELECT a.Id FROM McEmailAddress AS a WHERE likelihood (a.IsVip != 0, 0.01)) " +
                " ) " +
                " GROUP BY ConversationId " +
                " ORDER BY DateReceived DESC";

            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 1.0) AND ", accountId);
            var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 1.0) AND ", accountId);

            var query = String.Format (queryFormat, account0, account1, account0, account1);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query,
                McAbstrFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow, hotScore,
                McAbstrFolderEntry.ClassCodeEnum.Email, folderId);
        }


        public static List<McEmailMessageThread> QueryUnifiedInboxItemsByScore (double hotScore)
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
                " likelihood (f.Type = ?, 0.05) AND " +
                " likelihood (e.FlagUtcStartDate < ?, 0.99) AND " +
                " likelihood (e.UserAction > -1, 0.99) AND " +
                " (likelihood (e.Score >= ?, 0.1) OR likelihood (e.UserAction = 1, 0.01)) " +
                "UNION " +
                "SELECT e.Id as FirstMessageId, e.DateReceived as DateReceived, e.ConversationId as ConversationId FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " JOIN McFolder AS f ON f.Id = m.FolderId " +
                " JOIN McEmailMessageDependency AS d ON e.Id = d.EmailMessageId " +
                " WHERE " +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.IsChat = 0, 0.8) AND " +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (f.Type = ?, 0.05) AND " +
                " d.EmailAddressId IN (SELECT a.Id FROM McEmailAddress AS a WHERE likelihood (a.IsVip != 0, 0.01)) " +
                " ) " +
                " GROUP BY ConversationId " +
                " ORDER BY DateReceived DESC";

            var query = String.Format (queryFormat);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query,
                McAbstrFolderEntry.ClassCodeEnum.Email, Xml.FolderHierarchy.TypeCode.DefaultInbox_2, DateTime.UtcNow, hotScore,
                McAbstrFolderEntry.ClassCodeEnum.Email, Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
        }

        public static List<McEmailMessageThread> QueryActiveMessageItemsByScore2 (int accountId, int folderId, double hotScore, double ltrScore)
        {
            var queryFormat =
                "SELECT FirstMessageId, Count(FirstMessageId) as MessageCount, DateReceived, ConversationId FROM " +
                " ( " +
                " SELECT e.Id as FirstMessageId, e.DateReceived as DateReceived, e.ConversationId as ConversationId FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                //" JOIN McEmailMessageDependency AS d ON e.Id = d.EmailMessageId " +
                " WHERE " +
                "{0}" +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.IsChat = 0, 0.8) AND " +
                "{1}" +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (m.FolderId = ?, 0.05) AND " +
                " likelihood (e.FlagUtcStartDate < ?, 0.99) AND " +
                " likelihood (e.Score < ? AND e.Score2 >= ?, 0.1) AND " +
                " likelihood (e.UserAction <= 0, 0.99) " +
                " ) " +
                " GROUP BY ConversationId " +
                " ORDER BY DateReceived DESC";

            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 1.0) AND ", accountId);
            var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 1.0) AND ", accountId);

            var query = String.Format (queryFormat, account0, account1);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, McAbstrFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow, hotScore, ltrScore);
        }


        public static List<McEmailMessageThread> QueryUnifiedItemsByScore2 (double hotScore, double ltrScore)
        {
            var queryFormat =
                "SELECT FirstMessageId, Count(FirstMessageId) as MessageCount, DateReceived, ConversationId FROM " +
                " ( " +
                " SELECT e.Id as FirstMessageId, e.DateReceived as DateReceived, e.ConversationId as ConversationId FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " JOIN McFolder AS f ON f.Id = m.FolderId " +
                //" JOIN McEmailMessageDependency AS d ON e.Id = d.EmailMessageId " +
                " WHERE " +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.IsChat = 0, 0.8) AND " +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (f.Type = ?, 0.2) AND " +
                " likelihood (e.FlagUtcStartDate < ?, 0.99) AND " +
                " likelihood (e.Score < ? AND e.Score2 >= ?, 0.1) AND " +
                " likelihood (e.UserAction <= 0, 0.99) " +
                " ) " +
                " GROUP BY ConversationId " +
                " ORDER BY DateReceived DESC";

            var query = String.Format (queryFormat);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, McAbstrFolderEntry.ClassCodeEnum.Email,  Xml.FolderHierarchy.TypeCode.DefaultInbox_2, DateTime.UtcNow, hotScore, ltrScore);
        }

        /// TODO: Delete needs to clean up deferred
        public static List<McEmailMessageThread> QueryDeferredMessageItems (int accountId)
        {
            var queryFormat = 
                "SELECT e.Id as FirstMessageId, 1 as MessageCount FROM McEmailMessage AS e " +
                " WHERE " +
                "{0}" +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.FlagStatus <> 0, 0.001) AND " +
                " e.FlagUtcStartDate > ? " +
                " ORDER BY e.DateReceived DESC";
            
            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 1.0) AND ", accountId);

            var query = String.Format (queryFormat, account0);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, DateTime.UtcNow);
        }

        /// TODO: Delete needs to clean up deferred
        public static List<McEmailMessageThread> QueryDeferredMessageItemsByThreadId (int accountId, string threadId)
        {
            var queryFormat =
                "SELECT  e.Id as FirstMessageId, 1 as MessageCount FROM McEmailMessage AS e " +
                " WHERE " +
                "{0}" +
                " likelihood (e.ConversationId = ?, 0.01) AND " +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (e.FlagStatus <> 0, 0.001) AND " +
                " e.FlagUtcStartDate > ? " +
                " ORDER BY e.DateReceived DESC";

            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 1.0) AND ", accountId);

            var query = String.Format (queryFormat, account0);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (
                query, accountId, threadId, DateTime.UtcNow);
        }

        public static List<McEmailMessageThread> QueryDueDateMessageItems (int accountId)
        {
            var queryFormat =
                "SELECT e.Id as FirstMessageId, 1 as MessageCount FROM McEmailMessage AS e " +
                " WHERE " +
                "{0}" +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND" +
                " likelihood (e.FlagStatus <> 0, 0.001) AND" +
                " e.FlagType <> ?";
            
            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 1.0) AND ", accountId);

            var query = String.Format (queryFormat, account0);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (query, "Defer until");
        }

        public static List<McEmailMessageThread> QueryDueDateMessageItemsByThreadId (int accountId, string threadId)
        {
            var queryFormat =
                "SELECT  e.Id as FirstMessageId, 1 as MessageCount FROM McEmailMessage AS e " +
                " WHERE " +
                "{0}" +
                " e.ConversationId = ? AND" +
                " likelihood (e.IsAwaitingDelete = 0, 1.0) AND" +
                " likelihood (e.FlagStatus <> 0, 0.001) AND" +
                " e.FlagType <> ?";
            
            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 1.0) AND ", accountId);

            var query = String.Format (queryFormat, account0);

            return NcModel.Instance.Db.Query<McEmailMessageThread> (query, threadId, "Defer until");
        }

        public static List<McEmailMessageThread> QueryForMessageThreadSet (List<int> indexList)
        {
            var set = String.Format ("( {0} )", String.Join (",", indexList.ToArray<int> ()));
            var cmd = String.Format (
                          "SELECT  e.Id as FirstMessageId, 1 as MessageCount FROM McEmailMessage AS e " +
                          "WHERE " +
                          "e.ID IN {0} " +
                          " ORDER BY e.DateReceived DESC ",
                          set);
            return NcModel.Instance.Db.Query<McEmailMessageThread> (cmd); 
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
                EmailMessageIndexDocument.Version - 1, EmailMessageIndexDocument.Version, 
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

        public static List<McEmailMessage> QueryByThreadTopic (int accountId, string topic)
        {
            var queryFormat =
                "SELECT * FROM McEmailMessage WHERE " +
                "{0}" +
                " likelihood (IsAwaitingDelete = ?, 1.0) AND " +
                " likelihood (ThreadTopic = ?, 0.01) ";

            var account0 = SingleAccountString (" likelihood (AccountId = {0}, 1.0) AND ", accountId);

            var query = String.Format (queryFormat, account0);

            return NcModel.Instance.Db.Query<McEmailMessage> (query, false, topic);
        }

        public static List<McEmailMessage>  QueryUnreadAndHotAfter (DateTime since)
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
            
            var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 1.0) AND ", accountId);

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

            var account0 = SingleAccountString (" likelihood (f.AccountId = {0}, 1.0) AND ", accountId);

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

                var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 1.0) AND ", accountId);
                var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 1.0) AND ", accountId);

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

                var account0 = SingleAccountString (" likelihood (e.AccountId = {0}, 1.0) AND ", accountId);
                var account1 = SingleAccountString (" likelihood (m.AccountId = {0}, 1.0) AND ", accountId);

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
            if (IsChat && !String.IsNullOrEmpty(MessageID)) {
                var outbox = McFolder.GetClientOwnedOutboxFolder (AccountId);
                var outboxMessages = NcModel.Instance.Db.Query<McEmailMessage> ("SELECT m.* FROM McEmailMessage m JOIN McMapFolderFolderEntry e ON m.Id = e.FolderEntryId WHERE m.AccountId = ? AND m.MessageID = ? AND m.Id != ? AND e.FolderId = ?", AccountId, MessageID, Id, outbox.Id);
                foreach (var message in outboxMessages){
                    message.Delete ();
                }
            }
        }
    }

    public class McEmailMessageThread
    {
        public int MessageCount { set; get; }

        public int FirstMessageId { set; get; }

        public INachoEmailMessages Source;

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
        private bool emailAddressesChanged = false;

        /// Indexes of To in McEmailAddress table
        private List<int> dbToEmailAddressId = null;

        /// Indexes of Cc in McEmailAddress table
        private List<int> dbCcEmailAddressId = null;

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

        [Ignore]
        public List<int> ToEmailAddressId {
            get {
                ReadAddressMaps ();
                return dbToEmailAddressId;
            }
            set {
                emailAddressesChanged = true;
                dbToEmailAddressId = value;
            }
        }

        [Ignore]
        public List<int> CcEmailAddressId {
            get {
                ReadAddressMaps ();
                return dbCcEmailAddressId;
            }
            set {
                emailAddressesChanged = true;
                dbCcEmailAddressId = value;
            }
        }

        protected void ReadAddressMaps ()
        {
            if (null == dbToEmailAddressId) {
                if (0 == this.Id) {
                    dbToEmailAddressId = new List<int> ();
                } else {
                    dbToEmailAddressId = McMapEmailAddressEntry.QueryMessageToAddressIds (AccountId, Id);
                }
            }
            if (null == dbCcEmailAddressId) {
                if (0 == this.Id) {
                    dbCcEmailAddressId = new List<int> ();
                } else {
                    dbCcEmailAddressId = McMapEmailAddressEntry.QueryMessageCcAddressIds (AccountId, Id);
                }
            }
        }

        private void InsertAddressList (List<int> addressIdList, NcEmailAddress.Kind kind)
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

        private void InsertAddressMaps ()
        {
            if (0 < FromEmailAddressId) {
                var map = CreateAddressMap ();
                map.EmailAddressId = FromEmailAddressId;
                map.AddressType = NcEmailAddress.Kind.From;
                map.Insert ();
            }
            if (0 < SenderEmailAddressId) {
                var map = CreateAddressMap ();
                map.EmailAddressId = SenderEmailAddressId;
                map.AddressType = NcEmailAddress.Kind.Sender;
                map.Insert ();
            }
            InsertAddressList (dbToEmailAddressId, NcEmailAddress.Kind.To);
            InsertAddressList (dbCcEmailAddressId, NcEmailAddress.Kind.Cc);
            emailAddressesChanged = false;
        }

        private void DeleteAddressMaps ()
        {
            McMapEmailAddressEntry.DeleteMessageMapEntries (AccountId, Id);
        }

        public override int Insert ()
        {
            using (var capture = CaptureWithStart ("Insert")) {
                int returnVal = -1; 

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
                HasBeenNotified = (NcApplication.Instance.IsForeground || IsRead);

                NcModel.Instance.RunInTransaction (() => {
                    returnVal = base.Insert ();
                    InsertAddressMaps ();
                    InsertMeetingRequest ();
                    InsertCategories ();
                    InsertScoreStates ();
                    McEmailMessageNeedsUpdate.Insert(this, 0);
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
                if (emailAddressesChanged) {
                    DeleteAddressMaps ();
                    InsertAddressMaps ();
                }
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

        public void UpdateIsIndex (int newIsIndexed)
        {
            NcModel.Instance.BusyProtect (() => {
                return NcModel.Instance.Db.Execute ("UPDATE McEmailMessage SET IsIndexed = ?, RowVersion = RowVersion + 1 WHERE Id = ?",
                    newIsIndexed, Id);
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
        }

        public override int Delete ()
        {
            using (var capture = CaptureWithStart ("Delete")) {
                int returnVal = 0;
                NcModel.Instance.RunInTransaction (() => {
                    returnVal = base.Delete ();
                    // FIXME: Do we need to delete associated records like Attachments?
                    NcBrain.UnindexEmailMessage (this);
                    DeleteScoreStates ();
                    McEmailMessageNeedsUpdate.Delete(this);
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

        public int SetIndexVersion ()
        {
            int newIsIndexed;
            if (IsJunk) {
                // Don't index junk
                newIsIndexed = EmailMessageIndexDocument.Version;
            } else if (0 == BodyId) {
                // No body to index. Try again later.
                newIsIndexed = EmailMessageIndexDocument.Version - 1;
            } else {
                var body = GetBody ();
                if ((null != body) && body.IsComplete ()) {
                    // Message is fully indexed
                    newIsIndexed = EmailMessageIndexDocument.Version;
                } else {
                    // Body not downloaded; try again later
                    newIsIndexed = EmailMessageIndexDocument.Version - 1;
                }
            }
            return newIsIndexed;
        }

        public static McEmailMessage QueryByMessageId (int accountId, string messageId)
        {
            var queryFormat = "SELECT * FROM McEmailMessage WHERE {0} MessageID = ?";
            
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
                var query = "SELECT COUNT(*) FROM McEmailMessage WHERE AccountId = ? AND ConversationId = ? AND IsChat = 1";
                IsChat = NcModel.Instance.Db.ExecuteScalar<int> (query, AccountId, ConversationId) > 0;
            }
        }

        public McEmailMessage MarkHasBeenNotified (bool shouldNotify)
        {
            if (shouldNotify) {
                NcBrain.MessageNotificationStatusUpdated (this, DateTime.UtcNow, 0.1);
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
