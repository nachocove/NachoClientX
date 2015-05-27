using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;
using MimeKit;
using System.IO;
using NachoCore.ActiveSync;

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

        public bool ReminderIsSet { get; set; }

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

        /// Specifies the original format type of the item
        public int NativeBodyType { get; set; }

        public virtual string GetSubject ()
        {
            return Subject;
        }

        public virtual string GetLocation ()
        {
            return Location;
        }

        public virtual bool HasReminder ()
        {
            return ReminderIsSet;
        }

        public virtual uint GetReminder ()
        {
            return HasReminder () ? Reminder : 0;
        }

        public virtual bool HasResponseType ()
        {
            return ResponseTypeIsSet;
        }

        public virtual NcResponseType GetResponseType ()
        {
            return HasResponseType () ? ResponseType : NcResponseType.None;
        }

        /// <summary>
        /// Return the UID for this event.  This method must be overridden by derived classes.
        /// </summary>
        public virtual string GetUID ()
        {
            return "";
        }

        // Attendees that are stored in the database.
        private List<McAttendee> dbAttendees = null;
        // Attendees that were set by the app, either UI or sync.  They don't get saved to the database
        // until Insert() or Update() is called.
        private IList<McAttendee> appAttendees = null;

        [Ignore]
        public virtual IList<McAttendee> attendees {
            get {
                return GetAncillaryCollection (appAttendees, ref dbAttendees, ReadDbAttendees);
            }
            set {
                NcAssert.NotNull (value, "To clear the attendees, use an empty list instead of null.");
                appAttendees = value;
            }
        }

        private List<McAttendee> ReadDbAttendees ()
        {
            var attendeeParentType = McAttendee.GetParentType (this);
            return NcModel.Instance.Db.Table<McAttendee> ()
                .Where (x => x.ParentId == this.Id && x.ParentType == attendeeParentType).ToList ();
        }

        private void DeleteDbAttendees ()
        {
            DeleteAncillaryCollection (ref dbAttendees, ReadDbAttendees);
        }

        private void SaveAttendees ()
        {
            var attendeeParentType = McAttendee.GetParentType (this);
            SaveAncillaryCollection (ref appAttendees, ref dbAttendees, ReadDbAttendees, (McAttendee attendee) => {
                attendee.SetParent (this);
            }, (McAttendee attendee) => {
                return attendee.ParentId == this.Id && attendee.ParentType == attendeeParentType;
            });
        }

        private void InsertAttendees ()
        {
            InsertAncillaryCollection (ref appAttendees, ref dbAttendees, (McAttendee attendee) => {
                attendee.SetParent (this);
            });
        }

        // Categories that are stored in the database.
        private List<McCalendarCategory> dbCategories = null;
        // Categories that were set by the app, either UI or sync.  They don't get saved to the database
        // until Insert() or Update() is called.
        private IList<McCalendarCategory> appCategories = null;

        [Ignore]
        public virtual IList<McCalendarCategory> categories {
            get {
                return GetAncillaryCollection (appCategories, ref dbCategories, ReadDbCategories);
            }
            set {
                NcAssert.NotNull (value, "To clear the categories, use an empty list instead of null.");
                appCategories = value;
            }
        }

        private List<McCalendarCategory> ReadDbCategories ()
        {
            var categoryParentType = McCalendarCategory.GetParentType (this);
            return NcModel.Instance.Db.Table<McCalendarCategory> ()
                .Where (x => x.ParentId == this.Id && x.ParentType == categoryParentType).ToList ();
        }

        private void DeleteDbCategories ()
        {
            DeleteAncillaryCollection (ref dbCategories, ReadDbCategories);
        }

        private void SaveCategories ()
        {
            SaveAncillaryCollection (ref appCategories, ref dbCategories, ReadDbCategories, (McCalendarCategory category) => {
                category.SetParent (this);
            }, (McCalendarCategory category) => {
                var categoryParentType = McCalendarCategory.GetParentType (this);
                return category.ParentId == this.Id && category.ParentType == categoryParentType;
            });
        }

        private void InsertCategories ()
        {
            InsertAncillaryCollection (ref appCategories, ref dbCategories, (McCalendarCategory category) => {
                category.SetParent (this);
            });
        }

        [Ignore]
        public string Description {
            get {
                GetDescription ();
                return cachedDescription;
            }
        }

        [Ignore]
        public McAbstrFileDesc.BodyTypeEnum DescriptionType {
            get {
                GetDescription ();
                return cachedDescriptionType;
            }
        }

        public void SetDescription (string description, McAbstrFileDesc.BodyTypeEnum type)
        {
            if (description != cachedDescription || type != cachedDescriptionType) {
                cachedDescription = description;
                cachedDescriptionType = type;
                descriptionWasChanged = true;
            }
        }

        private string cachedDescription = null;
        private McAbstrFileDesc.BodyTypeEnum cachedDescriptionType = McAbstrFileDesc.BodyTypeEnum.None;
        private bool descriptionWasChanged = false;

        private void GetDescription ()
        {
            if (null == cachedDescription) {
                if (0 == BodyId) {
                    cachedDescription = "";
                    cachedDescriptionType = McAbstrFileDesc.BodyTypeEnum.None;
                } else {
                    McBody body = McBody.QueryById<McBody> (BodyId);
                    switch (body.BodyType) {
                    case McAbstrFileDesc.BodyTypeEnum.HTML_2:
                        cachedDescription = body.GetContentsString ();
                        cachedDescriptionType = McAbstrFileDesc.BodyTypeEnum.HTML_2;
                        break;
                    case McAbstrFileDesc.BodyTypeEnum.RTF_3:
                        cachedDescription = AsHelpers.Base64CompressedRtfToNormalRtf (body.GetContentsString ());
                        cachedDescriptionType = McAbstrFileDesc.BodyTypeEnum.RTF_3;
                        break;
                    case McAbstrFileDesc.BodyTypeEnum.MIME_4:
                        if (!MimeHelpers.FindTextWithType (
                                MimeHelpers.LoadMessage (body), out cachedDescription, out cachedDescriptionType,
                                McAbstrFileDesc.BodyTypeEnum.PlainText_1, McAbstrFileDesc.BodyTypeEnum.HTML_2,
                                McAbstrFileDesc.BodyTypeEnum.RTF_3)) {
                            // Couldn't find anything in the message.  Set the description to empty plain text.
                            cachedDescription = "";
                            cachedDescriptionType = McAbstrFileDesc.BodyTypeEnum.PlainText_1;
                        }
                        break;
                    default:
                        cachedDescription = body.GetContentsString ();
                        cachedDescriptionType = McAbstrFileDesc.BodyTypeEnum.PlainText_1;
                        break;
                    }
                }
            }
            if (null == cachedDescription) {
                Log.Error (Log.LOG_CALENDAR, "McAbstrCalendarRoot.GetDescription() completed without setting cachedDescription.");
                cachedDescription = "";
                cachedDescriptionType = McAbstrFileDesc.BodyTypeEnum.None;
            }
        }

        private void UpdateDescription ()
        {
            if (!descriptionWasChanged || null == cachedDescription) {
                return;
            }
            if (0 == BodyId) {
                // No existing body.  Create one.
                var body = McBody.InsertFile(AccountId, cachedDescriptionType, cachedDescription);
                BodyId = body.Id;
            } else {
                // Existing body.  We can't replace just the description, leaving
                // the attachments untouched.  So replace the entire body, which will
                // unfortunately destroy the attachments.
                var body = McBody.QueryById<McBody> (BodyId);
                body.UpdateData (cachedDescription);
                body.BodyType = cachedDescriptionType;
                body.Update ();
            }
            descriptionWasChanged = false;
            cachedDescription = null;
        }

        private List<McAttachment> dbAttachments = null;
        private IList<McAttachment> appAttachments = null;
        private List<McAttachment> cachedServerAttachments = null;

        [Ignore]
        public virtual IList<McAttachment> attachments {
            get {
                return GetAncillaryCollection (appAttachments, ref dbAttachments, ReadDbAttachments);
            }
            set {
                NcAssert.NotNull (value);
                appAttachments = value;
            }
        }

        private List<McAttachment> ReadDbAttachments ()
        {
            return McAttachment.QueryByItemId (this);
        }

        private void DeleteDbAttachments ()
        {
            DeleteAncillaryCollection (ref dbAttachments, ReadDbAttachments);
        }

        private void SaveAttachments ()
        {
            if (null == appAttachments && null == cachedServerAttachments) {
                // Nothing to save.
                return;
            }
            if (null != cachedServerAttachments) {
                foreach (var attachment in this.attachments) {
                    if (null == attachment.ContentId) {
                        cachedServerAttachments.Add (attachment);
                    }
                }
                appAttachments = cachedServerAttachments;
                cachedServerAttachments = null;
            }
            // Take ownership of any attachments that are unowned or owned by a different item.
            var cleanAttachments = new List<McAttachment>(appAttachments.Count);
            foreach (var attachment in appAttachments) {
                McAttachment cleanAttachment;
                if (0 == attachment.ItemId) {
                    // The attachment isn't owned by an item yet.  Claim it.
                    attachment.AccountId = this.AccountId;
                    attachment.ItemId = this.Id;
                    attachment.ClassCode = this.GetClassCode ();
                    attachment.Update ();
                    cleanAttachment = attachment;
                } else if (attachment.AccountId != this.AccountId || attachment.ItemId != this.Id || attachment.ClassCode != this.GetClassCode()) {
                    // The attachment is already owned by another item.  Make a copy.
                    var copy = new McAttachment () {
                        AccountId = this.AccountId,
                        ItemId = this.Id,
                        ClassCode = this.GetClassCode (),
                        ContentId = attachment.ContentId,
                        ContentType = attachment.ContentType,
                    };
                    copy.Insert ();
                    copy.SetDisplayName (attachment.DisplayName);
                    copy.UpdateFileCopy (attachment.GetFilePath ());
                    copy.Update ();
                    cleanAttachment = copy;
                } else {
                    // Already owned by this item.  Nothing to do.
                    cleanAttachment = attachment;
                }
                cleanAttachments.Add (cleanAttachment);
            }
            appAttachments = cleanAttachments;
            SaveAncillaryCollection (ref appAttachments, ref dbAttachments, ReadDbAttachments, (McAttachment attachment) => {
                NcAssert.True (false);
            }, (McAttachment attachment) => {
                return attachment.ItemId == this.Id && attachment.ClassCode == this.GetClassCode ();
            });
        }

        private void InsertAttachments ()
        {
            SaveAttachments ();
        }

        public void SetServerAttachments (MimeMessage message)
        {
            var attachmentEntities = MimeHelpers.AllAttachments (message);
            cachedServerAttachments = new List<McAttachment> (attachmentEntities.Count);
            foreach (var attachmentEntity in attachmentEntities) {
                var serverAttachment = new McAttachment () {
                    AccountId = this.AccountId,
                    ItemId = 0, // Will be set later
                    ContentId = attachmentEntity.ContentId ?? "fake@content.id",
                    ContentType = attachmentEntity.ContentType.ToString (),
                    IsInline = !attachmentEntity.ContentDisposition.IsAttachment,
                };
                // McAttachments need to be in the database before the content can be set.
                serverAttachment.Insert();
                serverAttachment.SetDisplayName (attachmentEntity.ContentDisposition.FileName);
                var mimePart = (MimePart)attachmentEntity;
                if (null == mimePart.ContentObject) {
                    // I don't know what causes this to happen, but a customer encountered this situation.
                    Log.Warn (Log.LOG_CALENDAR, "Event attachment {0} does not have any content.",
                        attachmentEntity.ContentDisposition.FileName);
                    serverAttachment.UpdateData ("");
                } else {
                    serverAttachment.UpdateData ((FileStream stream) => {
                        mimePart.ContentObject.DecodeTo (stream);
                    });
                }
                cachedServerAttachments.Add (serverAttachment);
            }
        }

        public override int Insert ()
        {
            int retval = 0;
            NcModel.Instance.RunInTransaction (() => {
                UpdateDescription (); // Must be called before base.Insert()
                retval = base.Insert ();
                InsertAttendees ();
                InsertCategories ();
                InsertAttachments ();
            });
            return retval;
        }

        public override int Update ()
        {
            int retval = 0;
            NcModel.Instance.RunInTransaction (() => {
                UpdateDescription (); // Must be called before base.Update()
                retval = base.Update ();
                SaveAttendees ();
                SaveCategories ();
                SaveAttachments ();
            });
            return retval;
        }

        public override void DeleteAncillary ()
        {
            base.DeleteAncillary ();
            DeleteDbAttendees ();
            DeleteDbCategories ();
            DeleteDbAttachments ();
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

