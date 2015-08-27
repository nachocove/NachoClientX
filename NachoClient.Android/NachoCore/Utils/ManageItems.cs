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
        /// Fill the calendar with a whole bunch of events, both appointments and meetings.
        /// Some of them are recurring events.  Most of them are one-time events.
        /// </summary>
        public static void PopulateCalendar ()
        {
            var account = NcApplication.Instance.Account;
            var folder = McFolder.GetDefaultCalendarFolder (account.Id);

            // The current year and month.
            int year = DateTime.Now.Year;
            int month = DateTime.Now.Month;

            // A daily appointment with no end date.
            var item = CreateAppointment (account);
            SetTimes (item, year, 1, 1, 8, 0, 30);
            AddDailyRecurrence (item, 0);
            SaveCalendar (item, folder);

            // For each month, create a daily meeting that lasts just that month.
            for (int i = 0; i < 12; ++i) {
                int eventYear = year;
                int eventMonth = month + i;
                if (12 < eventMonth) {
                    eventMonth -= 12;
                    eventYear += 1;
                }
                item = CreateMeeting (account);
                SetTimes (item, eventYear, eventMonth, 1, 8, 30, 30);
                AddDailyRecurrence (item, DateTime.DaysInMonth (eventYear, eventMonth));
                SaveCalendar (item, folder);
            }

            // For each day of the month, create a monthly appointment.
            for (int day = 1; day <= 31; ++day) {
                item = CreateAppointment (account);
                SetTimes (item, year, 1, day, 9, 0, 30);
                AddMonthlyRecurrence (item, day, 24);
                SaveCalendar (item, folder);
            }

            // An meeting that repeats only on weekdays.
            item = CreateMeeting (account);
            DateTime start = new DateTime (year, 1, 1);
            while (DayOfWeek.Saturday == start.DayOfWeek || DayOfWeek.Sunday == start.DayOfWeek) {
                start = start.AddDays (1);
            }
            SetTimes (item, year, 1, start.Day, 9, 30, 30);
            AddWeeklyRecurrence (item, NcDayOfWeek.Weekdays, 500); // Almost two years
            // Create an exception on every Monday, changing the meeting time and the location.
            while (DayOfWeek.Monday != start.DayOfWeek) {
                start = start.AddDays (1);
            }
            var exceptions = new List<McException> ();
            for (int i = 0; i < 90; ++i) {
                var exception = new McException ();
                exception.AccountId = account.Id;
                exception.ExceptionStartTime = new DateTime (start.Year, start.Month, start.Day, 9, 30, 0, DateTimeKind.Local).ToUniversalTime ();
                SetTimes (exception, start.Year, start.Month, start.Day, 9, 0, 90);
                exception.Location = "Somewhere";
                var attendees = new List<McAttendee> ();
                for (int j = 0; j < 4; ++j) {
                    attendees.Add (new McAttendee (
                        account.Id, string.Format ("Fake Guy{0}", j), string.Format ("fakeguy{0}@d2.officeburrito.com", j),
                        NcAttendeeType.Required));
                }
                exception.attendees = attendees;
                exceptions.Add (exception);
                start = start.AddDays (7);
            }
            item.exceptions = exceptions;
            SaveCalendar (item, folder);

            // Starting two weeks ago and going for about a year, fill up the rest of each weekday
            // with non-recurring events.
            DateTime date = DateTime.Now.AddDays (-14).Date;
            for (int i = 0; i < 380; ++i) {
                if (DayOfWeek.Saturday != date.DayOfWeek && DayOfWeek.Sunday != date.DayOfWeek) {
                    for (int hour = 10; hour < 17; ++hour) {
                        item = hour % 2 == 0 ? CreateAppointment (account) : CreateMeeting (account);
                        SetTimes (item, date.Year, date.Month, date.Day, hour, 0, 60);
                        SaveCalendar (item, folder);
                    }
                }
                date = date.AddDays (1);
            }
        }

        private static int itemNumber = 0;

        private static McCalendar CreateEvent (McAccount account)
        {
            var item = new McCalendar ();
            item.AccountId = account.Id;
            item.Subject = string.Format ("Test {0}", ++itemNumber);
            item.SetDescription ("This event was created by a tool that fills up a user's calendar with useless events.  Therefore, this event description has no useful information, and exists just to take up space.", McAbstrFileDesc.BodyTypeEnum.PlainText_1);
            item.Location = "Nowhere";
            item.AllDayEvent = false;
            item.OrganizerName = "Calendar Bot";
            item.OrganizerEmail = account.EmailAddr;
            item.DtStamp = DateTime.UtcNow;
            item.BusyStatusIsSet = true;
            item.BusyStatus = NcBusyStatus.Busy;
            item.ReminderIsSet = true;
            item.Reminder = 10;
            item.TimeZone = new AsTimeZone (CalendarHelper.SimplifiedLocalTimeZone (), DateTime.UtcNow).toEncodedTimeZone ();
            item.UID = System.Guid.NewGuid ().ToString ().Replace ("-", null).ToUpperInvariant ();
            return item;
        }

        private static McCalendar CreateAppointment (McAccount account)
        {
            var item = CreateEvent (account);
            item.MeetingStatusIsSet = true;
            item.MeetingStatus = NcMeetingStatus.Appointment;
            item.ResponseRequestedIsSet = true;
            item.ResponseRequested = false;
            return item;
        }

        private static McCalendar CreateMeeting (McAccount account)
        {
            var item = CreateEvent (account);
            item.MeetingStatusIsSet = true;
            item.MeetingStatus = NcMeetingStatus.MeetingOrganizer;
            item.ResponseRequestedIsSet = true;
            item.ResponseRequested = true;
            var attendees = new List<McAttendee> ();
            for (int i = 0; i < 5; ++i) {
                attendees.Add (new McAttendee (
                    account.Id, string.Format ("Fake Guy{0}", i), string.Format ("fakeguy{0}@d2.officeburrito.com", i),
                    NcAttendeeType.Required));
            }
            item.attendees = attendees;
            return item;
        }

        private static void SaveCalendar (McCalendar cal, McFolder folder)
        {
            cal.Insert ();
            folder.Link (cal);
            BackEnd.Instance.CreateCalCmd (cal.AccountId, cal.Id, folder.Id);
            System.Threading.Thread.Sleep (500);
        }

        private static void SetTimes (McAbstrCalendarRoot cal, int year, int month, int day, int hour, int minute, int duration)
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
                recurrence.OccurrencesIsSet = true;
                recurrence.Occurrences = occurrences;
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
        /// Create a really large meeting.  It should have lots of attendees, and lots of exceptions
        /// each with its own list of attendees.  The purpose is to maximize the number of database
        /// operations necessary to save the calendar item.
        /// </summary>
        public static void MegaMeeting ()
        {
            var account = NcApplication.Instance.Account;
            var folder = McFolder.GetDefaultCalendarFolder (account.Id);

            var item = new McCalendar ();

            // The boilerplate stuff.  Nothing interesting here.
            item.AccountId = account.Id;
            item.Subject = string.Format ("Test Mega Meeting");
            item.SetDescription ("This is a recurring meeting with lots of attendees and lots of exceptions, each with a long list of attendees.  The purpose is to maximize the number of database operations necessary to save the calendar item.", McAbstrFileDesc.BodyTypeEnum.PlainText_1);
            item.Location = "Nowhere";
            item.AllDayEvent = false;
            item.OrganizerName = "Calendar Bot";
            item.OrganizerEmail = account.EmailAddr;
            item.DtStamp = DateTime.UtcNow;
            item.BusyStatusIsSet = true;
            item.BusyStatus = NcBusyStatus.Busy;
            item.ReminderIsSet = true;
            item.Reminder = 10;
            item.TimeZone = new AsTimeZone (CalendarHelper.SimplifiedLocalTimeZone (), DateTime.UtcNow).toEncodedTimeZone ();
            item.UID = System.Guid.NewGuid ().ToString ().Replace ("-", null).ToUpperInvariant ();
            item.MeetingStatusIsSet = true;
            item.MeetingStatus = NcMeetingStatus.MeetingOrganizer;
            item.ResponseRequestedIsSet = true;
            item.ResponseRequested = true;

            // Have the meeting start tomorrow, so everything is in the future.
            DateTime startDate = DateTime.Now.AddDays (1).Date;
            DateTime startTime = new DateTime (startDate.Year, startDate.Month, startDate.Day, 10, 0, 0, DateTimeKind.Local);
            SetTimes (item, startTime.Year, startTime.Month, startTime.Day, startTime.Hour, startTime.Minute, 99);

            AddDailyRecurrence (item, 200);

            // Add ninety-nine attendees
            var attendees = new List<McAttendee> ();
            for (int a = 0; a < 99; ++a) {
                attendees.Add (new McAttendee (
                    account.Id, string.Format ("Fake Guy{0}", a), string.Format ("fakeguy{0}@d2.officeburrito.com", a),
                    NcAttendeeType.Required));
            }
            item.attendees = attendees;

            // Have every other day be an exception, each of which has a different starting time,
            // a different location, and a slightly different list of attendees.
            var exceptions = new List<McException> ();
            DateTime exceptionStartTime = startTime.AddDays (1);
            for (int e = 0; e < 95; ++e) {
                var exception = new McException ();
                exception.AccountId = account.Id;
                exception.ExceptionStartTime = exceptionStartTime.ToUniversalTime ();
                exception.Location = string.Format ("Room {0}", e);
                SetTimes (exception, exceptionStartTime.Year, exceptionStartTime.Month, exceptionStartTime.Day, 9, 45, e + 1);
                attendees = new List<McAttendee> ();
                for (int a = 0; a < 100; ++a) {
                    // Leave out one of the attendees, a different one for each exception.
                    if (a != e) {
                        attendees.Add (new McAttendee (
                            account.Id, string.Format ("Fake Guy{0}", a), string.Format ("fakeguy{0}@d2.officeburrito.com", a),
                            NcAttendeeType.Required));
                    }
                }
                exception.attendees = attendees;
                exceptions.Add (exception);
                exceptionStartTime = exceptionStartTime.AddDays (2);
            }
            item.exceptions = exceptions;

            SaveCalendar (item, folder);
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

