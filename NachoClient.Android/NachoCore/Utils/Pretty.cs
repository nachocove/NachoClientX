//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Model;
using MimeKit;

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
                return "No subject";
            } else {
                return Subject;
            }
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
        static public string ReminderString (uint reminder)
        {
            if (0 == reminder) {
                return "None";
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

            var startString = startTime.LocalT().ToString ("t");

            if (startTime == endTime) {
                return startString;
            }
            var localEndTime = endTime.LocalT();
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

        static public string UniversalFullDateTimeString(DateTime d)
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
            var local = utcDueDate.LocalT();
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
                alertMessage = " now";
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
    }
}

