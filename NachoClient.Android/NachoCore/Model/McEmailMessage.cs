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

    public partial class McEmailMessageCategory : McObject
    {
        /// Parent Calendar or Exception item index.
        [Indexed]
        public Int64 ParentId { get; set; }

        /// Name of category
        [MaxLength (256)]
        public string Name { get; set; }
    }

    public partial class McEmailMessage : McItem
    {
        private const string CrLf = "\r\n";
        private const string ColonSpace = ": ";

        /// All To addresses, comma separated (optional)
        public string To { set; get; }

        /// All Cc addresses, comma separated (optional)
        public string Cc { set; get; }

        /// Email address of the sender (optional)
        public string From { set; get; }
        public int cachedFromColor { set; get; }
        public string cachedFromLetters { set; get; }

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

        /// Specifies how the e-mail is stored on the server (optional)
        public byte NativeBodyType { set; get; }

        /// MIME original code page ID
        public string InternetCPID { set; get; }

        /// Set of timestamps used to generation conversation tree
        public byte[] ConversationIndex { set; get; }

        /// Specifies the content class of the data (optional) - Must be 'urn:content-classes:message' for email
        public string ContentClass { set; get; }

        [Ignore]
        /// Internal list of category elements
        protected List<McEmailMessageCategory> _Categories{ get; set; }

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

        //This is strictly used for testing purposes
        public List<McEmailMessageCategory> getInternalCategoriesList ()
        {
            return _Categories;
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

        public static List<McEmailMessage> QueryActiveMessages (int accountId, int folderId)
        {
            return NcModel.Instance.Db.Query<McEmailMessage> (
                "SELECT e.* FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                " e.AccountId = ? AND " +
                " e.IsAwaitingDelete = 0 AND " +
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
                " e.IsAwaitingDelete = 0 AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? AND " +
                " e.FlagUtcDeferUntil < ? " +
                " ORDER BY e.DateReceived DESC",
                accountId, accountId, McFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow);
        }

        public static IEnumerable<McEmailMessage> QueryNeedsFetch (int accountId, int folderId, int limit)
        {
            return NcModel.Instance.Db.Query<McEmailMessage> (
                "SELECT e.* FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                " e.AccountId = ? AND " +
                " e.IsAwaitingDelete = 0 AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? AND " +
                " e.BodyState != 0 " +
                " ORDER BY e.Score DESC, e.DateReceived DESC LIMIT ?",
                accountId, accountId, McFolderEntry.ClassCodeEnum.Email, folderId, limit);
        }

        public static List<McEmailMessageIndex> QueryActiveMessageItemsByScore (int accountId, int folderId)
        {
            return NcModel.Instance.Db.Query<McEmailMessageIndex> (
                "SELECT e.Id as Id FROM McEmailMessage AS e " +
                " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                " WHERE " +
                " e.AccountId = ? AND " +
                " e.IsAwaitingDelete = 0 AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? AND " +
                " e.FlagUtcDeferUntil < ? " +
                " ORDER BY e.Score DESC, e.DateReceived DESC LIMIT 20",
                accountId, accountId, McFolderEntry.ClassCodeEnum.Email, folderId, DateTime.UtcNow);
        }

        /// TODO: Need account id
        /// TODO: Delete needs to clean up deferred
        public static List<McEmailMessageIndex> QueryDeferredMessageItemsAllAccounts ()
        {
            return NcModel.Instance.Db.Query<McEmailMessageIndex> (
                "SELECT e.Id as Id FROM McEmailMessage AS e " +
                " WHERE " +
                " e.IsAwaitingDelete = 0 AND " +
                " e.FlagUtcDeferUntil > ? ORDER BY e.DateReceived DESC",
                DateTime.UtcNow);
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
            return NcModel.Instance.Db.Table<McEmailMessage> ().Where (x => 
                false == x.IsRead && since < x.LastModified).OrderByDescending (x => x.LastModified);
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

            foreach (var address in addresses) {
                var emailAddress = address as MailboxAddress;
                if (null != emailAddress) {
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

        public const double minHotScore = 0.1;

        public bool isHot ()
        {
            return (minHotScore < this.Score );
        }

        public bool IsDeferred ()
        {
            if ((DateTime.MinValue == FlagDeferUntil) && (DateTime.MinValue == FlagUtcDeferUntil)) {
                return false;
            }
            if (DateTime.MinValue != FlagDeferUntil) {
                return DateTime.Now < FlagDeferUntil;
            }
            if (DateTime.MinValue != FlagUtcDeferUntil) {
                return DateTime.UtcNow < FlagUtcDeferUntil;
            }
            NcAssert.CaseError ();
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
            var message = GetEmailMessage (0);
            NcAssert.NotNull (message);
            return message;
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

    public partial class McEmailMessage
    {
        protected Boolean isDeleted;
        protected Boolean isAncillaryInMemory;

        public McEmailMessage () : base ()
        {
            _Categories = new List<McEmailMessageCategory> ();
            isDeleted = false;
            isAncillaryInMemory = false;
        }

        [Ignore]
        public List<McEmailMessageCategory> Categories {
            get {
                ReadAncillaryData ();
                return _Categories;
            }
            set {
                _Categories = value;
            }
        }

        protected NcResult ReadAncillaryData ()
        {
            NcAssert.True (!isDeleted);
            if (!isAncillaryInMemory) {
                ForceReadAncillaryData ();
            }
            return NcResult.OK ();
        }

        protected NcResult ForceReadAncillaryData ()
        {
            SQLiteConnection db = NcModel.Instance.Db;
            _Categories = db.Table<McEmailMessageCategory> ().Where (x => x.ParentId == Id).ToList ();
            isAncillaryInMemory = true;
            return NcResult.OK ();
        }

        protected NcResult InsertAncillaryData (SQLiteConnection db)
        {
            return InsertCategories (db);
        }

        protected NcResult InsertCategories (SQLiteConnection db)
        {
            foreach (var c in _Categories) {
                c.SetParent (this);
                db.Insert (c);
            }
            return NcResult.OK ();
        }

        protected void DeleteAncillaryData (SQLiteConnection db)
        {
            DeleteCategoriesData (db);
        }

        protected void DeleteCategoriesData (SQLiteConnection db)
        {
            db.Query<McEmailMessageCategory> ("DELETE FROM McEmailMessageCategory WHERE ParentID=?", Id);
        }

        public override int Insert ()
        {
            NcAssert.True (!isDeleted);

            //FIXME better default returnVal
            int returnVal = -1; 

            try 
            {
                NcModel.Instance.Db.RunInTransaction (() => {
                    returnVal = base.Insert ();
                    InsertAncillaryData (NcModel.Instance.Db);
                });
            }
            catch (SQLiteException ex) 
            {
                Log.Error(Log.LOG_EMAIL,"Inserting the email failed: {0} No changes were made to the DB.", ex.Message);
            }
                
            return returnVal;
        }

        public override int Update ()
        {
            NcAssert.True (!isDeleted);

            //FIXME better default returnVal
            int returnVal = -1;  

            try 
            {
                NcModel.Instance.Db.RunInTransaction (() => {
                    returnVal = base.Update ();
                    if(!isAncillaryInMemory){
                        ForceReadAncillaryData();
                    }
                    DeleteAncillaryData (NcModel.Instance.Db);
                    InsertAncillaryData (NcModel.Instance.Db);
                });
            }
            catch (SQLiteException ex) 
            {
                Log.Error(Log.LOG_EMAIL,"Updating the email failed: {0} No changes were made to the DB.", ex.Message);
            }

            return returnVal;
        }

        public override void DeleteAncillary ()
        {
            NcAssert.True (NcModel.Instance.IsInTransaction ());
            if (!IsRead) {
                McContact sender = GetFromContact ();
                if (null != sender) {
                    sender.IncrementEmailsDeleted ();
                    sender.ForceReadAncillaryData ();
                    sender.Update ();
                }
            }
            DeleteBody ();
            DeleteAttachments ();
            DeleteAncillaryData (NcModel.Instance.Db);
        }
            
        public override int Delete ()
        {
            //FIXME better default returnVal
            int returnVal = base.Delete ();
            if (0 != returnVal || -1 != returnVal) {
                isDeleted = true;
            }
            return returnVal;
        }
    }

    public partial class McEmailMessageCategory
    {
        public McEmailMessageCategory ()
        {
            Id = 0;
            ParentId = 0;
            Name = null;
        }

        public McEmailMessageCategory (string name, int parentId)
        {
            Name = name;
            ParentId = parentId;
        }

        public McEmailMessageCategory (string name) : this ()
        {
            Name = name;
        }

        public void SetParent (McEmailMessage r)
        {
            ParentId = r.Id;
        }
    }
}
