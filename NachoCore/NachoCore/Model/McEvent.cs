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

        /// <summary>
        /// The identifier of the calendar item in the device calendar for this instance.  This field is used
        /// only for transitory McEvent objects that represent device calendar items.  This field is not stored
        /// in the database.  If it is non-zero, then this McEvent is an in-memory object only.
        /// </summary>
        [Ignore]
        public long DeviceEventId { get; set; }

        private McCalendar _Calendar;

        private McException _Exception;

        private bool? _IsRecurring;

        private bool? _HasAttendees;

        private McAccount _Account;

        [Ignore]
        public McCalendar Calendar {
            get {
                if (_Calendar == null) {
                    _Calendar = McCalendar.QueryById<McCalendar> (CalendarId);
                }
                return _Calendar;
            }
        }

        [Ignore]
        public McException Exception {
            get {
                if (_Exception == null && ExceptionId != 0) {
                    _Exception = McException.QueryById<McException> (ExceptionId);
                }
                return _Exception;
            }
        }

        [Ignore]
        public McAbstrCalendarRoot CalendarItem
        {
            get {
                return Exception != null ? Exception as McAbstrCalendarRoot : Calendar as McAbstrCalendarRoot;
            }
        }

        [Ignore]
        public McAccount Account {
            get {
                if (_Account == null) {
                    _Account = McAccount.QueryById<McAccount> (AccountId);
                }
                return _Account;
            }
        }

        [Ignore]
        public virtual bool IsRecurring {
            get {
                if (!_IsRecurring.HasValue) {
                    _IsRecurring = QueryRecurrences ().Count > 0;
                }
                return _IsRecurring.Value;
            }
        }

        [Ignore]
        public virtual bool HasAttendees {
            get {
                if (!_HasAttendees.HasValue) {
                    _HasAttendees = QueryAttendees ().Count > 0;
                }
                return _HasAttendees.Value;
            }
        }

        [Ignore]
        public virtual bool IsResponseRequested {
            get {
                return !IsAppointment && !IsOrganizer && CalendarItem.MeetingStatus != NcMeetingStatus.MeetingAttendeeCancelled;
            }
        }

        [Ignore]
        public virtual string Subject {
            get {
                return CalendarItem.GetSubject ();
            }
        }

        [Ignore]
        public virtual string OrganizerEmail {
            get {
                return Calendar.OrganizerEmail;
            }
        }

        [Ignore]
        public virtual string Location {
            get {
                return CalendarItem.GetLocation ();
            }
        }

        [Ignore]
        public virtual string PlainDescription {
        	get {
                return CalendarItem.PlainDescription;
            }
        }

        [Ignore]
        public virtual bool IsReminderSet {
        	get {
                return CalendarItem.ReminderIsSet;
            }
        }

        [Ignore]
        public virtual bool SupportsReminder {
        	get {
                return true;
            }
        }

        [Ignore]
        public virtual bool SupportsNote {
            get {
                return true;
            }
        }

        [Ignore]
        public virtual uint Reminder {
            get {
                return CalendarItem.Reminder;
            }
        }

        [Ignore]
        public virtual bool IsAppointment {
            get {
                return Calendar.IsAppointment;
            }
        }

        public virtual bool IsOrganizer {
            get {
                return Calendar.IsOrganizer;
            }
        }

        public virtual bool HasNonSelfOrganizer {
            get {
                return Calendar.HasNonSelfOrganizer;
            }
        }

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

        public bool IsValid ()
        {
            return Calendar != null && CalendarItem != null;
        }

        public virtual int GetColorIndex ()
        {
            int colorIndex = 0;
            var folder = McFolder.QueryByFolderEntryId<McCalendar> (AccountId, CalendarId).FirstOrDefault ();
            if (null != folder) {
                colorIndex = folder.DisplayColor;
            }
            return colorIndex;
        }

        public virtual string GetCalendarName()
        {
            return Calendar.GetCalendarName ();
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
        /// All events where at least part of the event is within the given range.  The events are returned in random order.
        /// </summary>
        public static List<McEvent> QueryEventsInRange (DateTime start, DateTime end)
        {
            start = start.ToUniversalTime ();
            end = end.ToUniversalTime ();
            return NcModel.Instance.Db.Table<McEvent> ().Where (x => x.EndTime >= start || x.StartTime < end).ToList ();
        }

        /// <summary>
        /// All events where at least part of the event is within the given range.  The events are returned in order of starting time.
        /// </summary>
        public static List<McEvent> QueryEventsInRangeInOrder (DateTime start, DateTime end)
        {
            start = start.ToUniversalTime ();
            end = end.ToUniversalTime ();
            return NcModel.Instance.Db.Table<McEvent> ().Where (x => x.EndTime >= start || x.StartTime < end).OrderBy (x => x.StartTime).ToList ();
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

        public virtual IList<McAttendee> QueryAttendees ()
        {
            return CalendarItem.attendees;
        }

        public virtual IList<McAttachment> QueryAttachments ()
        {
            return CalendarItem.attachments;
        }

        public virtual IList<McRecurrence> QueryRecurrences ()
        {
            return Calendar.recurrences;
        }

        public virtual McBody GetBody ()
        {
            return CalendarItem.GetBody ();
        }

        public virtual McNote QueryNote ()
        {
            return McNote.QueryByTypeId (CalendarId, McNote.NoteType.Event).FirstOrDefault ();
        }

        public virtual void UpdateReminder (bool isSet, uint reminder)
        {
            CalendarItem.ReminderIsSet = isSet;
            CalendarItem.Reminder = reminder;
            CalendarItem.Update ();
            BackEnd.Instance.UpdateCalCmd (AccountId, CalendarItem.Id, false);
        }

        public virtual void UpdateNote (string noteContent)
        {
            var note = QueryNote ();
            if (note != null) {
                note.noteContent = noteContent;
                note.Update ();
            } else {
                note = new McNote ();
                note.AccountId = AccountId;
                note.DisplayName = (Subject + " - " + Pretty.ShortDate (DateTime.UtcNow));
                note.TypeId = CalendarId;
                note.noteType = McNote.NoteType.Event;
                note.noteContent = noteContent;
                note.Insert ();
            }
        }
    }
}
