//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;

namespace NachoCore
{
    public class NachoCalendar : INachoCalendar
    {

        List<McCalendar> list;

        public NachoCalendar ()
        {
            Refresh ();
        }

        public void Refresh()
        {
            list = BackEnd.Instance.Db.Table<McCalendar> ().OrderByDescending (c => c.StartTime).ToList ();
            if (null == list) {
                list = new List<McCalendar> ();
            }
        }

        public int Count ()
        {
            return list.Count;
        }

        public McCalendar GetCalendarItem (int i)
        {
            return list.ElementAt (i);
        }
    }
}
