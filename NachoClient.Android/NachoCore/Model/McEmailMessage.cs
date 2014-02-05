using System;
using System.Collections.Generic;
using System.Globalization;
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

        /// Kind of delay being applied
        public MessageDeferralType DeferralType { set; get; }

        /// User has asked to hide the message for a while
        public DateTime UtcDeferUntil { set; get; }

        // User must complete task by.
        public DateTime UtcDue { set; get; }
        /// Attachments are separate

        [Indexed]
        /// Body data is in McBody
        public int BodyId { set; get; }

        /// Summary is extracted in gleaner
        public string Summary { set; get; }

        /// Integer -- plain test, html, rtf, mime
        public string BodyType { set; get; }

        public string ToMime (SQLiteConnection db)
        {
            string message = "";
            foreach (var propertyName in new [] {"From", "To", "Subject", "ReplyTo", "DisplayTo"}) {
                message = Append (message, propertyName);
            }
            string date = DateTime.UtcNow.ToString ("ddd, dd MMM yyyy HH:mm:ss K", DateTimeFormatInfo.InvariantInfo);
            message = message + CrLf + "Date" + ColonSpace + date;
            message = message + CrLf + CrLf + GetBody (db);
            return message;
        }

        private string Append (string message, string propertyName)
        {
            string propertyValue = (string)typeof(McEmailMessage).GetProperty (propertyName).GetValue (this);
            if (null == propertyValue) {
                return message;
            }
            if ("" == message) {
                return propertyName + ColonSpace + propertyValue;
            }
            return message + CrLf + propertyName + ColonSpace + propertyValue;
        }

        public string GetBody (SQLiteConnection db)
        {
            var body = db.Get<McBody> (BodyId);
            if (null == body) {
                return null;
            } else {
                return body.Body;
            }
        }

        public void DeleteBody (SQLiteConnection db)
        {
            if (0 != BodyId) {
                var body = new McBody ();
                body.Id = BodyId;
                db.Delete (body);
                BodyId = 0;
                db.Update (this);
            }
        }

        public void Update ()
        {
            BackEnd.Instance.Db.Update (this);
        }

        // Note need to paramtrize <T> and move to McItem.
        public static List<McEmailMessage> ActiveMessages (int accountId, int folderId)
        {
            return BackEnd.Instance.Db.Query<McEmailMessage> ("SELECT e.* FROM McEmailMessage AS e JOIN McMapFolderItem AS m ON e.Id = m.ItemId WHERE " +
            " m.AccountId = ? AND " +
            " m.FolderId = ? AND " +
                "e.UtcDeferUntil < ?",
                accountId, folderId, DateTime.UtcNow);
        }
        // TODO: Need account id
        // TODO: Delete needs to clean up deferred
        public static List<McEmailMessage> DeferredMessages ()
        {
            return BackEnd.Instance.Db.Query<McEmailMessage> ("SELECT e.* FROM McEmailMessage AS e JOIN McMapFolderItem AS m ON e.Id = m.ItemId WHERE " +
                "e.UtcDeferUntil > ?",
                DateTime.UtcNow);
        }
    }

    public class McBody : McObject
    {
        public string Body { get; set; }
    }
}

