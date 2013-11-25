using SQLite;
using System;
using  System.Globalization;

namespace NachoCore.Model
{
    public class NcTimeZone
    {
        public long Bias; // The offset from UTC, in minutes;
        public string StandardName; // Optional TZ description as an array of 32 WCHARs
        public System.DateTime StandardDate; // When the transition from DST to standard time occurs
        public long StandardBias; // Number of minutes to add to Bias during standard time
        public string DaylightName; // Optional DST description as an array of 32 WCHARs
        public System.DateTime DaylightDate; // When the transition from standard time to DST occurs
        public long DaylightBias; // Number of miniutes to add to Bias during DST
    }

    public enum NcBusyStatus {
        Free = 0,
        Tentative = 1,
        Busy = 2,
        OutOfOffice = 3,
    }

    public enum NcSensitivity {
        Normal = 0,
        Personal = 1,
        Private = 2,
        Confidential = 3,
    }

    public enum NcMeetingStatus {
        Appointment = 0, // No attendees
        Meeting = 1, // The user is the meeting organizer
        ForwardedMeeting = 3, // The meeting was recieved from someone else
        MeetingCancelled = 5, // The user is the cancelled meeting's organizer
        ForwardedMeetingCancelled = 7, // The cancelled meeting was recieved from someone else
    }

    public enum NcAttendeeStatus {
        ResponseUnkonwn = 0,
        Tentative = 2,
        Accept = 3,
        Decline = 4,
        NotResponded = 5,
    }

    public enum NcAttendeeType {
        Required = 1,
        Optional = 2,
        Resource = 3,
    }

    public class NcAttendee
    {
        public string Email;
        public string Name;
        public NcAttendeeStatus AttendeeStatus;
        public NcAttendeeType AttendeeType;
    }

    public enum NcRecurrenceType {
        Daily = 0,
        Weekly = 1,
        Monthly = 2,
        MonthlyOnDay = 3,
        Yearly = 5,
        YearlyOnDay = 6,
    }

    public enum NcDayOfWeek {
        Sunday = 1,
        Monday = 2,
        Tuesday = 4,
        Wednesday = 8,
        Thursday = 16,
        Friday = 32,
        Weekdays = 62,
        Saturday = 64,
        WeekendDays = 65,
        LastDayOfTheMonth = 127, // special value in monthly or yearly recurrences
    }

    public enum NcFirstDayOfWeek {
        Sunday = 0,
        Monday = 1,
        Tuesday = 2,
        Wednesday = 3,
        Thursday = 4,
        Friday = 5,
        Saturday = 6,
    }

    public enum NcCalendarType {
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

    // WeekOfMonth must be between 1 and 5; 5 is the last week of the month.

    public class NcRecurrence
    {
        public NcRecurrenceType Type;
        public int Occurences; // Maximum is 999
        public int Interval; // Interval between recurrences, range is 0 to 999
        public int WeekOfMonth; // The week of the month or the day of the month for the recurrence
        public DayOfWeek DayOfWeek;
        public int MonthOfYear; // The month of the year for the recurrence, range is 1..12
        public string Until; // Compact DateTime
        public int DayOfMonth; // The day of the month for the recurrence, range 1..31
        public NcCalendarType CalendarType;
        public bool isLeapMonth; // Takes place on the embolismic (leap) month
        public int FirstDayOfWeek; // Disambiguates recurrences across localities
    }

    public enum NcResponseType {
        None = 0, // The user's response has not been received
        Organizer = 1, // The  user is the organizer; no reply is required
        Tentative = 2, // The user is unsure about attending
        Accepted = 3, // The user has accepted the meeting
        Declined = 4, // The user has declined the meeting
        NotResponded = 5, // The user has not responded
    }

    public class NcException {
        public uint Deleted;
        public string ExceptionStartTime; // Start time of the original recurring meeting (Compact DateTime)
        public string Subject;
        public string StartTime; // The start time of the calendar item exception (Compact DateTime)
        public string EndTime; // The end time of the calendar item exception (Compact DateTime)
        public string Body;
        public string Location;
        public string[] Categories;
        public NcSensitivity Sensitivity;
        public NcBusyStatus BusyStatus;
        public bool AllDayEvent;
        public uint Reminder;
        public string DTStamp; // (Compact DateTime)
        public NcMeetingStatus MeetingStatus;
        public NcAttendee[] Attendees;
        public string AppointmentReplyTime; // When the user responded to the meeting request exception (Compact DateTime)
        public NcResponseType ResponseType;
        public string OnlineMeetingConfLink;
        public string OnlineMeetingExternalLink;
    }

    // Compact DateTime represents a UTC data and time as a string.
    // date_string = year month day "T" hour minute seconds [milliseconds]

    public class NcEvent : NcItem
    {
        public const string ClassName = "NcEvent";

        public NcTimeZone TimeZone; // TZ of the calendar item
        public bool AllDayEvent; // Item or exception runs for the entire day
        public NcBusyStatus BusyStatus; // Busy status of the meeting organizer (optional)
        public string OrganizerName; // Name of the creator of the calendar item (optional)
        public string OrganizerEmail; // Email of the creator of the calendar item (optional)
        public string DTStamp; // When this item was created or modified (Compact DateTime, optional)
        public string EndTime; // End time of this item (Compact DateTime, optional)
        public string Location; // Location of the event (optional)
        public uint Reminder; // Number of minutes before start time to display a message (optional)
        public NcSensitivity Sensitivity; // Recommended privacy policy for this item (optional)
        public string Subject; // Subect of then calendar or exception item
        public string StartTime; // Start time of this item (Compact DateTime)
        public string UID; // Unique 300 digit hexidecimal ID generated by the client
        public NcMeetingStatus MeetingStatus; // Status of the meeting (optional)
        public NcAttendee[] Attendees;
        public string[] Categories;
        public NcRecurrence Recurrence;
        public NcException[] Exceptions;


        // A Compact DateTime value is a representation of a UTC date and time.
        // The format of a Compact DateTime value is specified by the following
        // Augmented Backus-Naur Form (ABNF) notation.
        // ￼
        // date_string = year month day "T" hour minute seconds [milliseconds] "Z"
        // year = 4*DIGIT
        // month = ("0" DIGIT) / "10" / "11" / "12"
        // day = ("0" DIGIT) / ("1" DIGIT) / ("2" DIGIT ) / "30" / "31"
        // hour = ("0" DIGIT) / ("1" DIGIT) / "20" / "21" / "22" / "23"
        // minute = ("0" DIGIT) / ("1" DIGIT) / ("2" DIGIT) / ("3" DIGIT) / ("4" DIGIT) / ("5" DIGIT)
        // seconds = ("0" DIGIT) / ("1" DIGIT) / ("2" DIGIT) / ("3" DIGIT) / ("4" DIGIT) / ("5" DIGIT)
        // milliseconds  = 1*3DIGIT
        // 
        // E.g. 20131123T190243Z
        //
        public DateTime ParseCompactDateTime(string compactDateTime)
        {
            DateTime dateValue;
            const string fmt1 = "yyyyMMddTHHmmssZ";
            const string fmt2 = "yyyyMMddTHHmmssfffZ";
            DateTimeStyles styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;

            if (DateTime.TryParseExact (compactDateTime, fmt1, CultureInfo.InvariantCulture, styles, out dateValue )) {
                return dateValue;
            }
            if (DateTime.TryParseExact (compactDateTime, fmt2, CultureInfo.InvariantCulture, styles, out dateValue )) {
                return dateValue;
            }
            return DateTime.MinValue;
        }

        public NcTimeZone DecodeTimeZone(string encodedTimeZone)
        {
            // Convert the Base64 UUEncoded input into binary output. 
            byte[] binaryData;
            try {
                binaryData = System.Convert.FromBase64String(encodedTimeZone);
            }
            catch (System.ArgumentNullException) {
                System.Console.WriteLine("Encoded TimeZone string is null.");
                return null;
            }
            catch (System.FormatException) {
                System.Console.WriteLine("Encoded TimeZone string length is not 4 or is not an even multiple of 4." );
                return null;
            }
            if (binaryData.Length != (4 + 64 + 16 + 4 + 64 + 16 + 4)) {
                System.Console.WriteLine ("Decoded TimeZone string length is wrong: " + binaryData.Length.ToString ());
                return null;
            }
            string StandardName = ExtractStringFromTimeZone (binaryData, 4, 64);
            string DaylightName = ExtractStringFromTimeZone (binaryData, 4 + 64 + 16 + 4, 64);
            NcTimeZone tz = new NcTimeZone ();
            tz.StandardName = StandardName;
            tz.DaylightName = DaylightName;
            return tz;     
        }

        // The value of this field is an array of 32 WCHARs
        // It contains an optional description for standard time.
        // Any unused WCHARs in the array MUST be set to 0x0000.
        public string ExtractStringFromTimeZone(byte[] binaryData, int start, int fieldLength)
        {
            System.Diagnostics.Debug.Assert ((start + fieldLength) <= binaryData.Length);
            String field = System.Text.UnicodeEncoding.Unicode.GetString (binaryData, start, fieldLength);
            int index = field.IndexOf (System.Convert.ToChar(0)); // trim trailing null padding
            if (index < 0) { // no nulls
                return field;
            } else {
                return field.Substring (0, index);
            }
        }
    }

}

