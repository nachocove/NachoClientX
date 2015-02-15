﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using SQLite;

using NachoCore.Model;
using NachoCore.Utils;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class McEvent : McAbstrObjectPerAcc
    {
        public McEvent ()
        {
        }

        [Indexed]
        public DateTime StartTime { get; set; }

        [Indexed]
        public DateTime EndTime { get; set; }

        [Indexed]
        public DateTime ReminderTime { get; set; }

        [Indexed]
        public int CalendarId { get; set; }

        [Indexed]
        public int ExceptionId { get; set; }

        static public McEvent Create (int accountId, DateTime startTime, DateTime endTime, int calendarId, int exceptionId)
        {
            // Save the event
            var e = new McEvent ();
            e.AccountId = accountId;
            e.StartTime = startTime;
            e.EndTime = endTime;
            e.CalendarId = calendarId;
            e.ExceptionId = exceptionId;
            e.Insert ();
            NachoCore.Utils.Log.Info (Utils.Log.LOG_DB, "McEvent create: {0} {1}", startTime, calendarId);
            if (0 != exceptionId) {
                NachoCore.Utils.Log.Info (Utils.Log.LOG_DB, "McException found: eventId={0} exceptionId={1}", e.Id, exceptionId);
            }
            return e;
        }

        public void SetReminder (uint reminderMinutes)
        {
            ReminderTime = StartTime - new TimeSpan (reminderMinutes * TimeSpan.TicksPerMinute);
            Update ();
            LocalNotificationManager.ScheduleNotification (this);
        }

        public override int Delete ()
        {
            LocalNotificationManager.CancelNotification (this);
            return base.Delete ();
        }

        public McAbstrCalendarRoot GetCalendarItemforEvent()
        {
            if (0 != ExceptionId) {
                return McException.QueryById<McException> (ExceptionId);
            } else {
                return McCalendar.QueryById<McCalendar> (CalendarId);
            }
        }

        public static McEvent GetCurrentOrNextEvent()
        {
            foreach (var evt in NcModel.Instance.Db.Table<McEvent> ().Where (x => x.EndTime >= DateTime.UtcNow).OrderBy (x => x.StartTime)) {
                var cal = evt.GetCalendarItemforEvent ();
                if (null != cal && NcMeetingStatus.MeetingCancelled != cal.MeetingStatus && NcMeetingStatus.ForwardedMeetingCancelled != cal.MeetingStatus) {
                    // An event that hasn't been canceled.  This is what we are looking for.
                    return evt;
                }
                // The event was canceled.  Go to the next one in the list.
            }
            return null;
        }

        /// <summary>
        /// All events that have a reminder time within the given range, ordered by reminder time.
        /// </summary>
        public static IEnumerable<McEvent> QueryEventsWithRemindersInRange (DateTime start, DateTime end)
        {
            return NcModel.Instance.Db.Table<McEvent> ()
                .Where (e => start <= e.ReminderTime && e.ReminderTime < end)
                .OrderBy (e => e.ReminderTime);
        }
    }
}
