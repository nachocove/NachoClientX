//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;

namespace NachoCore
{
    public class NachoCalendarCommon : INachoCalendar
    {
        protected List<McCalendar> list;
        protected Dictionary<DateTime, Dictionary<DateTime, int>> bag;
        protected List<DateTime> listOfDays;
        protected List<int>[] listOfDaysEvents;

        protected NachoCalendarCommon ()
        {
            Refresh ();
        }

        public void Refresh ()
        {
            Reload ();
            bag = new Dictionary<DateTime, Dictionary<DateTime, int>> ();
            if (null == list) {
                listOfDays = new List<DateTime> ();
                listOfDaysEvents = new List<int>[0];
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
            listOfDays = bag.Keys.ToList ();
            listOfDays.Sort ();
            listOfDaysEvents = new List<int>[listOfDays.Count];
        }

        protected virtual void Reload ()
        {
            throw new Exception("You must implement Reload()");
        }

        protected void AddItem (DateTime d, int i)
        {
            Dictionary<DateTime, int> day;
            if (false == bag.TryGetValue (d.Date, out day)) {
                day = new Dictionary<DateTime, int> ();
                bag.Add (d.Date, day);
            }
            day.Add (d, i);
        }

        public int NumberOfDays ()
        {
            return listOfDays.Count;
        }

        public int IndexOfDate (DateTime target)
        {
            target = target.Date;
            for (var i = 0; i < listOfDays.Count; i++) {
                if (listOfDays [i] <= target) {
                    return i;
                }
            }
            return Math.Max (0, listOfDays.Count - 1);
        }

        public int NumberOfItemsForDay (int i)
        {
            if (null == listOfDaysEvents [i]) {
                listOfDaysEvents [i] = bag [listOfDays [i]].Values.ToList ();
            }
            return listOfDaysEvents [i].Count;
        }

        public DateTime GetDayDate(int day)
        {
            return listOfDays [day];
        }

        public McCalendar GetCalendarItem (int day, int item)
        {
            return GetCalendarItem (listOfDaysEvents [day] [item]);
        }

        public McCalendar GetCalendarItem (int i)
        {
            var c = list.ElementAt (i);
            c.ReadAncillaryData ();
            return c;
        }
    }
}
