//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Model;
using MimeKit;
using System.Collections.Generic;

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

        /// <summary>
        /// Calendar event duration, 0h0m style.
        /// Returns an empty string for a zero length time span.
        /// </summary>
        static public string CompactDuration (DateTime StartTime, DateTime EndTime)
        {
            if (StartTime == EndTime) {
                return "";
            }
            TimeSpan s = EndTime - StartTime;
            if (s.TotalMinutes < 60) {
                return String.Format ("{0}m", s.Minutes);
            }
            if (s.TotalHours < 24) {
                if (0 == s.Minutes) {
                    return String.Format ("{0}h", s.Hours);
                } else {
                    return String.Format ("{0}h{1}m", s.Hours, s.Minutes);
                }
            }
            return "1d+";
        }

        /// <summary>
        /// String for a reminder, in minutes.
        /// </summary>
        static public string ReminderString (bool reminderIsSet, uint reminder)
        {
            if (!reminderIsSet) {
                return "None";
            }
            if (0 == reminder) {
                return "At time of event";
            }
            if (1 == reminder) {
                return "1 minute before";
            }
            if (60 == reminder) {
                return "1 hour before";
            }
            if ((24 * 60) == reminder) {
                return "1 day before";
            }
            if ((7 * 24 * 60) == reminder) {
                return "1 week before";
            }
            if (0 == (reminder % (7 * 24 * 60))) {
                return String.Format ("{0} weeks before", reminder / (7 * 24 * 60));
            }
            if (0 == (reminder % (24 * 60))) {
                return String.Format ("{0} days before", reminder / (24 * 60));
            }
            if (0 == (reminder % 60)) {
                return String.Format ("{0} hours before", reminder / 60);
            }
            return String.Format ("{0} minutes before", reminder);
        }

        /// <summary>
        /// All day, with n days for multi-day events
        /// </summary>
        static public string AllDayStartToEnd (DateTime startTime, DateTime endTime)
        {
            var d = endTime.Date.Subtract (startTime.Date);
            if (d.Minutes < 1) {
                return "All day";
            }
            return String.Format ("All day ({0} days)", d.Days);
        }

        /// <summary>
        /// StartTime - EndTime, on two lines.
        /// </summary>
        static public string EventStartToEnd (DateTime startTime, DateTime endTime)
        {
            NcAssert.True (DateTimeKind.Local != startTime.Kind);
            NcAssert.True (DateTimeKind.Local != endTime.Kind);

            var startString = startTime.LocalT ().ToString ("t");

            if (startTime == endTime) {
                return startString;
            }
            var localEndTime = endTime.LocalT ();
            var durationString = PrettyEventDuration (startTime, endTime);
            if (startTime.Date == endTime.Date) {
                return String.Format ("{0} - {1} ({2})", startString, localEndTime.ToString ("t"), durationString);
            } else {
                return String.Format ("{0} -\n{1} ({2})", startString, FullDateTimeString (endTime), durationString);
            }
        }

        /// <summary>
        /// Day, date and time string: Saturday, March 1 - 12:01 pm
        /// </summary>
        static public string FullDateTimeString (DateTime d)
        {
            NcAssert.True (DateTimeKind.Local != d.Kind);
            return d.LocalT ().ToString ("ddd, MMM d - h:mm ") + d.LocalT ().ToString ("tt").ToLower ();

        }

        static public string FullDateYearTimeString (DateTime d)
        {
            NcAssert.True (DateTimeKind.Local != d.Kind);
            return d.LocalT ().ToString ("ddd, MMM d, yyyy - h:mm ") + d.LocalT ().ToString ("tt").ToLower ();
        }

        static public string UniversalFullDateTimeString (DateTime d)
        {
            return d.LocalT ().ToString ("U");
        }

        /// <summary>
        /// Day and date: Sat, Mar 1
        /// </summary>
        static public string FullDateString (DateTime d)
        {
            NcAssert.True (DateTimeKind.Local != d.Kind);
            return d.LocalT ().ToString ("ddd, MMM d");
        }

        /// <summary>
        /// Day and date: Sat, Mar 1
        /// </summary>
        static public string FullDateSpelledOutString (DateTime d)
        {
            NcAssert.True (DateTimeKind.Local != d.Kind);
            return d.LocalT ().ToString ("dddd, MMMM d");
        }

        /// <summary>
        /// Day, date and year: Sat, Mar 1, 2014
        /// </summary>
        static public string FullDateYearString (DateTime d)
        {
            NcAssert.True (DateTimeKind.Local != d.Kind);
            return d.LocalT ().ToString ("ddd, MMM d, yyyy");
        }

        /// <summary>
        /// Short date: 3/1/14
        /// </summary>
        static public string ShortDateString (DateTime d)
        {
            NcAssert.True (DateTimeKind.Local != d.Kind);
            return d.LocalT ().ToString ("M/d/yy");
        }

        /// <summary>
        /// Full day and date string: Saturday, March 1
        /// </summary>
        static public string ExtendedDateString (DateTime d)
        {
            NcAssert.True (DateTimeKind.Local != d.Kind);
            return d.LocalT ().ToString ("dddd, MMMM d");
        }

        /// <summary>
        /// Time: 3:00 pm
        /// </summary>
        static public string FullTimeString (DateTime d)
        {
            NcAssert.True (DateTimeKind.Local != d.Kind);
            return d.LocalT ().ToString ("t").ToLower ();
        }

        /// <summary>
        /// Month: March or March 2016
        /// </summary>
        static public string PrettyMonthLabel (DateTime d)
        {
            return (d.Year == DateTime.UtcNow.Year) ? d.ToString ("MMMM") : d.ToString ("Y");
        }

        /// <summary>
        /// Display a date that may or may not have a year associated with it, and for which the
        /// day of the week is not important. For example, "August 27" or "August 27, 2000".
        /// iOS uses a date in 1604 when the year doesn't matter, so leave off the year when the
        /// date is earlier than 1700.
        /// </summary>
        static public string BirthdayOrAnniversary (DateTime d)
        {
            if (d.Year < 1700) {
                return d.ToString ("MMMM d");
            } else {
                return d.ToString ("MMMM d, yyyy");
            }
        }

        /// <summary>
        /// Compact version of event duration
        /// </summary>
        static public string PrettyEventDuration (DateTime startTime, DateTime endTime)
        {
            var d = endTime.Subtract (startTime);

            if (0 == d.TotalMinutes) {
                return ""; // no duration
            }

            // Even number of days?
            if (0 == (d.TotalMinutes % (24 * 60))) {
                if (1 == d.Days) {
                    return "1 day";
                } else {
                    return String.Format ("{0} days", d.Days);
                }
            }
            // Even number of hours?
            if (0 == (d.TotalMinutes % 60)) {
                if (1 == d.Hours) {
                    return "1 hour";
                } else {
                    return String.Format ("{0} hours", d.Hours);
                }
            }
            // Less than one hour?
            if (60 > d.Minutes) {
                if (1 == d.Minutes) {
                    return "1 minute";
                } else {
                    return String.Format ("{0} minutes", d.Minutes);
                }
            }
            // Less than one day?
            if ((24 * 60) > d.Minutes) {
                return String.Format ("{0}:{1} hours", d.Hours, d.Minutes % 60);
            } else {
                return String.Format ("{0}d{1}h{2}m", d.Days, d.Hours % 24, d.Minutes % 60);
            }
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

        static public string RecipientString (string Recipient)
        {
            if (null == Recipient) {
                return "";
            }
            return Recipient;
        }

        /// <summary>
        /// Given an organizer name, return a string
        /// worthy of being displayed in the files list.
        /// </summary>
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

        /// <summary>
        /// Converts a date to a string worthy
        /// of being displayed in the message list.
        /// </summary>
        static public string CompactDateString (DateTime Date)
        {
            var local = Date.LocalT ();
            var diff = DateTime.Now - local;
            if (diff < TimeSpan.FromMinutes (60)) {
                return String.Format ("{0:n0}m", diff.TotalMinutes);
            }
            if (diff < TimeSpan.FromHours (24)) {
                return String.Format ("{0:n0}h", diff.TotalHours);
            }
            if (diff <= TimeSpan.FromHours (24)) {
                return "Yesterday";
            }
            if (diff < TimeSpan.FromDays (6)) {
                return local.ToString ("dddd");
            }
            return local.ToShortDateString ();
        }

        static public string ShortTimeString (DateTime Date)
        {
            return Date.LocalT ().ToString ("t");
        }

        static public string ShortDayTimeString (DateTime Date)
        {
            return Date.LocalT ().ToString ("dddd") + " " + Date.LocalT ().ToString ("t");
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
        static public string AccountName (McAccount account)
        {
            if (null == account.DisplayName) {
                return "Exchange";
            } else {
                return account.DisplayName;
            }
        }

        static public string ReminderDate (DateTime utcDueDate)
        {
            var local = utcDueDate.LocalT ();
            var duration = System.DateTime.UtcNow - utcDueDate;
            if (365 < Math.Abs (duration.Days)) {
                return local.ToString ("MMM dd, yyyy"); // FIXME: Localize
            } else {
                return local.ToString ("MMM dd, h:mm tt"); // FIXME: Localize
            }
        }

        static public string ReminderText (McEmailMessage message)
        {
            if (message.IsDeferred ()) {
                return  String.Format ("Hidden until {0}", Pretty.ReminderDate (message.FlagDueAsUtc ()));
            } else if (message.IsOverdue ()) {
                return String.Format ("Response was due {0}", Pretty.ReminderDate (message.FlagDueAsUtc ()));
            } else {
                return  String.Format ("Response is due {0}", Pretty.ReminderDate (message.FlagDueAsUtc ()));
            }
        }

        public static string ReminderTime (TimeSpan startsIn)
        {
            int minutes = Convert.ToInt32 (startsIn.TotalMinutes);
            if (0 > minutes) {
                return "already started";
            }
            if (0 == minutes) {
                return "now";
            }
            if (1 == minutes) {
                return "in a minute";
            }
            if (60 == minutes) {
                return "in an hour";
            }
            if ((60 * 24) == minutes) {
                return "in a day";
            }
            if ((60 * 24 * 7) == minutes) {
                return "in a week";
            }
            if (60 > minutes) {
                return string.Format ("in {0} minutes", minutes);
            }
            if (120 > minutes) {
                return string.Format ("in an hour and {0} minutes", minutes - 60);
            }
            if (0 == minutes % (60 * 24)) {
                return string.Format ("in {0} days", minutes / (60 * 24));
            }
            if (0 == minutes % 60) {
                return string.Format ("in {0} hours", minutes / 60);
            }
            return string.Format ("in {0}:{1:D2}", minutes / 60, minutes % 60);
        }

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

        public static bool TreatLikeAPhoto (string path)
        {
            string[] ext = {
                "tiff",
                "jpeg",
                "jpg",
                "gif",
                "png",
                "raw",
            };
            if (null == path) {
                return false;
            }
            foreach (var s in ext) {
                if (path.EndsWith (s, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        protected static string DayOfWeekAsString (NcDayOfWeek dow)
        {
            switch (dow) {
            case NcDayOfWeek.Sunday:
                return "Sunday";
            case NcDayOfWeek.Monday:
                return "Monday";
            case NcDayOfWeek.Tuesday:
                return "Tuesday";
            case NcDayOfWeek.Wednesday:
                return "Wednesday";
            case NcDayOfWeek.Thursday:
                return "Thursday";
            case NcDayOfWeek.Friday:
                return "Friday";
            case NcDayOfWeek.Saturday:
                return "Saturday";
            default:
                // A combination of days.
                var dayList = new List<string> ();
                if (0 != (dow & NcDayOfWeek.Sunday)) {
                    dayList.Add ("Sun");
                }
                if (0 != (dow & NcDayOfWeek.Monday)) {
                    dayList.Add ("Mon");
                }
                if (0 != (dow & NcDayOfWeek.Tuesday)) {
                    dayList.Add ("Tue");
                }
                if (0 != (dow & NcDayOfWeek.Wednesday)) {
                    dayList.Add ("Wed");
                }
                if (0 != (dow & NcDayOfWeek.Thursday)) {
                    dayList.Add ("Thu");
                }
                if (0 != (dow & NcDayOfWeek.Friday)) {
                    dayList.Add ("Fri");
                }
                if (0 != (dow & NcDayOfWeek.Saturday)) {
                    dayList.Add ("Sat");
                }
                return MakeCommaSeparatedList (dayList);
            }
        }

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

        protected static string WeekOfMonth (int week)
        {
            if (5 == week) {
                return "last";
            }
            return AddOrdinalSuffix (week);
        }

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
                    dateString = new DateTime (2004, r.MonthOfYear, r.DayOfMonth).ToString ("MMM d");
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

        public static string MakeCommaSeparatedList (List<string> stringList)
        {

            var endString = " and " + stringList [stringList.Count - 1];
            stringList.RemoveAt (stringList.Count - 1);
            var stringArray = stringList.ToArray ();
            var commaSeparatedString = String.Join (", ", stringArray);
            return commaSeparatedString + endString;
        }

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

        public static string MessageCount(string label, int count)
        {
            if (0 == count) {
                return String.Format ("No {0}s", label);
            } else {
                return String.Format("{0} {1}{2}", count, label, (1 == count) ? "" : "s");
            }
        }
    }
}

