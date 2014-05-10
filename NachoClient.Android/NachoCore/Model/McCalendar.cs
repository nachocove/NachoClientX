using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoCore.Model
{
    /// <summary>
    /// Root of calendar and exception items.
    /// Table of attendees, refer back to entry they are attending (1 unique set per entry)
    /// Table of categories, referring back to entry they categorize (1 unique set per entry)
    /// Table of exceptions, referring back to entry they are modifying (1 unique set per entry)
    /// Table of timezones, referred to by the calendar entry    /// </summary>
    public partial class McCalendarRoot : McItem
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

        /// None, Organizer, Tentative, ...
        public NcResponseType ResponseType;

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

        [Ignore]
        private List<McAttendee> DbAttendees { get; set; }
        [Ignore]
        private List<McCalendarCategory> DbCategories { get; set; }
    }

    public partial class McCalendar : McCalendarRoot
    {
        /// Implicit [Ignore]
        private List<McException> DbExceptions;
        /// Implicit [Ignore]
        private List<McRecurrence> DbRecurrences;

        /// Is a response to this meeting required? Calendar only.
        public bool ResponseRequested { get; set; }

        public bool ResponseRequestedIsSet { get; set; }

        /// The default is False.  Calendar only
        public bool DisallowNewTimeProposal { get; set; }

        public bool DisallowNewTimeProposalIsSet { get; set; }

        /// Name of the creator of the calendar item (optional). Calendar only.
        [MaxLength (256)]
        public string OrganizerName { get; set; }

        /// Email of the creator of the calendar item (optional). Calendar only.
        [MaxLength (256)]
        public string OrganizerEmail { get; set; }

        /// Unique 300 digit hexidecimal ID generated by the client. Calendar only.
        [MaxLength (300)]
        public string UID { get; set; }

        public static ClassCodeEnum GetClassCode ()
        {
            return McFolderEntry.ClassCodeEnum.Calendar;
        }
    }

    /// <summary>
    /// List of exceptions associated with the calendar entry
    /// </summary>
    public partial class McException : McCalendarRoot
    {
        [Indexed]
        public Int64 CalendarId { get; set; }

        /// Has this exception been deleted?  Exception only.
        public uint Deleted { get; set; }

        /// Start time of the original recurring meeting (Compact DateTime). Exception only.
        public DateTime ExceptionStartTime { get; set; }
    }

    /// <summary>
    /// The attendee table is a big old list of non-unique names.
    /// Each attendee record refers back to its Calendar record or
    /// exception record.
    /// </summary>
    public partial class McAttendee : McObject
    {
        /// Parent Calendar or Exception item index.
        [Indexed]
        public Int64 ParentId { get; set; }

        public static int CALENDAR = 1;
        public static int EXCEPTION = 2;

        /// Which table has the parent?
        public int ParentType { get; set; }

        /// Email address of attendee
        [MaxLength (256)]
        public string Email { get; set; }

        /// Display name of attendee
        [MaxLength (256)]
        public string Name { get; set; }

        /// Required, optional, resource
        public NcAttendeeType AttendeeType { get; set; }

        public bool AttendeeTypeIsSet { get; set; }

        /// Unknown, tentative, accept, ...
        public NcAttendeeStatus AttendeeStatus { get; set; }

        public bool AttendeeStatusIsSet { get; set; }
    }

    /// <summary>
    /// The category table represents a collection of categories
    /// assigned to a calendar or exception item.
    /// </summary>
    public partial class McCalendarCategory : McObject
    {
        /// Parent Calendar or Exception item index.
        [Indexed]
        public Int64 ParentId { get; set; }

        public static int CALENDAR = 1;
        public static int EXCEPTION = 2;
        // Which table has the parent?
        public int ParentType { get; set; }

        /// Name of category
        [MaxLength (256)]
        public string Name { get; set; }
    }


    public class McTimeZone : McObject
    {
        /// The offset from UTC, in minutes;
        public int Bias { get; set; }

        /// Optional TZ description as an array of 32 WCHARs
        public string StandardName { get; set; }

        /// When the transition from DST to standard time occurs
        public System.DateTime StandardDate { get; set; }

        /// Number of minutes to add to Bias during standard time
        public int StandardBias { get; set; }

        /// Optional DST description as an array of 32 WCHARs
        public string DaylightName { get; set; }

        /// When the transition from standard time to DST occurs
        public System.DateTime DaylightDate { get; set; }

        /// Number of miniutes to add to Bias during DST
        public int DaylightBias { get; set; }

        public McTimeZone ()
        {
            LastModified = DateTime.UtcNow;
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
        Appointment = 0,
        /// No attendees
        Meeting = 1,
        /// The user is the meeting organizer
        ForwardedMeeting = 3,
        /// The meeting was recieved from someone else
        MeetingCancelled = 5,
        /// The user is the cancelled meeting's organizer
        ForwardedMeetingCancelled = 7,
        /// The cancelled meeting was recieved from someone else
    }
    // Similar to NcResponseType
    public enum NcAttendeeStatus
    {
        ResponseUnknown = 0,
        /// The user's response is unknown
        Tentative = 2,
        /// The user is unsure about attending
        Accept = 3,
        /// The user has accepted the meeting
        Decline = 4,
        /// The user has decloned the meeting
        NotResponded = 5,
        /// The user has not responded
    }

    /// Similar to NcAttendeeStatus
    public enum NcResponseType
    {
        None = 0,
        /// The user's response has not been received
        Organizer = 1,
        /// The  user is the organizer; no reply is required
        Tentative = 2,
        /// The user is unsure about attending
        Accepted = 3,
        /// The user has accepted the meeting
        Declined = 4,
        /// The user has declined the meeting
        NotResponded = 5,
        /// The user has not responded
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

    public partial class McCalendarRoot
    {
        private Boolean HasReadAncillaryData;

        public McCalendarRoot () : base ()
        {
            DbAttendees = new List<McAttendee> ();
            DbCategories = new List<McCalendarCategory> ();
            HasReadAncillaryData = false;
        }

        [Ignore]
        public List<McAttendee> attendees
        {
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
        public List<McCalendarCategory> categories
        {
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
            NcResult result = NcResult.OK ();
            if (!HasReadAncillaryData) {
                result = ForceReadAncillaryData ();
                if (result.isOK()) {
                    HasReadAncillaryData = true;
                } else {
                    Log.Warn ("Fail to read calendar ancillary data (Id={0})", Id);
                }
            }
            return result;
        }

        private NcResult ForceReadAncillaryData ()
        {
            SQLiteConnection db = NcModel.Instance.Db;
            // FIXME: Parent types
            DbAttendees = db.Table<McAttendee> ().Where (x => x.ParentId == Id).ToList ();
            // FIXME: Parent types
            DbCategories = db.Table<McCalendarCategory> ().Where (x => x.ParentId == Id).ToList ();
            return NcResult.OK ();
        }
    }

    public partial class McCalendar
    {
        protected bool HasReadAncillaryData;

        public McCalendar () : base ()
        {
            HasReadAncillaryData = false;
            DbExceptions = new List<McException> ();
            DbRecurrences = new List<McRecurrence> ();
        }

        [Ignore]
        public List<McException> exceptions
        {
            get {
                ReadAncillaryData ();
                return DbExceptions;
            }
            set {
                ReadAncillaryData ();
                DbExceptions = value;
            }
        }

        [Ignore]
        public List<McRecurrence> recurrences
        {
            get {
                ReadAncillaryData ();
                return DbRecurrences;
            }
            set {
                ReadAncillaryData ();
                DbRecurrences = value;
            }
        }
            
        private NcResult ReadAncillaryData ()
        {
            if (!HasReadAncillaryData) {
                HasReadAncillaryData = true;
                return ForceReadAncillaryData ();
            }
            return NcResult.OK ();
        }

        public NcResult ForceReadAncillaryData ()
        {
            HasReadAncillaryData = true;
            DbExceptions = NcModel.Instance.Db.Table<McException> ().Where (x => x.CalendarId == Id).ToList ();
            DbRecurrences = NcModel.Instance.Db.Table<McRecurrence> ().Where (x => x.CalendarId == Id).ToList ();
            return NcResult.OK ();
        }

        public NcResult InsertAncillaryData (SQLiteConnection db)
        {
            NachoCore.NachoAssert.True (0 < Id);

            // TODO: Fix this hammer?
            DeleteAncillaryData (db);

            foreach (var a in attendees) {
                a.SetParent (this);
                db.Insert (a);
            }
            foreach (var c in categories) {
                c.SetParent (this);
                db.Insert (c);
            }

            // TODO: Exceptions and recurrences

            // FIXME: Error handling
            return NcResult.OK ();
        }

        public override int Insert ()
        {
            // FIXME db transaction.
            int retval = base.Insert ();
            InsertAncillaryData (NcModel.Instance.Db);
            return retval;
        }

        public override int Update ()
        {
            int retval = base.Update ();
            InsertAncillaryData (NcModel.Instance.Db);
            return retval;
        }

        public override int Delete ()
        {
            int retval = base.Delete ();
            DeleteAncillaryData (NcModel.Instance.Db);
            return retval;
        }

        private NcResult DeleteAncillaryData (SQLiteConnection db)
        {
            // FIXME: Parent types
            var attendees = db.Table<McAttendee> ().Where (x => x.ParentId == Id).ToList ();
            foreach (var a in attendees) {
                a.Delete ();
            }
            // FIXME: Parent types
            categories = db.Table<McCalendarCategory> ().Where (x => x.ParentId == Id).ToList ();
            foreach (var c in categories) {
                c.Delete ();
            }

            // TODO: Support exceptions and recurrences

            // TODO: Add error processing
            return NcResult.OK ();
        }
    }

    public partial class McAttendee
    {
        public McAttendee ()
        {
            Id = 0;
            ParentId = 0;
            ParentType = 0;
            Name = null;
            Email = null;
            AttendeeType = NcAttendeeType.Unknown;
            AttendeeStatus = NcAttendeeStatus.NotResponded;
        }

        public McAttendee (string name, string email, NcAttendeeType type = NcAttendeeType.Unknown, NcAttendeeStatus status = NcAttendeeStatus.NotResponded)
        {
            Id = 0;
            ParentId = 0;
            ParentType = 0;
            Name = name;
            Email = email;
            AttendeeType = type;
            AttendeeTypeIsSet = (NcAttendeeType.Unknown != type);
            AttendeeStatus = status;
            AttendeeStatusIsSet = (NcAttendeeStatus.NotResponded != status);
        }

        public static int GetParentType (McCalendarRoot r)
        {
            if (r.GetType () == typeof(McCalendar)) {
                return CALENDAR;
            } else if (r.GetType () == typeof(McException)) {
                return EXCEPTION;
            } else {
                NachoCore.NachoAssert.True (false);
                return 0;
            }
        }

        public void SetParent (McCalendarRoot r)
        {
            ParentId = r.Id;
            ParentType = GetParentType (r);
        }

        private string displayName;

        [Ignore]
        /// <summary>
        /// Gets the display name.
        /// </summary>
        /// <value>The display name is calculated unless set non-null.</value>
        public string DisplayName {
            get {
                if (!String.IsNullOrEmpty (displayName)) {
                    return displayName;
                }
                if (!String.IsNullOrEmpty (Name)) {
                    return Name;
                }
                if (!String.IsNullOrEmpty (Email)) {
                    return Email;
                }
                NachoCore.NachoAssert.CaseError ();
                return "";
            }
            protected set {
                displayName = value;
            }
        }
    }

    public partial class McCalendarCategory
    {
        public McCalendarCategory ()
        {
            Id = 0;
            ParentId = 0;
            ParentType = 0;
            Name = null;
        }

        public McCalendarCategory (string name) : this ()
        {
            Name = name;
        }

        public static int GetParentType (McCalendarRoot r)
        {
            if (r.GetType () == typeof(McCalendar)) {
                return CALENDAR;
            } else if (r.GetType () == typeof(McException)) {
                return EXCEPTION;
            } else {
                NachoCore.NachoAssert.True (false);
                return 0;
            }
        }

        public void SetParent (McCalendarRoot r)
        {
            ParentId = r.Id;
            ParentType = GetParentType (r);
        }
    }
}


