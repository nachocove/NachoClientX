//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Model;
using MimeKit;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using NachoPlatform;

namespace NachoCore.Utils
{
    public class Pretty
    {
        public Pretty ()
        {
        }

        #region Message Subject & Preview

        /// <summary>
        /// Subject of a message or calendar event.
        /// </summary>
        static public string SubjectString (String Subject)
        {
            if (null == Subject) {
                return "";
            } else {
                return Subject;
            }
        }

        static public string NoSubjectString ()
        {
            return "No subject";
        }

        static public string PreviewString (String preview)
        {
            if (null == preview) {
                return "";
            } else {
                return ConsecutiveWhitespace.Replace (preview, " ");
            }
        }

        static Regex SymbolRun = new Regex ("[~`\\-\\+=_#<>\\*\\/]{4,}");
        static Regex ConsecutiveWhitespace = new Regex ("\\s+");

        static public string MessagePreview (McEmailMessage message, out int subjectLength, int maxSubjectLength = 50)
        {
            string subject = "";
            if (message.Subject != null) {
                subject = message.Subject.Trim ();
            }
            if (maxSubjectLength > 0) {
                int flex = 10;
                if (subject.Length > maxSubjectLength) {
                    int index = maxSubjectLength;
                    bool foundSpace = false;
                    while (index < subject.Length && index < maxSubjectLength + flex) {
                        var c = subject [index];
                        if (c == ' ') {
                            foundSpace = true;
                            break;
                        } else {
                            ++index;
                        }
                    }
                    if (index != subject.Length) {
                        if (!foundSpace) {
                            index = maxSubjectLength;
                            while (index >= 0 && index > maxSubjectLength - flex) {
                                var c = subject [index];
                                if (c == ' ') {
                                    foundSpace = true;
                                    break;
                                } else {
                                    --index;
                                }
                            }
                        }
                        if (!foundSpace) {
                            index = maxSubjectLength;
                        }
                        subject = subject.Substring (0, index) + "...";
                    }
                }
            }
            string bodyPreview = "";
            if (message.BodyPreview != null) {
                var reader = new System.IO.StringReader (message.BodyPreview);
                var line = reader.ReadLine ();
                while (line != null) {
                    if (EmailHelper.IsQuoteLine (line) || line.StartsWith (">")) {
                        line = null;
                    } else {
                        bodyPreview += line + " ";
                        line = reader.ReadLine ();
                    }
                }
                bodyPreview = EmailHelper.AdjustPreviewText (bodyPreview).Trim ();
                bodyPreview = SymbolRun.Replace (bodyPreview, " ");
                bodyPreview = ConsecutiveWhitespace.Replace (bodyPreview, " ");
            }
            subjectLength = subject.Length;
            if (subject != "" && bodyPreview != "") {
                return subject + "  ·  " + bodyPreview;
            }
            if (subject != "") {
                return subject;
            }
            return bodyPreview;
        }

        #endregion

        #region Durations

        /// <summary>
        /// Calendar event duration, 0h0m style.
        /// Returns an empty string for a zero length time span.
        /// </summary>
        static public string CompactDuration (IStrings strings, DateTime StartTime, DateTime EndTime)
        {
            if (StartTime == EndTime) {
                return "";
            }
            TimeSpan s = EndTime - StartTime;
            if (s.TotalMinutes < 60) {
                return String.Format (strings.CompactMinutesFormat, s.Minutes);
            }
            if (s.TotalHours < 24) {
                if (0 == s.Minutes) {
                    return String.Format (strings.CompactHoursFormat, s.Hours);
                } else {
                    return String.Format (strings.CompactHourMinutesFormat, s.Hours, s.Minutes);
                }
            }
            return strings.CompactDayPlus;
        }

        #endregion

        #region Reminders

        /// <summary>
        /// String for a reminder, in minutes.
        /// </summary>
        static public string ReminderString (NachoPlatform.IStrings strings, bool reminderIsSet, uint reminder)
        {
            if (!reminderIsSet) {
                return strings.ReminderNone;
            }
            if (0 == reminder) {
                return strings.ReminderAtEvent;
            }
            if (1 == reminder) {
                return strings.ReminderOneMinute;
            }
            if (60 == reminder) {
                return strings.ReminderOneHour;
            }
            if ((24 * 60) == reminder) {
                return strings.ReminderOneDay;
            }
            if ((7 * 24 * 60) == reminder) {
                return strings.ReminderOneWeek;
            }
            if (0 == (reminder % (7 * 24 * 60))) {
                return String.Format (strings.ReminderWeeksFormat, reminder / (7 * 24 * 60));
            }
            if (0 == (reminder % (24 * 60))) {
                return String.Format (strings.ReminderDaysFormat, reminder / (24 * 60));
            }
            if (0 == (reminder % 60)) {
                return String.Format (strings.ReminderHoursFormat, reminder / 60);
            }
            return String.Format (strings.ReminderMinutesFormat, reminder);
        }

        // TODO: i18n
        static public string ReminderDate (DateTime utcDueDate)
        {
            var duration = System.DateTime.UtcNow - utcDueDate;
            if (180 < Math.Abs (duration.Days)) {
                return DateTimeFormatter.Instance.AbbreviatedDateWithYear (utcDueDate);
            } else {
                return DateTimeFormatter.Instance.AbbreviatedDateTime (utcDueDate);
            }
        }

        #endregion

        #region Special Dates

        /// <summary>
        /// Display a date that may or may not have a year associated with it, and for which the
        /// day of the week is not important. For example, "August 27" or "August 27, 2000".
        /// iOS uses a date in 1604 when the year doesn't matter, so leave off the year when the
        /// date is earlier than 1700.
        /// </summary>
        static public string BirthdayOrAnniversary (IDateTimeFormatter formatter, DateTime d)
        {
            if (d.Year < 1700) {
                return formatter.Date (d);
            } else {
                return formatter.DateWithYear (d);
            }
        }

        static public string FriendlyFullDateTime (DateTime dateTime)
        {
            var local = dateTime.ToLocalTime ();
            var now = DateTime.Now;
            var diff = now - local;
            var timeString = DateTimeFormatter.Instance.MinutePrecisionTime (dateTime);
            if (diff < now.TimeOfDay) {
                return String.Format (Strings.Instance.FriendlyDateTimeTodayFormat, timeString);
            } else if (diff < (now.TimeOfDay + TimeSpan.FromDays (1))) {
                return String.Format (Strings.Instance.FriendlyDateTimeYesterdayFormat, timeString);
            } else if (diff < (now.TimeOfDay + TimeSpan.FromDays (6))) {
                return String.Format (Strings.Instance.FriendlyDateTimeOtherFormat, DateTimeFormatter.Instance.WeekdayName (dateTime), timeString);
            }
            return String.Format (Strings.Instance.FriendlyDateTimeOtherFormat, DateTimeFormatter.Instance.DateWithWeekdayAndYearExceptPresent (dateTime), timeString);
        }

        static public string TimeWithDecreasingPrecision (DateTime dateTime)
        {
            var local = dateTime.ToLocalTime ();
            var now = DateTime.Now;
            var diff = now - local;
            TimeSpan cutoff;
            if (now.Hour < 12) {
                cutoff = now.TimeOfDay + TimeSpan.FromHours (12);
            } else {
                cutoff = now.TimeOfDay;
            }
            if (diff < cutoff) {
                return DateTimeFormatter.Instance.MinutePrecisionTime (dateTime);
            }
            if (diff < now.TimeOfDay + TimeSpan.FromDays (1)) {
                return Strings.Instance.DecreasingPrecisionTimeYesterday;
            }
            if (diff < TimeSpan.FromDays (6) + now.TimeOfDay) {
                return DateTimeFormatter.Instance.WeekdayName (dateTime);
            }
            return DateTimeFormatter.Instance.ShortNumericDateWithShortYear (dateTime);
        }

        static public string FutureDate (DateTime dateTime, bool timeMatters)
        {
            var local = dateTime.ToLocalTime ();
            var localStartOfDay = local - local.TimeOfDay;
            var now = DateTime.Now;
            if (now.Year == local.Year && now.Month == local.Month && now.Day == local.Day) {
                if (timeMatters) {
                    return DateTimeFormatter.Instance.MinutePrecisionTimeExceptZero (local);
                }
                return Strings.Instance.FutureDateToday;
            }
            if (now < localStartOfDay) {
                if (now + TimeSpan.FromDays (1) > localStartOfDay) {
                    return Strings.Instance.FutureDateTomorrow;
                }
                if (now + TimeSpan.FromDays (6) > localStartOfDay) {
                    return DateTimeFormatter.Instance.WeekdayName (dateTime);
                }
            } else {
                if (local + TimeSpan.FromDays (1) > now - now.TimeOfDay) {
                    return Strings.Instance.FutureDateYesterday;
                }
            }
            return DateTimeFormatter.Instance.ShortNumericDateWithShortYear (dateTime);
        }

        static public string VariableDayTime (DateTime dateTime)
        {
            var local = dateTime.ToLocalTime ();
            var now = DateTime.Now;
            var diff = now - local;
            if (diff < now.TimeOfDay) {
                return DateTimeFormatter.Instance.MinutePrecisionTime (dateTime);
            }
            if (diff < now.TimeOfDay + TimeSpan.FromDays (1)) {
                return String.Format (Strings.Instance.VariableDateTimeYesterdayFormat, DateTimeFormatter.Instance.MinutePrecisionTime (dateTime));
            }
            if (diff < TimeSpan.FromDays (6) + now.TimeOfDay) {
                return string.Format (Strings.Instance.VariableDateTimeOtherFormat, DateTimeFormatter.Instance.WeekdayName (dateTime), DateTimeFormatter.Instance.MinutePrecisionTime (dateTime));
            }
            return DateTimeFormatter.Instance.AbbreviatedDateTimeWithWeekdayAndYearExceptPresent (dateTime);
        }

        static public string UniversalFullDateTime (DateTime d)
        {
            return d.ToLocalTime ().ToString ("U");
        }

        #endregion

        #region Events

        static public string EventTime (DateTime dateTime, out TimeSpan validSpan)
        {
            var local = dateTime.ToLocalTime ();
            var now = DateTime.Now;
            var diff = local - now;

            if (diff < TimeSpan.FromSeconds (30)) {
                validSpan = TimeSpan.FromSeconds (-2);
                return Strings.Instance.EventTimeNow;
            }

            if (diff < TimeSpan.FromMinutes (1)) {
                validSpan = TimeSpan.FromSeconds (30);
                return Strings.Instance.EventTimeOneMinute;
            }

            if (diff < TimeSpan.FromMinutes (15)) {
                var minutes = (int)Math.Ceiling (diff.TotalMinutes);
                validSpan = (local - TimeSpan.FromMinutes (minutes - 1)) - now;
                return String.Format (Strings.Instance.EventTimeMinutesFormat, minutes);
            }

            if (diff <= TimeSpan.FromMinutes (60)) {
                var minutes = diff.TotalMinutes;
                var fiveMinuteBlocks = minutes / 5;
                var remainderMinutes = minutes % 5;
                var roundedMinutes = (int)Math.Floor (fiveMinuteBlocks) * 5;
                if (roundedMinutes == 15) {
                    validSpan = TimeSpan.FromMinutes (remainderMinutes + 1);
                } else {
                    validSpan = TimeSpan.FromMinutes (remainderMinutes + 2.5);
                }
                if (remainderMinutes > 2.5) {
                    roundedMinutes = (int)Math.Ceiling (fiveMinuteBlocks) * 5;
                    validSpan = TimeSpan.FromMinutes (remainderMinutes - 2.5);
                }
                return String.Format (Strings.Instance.EventTimeMinutesFormat, roundedMinutes);
            }

            validSpan = (local - TimeSpan.FromHours (1)) - now;
            TimeSpan cutoff;
            if (now.Hour < 12) {
                cutoff = now.TimeOfDay + TimeSpan.FromHours (12);
            } else {
                cutoff = now.TimeOfDay;
            }
            if (diff < cutoff) {
                return string.Format (Strings.Instance.EventTimeAtFormat, DateTimeFormatter.Instance.MinutePrecisionTime (dateTime));
            }
            return string.Format (Strings.Instance.EventTimeDateFormat, DateTimeFormatter.Instance.WeekdayName (dateTime), DateTimeFormatter.Instance.MinutePrecisionTime (dateTime));
        }

        static public string EventDay (DateTime dateTime, out TimeSpan validSpan)
        {
            var local = dateTime.ToLocalTime ();
            var reference = DateTime.Now;
            var tomorrow = reference.AddDays (1);
            tomorrow = tomorrow - tomorrow.TimeOfDay;
            validSpan = tomorrow - reference;
            if (local < reference) {
                return Strings.Instance.EventDayToday;
            }
            reference = reference.AddDays (1);
            if (local < reference) {
                return Strings.Instance.EventDayTomorrow;
            }
            return DateTimeFormatter.Instance.WeekdayName (local);
        }

        static public string EventDetailTime (McEvent calendarEvent)
        {
            var start = calendarEvent.StartTime;
            var end = calendarEvent.EndTime;
            if (calendarEvent.AllDayEvent) {
                start = new DateTime (start.Year, start.Month, start.Day, start.Hour, start.Minute, start.Second, DateTimeKind.Local);
                end = new DateTime (end.Year, end.Month, end.Day, end.Hour, end.Minute, end.Second, DateTimeKind.Local);
            }
            var lines = new List<string> ();
            var day = DateTimeFormatter.Instance.DateWithWeekdayAndYearExceptPresent (start);
            lines.Add (day);
            if (calendarEvent.AllDayEvent) {
                var span = (end - start);
                if (span > TimeSpan.FromDays (1)) {
                    var endDay = DateTimeFormatter.Instance.DateWithWeekdayAndYearExceptPresent (end);
                    lines.Add (String.Format (Strings.Instance.EventDetailTimeThroughFormat, endDay));
                }
            } else {
                if (start.ToLocalTime ().Date == end.ToLocalTime ().Date) {
                    lines.Add (string.Format (Strings.Instance.EventDetailTimeToFormat, DateTimeFormatter.Instance.MinutePrecisionTimeExceptZero (start), DateTimeFormatter.Instance.MinutePrecisionTimeExceptZero (end)));
                } else {
                    lines.Add (string.Format (Strings.Instance.EventDetailTimeToFormat, DateTimeFormatter.Instance.MinutePrecisionTimeExceptZero (start), DateTimeFormatter.Instance.AbbreviatedDateTimeWithWeekdayAndYearExceptPresent (end)));
                }
            }
            var recurrences = calendarEvent.QueryRecurrences ();
            if (recurrences.Count > 0) {
                lines.Add (MakeRecurrenceString (recurrences));
            }
            return String.Join ("\n", lines);
        }

        static public string MeetingRequestTime (McMeetingRequest meetingRequest)
        {
            var start = meetingRequest.StartTime;
            var end = meetingRequest.EndTime;
            var formatter = DateTimeFormatter.Instance;
            if (meetingRequest.AllDayEvent) {
                start = new DateTime (start.Year, start.Month, start.Day, start.Hour, start.Minute, start.Second, DateTimeKind.Local);
                end = new DateTime (end.Year, end.Month, end.Day, end.Hour, end.Minute, end.Second, DateTimeKind.Local);
            }
            var startString = DateTimeFormatter.Instance.DateWithWeekdayAndYearExceptPresent (start);
            string line;
            if (meetingRequest.AllDayEvent) {
                var span = (end - start);
                if (span > TimeSpan.FromDays (1)) {
                    var endString = DateTimeFormatter.Instance.DateWithWeekdayAndYearExceptPresent (end);
                    line = String.Format (Strings.Instance.MeetingRequestTimeAllDayFormat, startString, endString);
                } else {
                    line = startString;
                }
            } else {
                if (start.ToLocalTime ().Date == end.ToLocalTime ().Date) {
                    if ((start.ToLocalTime ().Hour < 12 && end.ToLocalTime ().Hour < 12) || (start.ToLocalTime ().Hour >= 12 && end.ToLocalTime ().Hour >= 12)) {
                        line = string.Format (Strings.Instance.MeetingRequestTimeSameHalfDayFormat, startString, formatter.MinutePrecisionTimeExceptZeroWithoutAmPm (start), formatter.MinutePrecisionTimeExceptZero (end));
                    } else {
                        line = string.Format (Strings.Instance.MeetingRequestTimeSameDayFormat, startString, DateTimeFormatter.Instance.MinutePrecisionTimeExceptZero (start), DateTimeFormatter.Instance.MinutePrecisionTimeExceptZero (end));
                    }
                } else {
                    line = string.Format (Strings.Instance.MeetingRequestTimeMultiDayFormat, startString, DateTimeFormatter.Instance.MinutePrecisionTimeExceptZero (start), DateTimeFormatter.Instance.AbbreviatedDateTimeWithWeekdayAndYearExceptPresent (end));
                }
            }
            var recurrences = meetingRequest.recurrences;
            if (recurrences.Count > 0) {
                line += "\n" + MakeRecurrenceString (recurrences);
            }
            return line;
        }

        static public string EventEditTime (DateTime date, bool isAllDay, bool isEnd)
        {
            if (isAllDay) {
                date = new DateTime (date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, DateTimeKind.Local);
                if (isEnd) {
                    date = date.AddDays (-1.0);
                }
                return DateTimeFormatter.Instance.DateWithWeekdayAndYearExceptPresent (date);
            }
            return DateTimeFormatter.Instance.DateTimeWithWeekdayAndYearExceptPresent (date);
        }

        public static void EventNotification (McEvent ev, out string title, out string body)
        {
            var calendarItem = ev.CalendarItem;
            if (null == calendarItem) {
                title = Strings.Instance.EventNotificationDefaultTitle;
            } else {
                title = Pretty.SubjectString (calendarItem.GetSubject ());
            }
            if (ev.AllDayEvent) {
                body = string.Format (Strings.Instance.EventNotificationAllDayTimeFormat, DateTimeFormatter.Instance.AbbreviatedDateWithWeekdayAndYearExceptPresent (ev.GetStartTimeLocal ()));
            } else {
                body = string.Format (Strings.Instance.EventNotificationTimeFormat, DateTimeFormatter.Instance.MinutePrecisionTime (ev.GetStartTimeLocal ()), DateTimeFormatter.Instance.MinutePrecisionTime (ev.GetEndTimeLocal ()));
            }
            if (null != calendarItem) {
                var location = calendarItem.GetLocation ();
                if (!string.IsNullOrEmpty (location)) {
                    body += ": " + location;
                }
            }
        }

        public static string AttendeeStatus (McAttendee attendee)
        {
            var tokens = new List<string> ();
            if (attendee.AttendeeTypeIsSet) {
                switch (attendee.AttendeeType) {
                case NcAttendeeType.Optional:
                    tokens.Add (Strings.Instance.AttendeeStatusOptional);
                    break;
                case NcAttendeeType.Required:
                    tokens.Add (Strings.Instance.AttendeeStatusRequired);
                    break;
                case NcAttendeeType.Resource:
                    tokens.Add (Strings.Instance.AttendeeStatusResource);
                    break;
                }
            }
            if (attendee.AttendeeStatusIsSet) {
                switch (attendee.AttendeeStatus) {
                case NcAttendeeStatus.Accept:
                    tokens.Add (Strings.Instance.AttendeeStatusAccepted);
                    break;
                case NcAttendeeStatus.Decline:
                    tokens.Add (Strings.Instance.AttendeeStatusDeclined);
                    break;
                case NcAttendeeStatus.Tentative:
                    tokens.Add (Strings.Instance.AttendeeStatusTentative);
                    break;
                case NcAttendeeStatus.NotResponded:
                    tokens.Add (Strings.Instance.AttendeeStatusNoResponse);
                    break;
                }
            }
            return String.Join (", ", tokens);
        }

        public static string MeetingResponse (McEmailMessage message)
        {
            string messageFormat;
            switch (message.MeetingResponseValue) {
            case NcResponseType.Accepted:
                messageFormat = Strings.Instance.MeetingResponseAcceptedFormat;
                break;
            case NcResponseType.Tentative:
                messageFormat = Strings.Instance.MeetingResponseTentativeFormat;
                break;
            case NcResponseType.Declined:
                messageFormat = Strings.Instance.MeetingResponseDeclinedFormat;
                break;
            default:
                Log.Warn (Log.LOG_CALENDAR, "Unknown meeting response status: {0}", message.MessageClass);
                messageFormat = Strings.Instance.MeetingResponseUnknownFormat;
                break;
            }

            string displayName;
            var responder = NcEmailAddress.ParseMailboxAddressString (message.From);
            if (null == responder) {
                displayName = message.From;
            } else if (!string.IsNullOrEmpty (responder.Name)) {
                displayName = responder.Name;
            } else {
                displayName = responder.Address;
            }

            return String.Format (messageFormat, displayName);
        }

        #endregion

        #region Messages

        /// <summary>
        /// Given an email address, return a string
        /// worthy of being displayed in the message list.
        /// </summary>
        static public string SenderString (string Sender)
        {
            if (null == Sender) {
                return "";
            }
            InternetAddress address;
            if (false == MailboxAddress.TryParse (Sender, out address)) {
                return Sender;
            }
            if (String.IsNullOrEmpty (address.Name)) {
                return Sender;
            } else {
                return address.Name;
            }
        }

        static public string ShortSenderString (string sender)
        {
            var longSenderString = SenderString (sender).Trim ();
            var senderString = longSenderString;
            var spaceIndex = senderString.IndexOf (' ');
            if (spaceIndex > 0) {
                senderString = senderString.Substring (0, spaceIndex);
            }
            var atIndex = senderString.IndexOf ('@');
            if (atIndex > 0) { // don't cut if @ is at the start
                senderString = senderString.Substring (0, atIndex);
            }
            if (!String.IsNullOrEmpty (senderString)) {
                return senderString;
            }
            return longSenderString;
        }

        static public string RecipientString (string Recipient)
        {
            if (String.IsNullOrWhiteSpace (Recipient)) {
                return Strings.Instance.NoRecipientsFallback;
            }
            InternetAddressList addresses;
            if (false == InternetAddressList.TryParse (Recipient, out addresses)) {
                return Recipient;
            }
            var names = new List<string> ();
            foreach (var mailbox in addresses.Mailboxes) {
                if (String.IsNullOrEmpty (mailbox.Name)) {
                    names.Add (mailbox.Address);
                } else {
                    names.Add (mailbox.Name);
                }
            }
            if (names.Count > 0) {
                return String.Join (", ", names);
            }
            return Recipient;
        }

        /// <summary>
        /// Given "From" (ex: "Steve Scalpone" <steves@nachocove.com>), 
        /// return a string containing just the address.
        /// </summary>
        static public string EmailString (string Sender)
        {
            if (null == Sender) {
                return "";
            }
            MailboxAddress address;
            if (false == MailboxAddress.TryParse (Sender, out address)) {
                return Sender;
            }
            if (String.IsNullOrEmpty (address.Name)) {
                return Sender;
            } else {
                return address.Address;
            }
        }

        #endregion

        #region Recurrences

        // TODO: l10n
        // Use DateTimeFormatter rather than C# l10n for consistency
        protected static string DayOfWeekAsString (NcDayOfWeek dow)
        {
            var fullNames = CultureInfo.CurrentCulture.DateTimeFormat.DayNames;

            switch (dow) {

            case NcDayOfWeek.Sunday:
                return fullNames [(int)DayOfWeek.Sunday];

            case NcDayOfWeek.Monday:
                return fullNames [(int)DayOfWeek.Monday];

            case NcDayOfWeek.Tuesday:
                return fullNames [(int)DayOfWeek.Tuesday];

            case NcDayOfWeek.Wednesday:
                return fullNames [(int)DayOfWeek.Wednesday];

            case NcDayOfWeek.Thursday:
                return fullNames [(int)DayOfWeek.Thursday];

            case NcDayOfWeek.Friday:
                return fullNames [(int)DayOfWeek.Friday];

            case NcDayOfWeek.Saturday:
                return fullNames [(int)DayOfWeek.Saturday];

            default:
                // A combination of days.
                var shortNames = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
                var dayList = new List<string> ();
                if (0 != (dow & NcDayOfWeek.Sunday)) {
                    dayList.Add (shortNames [(int)DayOfWeek.Sunday]);
                }
                if (0 != (dow & NcDayOfWeek.Monday)) {
                    dayList.Add (shortNames [(int)DayOfWeek.Monday]);
                }
                if (0 != (dow & NcDayOfWeek.Tuesday)) {
                    dayList.Add (shortNames [(int)DayOfWeek.Tuesday]);
                }
                if (0 != (dow & NcDayOfWeek.Wednesday)) {
                    dayList.Add (shortNames [(int)DayOfWeek.Wednesday]);
                }
                if (0 != (dow & NcDayOfWeek.Thursday)) {
                    dayList.Add (shortNames [(int)DayOfWeek.Thursday]);
                }
                if (0 != (dow & NcDayOfWeek.Friday)) {
                    dayList.Add (shortNames [(int)DayOfWeek.Friday]);
                }
                if (0 != (dow & NcDayOfWeek.Saturday)) {
                    dayList.Add (shortNames [(int)DayOfWeek.Saturday]);
                }
                return MakeCommaSeparatedList (dayList);
            }
        }

        public static string MakeRecurrenceString (IList<McRecurrence> recurrences)
        {
            if (0 == recurrences.Count) {
                return Strings.Instance.RecurrenceDoesNotRepeat;
            }
            McRecurrence r = recurrences [0];

            int interval = r.IntervalIsSet && 1 < r.Interval ? r.Interval : 1;

            switch (r.Type) {

            case NcRecurrenceType.Daily:
                if (1 == interval) {
                    return Strings.Instance.RecurrenceDaily;
                }
                return string.Format (Strings.Instance.RecurrenceEveryXDaysFormat, interval);

            case NcRecurrenceType.Weekly:
                if (1 == interval) {
                    if (NcDayOfWeek.LastDayOfTheMonth == r.DayOfWeek) {
                        // Repeats weekly on every day of the week, which is the same as daily.
                        return Strings.Instance.RecurrenceDaily;
                    }
                    if (NcDayOfWeek.Weekdays == r.DayOfWeek) {
                        return Strings.Instance.RecurrenceWeekdays;
                    }
                    if (NcDayOfWeek.WeekendDays == r.DayOfWeek) {
                        return Strings.Instance.RecurrenceWeekends;
                    }
                    return string.Format (Strings.Instance.RecurrenceWeeklyFormat, DayOfWeekAsString (r.DayOfWeek));
                }
                if (NcDayOfWeek.Weekdays == r.DayOfWeek) {
                    return string.Format (Strings.Instance.RecurrenceEveryXWeekdaysFormat, interval);
                }
                return string.Format (Strings.Instance.RecurrenceEveryXWeeksFormat, interval, DayOfWeekAsString (r.DayOfWeek));

            case NcRecurrenceType.Monthly:
                if (1 == interval) {
                    return string.Format (Strings.Instance.RecurrenceMonthlyFormat, r.DayOfMonth);
                }
                return string.Format (Strings.Instance.RecurrenceEveryXMonthsFormat, interval, r.DayOfMonth);

            case NcRecurrenceType.Yearly:
                string dateString;
                try {
                    dateString = NachoPlatform.DateTimeFormatter.Instance.Date (new DateTime (2004, r.MonthOfYear, r.DayOfMonth, 0, 0, 0, DateTimeKind.Local));
                } catch (ArgumentOutOfRangeException) {
                    dateString = string.Format ("{0}/{1}", r.MonthOfYear, r.DayOfMonth);
                }
                if (1 == interval) {
                    return string.Format (Strings.Instance.RecurrenceYearlyFormat, dateString);
                }
                return string.Format (Strings.Instance.RecurrenceEveryXYearsFormat, dateString);

            case NcRecurrenceType.MonthlyOnDay:
                if (1 == interval) {
                    switch (r.DayOfWeek) {
                    case NcDayOfWeek.LastDayOfTheMonth:
                        return string.Format (Strings.Instance.RecurrenceLastDayOfMonth);
                    case NcDayOfWeek.Weekdays:
                        switch (r.WeekOfMonth) {
                        case 5:
                            return string.Format (Strings.Instance.RecurrenceLastWeekdayOfMonth);
                        default:
                            return string.Format (Strings.Instance.RecurrenceWeekdaysInWeekOfMonthFormat, r.WeekOfMonth);
                        }
                    case NcDayOfWeek.WeekendDays:
                        switch (r.WeekOfMonth) {
                        case 5:
                            return string.Format (Strings.Instance.RecurrenceLastWeekendDayOfMonth);
                        default:
                            return string.Format (Strings.Instance.RecurrenceWeekendDaysInWeekOfMonthFormat, r.WeekOfMonth);
                        }
                    default:
                        switch (r.WeekOfMonth) {
                        case 5:
                            return string.Format (Strings.Instance.RecurrenceLastNamedDayOfMonthFormat, DayOfWeekAsString (r.DayOfWeek));
                        default:
                            return string.Format (Strings.Instance.RecurrenceNamedDayInWeekOfMonthFormat, DayOfWeekAsString (r.DayOfWeek), r.WeekOfMonth);
                        }
                    }
                }
                switch (r.DayOfWeek) {
                case NcDayOfWeek.LastDayOfTheMonth:
                    return string.Format (Strings.Instance.RecurrenceLastDayOfEveryXMonthsFormat, interval);
                case NcDayOfWeek.Weekdays:
                    switch (r.WeekOfMonth) {
                    case 5:
                        return string.Format (Strings.Instance.RecurrenceLastWeekdayOfEveryXMonthsFormat, interval);
                    default:
                        return string.Format (Strings.Instance.RecurrenceWeekdaysInWeekOfEveryXMonthFormat, interval, r.WeekOfMonth);
                    }
                case NcDayOfWeek.WeekendDays:
                    switch (r.WeekOfMonth) {
                    case 5:
                        return string.Format (Strings.Instance.RecurrenceLastWeekendDayOfEveryXMonthsFormat, interval);
                    default:
                        return string.Format (Strings.Instance.RecurrenceWeekendDaysInWeekOfEveryXMonthFormat, interval, r.WeekOfMonth);
                    }
                default:
                    switch (r.WeekOfMonth) {
                    case 5:
                        return string.Format (Strings.Instance.RecurrenceLastNamedDayOfEveryXMonthsFormat, interval, DayOfWeekAsString (r.DayOfWeek));
                    default:
                        return string.Format (Strings.Instance.RecurrenceNamedDayInWeekOfEveryXMonthFormat, interval, DayOfWeekAsString (r.DayOfWeek), r.WeekOfMonth);
                    }
                }

            case NcRecurrenceType.YearlyOnDay:
                string monthName;
                try {
                    monthName = DateTimeFormatter.Instance.MonthName (new DateTime (2000, r.MonthOfYear, 1));
                } catch (ArgumentOutOfRangeException) {
                    monthName = string.Format (Strings.Instance.RecurrenceUnknownMonthFormat, r.MonthOfYear);
                }
                if (1 == interval) {
                    switch (r.DayOfWeek) {
                    case NcDayOfWeek.LastDayOfTheMonth:
                        return string.Format (Strings.Instance.RecurrenceLastDayOfNamedMonthFormat, monthName);
                    case NcDayOfWeek.Weekdays:
                        switch (r.WeekOfMonth) {
                        case 5:
                            return string.Format (Strings.Instance.RecurrenceLastWeekdayOfNamedMonthFormat, monthName);
                        default:
                            return string.Format (Strings.Instance.RecurrenceWeekdaysInWeekOfNamedMonthFormat, r.WeekOfMonth, monthName);
                        }
                    case NcDayOfWeek.WeekendDays:
                        switch (r.WeekOfMonth) {
                        case 5:
                            return string.Format (Strings.Instance.RecurrenceLastWeekendDayOfNamedMonthFormat, monthName);
                        default:
                            return string.Format (Strings.Instance.RecurrenceWeekendDaysInWeekOfNamedMonthFormat, r.WeekOfMonth, monthName);
                        }
                    default:
                        switch (r.WeekOfMonth) {
                        case 5:
                            return string.Format (Strings.Instance.RecurrenceLastNamedDayOfNamedMonthFormat, DayOfWeekAsString (r.DayOfWeek), monthName);
                        default:
                            return string.Format (Strings.Instance.RecurrenceNamedDayInWeekOfNamedMonthFormat, DayOfWeekAsString (r.DayOfWeek), r.WeekOfMonth, monthName);
                        }
                    }
                }
                switch (r.DayOfWeek) {
                case NcDayOfWeek.LastDayOfTheMonth:
                    return string.Format (Strings.Instance.RecurrenceLastDayOfNamedMonthEveryXYearsFormat, monthName);
                case NcDayOfWeek.Weekdays:
                    switch (r.WeekOfMonth) {
                    case 5:
                        return string.Format (Strings.Instance.RecurrenceLastWeekdayOfNamedMonthEveryXYearsFormat, monthName);
                    default:
                        return string.Format (Strings.Instance.RecurrenceWeekdaysInWeekOfNamedMonthEveryXYearsFormat, r.WeekOfMonth, monthName);
                    }
                case NcDayOfWeek.WeekendDays:
                    switch (r.WeekOfMonth) {
                    case 5:
                        return string.Format (Strings.Instance.RecurrenceLastWeekendDayOfNamedMonthEveryXYearsFormat, monthName);
                    default:
                        return string.Format (Strings.Instance.RecurrenceWeekendDaysInWeekOfNamedMonthEveryXYearsFormat, r.WeekOfMonth, monthName);
                    }
                default:
                    switch (r.WeekOfMonth) {
                    case 5:
                        return string.Format (Strings.Instance.RecurrenceLastNamedDayOfNamedMonthEveryXYearsFormat, DayOfWeekAsString (r.DayOfWeek), monthName);
                    default:
                        return string.Format (Strings.Instance.RecurrenceNamedDayInWeekOfNamedMonthEveryXYearsFormat, DayOfWeekAsString (r.DayOfWeek), r.WeekOfMonth, monthName);
                    }
                }

            default:
                return Strings.Instance.RecurrenceUnknown;
            }
        }

        public static string MakeCommaSeparatedList (List<string> stringList)
        {
            var endString = Strings.Instance.RecurrenceListFinalJoiner + stringList [stringList.Count - 1];
            stringList.RemoveAt (stringList.Count - 1);
            var stringArray = stringList.ToArray ();
            var commaSeparatedString = String.Join (Strings.Instance.RecurrenceListJoiner, stringArray);
            return commaSeparatedString + endString;
        }

        #endregion

        #region Attachments

        // TODO: i18n
        // TODO: l10n
        public static string PrettyFileSize (long fileSize)
        {
            NcAssert.True (0 <= fileSize);
            if (1000 > fileSize) {
                return String.Format ("{0} B", fileSize);
            }
            if (1000000 > fileSize) {
                return String.Format ("{0:F1} KB", (double)fileSize / 1.0e3);
            }
            if ((1000 * 1000 * 1000) > fileSize) {
                return String.Format ("{0:F1} MB", (double)fileSize / 1.0e6);
            }
            if ((1000L * 1000L * 1000L * 1000L) > fileSize) {
                return String.Format ("{0:F1} GB", (double)fileSize / 1.0e9);
            }
            return String.Format ("{0:F1}TB", (double)fileSize / 1.0e12);
        }

        public static string GetExtension (string path)
        {
            if (null == path) {
                return String.Empty;
            }
            return System.IO.Path.GetExtension (path).ToUpper ();
        }

        public static string GetAttachmentDetail (McAttachment attachment)
        {
            string extension = Pretty.GetExtension (attachment.DisplayName);
            string typeString = null;
            if (extension.Length > 1) {
                typeString = extension.Substring (1) + " ";
            } else if (!String.IsNullOrEmpty (attachment.ContentType)) {
                var mimeInfo = attachment.ContentType.Split (new char [] { '/' });
                if (mimeInfo.Length == 2) {
                    typeString = mimeInfo [1].ToUpper () + " ";
                }
            }
            string description;
            if (attachment.IsInline) {
                if (typeString == null) {
                    description = Strings.Instance.AttachmentInlineUnknownFile;
                } else {
                    description = string.Format (Strings.Instance.AttachmentInlineTypedFileFormat, typeString);
                }
            } else {
                if (typeString == null) {
                    description = Strings.Instance.AttachmentUnknownFile;
                } else {
                    description = string.Format (Strings.Instance.AttachmentTypedFileFormat, typeString);
                }
            }
            if (0 != attachment.FileSize) {
                return string.Format ("{0} - {1}", description, Pretty.PrettyFileSize (attachment.FileSize));
            }
            return description;
        }

        #endregion

        // The display name of the account is a nickname for
        // the account, like Exchange or My Work Account, etc.
        // We do not support this yet, so null is the right value.
        static public string UserNameForAccount (McAccount account)
        {
            NcAssert.True (null == account.DisplayUserName);
            return null;
        }

        public static string Join (String one, String two, String separator = " ")
        {
            if (String.IsNullOrEmpty (one)) {
                return two;
            }
            if (String.IsNullOrEmpty (two)) {
                return one;
            }
            return one + separator + two;
        }

        // TODO: i18n
        public static string MaxAgeFilter (ActiveSync.Xml.Provision.MaxAgeFilterCode code)
        {
            switch (code) {
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.SyncAll_0:
                return Strings.Instance.MaxAgeFilterAllMessages;
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.OneDay_1:
                return Strings.Instance.MaxAgeFilterOneDay;
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.ThreeDays_2:
                return Strings.Instance.MaxAgeFilterThreeDays;
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.OneWeek_3:
                return Strings.Instance.MaxAgeFilterOneWeek;
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.TwoWeeks_4:
                return Strings.Instance.MaxAgeFilterTwoWeeks;
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.OneMonth_5:
                return Strings.Instance.MaxAgeFilterOneMonth;
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.ThreeMonths_6:
                return Strings.Instance.MaxAgeFilterThreeMonths;
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.SixMonths_7:
                return Strings.Instance.MaxAgeFilterSixMonths;
            default:
                NcAssert.CaseError ();
                break;
            }
            return "";
        }

        // Some of these are hidden on purpose, see notification code
        public static string NotificationConfiguration (McAccount.NotificationConfigurationEnum code)
        {
            var list = new List<string> ();
            //            if (McAccount.NotificationConfigurationEnum.ALLOW_ALL_1 == (McAccount.NotificationConfigurationEnum.ALLOW_ALL_1 & code)) {
            //                list.Add ("All");
            //            }
            if (McAccount.NotificationConfigurationEnum.ALLOW_HOT_2 == (McAccount.NotificationConfigurationEnum.ALLOW_HOT_2 & code)) {
                list.Add (Strings.Instance.NotificationConfigurationHot);
            }
            if (McAccount.NotificationConfigurationEnum.ALLOW_VIP_4 == (McAccount.NotificationConfigurationEnum.ALLOW_VIP_4 & code)) {
                list.Add (Strings.Instance.NotificationConfigurationVIPs);
            }
            if (McAccount.NotificationConfigurationEnum.ALLOW_INBOX_64 == (McAccount.NotificationConfigurationEnum.ALLOW_INBOX_64 & code)) {
                list.Add (Strings.Instance.NotificationConfigirationInbox);
            }
            //            if (McAccount.NotificationConfigurationEnum.ALLOW_INVITES_16 == (McAccount.NotificationConfigurationEnum.ALLOW_INVITES_16 & code)) {
            //                list.Add ("Invitations");
            //            }
            //            if (McAccount.NotificationConfigurationEnum.ALLOW_REMINDERS_32 == (McAccount.NotificationConfigurationEnum.ALLOW_REMINDERS_32 & code)) {
            //                list.Add ("Reminders");
            //            }
            if (0 == list.Count) {
                return Strings.Instance.NotificationConfigirationNone;
            } else {
                return String.Join (", ", list);
            }
        }

        // TODO: i18n
        public static string LimitedBadgeCount (int count)
        {
            if (count < 100000) {
                return String.Format ("{0:N0}", count);
            } else {
                return "100K+";
            }
        }
    }
}

