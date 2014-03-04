using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public enum MessageDeferralType
    {
        None,
        Later,
        Tonight,
        Tomorrow,
        NextWeek,
        MonthEnd,
        NextMonth,
        Forever,
        Meeting,
        Custom,
    };

    public class McEmailMessage : McItem
    {
        private const string CrLf = "\r\n";
        private const string ColonSpace = ": ";

        /// All To addresses, comma separated (optional)
        public string To { set; get; }

        /// All Cc addresses, comma separated (optional)
        public string Cc { set; get; }

        /// Email address of the sender (optional)
        public string From { set; get; }

        /// Subject of the message (optional)
        public string Subject { set; get; }

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
        public uint Importance { set; get; }

        [Indexed]
        /// Has the message been read? (optional)
        public bool IsRead { set; get; }

        /// A hint from the server (optional)
        public string MessageClass { set; get; }

        /// Sender, maybe not the same as From (optional)
        public string Sender { set; get; }

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

        ///
        /// <Flag> STUFF.
        ///

        /// Kind of delay being applied
        /// FIXME - this should go away and be a function of the Sync-ed
        /// data in the model. Otherwise, it won't work right in another 
        /// NachoClient.
        public MessageDeferralType DeferralType { set; get; }
        // NOTE: These values ARE the AS values.
        public enum FlagStatusValue : uint
        {
            Cleared = 0,
            Complete = 1,
            Active = 2,
        };

        public uint FlagStatus { set; get; }
        // This is the string associated with the flag.
        public string FlagType { set; get; }

        /// User has asked to hide the message for a while
        public DateTime FlagUtcDeferUntil { set; get; }

        public DateTime FlagDeferUntil { set; get; }
        // User must complete task by.
        public DateTime FlagUtcDue { set; get; }

        public DateTime FlagDue { set; get; }

        public DateTime FlagDateCompleted { set; get; }

        public DateTime FlagCompleteTime { set; get; }

        public bool FlagReminderSet { set; get; }

        public DateTime FlagReminderTime { set; get; }

        public DateTime FlagOrdinalDate { set; get; }

        public DateTime FlagSubOrdinalDate { set; get; }

        ///
        /// </Flag> STUFF.
        ///

        /// Attachments are separate

        [Indexed]
        /// Body data is in McBody
        public int BodyId { set; get; }

        /// Summary is extracted in gleaner
        public string Summary { set; get; }

        /// Integer -- plain test, html, rtf, mime
        public string BodyType { set; get; }

        // TODO: Support other types besides mime!
        public string ToMime ()
        {
            return GetBody ();
        }

        public string GetBody ()
        {
            var body = BackEnd.Instance.Db.Get<McBody> (BodyId);
            if (null == body) {
                return null;
            } else {
                return body.Body;
            }
        }

        private void DeleteBody ()
        {
            if (0 != BodyId) {
                var body = new McBody ();
                body.Id = BodyId;
                BackEnd.Instance.Db.Delete (body);
                BodyId = 0;
                BackEnd.Instance.Db.Update (this);
            }
        }

        public static McEmailMessage QueryByServerId (int accountId, string serverId)
        {
            return BackEnd.Instance.Db.Table<McEmailMessage> ().SingleOrDefault (fld => 
                fld.AccountId == accountId &&
            fld.ServerId == serverId);
        }
        // Note need to paramtrize <T> and move to McItem.
        public static McEmailMessage QueryById (int id)
        {
            return BackEnd.Instance.Db.Query<McEmailMessage> ("SELECT e.* FROM McEmailMessage AS e WHERE " +
            " e.Id = ? ",
                id).SingleOrDefault ();
        }
        // Note need to paramtrize <T> and move to McItem.
        public static List<McEmailMessage> QueryByFolderId (int accountId, int folderId)
        {
            return BackEnd.Instance.Db.Query<McEmailMessage> ("SELECT e.* FROM McEmailMessage AS e JOIN McMapFolderItem AS m ON e.Id = m.ItemId WHERE " +
            " e.AccountId = ? AND " +
            " m.AccountId = ? AND " +
            " m.FolderId = ? ",
                accountId, accountId, folderId);
        }

        public static List<McEmailMessage> QueryActiveMessages (int accountId, int folderId)
        {
            return BackEnd.Instance.Db.Query<McEmailMessage> ("SELECT e.* FROM McEmailMessage AS e JOIN McMapFolderItem AS m ON e.Id = m.ItemId WHERE " +
            " e.AccountId = ? AND " +
            " m.AccountId = ? AND " +
            " m.FolderId = ? AND " +
            " e.FlagUtcDeferUntil < ?",
                accountId, accountId, folderId, DateTime.UtcNow);
        }
        // TODO: Need account id
        // TODO: Delete needs to clean up deferred
        public static List<McEmailMessage> QueryDeferredMessagesAllAccounts ()
        {
            return BackEnd.Instance.Db.Query<McEmailMessage> ("SELECT e.* FROM McEmailMessage AS e WHERE " +
            " e.FlagUtcDeferUntil > ?",
                DateTime.UtcNow);
        }

        public override int Delete ()
        {
            DeleteBody ();
            return base.Delete ();
        }
    }

    public class McBody : McObject
    {
        public string Body { get; set; }
    }
}

