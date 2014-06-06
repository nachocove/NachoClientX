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
                return "";
            } else {
                return Subject;
            }
        }

        /// <summary>
        /// Calendar event duration, 0h0m style.
        /// Returns empty string for an appointment.
        /// </summary>
        static public string CompactDuration (McCalendar c)
        {
            if (c.StartTime == c.EndTime) {
                return "";
            }
            TimeSpan s = c.EndTime - c.StartTime;
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

        static DateTime LocalT (DateTime d)
        {
            switch (d.Kind) {
            case DateTimeKind.Utc:
                {
                    var l = d.ToLocalTime ();
                    return l;
                }
            case DateTimeKind.Unspecified:
                {
                    var l = d.ToLocalTime ();
                    return l;
                }
            case DateTimeKind.Local:
            default:
                NcAssert.CaseError ();
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// StartTime - EndTime, on two lines.
        /// </summary>
        static public string EventStartToEnd (DateTime startTime, DateTime endTime)
        {
            NcAssert.True (DateTimeKind.Local != startTime.Kind);
            NcAssert.True (DateTimeKind.Local != endTime.Kind);

            var startString = LocalT (startTime).ToString ("t");

            if (startTime == endTime) {
                return startString;
            }
            var localEndTime = LocalT (endTime);
            var durationString = PrettyEventDuration (startTime, endTime);
            if (startTime.Date == endTime.Date) {
                return String.Format ("{0} - {1} ({2})", startString, localEndTime.ToString ("t"), durationString);
            } else {
                return String.Format ("{0} -\n{1} ({2})", startString, FullDateString (endTime), durationString);
            }
        }

        /// <summary>
        /// Full the date string: Saturday, March 1, 2014
        /// </summary>
        static public string FullDateString (DateTime d)
        {
            NcAssert.True (DateTimeKind.Local != d.Kind);
            return LocalT (d).ToString ("D");
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
            var diff = DateTime.Now - Date;
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
                return LocalT (Date).ToString ("dddd");
            }
            return LocalT (Date).ToShortDateString ();
        }

        static public string ShortTimeString (DateTime Date)
        {
            return LocalT (Date).ToString ("t");
        }

        static public string DisplayNameForAccount (McAccount account)
        {
            if (null == account.DisplayName) {
                return account.EmailAddr;
            } else {
                return account.DisplayName;
            }
        }
    }
}

