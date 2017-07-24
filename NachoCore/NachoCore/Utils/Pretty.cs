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
                    return String.Format ("{0}h{1}m", s.Hours, s.Minutes);
                }
            }
            return "1d+";
        }

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

        /// <summary>
        /// Display a date that may or may not have a year associated with it, and for which the
        /// day of the week is not important. For example, "August 27" or "August 27, 2000".
        /// iOS uses a date in 1604 when the year doesn't matter, so leave off the year when the
        /// date is earlier than 1700.
        /// </summary>
        // TODO: l10n?
        static public string BirthdayOrAnniversary (DateTime d)
        {
            if (d.Year < 1700) {
                return d.ToString ("M");
            } else {
                return d.ToString (CollapseSpaces (DTFormat.LongDatePattern.Replace ("dddd", "")));
            }
        }

        /// <summary>
        /// "October" or "October 2015"
        /// </summary>
        // TODO: l10n
        static public string LongMonthYear (DateTime date)
        {
            date = date.ToLocalTime ();
            if (date.Year == DateTime.Now.Year) {
                return date.ToString ("MMMM");
            } else {
                return date.ToString ("Y");
            }
        }

        /// <summary>
        /// "Wednesday, October 21" or "Wednesday, October 21, 2015"
        /// </summary>
        // TODO: l10n
        static public string LongFullDate (DateTime date)
        {
            date = date.ToLocalTime ();
            string format = DTFormat.LongDatePattern;
            // We want "October 7" instead of "October 07".
            format = format.Replace ("MMMM dd", "MMMM d");
            if (date.Year == DateTime.Now.Year) {
                // Remove the year
                format = CollapseSpaces (format.Replace ("yyyy", ""));
            }
            return date.ToString (format);
        }

        /// <summary>
        /// "October 21"
        /// </summary>
        // TODO: l10n
        static public string LongMonthDay (DateTime date)
        {
            return date.ToLocalTime ().ToString (DTFormat.MonthDayPattern.Replace ("MMMM dd", "MMMM d"));
        }

        /// <summary>
        /// "October 21, 2015"
        /// </summary>
        // TODO: l10n
        static public string LongMonthDayYear (DateTime date)
        {
            return date.ToLocalTime ().ToString (CollapseSpaces (DTFormat.LongDatePattern.Replace ("MMMM dd", "MMMM d").Replace ("dddd", "")));
        }

        /// <summary>
        /// "Wed, Oct 21" or "Wed, Oct 21, 2015"
        /// </summary>
        // TODO: l10n
        static public string MediumFullDate (DateTime date)
        {
            date = date.ToLocalTime ();
            string format = DTFormat.LongDatePattern;
            // We want "October 7" instead of "October 07".
            format = format.Replace ("MMMM dd", "MMMM d");
            if (date.Year == DateTime.Now.Year) {
                // Remove the year
                format = CollapseSpaces (format.Replace ("yyyy", ""));
            }
            format = format.Replace ("dddd", "ddd").Replace ("MMMM", "MMM");
            return date.ToString (format);
        }

        /// <summary>
        /// "Oct 21"
        /// </summary>
        // TODO: l10n
        static public string MediumMonthDay (DateTime date)
        {
            return date.ToLocalTime ().ToString (DTFormat.MonthDayPattern.Replace ("MMMM dd", "MMMM d").Replace ("MMMM", "MMM"));
        }

        /// <summary>
        /// "Oct 21, 2015"
        /// </summary>
        // TODO: l10n
        static public string MediumMonthDayYear (DateTime date)
        {
            return date.ToLocalTime ().ToString (CollapseSpaces (DTFormat.LongDatePattern.Replace ("MMMM dd", "MMMM d").Replace ("dddd", "").Replace ("MMMM", "MMM")));
        }

        /// <summary>
        /// "10/21/15"
        /// </summary>
        // TODO: l10n
        static public string ShortDate (DateTime date)
        {
            return date.ToLocalTime ().ToString (DTFormat.ShortDatePattern.Replace ("yyyy", "yy"));
        }

        /// <summary>
        /// "4:28 pm" or "16:28"
        /// </summary>
        // TODO: l10n
        static public string Time (DateTime time)
        {
            time = time.ToLocalTime ();
            string result = time.ToString ("t");
            if ("AM" == DTFormat.AMDesignator && "PM" == DTFormat.PMDesignator) {
                result = result.Replace ("AM", "am").Replace ("PM", "pm");
            }
            return result;
        }

        // TODO: l10n
        static public string ShortTime (DateTime time)
        {
            var timeString = Time (time);
            if (timeString.EndsWith (":00 pm")) {
                timeString = timeString.Substring (0, timeString.Length - 6) + " pm";
            } else if (timeString.EndsWith (":00 am")) {
                timeString = timeString.Substring (0, timeString.Length - 6) + " am";
            }
            return timeString;
        }

        // TODO: l10n
        static public string MicroTime (DateTime time)
        {
            var timeString = ShortTime (time);
            if (timeString.EndsWith (" am") || timeString.EndsWith (" pm")) {
                return timeString.Substring (0, timeString.Length - 3);
            }
            return timeString;
        }

        /// <summary>
        /// "Wed, Oct 21 - 4:28 pm" or "Wed, Oct 21, 2015 - 4:28 pm"
        /// </summary>
        // TODO: i18n
        static public string MediumFullDateTime (DateTime dateTime)
        {
            return string.Format ("{0} - {1}", MediumFullDate (dateTime), Time (dateTime));
        }

        // TODO: i18n
        static public string FriendlyFullDateTime (DateTime dateTime)
        {
            var local = dateTime.ToLocalTime ();
            var now = DateTime.Now;
            var diff = now - local;
            var dayString = "";
            var timeString = Time (dateTime);
            if (diff < now.TimeOfDay) {
                return String.Format ("Today - {0}", timeString);
            } else if (diff < (now.TimeOfDay + TimeSpan.FromDays (1))) {
                return String.Format ("Yesterday - {0}", timeString);
            } else if (diff < (now.TimeOfDay + TimeSpan.FromDays (6))) {
                return String.Format ("{0} - {1}", local.ToString ("dddd"), timeString);
            }
            return String.Format ("{0} - {1}", LongFullDate (dateTime), timeString);
        }

        /// <summary>
        /// "Wednesday 4:28 pm"
        /// </summary>
        // TODO: i18n
        static public string LongDayTime (DateTime dateTime)
        {
            return string.Format ("{0} {1}", dateTime.ToLocalTime ().ToString ("dddd"), Time (dateTime));
        }

        // TODO: i18n
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
                return Time (dateTime);
            }
            if (diff < now.TimeOfDay + TimeSpan.FromDays (1)) {
                return "Yesterday";
            }
            if (diff < TimeSpan.FromDays (6) + now.TimeOfDay) {
                return local.ToString ("dddd");
            }
            return ShortDate (dateTime);
        }

        // TODO: i18n
        static public string FutureDate (DateTime dateTime, bool timeMatters)
        {
            var local = dateTime.ToLocalTime ();
            var localStartOfDay = local - local.TimeOfDay;
            var now = DateTime.Now;
            if (now.Year == local.Year && now.Month == local.Month && now.Day == local.Day) {
                if (timeMatters) {
                    return ShortTime (local);
                }
                return "Today";
            }
            if (now < localStartOfDay) {
                if (now + TimeSpan.FromDays (1) > localStartOfDay) {
                    return "Tomorrow";
                }
                if (now + TimeSpan.FromDays (6) > localStartOfDay) {
                    return local.ToString ("dddd");
                }
            } else {
                if (local + TimeSpan.FromDays (1) > now - now.TimeOfDay) {
                    return "Yesterday";
                }
            }
            return ShortDate (dateTime);
        }

        // TODO: i18n
        static public string EventTime (DateTime dateTime, out TimeSpan validSpan)
        {
            var local = dateTime.ToLocalTime ();
            var now = DateTime.Now;
            var diff = local - now;

            if (diff < TimeSpan.FromSeconds (30)) {
                validSpan = TimeSpan.FromSeconds (-2);
                return "now";
            }

            if (diff < TimeSpan.FromMinutes (1)) {
                validSpan = TimeSpan.FromSeconds (30);
                return "in 1 minute";
            }

            if (diff < TimeSpan.FromMinutes (15)) {
                var minutes = (int)Math.Ceiling (diff.TotalMinutes);
                validSpan = (local - TimeSpan.FromMinutes (minutes - 1)) - now;
                return String.Format ("in {0} minutes", minutes);
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
                return String.Format ("in {0} minutes", roundedMinutes);
            }

            validSpan = (local - TimeSpan.FromHours (1)) - now;
            TimeSpan cutoff;
            if (now.Hour < 12) {
                cutoff = now.TimeOfDay + TimeSpan.FromHours (12);
            } else {
                cutoff = now.TimeOfDay;
            }
            if (diff < cutoff) {
                return "at " + Time (dateTime);
            }
            return LongDayTime (dateTime);
        }

        // TODO: i18n
        static public string EventDay (DateTime dateTime, out TimeSpan validSpan)
        {
            var local = dateTime.ToLocalTime ();
            var reference = DateTime.Now;
            var tomorrow = reference.AddDays (1);
            tomorrow = tomorrow - tomorrow.TimeOfDay;
            validSpan = tomorrow - reference;
            if (local < reference) {
                return "Today";
            }
            reference = reference.AddDays (1);
            if (local < reference) {
                return "Tomorrow";
            }
            return local.ToString ("dddd");
        }

        // TODO: i18n
        static public string VariableDayTime (DateTime dateTime)
        {
            var local = dateTime.ToLocalTime ();
            var now = DateTime.Now;
            var diff = now - local;
            if (diff < now.TimeOfDay) {
                return Time (dateTime);
            }
            if (diff < now.TimeOfDay + TimeSpan.FromDays (1)) {
                return String.Format ("Yesterday {0}", Time (dateTime));
            }
            if (diff < TimeSpan.FromDays (6) + now.TimeOfDay) {
                return LongDayTime (dateTime);
            }
            return MediumFullDateTime (dateTime);
        }

        static public string UniversalFullDateTime (DateTime d)
        {
            return d.ToLocalTime ().ToString ("U");
        }

        static private DateTimeFormatInfo DTFormat {
            get {
                return CultureInfo.CurrentCulture.DateTimeFormat;
            }
        }

        static private string CollapseSpaces (string format)
        {
            string result = Regex.Replace (format.Trim (), @"\s+", " ");
            if (result.EndsWith (",")) {
                result = result.Substring (0, result.Length - 1).Trim ();
            }
            if (result.StartsWith (",")) {
                result = result.Substring (1).Trim ();
            }
            return result;
        }

        // TODO: i18n
        static public string EventDetailTime (McEvent calendarEvent)
        {
            var start = calendarEvent.StartTime;
            var end = calendarEvent.EndTime;
            if (calendarEvent.AllDayEvent) {
                start = new DateTime (start.Year, start.Month, start.Day, start.Hour, start.Minute, start.Second, DateTimeKind.Local);
                end = new DateTime (end.Year, end.Month, end.Day, end.Hour, end.Minute, end.Second, DateTimeKind.Local);
            }
            var lines = new List<string> ();
            var day = LongFullDate (start);
            lines.Add (day);
            if (calendarEvent.AllDayEvent) {
                var span = (end - start);
                if (span > TimeSpan.FromDays (1)) {
                    var endDay = LongFullDate (end);
                    lines.Add (String.Format ("through {0}", endDay));
                }
            } else {
                if (start.ToLocalTime ().Date == end.ToLocalTime ().Date) {
                    lines.Add (string.Format ("{0} to {1}", Pretty.ShortTime (start), Pretty.ShortTime (end)));
                } else {
                    lines.Add (string.Format ("{0} to {1}", Pretty.ShortTime (start), Pretty.MediumFullDateTime (end)));
                }
            }
            var recurrences = calendarEvent.QueryRecurrences ();
            if (recurrences.Count > 0) {
                lines.Add (MakeRecurrenceString (recurrences));
            }
            return String.Join ("\n", lines);
        }

        // TODO: i18n
        static public string MeetingRequestTime (McMeetingRequest meetingRequest)
        {
            var start = meetingRequest.StartTime;
            var end = meetingRequest.EndTime;
            if (meetingRequest.AllDayEvent) {
                start = new DateTime (start.Year, start.Month, start.Day, start.Hour, start.Minute, start.Second, DateTimeKind.Local);
                end = new DateTime (end.Year, end.Month, end.Day, end.Hour, end.Minute, end.Second, DateTimeKind.Local);
            }
            var line = LongFullDate (start);
            if (meetingRequest.AllDayEvent) {
                var span = (end - start);
                if (span > TimeSpan.FromDays (1)) {
                    var endDay = LongFullDate (end);
                    line += String.Format (" through {0}", endDay);
                }
            } else {
                if (start.ToLocalTime ().Date == end.ToLocalTime ().Date) {
                    if ((start.ToLocalTime ().Hour < 12 && end.ToLocalTime ().Hour < 12) || (start.ToLocalTime ().Hour >= 12 && end.ToLocalTime ().Hour >= 12)) {
                        line += string.Format (" from {0} to {1}", Pretty.MicroTime (start), Pretty.ShortTime (end));
                    } else {
                        line += string.Format (", {0} to {1}", Pretty.ShortTime (start), Pretty.ShortTime (end));
                    }
                } else {
                    line += string.Format (" at {0} to {1}", Pretty.ShortTime (start), Pretty.MediumFullDateTime (end));
                }
            }
            var recurrences = meetingRequest.recurrences;
            if (recurrences.Count > 0) {
                line += "\n" + MakeRecurrenceString (recurrences);
            }
            return line;
        }

        // TODO: i18n
        static public string EventEditTime (DateTime date, bool isAllDay, bool isEnd)
        {
            if (isAllDay) {
                date = new DateTime (date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, DateTimeKind.Local);
                if (isEnd) {
                    date = date.AddDays (-1.0);
                }
                return Pretty.LongFullDate (date);
            }
            return Pretty.LongFullDate (date) + " " + Pretty.Time (date);
        }

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

        // TODO: i18n
        static public string RecipientString (string Recipient)
        {
            if (String.IsNullOrWhiteSpace (Recipient)) {
                return "(No Recipients)";
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
        /// Given an organizer name, return a string
        /// worthy of being displayed in the files list.
        /// </summary>
        // TODO: i18n
        static public string OrganizerString (string organizer)
        {
            if (null == organizer) {
                return "Organizer unavailable";
            } else {
                return organizer;
            }
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

        // The display name of the account is a nickname for
        // the account, like Exchange or My Work Account, etc.
        // We do not support this yet, so null is the right value.
        static public string UserNameForAccount (McAccount account)
        {
            NcAssert.True (null == account.DisplayUserName);
            return null;
        }

        // "Exchange" predates always setting the display name
        // TODO: i18n
        static public string AccountName (McAccount account)
        {
            if (null == account.DisplayName) {
                return "Exchange";
            } else {
                return account.DisplayName;
            }
        }

        // TODO: i18n
        static public string ReminderDate (DateTime utcDueDate)
        {
            var duration = System.DateTime.UtcNow - utcDueDate;
            if (180 < Math.Abs (duration.Days)) {
                return MediumMonthDayYear (utcDueDate);
            } else {
                return string.Format ("{0} - {1}", MediumMonthDay (utcDueDate), Time (utcDueDate));
            }
        }

        // TODO: i18n
        public static void EventNotification (McEvent ev, out string title, out string body)
        {
            var calendarItem = ev.CalendarItem;
            if (null == calendarItem) {
                title = "Event";
            } else {
                title = Pretty.SubjectString (calendarItem.GetSubject ());
            }
            if (ev.AllDayEvent) {
                body = string.Format ("{0} all day", Pretty.MediumFullDate (ev.GetStartTimeLocal ()));
            } else {
                body = string.Format ("{0} - {1}", Pretty.Time (ev.GetStartTimeLocal ()), Pretty.Time (ev.GetEndTimeLocal ()));
            }
            if (null != calendarItem) {
                var location = calendarItem.GetLocation ();
                if (!string.IsNullOrEmpty (location)) {
                    body += ": " + location;
                }
            }
        }

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

        // TODO: l10n
        protected static string DayOfWeekAsString (NcDayOfWeek dow)
        {
            var fullNames = DTFormat.DayNames;

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
                var shortNames = DTFormat.AbbreviatedDayNames;
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

        // TODO: i18n
        protected static string DayOfWeekMonthly (NcDayOfWeek dow)
        {
            switch (dow) {
            case NcDayOfWeek.LastDayOfTheMonth:
                return "day";
            case NcDayOfWeek.Weekdays:
                return "weekday";
            case NcDayOfWeek.WeekendDays:
                return "weekend day";
            default:
                return DayOfWeekAsString (dow);
            }
        }

        // TODO: i18n
        protected static string WeekOfMonth (int week)
        {
            if (5 == week) {
                return "last";
            }
            return AddOrdinalSuffix (week);
        }

        // TODO: i18n
        public static string MakeRecurrenceString (IList<McRecurrence> recurrences)
        {
            if (0 == recurrences.Count) {
                return "does not repeat";
            }
            McRecurrence r = recurrences [0];

            int interval = r.IntervalIsSet && 1 < r.Interval ? r.Interval : 1;

            switch (r.Type) {

            case NcRecurrenceType.Daily:
                if (1 == interval) {
                    return "repeats daily";
                }
                return string.Format ("repeats every {0} days", interval);

            case NcRecurrenceType.Weekly:
                if (1 == interval) {
                    if (NcDayOfWeek.LastDayOfTheMonth == r.DayOfWeek) {
                        // Repeats weekly on every day of the week, which is the same as daily.
                        return "repeats daily";
                    }
                    if (NcDayOfWeek.Weekdays == r.DayOfWeek) {
                        return "repeats weekly on weekdays";
                    }
                    if (NcDayOfWeek.WeekendDays == r.DayOfWeek) {
                        return "repeats weekly on weekends";
                    }
                    return string.Format ("repeats weekly on {0}", DayOfWeekAsString (r.DayOfWeek));
                }
                if (NcDayOfWeek.Weekdays == r.DayOfWeek) {
                    return string.Format ("repeats every {0} weeks on weekdays", interval);
                }
                return string.Format ("repeats every {0} weeks on {1}", interval, DayOfWeekAsString (r.DayOfWeek));

            case NcRecurrenceType.Monthly:
                if (1 == interval) {
                    return string.Format ("repeats monthly on the {0}", AddOrdinalSuffix (r.DayOfMonth));
                }
                return string.Format ("repeats every {0} months on the {1}", interval, AddOrdinalSuffix (r.DayOfMonth));

            case NcRecurrenceType.Yearly:
                string dateString;
                try {
                    dateString = LongMonthDay (new DateTime (2004, r.MonthOfYear, r.DayOfMonth, 0, 0, 0, DateTimeKind.Local));
                } catch (ArgumentOutOfRangeException) {
                    dateString = string.Format ("{0}/{1}", r.MonthOfYear, r.DayOfMonth);
                }
                if (1 == interval) {
                    return string.Format ("repeats yearly on {0}", dateString);
                }
                return string.Format ("repeats every {0} years on {1}", dateString);

            case NcRecurrenceType.MonthlyOnDay:
                if (1 == interval) {
                    return string.Format ("repeats monthly on the {0} {1} of the month", WeekOfMonth (r.WeekOfMonth), DayOfWeekMonthly (r.DayOfWeek));
                }
                return string.Format ("repeats every {0} months on the {1} {2} of the month", interval, WeekOfMonth (r.WeekOfMonth), DayOfWeekMonthly (r.DayOfWeek));

            case NcRecurrenceType.YearlyOnDay:
                string monthName;
                try {
                    monthName = new DateTime (2000, r.MonthOfYear, 1).ToString ("MMMM");
                } catch (ArgumentOutOfRangeException) {
                    monthName = string.Format ("the {0} month", AddOrdinalSuffix (r.MonthOfYear));
                }
                if (1 == interval) {
                    return string.Format ("repeats yearly on the {0} {1} of {2}", WeekOfMonth (r.WeekOfMonth), DayOfWeekMonthly (r.DayOfWeek), monthName);
                }
                return string.Format ("repeats every {0} years on the {1} {2} of {3}", interval, WeekOfMonth (r.WeekOfMonth), DayOfWeekMonthly (r.DayOfWeek), monthName);

            default:
                return "repeats with an unknown frequency";
            }
        }

        // TODO: i18n
        public static string MakeCommaSeparatedList (List<string> stringList)
        {

            var endString = " and " + stringList [stringList.Count - 1];
            stringList.RemoveAt (stringList.Count - 1);
            var stringArray = stringList.ToArray ();
            var commaSeparatedString = String.Join (", ", stringArray);
            return commaSeparatedString + endString;
        }

        // TODO: i18n
        public static string AddOrdinalSuffix (int num)
        {
            if (num <= 0)
                return num.ToString ();

            switch (num % 100) {
            case 11:
            case 12:
            case 13:
                return num + "th";
            }

            switch (num % 10) {
            case 1:
                return num + "st";
            case 2:
                return num + "nd";
            case 3:
                return num + "rd";
            default:
                return num + "th";
            }
        }

        public static string GetExtension (string path)
        {
            if (null == path) {
                return String.Empty;
            }
            return System.IO.Path.GetExtension (path).ToUpper ();
        }

        // TODO: i18n
        public static string GetAttachmentDetail (McAttachment attachment)
        {
            string extension = Pretty.GetExtension (attachment.DisplayName);
            var detailText = "";
            if (attachment.IsInline) {
                detailText += "Inline ";
            }
            if (1 < extension.Length) {
                detailText += extension.Substring (1) + " ";
            } else if (!String.IsNullOrEmpty (attachment.ContentType)) {
                var mimeInfo = attachment.ContentType.Split (new char [] { '/' });
                if (2 == mimeInfo.Length) {
                    detailText += mimeInfo [1].ToUpper () + " ";
                }
            } else {
                detailText += "Unrecognized ";
            }
            detailText += "file";
            if (0 != attachment.FileSize) {
                detailText += " - " + Pretty.PrettyFileSize (attachment.FileSize);
            }
            return detailText;
        }

        // TODO: i18n
        public static string MaxAgeFilter (ActiveSync.Xml.Provision.MaxAgeFilterCode code)
        {
            switch (code) {
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.SyncAll_0:
                return "All messages";
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.OneDay_1:
                return "One day";
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.ThreeDays_2:
                return "Three days";
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.OneWeek_3:
                return "One week";
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.TwoWeeks_4:
                return "Two weeks";
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.OneMonth_5:
                return "One month";
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.ThreeMonths_6:
                return "Three months";
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.SixMonths_7:
                return "Six months";
            default:
                NcAssert.CaseError ();
                break;
            }
            return "";
        }

        // TODO: i18n
        // Some of these are hidden on purpose, see notification code
        public static string NotificationConfiguration (McAccount.NotificationConfigurationEnum code)
        {
            var list = new List<string> ();
            //            if (McAccount.NotificationConfigurationEnum.ALLOW_ALL_1 == (McAccount.NotificationConfigurationEnum.ALLOW_ALL_1 & code)) {
            //                list.Add ("All");
            //            }
            if (McAccount.NotificationConfigurationEnum.ALLOW_HOT_2 == (McAccount.NotificationConfigurationEnum.ALLOW_HOT_2 & code)) {
                list.Add ("Hot");
            }
            if (McAccount.NotificationConfigurationEnum.ALLOW_VIP_4 == (McAccount.NotificationConfigurationEnum.ALLOW_VIP_4 & code)) {
                list.Add ("VIPs");
            }
            if (McAccount.NotificationConfigurationEnum.ALLOW_INBOX_64 == (McAccount.NotificationConfigurationEnum.ALLOW_INBOX_64 & code)) {
                list.Add ("Inbox");
            }
            //            if (McAccount.NotificationConfigurationEnum.ALLOW_INVITES_16 == (McAccount.NotificationConfigurationEnum.ALLOW_INVITES_16 & code)) {
            //                list.Add ("Invitations");
            //            }
            //            if (McAccount.NotificationConfigurationEnum.ALLOW_REMINDERS_32 == (McAccount.NotificationConfigurationEnum.ALLOW_REMINDERS_32 & code)) {
            //                list.Add ("Reminders");
            //            }
            if (0 == list.Count) {
                return "None";
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

        // TODO: i18n
        public static string AttendeeStatus (McAttendee attendee)
        {
            var tokens = new List<string> ();
            if (attendee.AttendeeTypeIsSet) {
                switch (attendee.AttendeeType) {
                case NcAttendeeType.Optional:
                    tokens.Add ("Optional");
                    break;
                case NcAttendeeType.Required:
                    tokens.Add ("Required");
                    break;
                case NcAttendeeType.Resource:
                    tokens.Add ("Resource");
                    break;
                }
            }
            if (attendee.AttendeeStatusIsSet) {
                switch (attendee.AttendeeStatus) {
                case NcAttendeeStatus.Accept:
                    tokens.Add ("Accepted");
                    break;
                case NcAttendeeStatus.Decline:
                    tokens.Add ("Declined");
                    break;
                case NcAttendeeStatus.Tentative:
                    tokens.Add ("Tentative");
                    break;
                case NcAttendeeStatus.NotResponded:
                    tokens.Add ("No Response");
                    break;
                }
            }
            return String.Join (", ", tokens);
        }

        // TODO: i18n
        public static string MeetingResponse (McEmailMessage message)
        {
            string messageFormat;
            switch (message.MeetingResponseValue) {
            case NcResponseType.Accepted:
                messageFormat = "{0} has accepted the meeting.";
                break;
            case NcResponseType.Tentative:
                messageFormat = "{0} has tentatively accepted the meeting.";
                break;
            case NcResponseType.Declined:
                messageFormat = "{0} has declined the meeting.";
                break;
            default:
                Log.Warn (Log.LOG_CALENDAR, "Unkown meeting response status: {0}", message.MessageClass);
                messageFormat = "The status of {0} is unknown.";
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
    }
}

