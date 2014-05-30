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
        Custom,
    };

    public enum NcImportance
    {
        Low_0 = 0,
        Normal_1 = 1,
        High_2 = 2,
    };

    public class McEmailMessageIndex
    {
        public int Id { set; get; }

        public McEmailMessage GetMessage ()
        {
            return NcModel.Instance.Db.Get<McEmailMessage> (Id);
        }
    }

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

        private int GetMaxContactScore (string emailAddressString)
        {
            // TODO: Test this
            int score = int.MinValue;
            var addresses = NcEmailAddress.ParseString (emailAddressString);
            foreach (var address in addresses) {
                var emailAddress = address as MailboxAddress;
                if (null != emailAddress) {
                    List<McContact> contactList = McContact.QueryByEmailAddress (AccountId, emailAddress.Address);
                    foreach (McContact contact in contactList) {
                        score = Math.Max (score, contact.Score);
                    }
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

        /// TODO: Support other types besides mime!
        public StreamContent ToMime ()
        {
            var bodyPath = MimePath ();
            if (null == bodyPath) {
                return null;
            }
            var fileStream = new FileStream (bodyPath, FileMode.Open);
            if (null == fileStream) {
                Log.Error (Log.LOG_EMAIL, "BodyPath {0} doesn't find a file.", bodyPath);
                return null;
            }
            return new StreamContent (fileStream);
        }

        public string MimePath ()
        {
            var body = McBody.GetDescr (BodyId);
            if (null == body) {
                return null;
            }
            return body.BodyPath;
        }

        public void DeleteAttachments ()
        {
            var atts = McAttachment.QueryByItemId<McEmailMessage> (AccountId, Id);
            foreach (var toNix in atts) {
                toNix.Delete ();
            }
        }

        public void Summarize ()
        {
            Summary = MimeHelpers.ExtractSummary (this);
            if (null == Summary) {
                Summary = " ";
            }
            this.Update ();
        }

        public static List<McEmailMessage> QueryActiveMessages (int accountId, int folderId)
        {
            return NcModel.Instance.Db.Query<McEmailMessage> (
                "SELECT e.* FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                " e.AccountId = ? AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? AND " +
                " e.FlagUtcDeferUntil < ?",
                accountId, accountId, McFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow);
        }

        public static List<McEmailMessageIndex> QueryActiveMessageItems (int accountId, int folderId)
        {
            return NcModel.Instance.Db.Query<McEmailMessageIndex> (
                "SELECT e.Id as Id FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                " e.AccountId = ? AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? AND " +
                " e.FlagUtcDeferUntil < ? " +
                " ORDER BY e.DateReceived DESC",
                accountId, accountId, McFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow);
        }

        public static List<McEmailMessageIndex> QueryActiveMessageItemsByScore (int accountId, int folderId)
        {
            return NcModel.Instance.Db.Query<McEmailMessageIndex> (
                "SELECT e.Id as Id FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                " e.AccountId = ? AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? AND " +
                " e.FlagUtcDeferUntil < ? " +
                " ORDER BY e.ContentScore DESC, e.DateReceived DESC",
                accountId, accountId, McFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow);
        }

        /// TODO: Need account id
        /// TODO: Delete needs to clean up deferred
        public static List<McEmailMessageIndex> QueryDeferredMessageItemsAllAccounts ()
        {
            return NcModel.Instance.Db.Query<McEmailMessageIndex> (
                "SELECT e.Id as Id FROM McEmailMessage AS e " +
                " WHERE " +
                " e.FlagUtcDeferUntil > ? ORDER BY e.DateReceived DESC",
                DateTime.UtcNow);
        }

        public static List<McEmailMessage> QueryByThreadTopic (int accountId, string topic)
        {
            return NcModel.Instance.Db.Table<McEmailMessage> ().Where (
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
            // TODO: Do we need to return just one?
            // NachoAssert.True (1 == contactList.Count);
            if ((null == contactList) || (0 == contactList.Count)) {
                return null;
            }
            return contactList [0];
        }

        public List<McContact> GetContactsFromEmailAddressString (string emailAddressString)
        {
            //TODO: Test this
            var addresses = NcEmailAddress.ParseString (emailAddressString);

            // Use a set to eliminate duplicates
            HashSet<McContact> contactSet = new HashSet<McContact> ();

            Log.Info (Log.LOG_BRAIN, "SCORE: emailAddressString={0}", emailAddressString);
            foreach (var address in addresses) {
                var emailAddress = address as MailboxAddress;
                if (null != emailAddress) {
                    Log.Info (Log.LOG_BRAIN, "SCORE: emailAddress={0}", emailAddress.Address);
                    List<McContact> queryResult = McContact.QueryByEmailAddress (AccountId, emailAddress.Address);
                    if (0 == queryResult.Count) {
                        Log.Warn (Log.LOG_BRAIN, "Unknown email address {0}", emailAddress);
                    }
                    foreach (McContact contact in queryResult) {
                        contactSet.Add (contact);
                    }
                }
            }

            // Convert set to list
            List<McContact> contactList = new List<McContact> ();
            foreach (McContact contact in contactSet) {
                contactList.Add (contact);
            }
            return contactList;
        }

        public bool IsDeferred ()
        {
            if ((DateTime.MinValue == FlagDeferUntil) && (DateTime.MinValue == FlagUtcDeferUntil)) {
                return false;
            }
            if (DateTime.MinValue != FlagDeferUntil) {
                return DateTime.Now > FlagDeferUntil;
            }
            if (DateTime.MinValue != FlagUtcDeferUntil) {
                return DateTime.UtcNow > FlagUtcDeferUntil;
            }
            NachoAssert.CaseError ();
            return false;
        }

        public bool HasDueDate ()
        {
            if ((DateTime.MinValue == FlagDue) && (DateTime.MinValue == FlagUtcDue)) {
                return false;
            }
            if ((FlagDue == FlagDeferUntil) && (FlagUtcDue == FlagUtcDeferUntil)) {
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
                NachoAssert.CaseError ();
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
        List<McEmailMessageIndex> thread;

        public McEmailMessageThread ()
        {
            thread = new List<McEmailMessageIndex> ();
        }

        public void Add (McEmailMessageIndex index)
        {
            thread.Add (index);
        }

        public int GetEmailMessageIndex (int i)
        {
            return thread.ElementAt (i).Id;
        }

        public McEmailMessage GetEmailMessage (int i)
        {
            return thread.ElementAt (i).GetMessage ();
        }

        public McEmailMessage SingleMessageSpecialCase ()
        {
            return GetEmailMessage (0);
        }

        public int Count {
            get {
                return thread.Count;
            }
        }

        public IEnumerator<McEmailMessage> GetEnumerator ()
        {
            using (IEnumerator<McEmailMessageIndex> ie = thread.GetEnumerator ()) {
                while (ie.MoveNext ()) {
                    yield return ie.Current.GetMessage ();
                }
            }
        }
    }
}
