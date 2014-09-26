using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;
using MimeKit;
using System.IO;

namespace NachoCore.Model
{
    /// <summary>
    /// Root of calendar and exception items.
    /// Table of attendees, refer back to entry they are attending (1 unique set per entry)
    /// Table of categories, referring back to entry they categorize (1 unique set per entry)
    /// Table of exceptions, referring back to entry they are modifying (1 unique set per entry)
    /// Table of timezones, referred to by the calendar entry
    /// </summary>
    public partial class McAbstrCalendarRoot : McAbstrItem
    {
        /// Item runs for the entire day
        public bool AllDayEvent { get; set; }

        /// When this item was created or modified (Compact DateTime, optional)
        public DateTime DtStamp { get; set; }

        /// Start time of this item (Compact DateTime, optional)
        public DateTime StartTime { get; set; }

        /// End time of this item (Compact DateTime, optional)
        public DateTime EndTime { get; set; }

        /// Number of minutes before start time to display a message (optional)
        public uint Reminder { get; set; }

        // The actual string from activesync
        public string TimeZone { get; set; }

        /// Subject of then calendar or exception item
        public string Subject { get; set; }

        /// Location of the event (optional)
        [MaxLength (256)]
        public string Location { get; set; }

        /// Recommended privacy policy for this item (optional)
        public NcSensitivity Sensitivity { get; set; }

        public bool SensitivityIsSet { get; set; }

        /// Busy status of the meeting organizer (optional)
        public NcBusyStatus BusyStatus { get; set; }

        public bool BusyStatusIsSet { get; set; }

        /// The default is False.  Calendar & MeetingRequest only
        public bool DisallowNewTimeProposal { get; set; }

        public bool DisallowNewTimeProposalIsSet { get; set; }

        /// Is a response to this meeting required? Calendar & MeetingRequest only.
        public bool ResponseRequested { get; set; }

        public bool ResponseRequestedIsSet { get; set; }

        /// None, Organizer, Tentative, ...
        public NcResponseType ResponseType { get; set; }

        public bool ResponseTypeIsSet { get; set; }

        /// Status of the meeting (optional)
        public NcMeetingStatus MeetingStatus { get; set; }

        public bool MeetingStatusIsSet { get; set; }

        /// The time this user responded to the request
        public DateTime AppointmentReplyTime { get; set; }

        /// The GRUU for the UAC
        public string OnlineMeetingConfLink { get; set; }

        /// The URL for the online meeting
        public string OnlineMeetingExternalLink { get; set; }

        // Specifies the original format type of the item
        public int NativeBodyType { get; set; }

        [Ignore]
        private List<McAttendee> DbAttendees { get; set; }

        [Ignore]
        private List<McCalendarCategory> DbCategories { get; set; }

        private Boolean HasReadAncillaryData;

        /// <summary>
        /// The plain text version of the description of the event.
        /// </summary>
        /// The description is stored in the item's body, along with other information,
        /// such as attachments.  It is not stored in the database directly.
        [Ignore]
        public string Description {
            get {
                if (null == cachedDescription) {
                    if (0 == BodyId) {
                        return "";
                    }
                    McBody body = McBody.QueryById<McBody> (BodyId);
                    if (McBody.MIME == BodyType) {
                        cachedDescription = MimeHelpers.ExtractTextPart (body);
                        if (null == cachedDescription) {
                            cachedDescription = "";
                        }
                    } else {
                        cachedDescription = body.GetContentsString ();
                    }
                }
                return cachedDescription;
            }
            set {
                if (!string.Equals (value, cachedDescription)) {
                    cachedDescription = value;
                    descriptionWasChanged = true;
                }
            }
        }
        private string cachedDescription = null;
        private bool descriptionWasChanged = false;

        // Commit any changes to the item's description to the database.  This should be called within a transaction.
        private void UpdateDescription ()
        {
            // This should happen within a transaction.  But it doesn't yet.
            // NcAssert.True (NcModel.Instance.IsInTransaction ());

            if (!descriptionWasChanged || null == cachedDescription) {
                return;
            }
            if (0 == BodyId) {
                // No existing body.  Create one.
                McBody body = McBody.InsertFile (AccountId, cachedDescription);
                BodyId = body.Id;
                BodyType = McBody.PlainText;
            } else {
                // Existing body.  We can't replace just the description, leaving
                // the attachments untouched.  So replace the entire body, which will
                // unfortunately destroy the attachments.
                var body = McBody.QueryById<McBody> (BodyId);
                body.UpdateData (cachedDescription);
                BodyType = McBody.PlainText;
            }
            descriptionWasChanged = false;
            cachedDescription = null;
        }

        public McAbstrCalendarRoot () : base ()
        {
            DbAttendees = new List<McAttendee> ();
            DbCategories = new List<McCalendarCategory> ();
            HasReadAncillaryData = false;
        }

        [Ignore]
        public List<McAttachment> attachments {
            get {
                if (null == cachedAttachments) {
                    // Retrieve all the attachments that are stored in the database.
                    // This includes both attachments that were created locally (with ContentId == null)
                    // and ones that came from the server in a previous synch (with ContentId != null)
                    var dbAttachments = McAttachment.QueryByItemId (this);
                    // Retrieve all the attachments that are in the body of event.
                    var bodyAttachments = MimeHelpers.AllAttachments (MimeHelpers.LoadMessage (McBody.GetFilePath (BodyId)));
                    // The majority of the time, there will be no attachments. Optimize this case.
                    if (0 == dbAttachments.Count && 0 == bodyAttachments.Count) {
                        cachedAttachments = new List<McAttachment> ();
                    } else {
                        // Synchronize the two lists.  Local attachments are left alone.
                        // Server attachments are paired up, with attachments from the event body
                        // taking precedence.
                        // The matching algorithm is O(n^2), but the number of attachments is normally
                        // small enough that this shouldn't be a problem.
                        var synchedAttachments = new List<McAttachment> (bodyAttachments.Count);
                        foreach (var bodyAttachment in bodyAttachments) {
                            NcAssert.True (bodyAttachment is MimePart && null != bodyAttachment.ContentDisposition,
                                "MimeHelpers.AllAttachments() returned something that doesn't look like an attachment.");
                            bool foundMatch = false;
                            for (int i = 0; !foundMatch && i < dbAttachments.Count; ++i) {
                                // If the attachment from the MIME body has a ContentId, match on that, since it is
                                // supposed to be a unique identifier.  If the ContentId field is missing, match on
                                // the file name and hope there aren't any duplicates.
                                if (null != bodyAttachment.ContentId ?
                                        string.Equals (bodyAttachment.ContentId, dbAttachments [i].ContentId) :
                                        string.Equals (bodyAttachment.ContentDisposition.FileName, dbAttachments [i].DisplayName)) {
                                    synchedAttachments.Add (dbAttachments [i]);
                                    dbAttachments.RemoveAt (i);
                                    foundMatch = true;
                                }
                            }
                            if (!foundMatch) {
                                var newAttachment = new McAttachment () {
                                    AccountId = this.AccountId,
                                    ItemId = this.Id,
                                    ClassCode = this.GetClassCode (),
                                    ContentId = bodyAttachment.ContentId ?? "faked@content.id",
                                    ContentType = bodyAttachment.ContentType.ToString (),
                                    IsInline = !bodyAttachment.ContentDisposition.IsAttachment,
                                };
                                newAttachment.Insert ();
                                newAttachment.SetDisplayName (bodyAttachment.ContentDisposition.FileName);
                                newAttachment.UpdateData ((FileStream stream) => {
                                    (bodyAttachment as MimePart).ContentObject.DecodeTo (stream);
                                });
                                synchedAttachments.Add (newAttachment);
                            }
                        }
                        // Anything left in dbAttachments that has a ContentId originally came from
                        // the server, but is no longer part of the event.  Delete it.
                        foreach (var unmatchedAttachment in dbAttachments) {
                            if (null == unmatchedAttachment.ContentId) {
                                synchedAttachments.Add (unmatchedAttachment);
                            } else {
                                unmatchedAttachment.Delete ();
                            }
                        }
                        cachedAttachments = synchedAttachments;
                    }
                }
                return cachedAttachments;
            }
            set {
                NcAssert.True (null != value);
                attachmentsMightHaveChanged = true;
                cachedAttachments = value;
            }
        }

        private List<McAttachment> cachedAttachments = null;
        private bool attachmentsMightHaveChanged = false;

        private void UpdateAttachments ()
        {
            if (!attachmentsMightHaveChanged) {
                return;
            }
            // Make sure all the attachments are owned by this event. Identify the ones that were
            // created locally rather than coming from the server.
            var localAttachments = new List<McAttachment> (cachedAttachments.Count);
            foreach (var attachment in cachedAttachments) {
                if (attachment.AccountId != this.AccountId ||
                        (0 != attachment.ItemId && attachment.ItemId != this.Id) ||
                        (0 != (int)attachment.ClassCode && attachment.ClassCode != this.GetClassCode ())) {
                    // The attachment is owed by something else.  Make a copy.
                    var copy = new McAttachment () {
                        AccountId = this.AccountId,
                        ItemId = this.Id,
                        ClassCode = this.GetClassCode (),
                        ContentId = null, // This is a local copy, so it shouldn't have a ContentId
                        ContentType = attachment.ContentType,
                    };
                    copy.SetDisplayName (attachment.DisplayName);
                    copy.Insert ();
                    copy.UpdateFileCopy (attachment.GetFilePath ());
                    localAttachments.Add (copy);
                } else if (0 == attachment.ItemId) {
                    // The attachment is not owned by anything yet.
                    attachment.ItemId = this.Id;
                    attachment.ClassCode = this.GetClassCode ();
                    attachment.Update ();
                    localAttachments.Add (attachment);
                } else {
                    NcAssert.True (attachment.ItemId == this.Id && attachment.ClassCode == this.GetClassCode ());
                    if (null == attachment.ContentId) {
                        localAttachments.Add (attachment);
                    }
                }
            }

            // Retrieve all the attachments in the database that are owned by this event.
            // Separate out the ones that came from the server.  Delete from the database
            // any locally created ones that the user removed from the event.
            var serverAttachments = new List<McAttachment> ();
            var dbAttachments = McAttachment.QueryByItemId (this);
            foreach (var dbAttachment in dbAttachments) {
                if (null == dbAttachment.ContentId) {
                    // Locally created.  See if it is still needed.
                    bool foundMatch = false;
                    foreach (var attachment in localAttachments) {
                        if (dbAttachment.Id == attachment.Id) {
                            foundMatch = true;
                            break;
                        }
                    }
                    if (!foundMatch) {
                        dbAttachment.Delete ();
                    }
                } else {
                    serverAttachments.Add (dbAttachment);
                }
            }
            // Merge the local and server attachments together.
            localAttachments.AddRange (serverAttachments);
            cachedAttachments = localAttachments;
            attachmentsMightHaveChanged = false;
        }

        /// Delete from the database any attachments owned by this event.
        private void DeleteAttachments ()
        {
            if (0 != this.Id) {
                var attachments = McAttachment.QueryByItemId (this);
                foreach (var attachment in attachments) {
                    attachment.Delete ();
                }
            }
        }

        [Ignore]
        public List<McAttendee> attendees {
            get {
                ReadAncillaryData ();
                return DbAttendees;
            }
            set {
                ReadAncillaryData ();
                DbAttendees = value;
            }
        }

        [Ignore]
        public List<McCalendarCategory> categories {
            get {
                ReadAncillaryData ();
                return DbCategories;
            }
            set {
                ReadAncillaryData ();
                DbCategories = value;
            }
        }

        private NcResult ReadAncillaryData ()
        {
            if (HasReadAncillaryData) {
                return NcResult.OK ();
            }
            HasReadAncillaryData = true;
            SQLiteConnection db = NcModel.Instance.Db;
            var attendeeParentType = McAttendee.GetParentType (this);
            DbAttendees = db.Table<McAttendee> ().Where (x => (x.ParentId == Id) && (x.ParentType == attendeeParentType)).ToList ();
            var categoryParentType = McCalendarCategory.GetParentType (this);
            DbCategories = db.Table<McCalendarCategory> ().Where (x => (x.ParentId == Id) && (x.ParentType == categoryParentType)).ToList ();
            // TODO: Deal with errors
            return NcResult.OK ();
        }

        public override int Insert ()
        {
            // FIXME db transaction.
            UpdateDescription (); // Must be called before base.Insert()
            int retval = base.Insert ();
            InsertAncillaryData ();
            return retval;
        }

        public override int Update ()
        {
            // FIXME db transaction
            UpdateDescription (); // Must be called before base.Update()
            int retval = base.Update ();
            UpdateAncillaryData (NcModel.Instance.Db);
            return retval;
        }

        private NcResult InsertAncillaryData ()
        {
            NcAssert.True (0 < Id);
            foreach (var a in attendees) {
                a.Id = 0;
                a.SetParent (this);
                a.Insert ();
            }
            foreach (var c in categories) {
                c.Id = 0;
                c.SetParent (this);
                c.Insert ();
            }
            UpdateAttachments ();
            return NcResult.OK ();
        }

        private void UpdateAncillaryData (SQLiteConnection db)
        {
            ReadAncillaryData ();
            DeleteAncillaryDataFromDB (db);
            InsertAncillaryData ();
        }

        public override void DeleteAncillary ()
        {
            NcAssert.True (NcModel.Instance.IsInTransaction ());
            base.DeleteAncillary ();
            DeleteAncillaryDataFromDB (NcModel.Instance.Db);
            DeleteAttachments ();
        }

        private NcResult DeleteAncillaryDataFromDB (SQLiteConnection db)
        {
            var attendeeParentType = McAttendee.GetParentType (this);
            var attendees = db.Table<McAttendee> ().Where (x => (x.ParentId == Id) && (x.ParentType == attendeeParentType)).ToList ();
            foreach (var a in attendees) {
                a.Delete ();
            }
            var categoryParentType = McAttendee.GetParentType (this);
            var categories = db.Table<McCalendarCategory> ().Where (x => (x.ParentId == Id) && (x.ParentType == categoryParentType)).ToList ();
            foreach (var c in categories) {
                c.Delete ();
            }
            return NcResult.OK ();
        }

    }

    public enum NcBusyStatus
    {
        Free = 0,
        Tentative = 1,
        Busy = 2,
        OutOfOffice = 3,
    }

    public enum NcSensitivity
    {
        Normal = 0,
        Personal = 1,
        Private = 2,
        Confidential = 3,
    }

    public enum NcMeetingStatus
    {
        /// No attendees
        Appointment = 0,
        /// The user is the meeting organizer
        Meeting = 1,
        /// The meeting was recieved from someone else
        ForwardedMeeting = 3,
        /// The user is the cancelled meeting's organizer
        MeetingCancelled = 5,
        /// The cancelled meeting was recieved from someone else
        ForwardedMeetingCancelled = 7,
    }
    // Similar to NcResponseType
    public enum NcAttendeeStatus
    {
        /// The user's response is unknown
        ResponseUnknown = 0,
        /// The user is unsure about attending
        Tentative = 2,
        /// The user has accepted the meeting
        Accept = 3,
        /// The user has decloned the meeting
        Decline = 4,
        /// The user has not responded
        NotResponded = 5,
    }

    /// Similar to NcAttendeeStatus
    public enum NcResponseType
    {
        /// The user's response to the meeting has not yet been received.
        None = 0,
        /// The current user is the organizer of the meeting and, therefore, no reply is required.
        Organizer = 1,
        /// TThe user is unsure whether he or she will attend.
        Tentative = 2,
        /// The user has accepted the meeting request.
        Accepted = 3,
        /// The user has declined the meeting request.
        Declined = 4,
        /// The user has not yet responded to the meeting request.
        NotResponded = 5,
    }

    public enum NcAttendeeType
    {
        Unknown = 0,
        Required = 1,
        Optional = 2,
        Resource = 3,
    }

    public enum NcRecurrenceType
    {
        Daily = 0,
        Weekly = 1,
        Monthly = 2,
        MonthlyOnDay = 3,
        Yearly = 5,
        YearlyOnDay = 6,
    }

    public enum NcDayOfWeek
    {
        Sunday = 1,
        Monday = 2,
        Tuesday = 4,
        Wednesday = 8,
        Thursday = 16,
        Friday = 32,
        Weekdays = 62,
        Saturday = 64,
        WeekendDays = 65,
        LastDayOfTheMonth = 127,
        /// special value in monthly or yearly recurrences
    }

    public enum NcFirstDayOfWeek
    {
        Sunday = 0,
        Monday = 1,
        Tuesday = 2,
        Wednesday = 3,
        Thursday = 4,
        Friday = 5,
        Saturday = 6,
    }

    public enum NcCalendarType
    {
        Default = 0,
        Gregorian = 1,
        GregorianUnitedStates = 2,
        JapaneseEmperorEra = 3,
        Taiwan = 4,
        KoreanTangunEra = 5,
        HijriArabicLunar = 6,
        Thai = 7,
        HebrewLunar = 8,
        GregorianMiddleEastFrench = 9,
        GregorianArabic = 10,
        GregorianTransliteratedEnglish = 11,
        GregorianTransliteratedFrench = 12,
        ReservedMustNotBeUsed = 13,
        JapaneseLunar = 14,
        ChineseLunar = 15,
        SakaEraReservedMustNotBeUsed = 16,
        ChineseLunarEtoReservedMustNotbeUsed = 17,
        KoreanLunarEtoReservedMustNotBeUsed = 18,
        JapaneseRokuyouLunarReservedMustNotBeUsed = 19,
        KoreanLunar = 20,
        ReservedMustNotBeUsed_21 = 21,
        ReservedmustNotBeUsed_22 = 22,
        UmalQuraReservedMustNotBeUsed = 23,
    }
}

