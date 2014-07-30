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
    public class NachoCalendarCommon : INachoCalendar
    {
        protected List<McCalendar> list;
        protected Dictionary<DateTime, List<int>> bag;
        protected List<DateTime> listOfDaysThatHaveEvents;
        protected List<int>[] listOfEventsOnADay;

        protected NachoCalendarCommon ()
        {
            Refresh ();
        }

        public void Refresh ()
        {
            Reload ();
            bag = new Dictionary<DateTime, List<int>> ();
            if (null == list) {
                listOfDaysThatHaveEvents = new List<DateTime> ();
                listOfEventsOnADay = new List<int>[0];
                return;
            }
            for(var i = 0; i < list.Count; i++) {
                var c = list [i];
                if (c.AllDayEvent) {
                    var s = c.StartTime.Date;
                    while (s <= c.EndTime.Date) {
                        AddItem (s.Date, i);
                        s = s.AddDays (1.0d);
                    }
                } else {
                    AddItem (c.StartTime, i);
                }
            }
            listOfDaysThatHaveEvents = bag.Keys.ToList ();
            listOfDaysThatHaveEvents.Sort ();
            listOfEventsOnADay = new List<int>[listOfDaysThatHaveEvents.Count];
        }

        protected virtual void Reload ()
        {
            throw new Exception("You must implement Reload()");
        }

        protected void AddItem (DateTime d, int i)
        {
            List<int> day;
            if (false == bag.TryGetValue (d.Date, out day)) {
                day = new List<int> ();
                bag.Add (d.Date, day);
            }
            day.Add (i);
        }

        public int NumberOfDays ()
        {
            return listOfDaysThatHaveEvents.Count;
        }

        public int IndexOfDate (DateTime target)
        {
            //target = target.Date.AddMilliseconds (1.0);
            for (var i = 0; i < listOfDaysThatHaveEvents.Count; i++) {
                if (listOfDaysThatHaveEvents [i] >= target) {
                    return i;
                }
            }
            return listOfDaysThatHaveEvents.Count - 1;
        }

        public int IndexOfThisOrNext(DateTime target)
        {
            for (var i = 0; i < listOfDaysThatHaveEvents.Count; i++) {
                if (target >= listOfDaysThatHaveEvents [i]) {
                    return i;
                }
            }
            return Math.Max (0, listOfDaysThatHaveEvents.Count - 1);
        }

        public int NumberOfItemsForDay (int i)
        {
            if (null == listOfEventsOnADay [i]) {
                listOfEventsOnADay [i] = bag [listOfDaysThatHaveEvents [i]];
            }
            return listOfEventsOnADay [i].Count;
        }

        public DateTime GetDateUsingDayIndex(int day)
        {
            return listOfDaysThatHaveEvents [day];
        }

        public McCalendar GetCalendarItem (int day, int item)
        {
            NcAssert.True (0 != listOfEventsOnADay.Length, "List is empty");
            NcAssert.True (day >= 0, "Day is negative");
            NcAssert.True (day < listOfEventsOnADay.Length, "Day greater than or equal to list count");
            NcAssert.True (item >= 0, "Day is negative");
            NcAssert.True (item < listOfEventsOnADay [day].Count, "Day greater than or equal to list count");
            return GetCalendarItem (listOfEventsOnADay [day] [item]);
        }

        public McCalendar GetCalendarItem (int i)
        {
            var c = list.ElementAt (i);
            return c;
        }
    }
}
