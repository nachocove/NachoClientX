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

        /// <summary>
        /// The UID of the root calendar item.  It is stored in the McEvent to avoid database lookups when
        /// eliminating duplicates from the calendar view.  The UID is not unique to this McEvent when
        /// (1) this is one occurrence in a recurring meeting, or (2) the same event is on multiple calendars
        /// that are being tracked by the app.
        /// </summary>
        public string UID { get; set; }

        static public McEvent Create (int accountId, DateTime startTime, DateTime endTime, string UID, bool allDayEvent, int calendarId, int exceptionId)
        {
            // Save the event
            var e = new McEvent ();
            e.AccountId = accountId;
            e.StartTime = startTime;
            e.EndTime = endTime;
            e.UID = UID;
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
            // Don't set a reminder if the event came from a device calendar.  The device's calendar app should handle those notifications.
            // A notification from Nacho Mail would probably be a duplicate.
            if (AccountId != McAccount.GetDeviceAccount ().Id) {
                ReminderTime = GetStartTimeUtc () - TimeSpan.FromMinutes (reminderMinutes);
                Update ();
                LocalNotificationManager.ScheduleNotification (this);
            }
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

        public static TableQuery<McEvent> UpcomingEvents (TimeSpan window)
        {
            DateTime now = DateTime.UtcNow;
            DateTime end = now.Add (window);
            return NcModel.Instance.Db.Table<McEvent> ().Where (x => x.EndTime >= now && x.StartTime < end).OrderBy (x => x.StartTime);
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
            NcAssert.True (NcModel.Instance.IsInTransaction ());
            return NcModel.Instance.Db.Execute (
                "DELETE FROM McEvent WHERE CalendarId = ?", calendarId);
        }
    }
}
