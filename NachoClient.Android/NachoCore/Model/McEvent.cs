//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using SQLite;

using NachoCore.Model;
using NachoCore.Utils;
using System.Collections.Generic;
using System.Linq;

namespace NachoCore.Model
{
    /// <summary>
    /// For queries that only need the McEvent ID, not the entire object.
    /// </summary>
    public class NcEventIndex
    {
        public int Id { set; get; }
    }

    public class McEvent : McAbstrObjectPerAcc
    {
        public McEvent ()
        {
        }

        [Indexed]
        public DateTime StartTime { get; set; }

        [Indexed]
        public DateTime EndTime { get; set; }

        public bool AllDayEvent { get; set; }

        [Indexed]
        public DateTime ReminderTime { get; set; }

        [Indexed]
        public int CalendarId { get; set; }

        [Indexed]
        public int ExceptionId { get; set; }

        static public McEvent Create (int accountId, DateTime startTime, DateTime endTime, bool allDayEvent, int calendarId, int exceptionId)
        {
            // Save the event
            var e = new McEvent ();
            e.AccountId = accountId;
            e.StartTime = startTime;
            e.EndTime = endTime;
            e.AllDayEvent = allDayEvent;
            e.CalendarId = calendarId;
            e.ExceptionId = exceptionId;
            e.Insert ();
            if (0 == exceptionId) {
                NachoCore.Utils.Log.Info (Utils.Log.LOG_DB, "McEvent create: {0} {1} {2}", startTime, e.Id, calendarId);
            } else {
                NachoCore.Utils.Log.Info (Utils.Log.LOG_DB, "McEvent create with exception: {0} {1} {2} {3}",
                    startTime, e.Id, exceptionId, calendarId);
            }
            return e;
        }

        public DateTime GetStartTimeUtc ()
        {
            if (AllDayEvent) {
                return DateTime.SpecifyKind (StartTime, DateTimeKind.Local).ToUniversalTime ();
            } else {
                return StartTime;
            }
        }

        public DateTime GetEndTimeUtc ()
        {
            if (AllDayEvent) {
                return DateTime.SpecifyKind (EndTime, DateTimeKind.Local).ToUniversalTime ();
            } else {
                return EndTime;
            }
        }

        public DateTime GetStartTimeLocal ()
        {
            if (AllDayEvent) {
                return DateTime.SpecifyKind (StartTime, DateTimeKind.Local);
            } else {
                return StartTime.ToLocalTime ();
            }
        }

        public DateTime GetEndTimeLocal ()
        {
            if (AllDayEvent) {
                return DateTime.SpecifyKind (EndTime, DateTimeKind.Local);
            } else {
                return EndTime.ToLocalTime ();
            }
        }

        public void SetReminder (uint reminderMinutes)
        {
            ReminderTime = GetStartTimeUtc () - new TimeSpan (reminderMinutes * TimeSpan.TicksPerMinute);
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

        /// <summary>
        /// Return the event that is currently in progress, or the next one to start.
        /// Ignore events associated with a canceled meeting.  Ignore events that are
        /// more than a week away.  Return null if no event is found.
        /// </summary>
        public static McEvent GetCurrentOrNextEvent()
        {
            // Due to the way that all-day events are stored, the StartTime field of the event may be different than
            // the start time that we want to present to the user.  So we have to handle the case where the database
            // returns events in what appears to be the wrong order.
            DateTime now = DateTime.UtcNow;
            DateTime weekInFuture = now + new TimeSpan (7, 0, 0, 0);
            McEvent result = null;
            foreach (var evt in NcModel.Instance.Db.Table<McEvent> ().Where (x => x.EndTime >= now && x.StartTime < weekInFuture).OrderBy (x => x.StartTime)) {
                var cal = evt.GetCalendarItemforEvent ();
                if (null != cal && (NcMeetingStatus.MeetingOrganizerCancelled == cal.MeetingStatus || NcMeetingStatus.MeetingAttendeeCancelled == cal.MeetingStatus)) {
                    // A meeting that has been canceled.  Ignore it.
                    continue;
                }
                if (null == result) {
                    // The first event that we have looked at.  Make a note of it and keep looking.
                    result = evt;
                } else if (evt.GetStartTimeUtc () < result.GetStartTimeUtc ()) {
                    // Found an out-of-order event.  None of the later events will be any earlier, so we can quit now.
                    result = evt;
                    break;
                } else if (result.AllDayEvent && evt.GetStartTimeUtc () > result.GetStartTimeUtc ()) {
                    // None of the later events will be any earlier than the all-day event that we already have,
                    // so we can quit now.
                    break;
                } else if (!result.AllDayEvent && (evt.AllDayEvent || evt.GetStartTimeUtc () > DateTime.SpecifyKind (result.GetStartTimeLocal ().Date, DateTimeKind.Utc))) {
                    // We found an event whose starting time (as recorded in the database) is later than the
                    // starting time of an all-day event on the current day.  Therefore, if we keep looking we
                    // will not find any all-day events that start earlier than the non-all-day event that we
                    // already have.  So we can quit now.
                    break;
                }
            }
            return result;
        }

        /// <summary>
        /// All events in the database in chronological order.
        /// </summary>
        public static List<McEvent> QueryAllEventsInOrder ()
        {
            return NcModel.Instance.Db.Table<McEvent> ().OrderBy (v => v.StartTime).ToList ();
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

        /// <summary>
        /// All events associated wih the given calendar item that start after the given date.
        /// </summary>
        public static IEnumerable<McEvent> QueryEventsForCalendarItemAfter (int calendarId, DateTime after)
        {
            return NcModel.Instance.Db.Table<McEvent> ()
                .Where (e => e.CalendarId == calendarId && e.StartTime > after);
        }

        /// <summary>
        /// Return the IDs for all of the McEvents associated with the given calendar item.
        /// </summary>
        public static List<NcEventIndex> QueryEventIdsForCalendarItem (int calendarId)
        {
            return NcModel.Instance.Db.Query<NcEventIndex> (
                "SELECT e.Id as Id FROM McEvent AS e WHERE e.CalendarId = ?", calendarId);
        }

        /// <summary>
        /// Delete all the McEvents associated with the given calendar item. This method does not cancel
        /// any local notifications for the events.  Callers of this method must handle local notification
        /// cancelation themselves.
        /// </summary>
        /// <returns>The number of McEvents that were deleted.</returns>
        public static int DeleteEventsForCalendarItem (int calendarId)
        {
            return NcModel.Instance.Db.Execute (
                "DELETE FROM McEvent WHERE CalendarId = ?", calendarId);
        }
    }
}
