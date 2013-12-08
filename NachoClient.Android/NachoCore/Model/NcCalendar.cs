using SQLite;
using System;
using System.Collections.Generic;

namespace NachoCore.Model
{
    /// <summary>
    /// Root of calendar and exception items.
    /// Table of attendees, refer back to entry they are attending (1 unique set per entry)
    /// Table of categories, referring back to entry they categorize (1 unique set per entry)
    /// Table of exceptions, referring back to entry they are modifying (1 unique set per entry)
    /// Table of timezones, referred to by the calendar entry    /// </summary>
    public partial class NcCalendarRoot : NcItem
    {
        /// Item runs for the entire day
        public bool AllDayEvent { get; set; }

        /// When this item was created or modified (Compact DateTime, optional)
        public DateTime DTStamp { get; set; }

        /// Start time of this item (Compact DateTime)
        public DateTime StartTime { get; set; }

        /// End time of this item (Compact DateTime, optional)
        public DateTime EndTime { get; set; }

        /// Number of minutes before start time to display a message (optional)
        public uint Reminder { get; set; }

        /// TZ of the calendar item.  Calendar only.
        /// Foreign key to TimeZone table.
        public int TimeZoneId { get; set; }

        /// Subject of then calendar or exception item
        public string Subject { get; set; }

        /// Location of the event (optional)
        [MaxLength (256)]
        public string Location { get; set; }

        /// Recommended privacy policy for this item (optional)
        public NcSensitivity Sensitivity { get; set; }

        /// Busy status of the meeting organizer (optional)
        public NcBusyStatus BusyStatus { get; set; }

        /// None, Organizer, Tentative, ...
        public NcResponseType ResponseType;

        /// Status of the meeting (optional)
        public NcMeetingStatus MeetingStatus { get; set; }

        /// The time this user responded to the request
        public DateTime AppointmentReplyTime { get; set; }

        /// The GRUU for the UAC
        public string OnlineMeetingConfLink { get; set; }

        /// The URL for the online meeting
        public string OnlineMeetingExternalLink { get; set; }

        /// Index of Body container
        public int BodyId { get; set; }

        /// Implicit [Ignore]
        public List<NcAttendee> attendees;
        /// Implicit [Ignore]
        public List<NcCategory> categories;
    }

    public partial class NcCalendar : NcCalendarRoot
    {
        /// Implicit [Ignore]
        public List<NcException> exceptions;
        /// Implicit [Ignore]
        public List<NcRecurrence> recurrences;

        /// Is a response to this meeting required? Calendar only.
        public bool ResponseRequested { get; set; }

        /// The default is False.  Calendar only
        public bool DisallowNewTimeProposal { get; set; }

        /// Name of the creator of the calendar item (optional). Calendar only.
        [MaxLength (256)]
        public string OrganizerName { get; set; }

        /// Email of the creator of the calendar item (optional). Calendar only.
        [MaxLength (256)]
        public string OrganizerEmail { get; set; }

        /// Unique 300 digit hexidecimal ID generated by the client. Calendar only.
        [MaxLength (300)]
        public string UID { get; set; }

        /// How is the body stored on the server?  Calendar only.
        public int NativeBodyType { get; set; }
    }

    public partial class NcException : NcCalendarRoot
    {
        [Indexed]
        public Int64 CalendarId { get; set; }

        /// Has this exception been deleted?  Exception only.
        public uint Deleted { get; set; }

        /// Start time of the original recurring meeting (Compact DateTime). Exception only.
        public DateTime ExceptionStartTime { get; set; }
    }
    // The attendee table is a big old list of non-unique names.
    // Each attendee record refers back to its Calendar record or
    // exception record.
    public partial class NcAttendee : NcObject
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

        /// Unknown, tentative, accept, ...
        public NcAttendeeStatus AttendeeStatus { get; set; }
    }

    /// The category table represents a collection of categories
    /// assigned to a calendar or exception item.
    public partial class NcCategory : NcObject
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

    public partial class NcRecurrence : NcObject
    {
        /// Recurrence.  Calendar only.

        [Indexed]
        public Int64 CalendarId { get; set; }

        ///The following fields define the
        /// Recurrence pattern of an event.

        public NcRecurrenceType Type { get; set; }

        /// Maximum is 999
        public int Occurences { get; set; }

        /// Interval between recurrences, range is 0 to 999
        public int Interval { get; set; }

        /// The week of the month or the day of the month for the recurrence
        /// WeekOfMonth must be between 1 and 5; 5 is the last week of the month.
        public int WeekOfMonth { get; set; }

        public NcDayOfWeek DayOfWeek { get; set; }

        /// The month of the year for the recurrence, range is 1..12
        public int MonthOfYear { get; set; }

        /// Compact DateTime
        public DateTime Until { get; set; }

        /// The day of the month for the recurrence, range 1..31
        public int DayOfMonth { get; set; }

        public NcCalendarType CalendarType { get; set; }

        /// Takes place on the embolismic (leap) month
        public bool isLeapMonth { get; set; }

        /// Disambiguates recurrences across localities
        public int FirstDayOfWeek { get; set; }
    }

    public class NcTimeZone : NcObject
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

        public NcTimeZone ()
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

    public partial class NcCalendar
    {
    }

    public partial class NcAttendee
    {
        public NcAttendee ()
        {
            Id = 0;
            ParentId = 0;
            ParentType = 0;
            Name = null;
            Email = null;
            AttendeeType = NcAttendeeType.Unknown;
            AttendeeStatus = NcAttendeeStatus.NotResponded;
        }

        public NcAttendee (string name, string email, NcAttendeeType type = NcAttendeeType.Unknown, NcAttendeeStatus status = NcAttendeeStatus.NotResponded)
        {
            Id = 0;
            ParentId = 0;
            ParentType = 0;
            Name = name;
            Email = email;
            AttendeeType = type;
            AttendeeStatus = status;
        }

        public static int GetParentType (NcCalendarRoot r)
        {
            if (r.GetType () == typeof(NcCalendar)) {
                return CALENDAR;
            } else if (r.GetType () == typeof(NcException)) {
                return EXCEPTION;
            } else {
                System.Diagnostics.Trace.Assert (false);
                return 0;
            }
        }

        public void SetParent (NcCalendarRoot r)
        {
            ParentId = r.Id;
            ParentType = GetParentType (r);
        }
    }

    public partial class NcCategory
    {
        public NcCategory ()
        {
            Id = 0;
            ParentId = 0;
            ParentType = 0;
            Name = null;
        }

        public NcCategory (string name) : this ()
        {
            Name = name;
        }

        public static int GetParentType (NcCalendarRoot r)
        {
            if (r.GetType () == typeof(NcCalendar)) {
                return CALENDAR;
            } else if (r.GetType () == typeof(NcException)) {
                return EXCEPTION;
            } else {
                System.Diagnostics.Trace.Assert (false);
                return 0;
            }
        }

        public void SetParent (NcCalendarRoot r)
        {
            ParentId = r.Id;
            ParentType = GetParentType (r);
        }
    }
}


