//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.UIKit;
using NachoClient.iOS;
using NachoCore;
using NachoCore.Model;

namespace NachoCore
{
    public class NachoCalendar : INachoCalendar
    {
        AppDelegate appDelegate { get; set; }

        List<NcCalendar> list;

        public NachoCalendar ()
        {
            appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
            list = appDelegate.Be.Db.Table<NcCalendar> ().OrderBy (c => c.StartTime).ToList ();
            if (null == list) {
                list = new List<NcCalendar> ();
            }
        }

        public int Count ()
        {
            return list.Count;
        }

        public NcCalendar GetCalendarItem (int i)
        {
            return list.ElementAt (i);
        }
    }
}
