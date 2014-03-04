//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Model;

namespace NachoClient
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
        static public string AllDayStartToEnd (McCalendar c)
        {
            var d = c.EndTime.Date.Subtract (c.StartTime.Date);
            if (d.Minutes < 1) {
                return "All day";
            }
            return String.Format ("All day ({0} days)", d.Days);
        }

        /// <summary>
        /// StartTime - EndTime, on two lines.
        /// </summary>
        static public string EventStartToEnd (McCalendar c)
        {
            var startString = c.StartTime.ToString ("t");

            if (c.StartTime == c.EndTime) {
                return startString;
            }
            var durationString = PrettyEventDuration (c);
            if (c.StartTime.Date == c.EndTime.Date) {
                return String.Format ("{0} - {1} ({2})", startString, c.EndTime.ToString ("t"), durationString);
            } else {
                return String.Format ("{0} -\n{1} ({2})", startString, FullDateString (c.EndTime), durationString);
            }
        }

        /// <summary>
        /// Full the date string: Saturday, March 1, 2014
        /// </summary>
        static public string FullDateString (DateTime d)
        {
            return d.ToString ("D");
        }

        /// <summary>
        /// Compact version of event duration
        /// </summary>
        static public string PrettyEventDuration (McCalendar c)
        {
            var d = c.EndTime.Subtract (c.StartTime);

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
            System.Net.Mail.MailAddress address = new System.Net.Mail.MailAddress (Sender);
            if (null != address.DisplayName) {
                return address.DisplayName;
            }
            if (null != address.User) {
                return address.User;
            }
            return Sender;
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
                return Date.ToString ("dddd");
            }
            return Date.ToShortDateString ();
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

