//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore
{
    public class NcEventsCommon : INcEventProvider
    {
        protected List<McEvent> list;
        protected List<List<int>> listOfEventsOnADay;

        protected DateTime firstDayInList;
        protected DateTime finalDayInList;

        protected NcEventsCommon ()
        {
            Refresh ();
        }

        protected DateTime LocalT (DateTime date)
        {
            if (DateTimeKind.Local == date.Kind) {
                return date;
            } else {
                return date.ToLocalTime ();
            }
        }

        protected DateTime MidnightOf (DateTime date)
        {
            return new DateTime (date.Year, date.Month, date.Day, 0, 0, 0, 0, DateTimeKind.Local);
        }

        public int IndexOfDate (DateTime date)
        {
            date = LocalT (date);
            var ts = date - firstDayInList;
            return ts.Days;
        }

        protected void Initialize ()
        {
            listOfEventsOnADay = new List<List<int>> ();
        }

        public void ExpandRecurrences ()
        {
            CalendarHelper.ExpandRecurrences (finalDayInList);
        }

        // We extend from midnight of the previous finalDayInList to
        // midnight of the date after the until date. This insures that
        // each per-day bin won't change size, which is important to iOS
        // UITableView's InsertSections call.
        public int ExtendEventMap (DateTime untilDate)
        {
            var sentinelDate = MidnightOf (untilDate).AddDays (1);

            if (sentinelDate < finalDayInList) {
                sentinelDate = finalDayInList;
            }

            var daysInList = IndexOfDate (sentinelDate);
            var daysAdded = daysInList - listOfEventsOnADay.Count;

            CalendarHelper.ExpandRecurrences (sentinelDate);
            Reload ();

            listOfEventsOnADay = new List<List<int>> (daysInList);
            for (int i = 0; i < daysInList; i++) {
                listOfEventsOnADay.Add (null);
            }

            for (var i = 0; i < list.Count; i++) {
                var e = list [i];
                var startTime = e.StartTime.LocalT ();
                if ((firstDayInList <= startTime) && (sentinelDate > startTime)) {
                    AddItem (IndexOfDate (startTime), i);
                }
            }
            finalDayInList = sentinelDate;
            return daysAdded;
        }

        protected const int startingOffsetInDays = 30;

        public void Refresh ()
        {
            // Update list from data base
            Reload ();
            listOfEventsOnADay = new List<List<int>> ();
            firstDayInList = MidnightOf (DateTime.Today).AddDays (-startingOffsetInDays);
            finalDayInList = firstDayInList;
            // Initialize the table
            ExtendEventMap (firstDayInList.AddDays (6 * startingOffsetInDays));
        }

        protected virtual void Reload ()
        {
            throw new Exception ("You must implement Reload()");
        }

        protected void AddItem (int index, int i)
        {
            if (null == listOfEventsOnADay.ElementAt (index)) {
                listOfEventsOnADay [index] = new List<int> ();
            }
            listOfEventsOnADay [index].Add (i);
        }

        public int NumberOfDays ()
        {
            return listOfEventsOnADay.Count;
        }

        public int NumberOfItemsForDay (int i)
        {
            if (null == listOfEventsOnADay [i]) {
                return 0;
            } else {
                return listOfEventsOnADay [i].Count;
            }
        }

        public DateTime GetDateUsingDayIndex (int day)
        {
            return firstDayInList.AddDays (day);
        }

        public McEvent GetEvent (int day, int item)
        {
            NcAssert.True (0 != listOfEventsOnADay.Count, "List is empty");
            NcAssert.True (day >= 0, "Day is negative");
            NcAssert.True (day < listOfEventsOnADay.Count, "Day greater than or equal to list count");
            NcAssert.True (item >= 0, "Day is negative");
            NcAssert.True (item < listOfEventsOnADay [day].Count, "Day greater than or equal to list count");
            return GetEvent (listOfEventsOnADay [day] [item]);
        }

        public McAbstrCalendarRoot GetEventDetail (int day, int item)
        {
            var e = GetEvent (day, item);
            if (null == e) {
                return null;
            }
            if (0 == e.ExceptionId) {
                return McCalendar.QueryById<McCalendar> (e.CalendarId);
            } else {
                return McException.QueryById<McException> (e.ExceptionId);
            }
        }

        public McEvent GetEvent (int i)
        {
            var e = list.ElementAt (i);
            return e;
        }

        public bool FindEventNearestTo (DateTime date, out int item, out int section)
        {
            date = LocalT (date);
            for (int i = IndexOfDate (date); i < listOfEventsOnADay.Count; i++) {
                if (null == listOfEventsOnADay [i]) {
                    continue;
                }
                for (int j = 0; j < listOfEventsOnADay [i].Count; j++) {
                    var e = listOfEventsOnADay [i] [j];
                    if (date <= list [e].StartTime.LocalT ()) {
                        item = j;
                        section = i;
                        return true;
                    }
                }
            }
            item = -1;
            section = -1;
            return false;
        }

    }
}
