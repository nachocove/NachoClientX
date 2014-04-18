// Copyright (C) 2013, Nacho Cove, Inc.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SQLite;
using NachoCore.Utils;
using System.Net.Mail;

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

    public enum NcImportance
    {
        Low_0 = 0,
        Normal_1 = 1,
        High_2 = 2,
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
        public NcImportance Importance { set; get; }

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

        /// Summary is extracted in gleaner
        public string Summary { set; get; }

        /// The score based on content. The current attribute that
        /// affects this value is the number of messages in the thread.
        public int ContentScore { set; get; }

        private int GetMaxContactScore (string addressString)
        {
            List<MailAddress> emailAddressList = EmailAddressHelper.ParseString (addressString);
            int score = int.MinValue;
            foreach (MailAddress emailAddress in emailAddressList) {
                List<McContact> contactList = McContact.QueryByEmailAddress (AccountId, emailAddress.Address);
                foreach (McContact contact in contactList) {
                    score = Math.Max (score, contact.Score);
                }
            }
            return score;
        }

        public int GetScore ()
        {
            /// SCORING - Return the sum of the content score and 
            /// and the max contact score.
            int contactScore = GetMaxContactScore (To);
            contactScore = Math.Max (contactScore, GetMaxContactScore (From));
            contactScore = Math.Max (contactScore, GetMaxContactScore (Cc));

            return ContentScore + contactScore;
        }
        // TODO: Support other types besides mime!
        public string ToMime ()
        {
            return GetBody ();
        }

        public void DeleteAttachments ()
        {
            var atts = McAttachment.QueryByItemId<McEmailMessage> (AccountId, Id);
            foreach (var toNix in atts) {
                toNix.Delete ();
            }
        }

        public static List<McEmailMessage> QueryActiveMessages (int accountId, int folderId)
        {
            return BackEnd.Instance.Db.Query<McEmailMessage> ("SELECT e.* FROM McEmailMessage AS e JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId WHERE " +
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

        public static List<McEmailMessage> QueryByThreadTopic (int accountId, string topic)
        {
            return BackEnd.Instance.Db.Table<McEmailMessage> ().Where (
                x => x.AccountId == accountId && x.ThreadTopic == topic).ToList ();
        }

        public override int Delete ()
        {
            DeleteBody ();
            DeleteAttachments ();
            return base.Delete ();
        }

        public static ClassCodeEnum GetClassCode ()
        {
            return McFolderEntry.ClassCodeEnum.Email;
        }

        public McContact GetFromContact ()
        {
            List<McContact> contactList = GetContactsFromEmailAddressString (From);
            NachoAssert.True (1 == contactList.Count);
            return contactList [0];
        }

        public List<McContact> GetContactsFromEmailAddressString (string emailAddressString)
        {
            List<MailAddress> emailAddresses = EmailAddressHelper.ParseString (emailAddressString);
            // Use a set to eliminate duplicates
            HashSet<McContact> contactSet = new HashSet<McContact> ();

            Log.Info ("SCORE: emailAddressString={0}", emailAddressString);
            foreach (MailAddress emailAddress in emailAddresses) {
                Log.Info ("SCORE: emailAddress={0}", emailAddress.Address);
                List<McContact> queryResult = McContact.QueryByEmailAddress (AccountId, emailAddress.Address);
                if (0 == queryResult.Count) {
                    Log.Warn ("Unknown email address {0}", emailAddress);
                }
                foreach (McContact contact in queryResult) {
                    contactSet.Add (contact);
                }
            }

            // Convert set to list
            List<McContact> contactList = new List<McContact> ();
            foreach (McContact contact in contactSet) {
                contactList.Add (contact);
            }
            return contactList;
        }
    }

    public class McBody : McObject
    {
        // FIXME - we should carry the encoding type (RTF, Mime, etc) here.
        public string Body { get; set; }
    }
}

