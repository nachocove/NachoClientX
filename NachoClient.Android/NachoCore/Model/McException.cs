//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.Model
{
    /// <summary>
    /// List of exceptions associated with the calendar entry
    /// </summary>
    public partial class McException : McAbstrCalendarRoot
    {
        [Indexed]
        public Int64 CalendarId { get; set; }

        /// Has this exception been deleted?  Exception only.
        public uint Deleted { get; set; }

        /// Start time of the original recurring meeting (Compact DateTime). Exception only.
        public DateTime ExceptionStartTime { get; set; }

        /// <summary>
        /// Calendar events don't need to know whether or not AllDayEvent was present in the XML,
        /// because a missing AllDayEvent always means the same thing as setting it to false.
        /// But exceptions to a recurring meeting do need to know that, because a missing AllDayEvent
        /// means "inherit the value from the parent item," not that the value is false.
        /// </summary>
        public bool AllDayEventIsSet { get; set; }

        public override string GetSubject ()
        {
            return this.Subject ?? CalendarItemOrSelf ().Subject;
        }

        public override string GetLocation ()
        {
            return this.Location ?? CalendarItemOrSelf ().Location;
        }

        public override bool HasReminder ()
        {
            return this.ReminderIsSet || CalendarItemOrSelf ().ReminderIsSet;
        }

        public override uint GetReminder ()
        {
            return this.ReminderIsSet ? this.Reminder : CalendarItemOrSelf ().Reminder;
        }

        public override bool HasResponseType ()
        {
            return this.ResponseTypeIsSet || CalendarItemOrSelf ().ResponseTypeIsSet;
        }

        public override NcResponseType GetResponseType ()
        {
            return this.ResponseTypeIsSet ? this.ResponseType : CalendarItemOrSelf ().ResponseType;
        }

        [Ignore]
        public override IList<McAttendee> attendees {
            get {
                var exceptionAttendees = base.attendees;
                if (0 == exceptionAttendees.Count) {
                    var calendarItem = CalendarItem ();
                    if (null != calendarItem) {
                        return calendarItem.attendees;
                    }
                }
                return exceptionAttendees;
            }
            set {
                base.attendees = value;
            }
        }

        [Ignore]
        public override IList<McCalendarCategory> categories {
            get {
                var exceptionCategories = base.categories;
                if (0 == exceptionCategories.Count) {
                    var calendarItem = CalendarItem ();
                    if (null != calendarItem) {
                        return calendarItem.categories;
                    }
                }
                return exceptionCategories;
            }
            set {
                base.categories = value;
            }
        }

        private McCalendar cachedCal = null;

        private McCalendar CalendarItem ()
        {
            if (0 == CalendarId || 0 != Deleted) {
                return null;
            }
            if (null == cachedCal || CalendarId != cachedCal.Id) {
                cachedCal = McCalendar.QueryById<McCalendar> ((int)CalendarId);
            }
            return cachedCal;
        }

        private McAbstrCalendarRoot CalendarItemOrSelf ()
        {
            return (McAbstrCalendarRoot)CalendarItem () ?? this;
        }

        public static List<McException> QueryForExceptionId (int calendarId, DateTime exceptionStartTime)
        {
            var query = "SELECT * from McException WHERE CalendarId = ? AND ExceptionStartTime = ?";
            var result = NcModel.Instance.Db.Query<McException> (query, calendarId, exceptionStartTime).ToList ();
            return result;
        }

        /// <summary>
        /// All of the exceptions associated with the given calendar item.
        /// </summary>
        public static IEnumerable<McException> QueryExceptionsForCalendarItem (int calendarId)
        {
            return NcModel.Instance.Db.Table<McException> ().Where (x => x.CalendarId == calendarId);
        }
    }
}
