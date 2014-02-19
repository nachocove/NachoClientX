//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using MonoTouch.UIKit;
using MonoTouch.EventKit;
using MonoTouch.Foundation;
using NachoClient.iOS;
using NachoCore.Model;

namespace NachoCore
{
    public class DeviceCalendar : INachoCalendar
    {
        AppDelegate appDelegate { get; set; }

        EKCalendarItem[] events;

        private static DateTime DecipherStartTime (EKCalendarItem i)
        {
            if (null == i) {
                return DateTime.MinValue;
            }
            EKEvent e = (EKEvent)i;
            if (null == e.StartDate) {
                return DateTime.MinValue;
            }
            return e.StartDate;
        }

        private static DateTime DecipherEndTime (EKCalendarItem i)
        {
            if (null == i) {
                return DateTime.MaxValue;
            }
            EKEvent e = (EKEvent)i;
            if (null == e.EndDate) {
                return DateTime.MaxValue;
            }
            return e.EndDate;
        }

        private static int sortByStartDate (EKCalendarItem a, EKCalendarItem b)
        {
            // TODO: Adjust for timezones.
            // TODO: Adjust for all day events.
            DateTime aStart = DecipherStartTime (a);
            DateTime bStart = DecipherStartTime (b);
            return aStart.CompareTo (bStart);
        }

        public DeviceCalendar ()
        {
            Refresh ();
        }

        public void Refresh()
        {
            appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;

            var calendars = appDelegate.EventStore.GetCalendars (EKEntityType.Event);

            // TODO: Fix this up
            DateTime startDate = DateTime.Now.AddDays (-90);
            DateTime endDate = DateTime.Now.AddDays (90);
            NSPredicate query = appDelegate.EventStore.PredicateForEvents (startDate, endDate, calendars);
            events = appDelegate.EventStore.EventsMatching (query);

            if (null == events) {
                events = new EKCalendarItem[0];
            }
            Array.Sort<EKCalendarItem> (events, sortByStartDate);
        }

        public int Count ()
        {
            return events.Length;
        }

        public McCalendar GetCalendarItem (int i)
        {
            var e = events [i];
            var c = new McCalendar ();
            c.StartTime = DecipherStartTime (e);
            c.EndTime = DecipherEndTime (e);
            c.Subject = e.Title;
            return c;
        }
    }
}

