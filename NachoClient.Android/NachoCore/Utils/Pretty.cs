﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
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
        /// Returns empty string for an appointment.
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
        /// Full the date string: Saturday, March 1, 2014
        /// </summary>
        static public string FullDateTimeString (DateTime d)
        {
            NcAssert.True (DateTimeKind.Local != d.Kind);
            return d.LocalT ().ToString ("ddd, MMM d - h:mm ") + d.LocalT ().ToString ("tt").ToLower ();

        }

        static public string UniversalFullDateTimeString (DateTime d)
        {
            return d.LocalT ().ToString ("U");
        }

        static public string FullDateString (DateTime d)
        {
            NcAssert.True (DateTimeKind.Local != d.Kind);
            return d.LocalT ().ToString ("ddd, MMM d");
        }

        static public string ShortDateString (DateTime d)
        {
            NcAssert.True (DateTimeKind.Local != d.Kind);
            return d.LocalT ().ToString ("M/d/yy");
        }

        static public string ExtendedDateString (DateTime d)
        {
            NcAssert.True (DateTimeKind.Local != d.Kind);
            return d.LocalT ().ToString ("dddd, MMMM d");
        }

        static public string FullTimeString (DateTime d)
        {
            NcAssert.True (DateTimeKind.Local != d.Kind);
            return d.LocalT ().ToString ("t").ToLower ();
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

        static public string DisplayNameForAccount (McAccount account)
        {
            if (null == account.DisplayName) {
                return account.EmailAddr;
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


        public static string FormatAlert (uint alert)
        {
            var alertMessage = "";
            if (0 == alert) {
                alertMessage = "now";
            } else if (1 == alert) {
                alertMessage = " in a minute";
            } else if (5 == alert || 15 == alert || 30 == alert) {
                alertMessage = " in " + alert + " minutes";
            } else if (60 == alert) {
                alertMessage = " in an hour";
            } else if (120 == alert) {
                alertMessage = " in two hours";
            } else if ((60 * 24) == alert) {
                alertMessage = " in one day";
            } else if ((60 * 48) == alert) {
                alertMessage = " in two days";
            } else if ((60 * 24 * 7) == alert) {
                alertMessage = " in a week";
            } else {
                alertMessage = String.Format (" in {0} minutes", alert);
            }
            return alertMessage;
        }

        public static string PrettyFileSize (long fileSize)
        {
            NcAssert.True (0 <= fileSize);
            if (1000 > fileSize) {
                return String.Format ("{0}B", fileSize);
            }
            if (1000000 > fileSize) {
                return String.Format ("{0:F1}KB", (double)fileSize / 1.0e3);
            }
            if ((1000 * 1000 * 1000) > fileSize) {
                return String.Format ("{0:F1}MB", (double)fileSize / 1.0e6);
            }
            if ((1000L * 1000L * 1000L * 1000L) > fileSize) {
                return String.Format ("{0:F1}GB", (double)fileSize / 1.0e9);
            }
            return String.Format ("{0:F1}TB", (double)fileSize / 1.0e12);
        }

        public static string PointF (PointF p)
        {
            return String.Format ("({0},{1})", p.X, p.Y);
        }

        public static string SizeF (SizeF s)
        {
            return String.Format ("({0},{1})", s.Width, s.Height);
        }

        public static string Join (String one, String two)
        {
            if (String.IsNullOrEmpty (one)) {
                return two;
            } else {
                return one + " " + two;
            }
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

        public static string MakeRecurrenceString (List<McRecurrence> recurrences)
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
    }
}

