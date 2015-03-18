//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using System.Linq;
using NachoCore.ActiveSync;
using System.Collections.Generic;

namespace NachoCore.Utils
{
    public class ManageItems
    {
        /// <summary>
        /// Fill the calendar with a whole bunch of events.  Some of them are recurring events.
        /// Most of them are one-time events.
        /// </summary>
        public static void PopulateCalendar ()
        {
            var account = NcApplication.Instance.Account;

            // Find the correct calendar folder.
            var calendars = new NachoFolders (account.Id, NachoFolders.FilterForCalendars);
            var folder = calendars.GetFolder (0);
            for (int i = 1; i < calendars.Count (); ++i) {
                var calFolder = calendars.GetFolder (i);
                if (Xml.FolderHierarchy.TypeCode.DefaultCal_8 == calFolder.Type) {
                    folder = calFolder;
                    break;
                }
            }

            // The current year and month.
            int year = DateTime.Now.Year;
            int month = DateTime.Now.Month;

            // A daily appointment with no end date.
            var item = CreateAppointmentCommon (account);
            SetTimes (item, year, 1, 1, 8, 0, 30);
            AddDailyRecurrence (item, 0);
            SaveCalendar (item, folder);

            // For each month, create a daily appointment that lasts just that month.
            for (int i = 0; i < 12; ++i) {
                int eventYear = year;
                int eventMonth = month + i;
                if (12 < eventMonth) {
                    eventMonth -= 12;
                    eventYear += 1;
                }
                item = CreateAppointmentCommon (account);
                SetTimes (item, eventYear, eventMonth, 1, 8, 30, 30);
                AddDailyRecurrence (item, DateTime.DaysInMonth (eventYear, eventMonth));
                SaveCalendar (item, folder);
            }

            // For each day of the month, create a monthly appointment.
            for (int day = 1; day <= 31; ++day) {
                item = CreateAppointmentCommon (account);
                SetTimes (item, year, 1, day, 9, 0, 30);
                AddMonthlyRecurrence (item, day, 24);
                SaveCalendar (item, folder);
            }

            // An appointment that repeats only on weekdays.
            item = CreateAppointmentCommon (account);
            DateTime start = new DateTime (year, 1, 1);
            while (DayOfWeek.Saturday == start.DayOfWeek || DayOfWeek.Sunday == start.DayOfWeek) {
                start = start.AddDays (1);
            }
            SetTimes (item, year, 1, start.Day, 9, 30, 30);
            AddWeeklyRecurrence (item, NcDayOfWeek.Weekdays, 500); // Almost two years
            SaveCalendar (item, folder);

            // Starting two weeks ago and going for about a year, fill up the rest of each weekday
            // with non-recurring appointments.
            DateTime date = DateTime.Now.AddDays (-14).Date;
            for (int i = 0; i < 380; ++i) {
                if (DayOfWeek.Saturday != date.DayOfWeek && DayOfWeek.Sunday != date.DayOfWeek) {
                    for (int hour = 10; hour < 17; ++hour) {
                        item = CreateAppointmentCommon (account);
                        SetTimes (item, date.Year, date.Month, date.Day, hour, 0, 60);
                        SaveCalendar (item, folder);
                    }
                }
                date = date.AddDays (1);
            }
        }

        private static int itemNumber = 0;
        private static McCalendar CreateAppointmentCommon (McAccount account)
        {
            var item = new McCalendar ();
            item.AccountId = account.Id;
            item.Subject = string.Format ("Test {0}", ++itemNumber);
            item.Description = "This event was created by a tool that fills up a user's calendar with useless events.  Therefore, this event description has no useful information, and exists just to take up space.";
            item.Location = "Nowhere";
            item.AllDayEvent = false;
            item.OrganizerName = "Calendar Bot";
            item.OrganizerEmail = account.EmailAddr;
            item.DtStamp = DateTime.UtcNow;
            item.MeetingStatusIsSet = true;
            item.MeetingStatus = NcMeetingStatus.Appointment;
            item.ResponseRequestedIsSet = true;
            item.ResponseRequested = false;
            item.BusyStatusIsSet = true;
            item.BusyStatus = NcBusyStatus.Busy;
            item.TimeZone = new AsTimeZone (CalendarHelper.SimplifiedLocalTimeZone (), DateTime.UtcNow).toEncodedTimeZone ();
            item.UID = System.Guid.NewGuid ().ToString ().Replace ("-", null).ToUpper ();
            return item;
        }

        private static void SaveCalendar (McCalendar cal, McFolder folder)
        {
            cal.Insert ();
            folder.Link (cal);
            BackEnd.Instance.CreateCalCmd (cal.AccountId, cal.Id, folder.Id);
            System.Threading.Thread.Sleep (500);
        }

        private static void SetTimes (McCalendar cal, int year, int month, int day, int hour, int minute, int duration)
        {
            DateTime start = new DateTime (year, month, day, hour, minute, 0, DateTimeKind.Local).ToUniversalTime ();
            cal.StartTime = start;
            cal.EndTime = start.AddMinutes (duration);
        }

        private static void AddRecurrence (McCalendar cal, McRecurrence recurrence, int occurrences)
        {
            recurrence.AccountId = cal.AccountId;
            if (!recurrence.IntervalIsSet) {
                recurrence.IntervalIsSet = true;
                recurrence.Interval = 1;
            }
            if (0 < occurrences) {
                recurrence.OccurencesIsSet = true;
                recurrence.Occurences = occurrences;
            }
            var recurrences = new List<McRecurrence> ();
            recurrences.Add (recurrence);
            cal.recurrences = recurrences;
        }

        private static void AddDailyRecurrence (McCalendar cal, int occurrences)
        {
            var recurrence = new McRecurrence ();
            recurrence.Type = NcRecurrenceType.Daily;
            AddRecurrence (cal, recurrence, occurrences);
        }

        private static void AddWeeklyRecurrence (McCalendar cal, NcDayOfWeek dayOfWeek, int occurrences)
        {
            var recurrence = new McRecurrence ();
            recurrence.Type = NcRecurrenceType.Weekly;
            recurrence.DayOfWeekIsSet = true;
            recurrence.DayOfWeek = dayOfWeek;
            AddRecurrence (cal, recurrence, occurrences);
        }

        private static void AddMonthlyRecurrence (McCalendar cal, int dayOfMonth, int occurrences)
        {
            var recurrence = new McRecurrence ();
            recurrence.Type = NcRecurrenceType.Monthly;
            recurrence.DayOfMonth = dayOfMonth;
            AddRecurrence (cal, recurrence, occurrences);
        }

        /// <summary>
        /// Delete all calendar events associated with the current account.  This will leave the
        /// calendar completely empty.  Use with care.
        /// </summary>
        public static void CleanCalendar ()
        {
            var account = NcApplication.Instance.Account;

            foreach (var item in McCalendar.QueryByAccountId<McCalendar>(account.Id)) {
                BackEnd.Instance.DeleteCalCmd (account.Id, item.Id);
                System.Threading.Thread.Sleep (500);
            }
        }
    }
}

