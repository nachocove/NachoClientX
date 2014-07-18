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

        // Specifies the original format type of the item
        public int NativeBodyType { get; set; }

        [Ignore]
        private List<McAttendee> DbAttendees { get; set; }

        [Ignore]
        private List<McCalendarCategory> DbCategories { get; set; }

        private Boolean HasReadAncillaryData;

        public McAbstrCalendarRoot () : base ()
        {
            DbAttendees = new List<McAttendee> ();
            DbCategories = new List<McCalendarCategory> ();
            HasReadAncillaryData = false;
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
            NcResult result = NcResult.OK ();
            if (!HasReadAncillaryData) {
                result = ForceReadAncillaryData ();
                if (result.isOK ()) {
                    HasReadAncillaryData = true;
                } else {
                    Log.Warn (Log.LOG_CALENDAR, "Fail to read calendar ancillary data (Id={0})", Id);
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
}

