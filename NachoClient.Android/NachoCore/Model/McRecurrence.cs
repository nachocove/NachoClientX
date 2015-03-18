//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using SQLite;

namespace NachoCore.Model
{
    /// <summary>
    /// A calendar entry can have one or more
    /// recurrence records associated with it.
    /// </summary>
    public class McRecurrence : McAbstrObjectPerAcc
    {
        // FIXME - replace XxxId with a join (map?) table.

        /// Recurrence.  Calendar only.

        [Indexed]
        public int CalendarId { get; set; }

        [Indexed]
        public int TaskId { get; set; }

        [Indexed]
        public int MeetingRequestId { get; set; }

        ///The following fields define the
        /// Recurrence pattern of an event.
        /// Required for Task.
        public NcRecurrenceType Type { get; set; }

        /// Maximum is 999
        public int Occurrences { get; set; }

        public bool OccurrencesIsSet { get; set; }

        /// Interval between recurrences, range is 0 to 999
        public int Interval { get; set; }

        public bool IntervalIsSet { get; set; }

        /// The week of the month or the day of the month for the recurrence
        /// WeekOfMonth must be between 1 and 5; 5 is the last week of the month.
        public int WeekOfMonth { get; set; }

        public NcDayOfWeek DayOfWeek { get; set; }

        public bool DayOfWeekIsSet { get; set; }

        /// The month of the year for the recurrence, range is 1..12
        public int MonthOfYear { get; set; }

        /// Compact DateTime
        public DateTime Until { get; set; }

        /// The day of the month for the recurrence, range 1..31
        public int DayOfMonth { get; set; }

        public NcCalendarType CalendarType { get; set; }

        public bool CalendarTypeIsSet { get; set; }

        /// Takes place on the embolismic (leap) month
        public bool isLeapMonth { get; set; }

        /// Disambiguates recurrences across localities
        public int FirstDayOfWeek { get; set; }

        public bool FirstDayOfWeekIsSet { get; set; }

        /// Required for Task.
        public DateTime Start { get; set; }

        public McRecurrence ()
        {
            // Need this for LINQ.
        }

        public McRecurrence (int accountId) : this ()
        {
            AccountId = accountId;
        }

        public static McRecurrence QueryByTaskId (int taskId)
        {
            return NcModel.Instance.Db.Table<McRecurrence> ().Where (x => x.TaskId == taskId).SingleOrDefault ();
        }

        public override int Insert ()
        {
            if (0 != CalendarId) {
                Utils.NcAssert.True (0 == MeetingRequestId);
            }
            if (0 != MeetingRequestId) {
                Utils.NcAssert.True (0 == CalendarId);
            }
            Utils.NcAssert.True ((0 != CalendarId) || (0 != MeetingRequestId) || (0 != TaskId));
            return base.Insert ();
        }

        public override int Update ()
        {
            if (0 != CalendarId) {
                Utils.NcAssert.True (0 == MeetingRequestId);
            }
            if (0 != MeetingRequestId) {
                Utils.NcAssert.True (0 == CalendarId);
            }
            Utils.NcAssert.True ((0 != CalendarId) || (0 != MeetingRequestId) || (0 != TaskId));
            return base.Update ();
        }
    }
}

