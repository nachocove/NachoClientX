﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore
{
    /// <summary>
    /// Maintain a mapping between events and the days on a calendar.  This is intended to be used
    /// by the calendar view.  This class is abstract.  The one thing that must be implemented by
    /// the derived class is choosing which events to display.
    /// </summary>
    public abstract class NcEventsCalendarMapCommon : INcEventProvider
    {
        // The events to be displayed.  The events must be in chronological order.
        private List<McEvent> events;

        // Each element in the array represents a day whose events are being tracked.
        // The first day being tracked is days[0].  The day after that is days[1].
        // The value for a day is the index into "events" of the first event on or
        // after the given day.
        private int[] days;

        // The beginning and end of the time period being tracked.  These times should be
        // midnight on the given days.
        private DateTime firstDay;
        private DateTime finalDay;

        private bool isActive = false;

        private const int startingOffsetInDays = 30;

        /// <summary>
        /// Get the events that should be displayed.  The list of events must be in chronological
        /// order.
        /// </summary>
        protected abstract List<McEvent> GetEvents ();

        protected NcEventsCalendarMapCommon ()
        {
            events = new List<McEvent> ();
            days = new int[1] { 0 };
            firstDay = DateTime.Today.AddDays (-startingOffsetInDays);
            finalDay = firstDay;
            ExtendEventMap (firstDay.AddDays (6 * startingOffsetInDays));
        }

        public int IndexOfDate (DateTime date)
        {
            return (date.ToLocalTime () - firstDay).Days;
        }

        public int ExtendEventMap (DateTime untilDate)
        {
            untilDate = NextMidnight (untilDate);
            if (untilDate <= finalDay) {
                return 0;
            }

            // Extend the map, filling it in with any events that happen to exist.  Do this immediately,
            // so the caller can access the new days as soon as this method returns.  The events for the
            // new days will be filled in on a background thread, and an EventSetChanged status will be
            // fired when everything is ready.

            if (isActive) {
                NcEventManager.AddEventWindow (this, untilDate.ToUniversalTime ());
            }

            int numDays = IndexOfDate (untilDate);
            int numNewDays = numDays - NumberOfDays ();

            int[] newDays = new int[numDays + 1];
            Array.Copy (days, newDays, days.Length);
            for (int day = days.Length; day < newDays.Length; ++day) {
                newDays [day] = int.MaxValue;
            }
            int endEvent = events.Count;
            for (int e = days [NumberOfDays ()]; e < events.Count; ++e) {
                DateTime start = events [e].StartTime.ToLocalTime ();
                if (start >= untilDate) {
                    endEvent = e;
                    break;
                }
                if (firstDay <= start) {
                    for (int day = IndexOfDate (start); 0 <= day && int.MaxValue == newDays [day]; --day) {
                        newDays [day] = e;
                    }
                }
            }
            for (int day = numDays; 0 <= day && int.MaxValue == newDays [day]; --day) {
                newDays [day] = endEvent;
            }

            finalDay = untilDate;
            days = newDays;

            return numNewDays;
        }

        public int NumberOfDays ()
        {
            return days.Length - 1;
        }

        public int NumberOfItemsForDay (int day)
        {
            return days [day + 1] - days [day];
        }

        public DateTime GetDateUsingDayIndex (int day)
        {
            return firstDay.AddDays (day);
        }

        public McEvent GetEvent (int day, int item)
        {
            return events [days [day] + item];
        }

        public McAbstrCalendarRoot GetEventDetail (int day, int item)
        {
            var evt = GetEvent (day, item);
            if (0 == evt.ExceptionId) {
                return McCalendar.QueryById<McCalendar> (evt.CalendarId);
            } else {
                return McException.QueryById<McException> (evt.ExceptionId);
            }
        }

        public bool FindEventNearestTo (DateTime date, out int item, out int section)
        {
            date = date.ToLocalTime ();
            int day = IndexOfDate (date);
            for (int i = days [day]; i < events.Count; ++i) {
                if (date <= events [i].StartTime.ToLocalTime ()) {
                    while (days [day + 1] <= i) {
                        ++day;
                    }
                    section = day;
                    item = i - days [day];
                    return true;
                }
            }
            section = -1;
            item = -1;
            return false;
        }

        public void StopTrackingEventChanges ()
        {
            NcEventManager.RemoveEventWindow (this);
            isActive = false;
        }

        public void Refresh (Action completionAction)
        {
            if (!isActive) {
                NcEventManager.AddEventWindow (this, finalDay.ToUniversalTime ());
                isActive = true;
            }

            // Most of the work needs to happen on a background thread, because GetEvents() can
            // take a long time.
            NcTask.Run (delegate {

                lock (this) {

                    // Find all the events that are to be displayed.
                    var newEvents = GetEvents ();

                    // Make a copy of the end date, in case it changes while the events are being processed.
                    DateTime untilDate = finalDay;

                    int numDays = IndexOfDate (untilDate);
                    int[] newDays = new int[numDays + 1];
                    for (int day = 0; day < newDays.Length; ++day) {
                        newDays [day] = int.MaxValue;
                    }
                    int endEvent = newEvents.Count;

                    // Iterate over all the events, figuring out on which day they occur.
                    for (int e = 0; e < newEvents.Count; ++e) {
                        DateTime start = newEvents [e].StartTime.ToLocalTime ();
                        if (start >= untilDate) {
                            // This event is after our end date.  We can stop processing.
                            endEvent = e;
                            break;
                        }
                        if (firstDay <= start) {
                            // If this is the first event of the day, set the day's entry to this event.
                            // And also set the entry for any previous day that hasn't already been set
                            // to this event.
                            for (int day = IndexOfDate (start); 0 <= day && int.MaxValue == newDays [day]; --day) {
                                newDays [day] = e;
                            }
                        }
                    }
                    // Take care of days at the very end that don't have an event yet.
                    for (int day = numDays; 0 <= day && int.MaxValue == newDays [day]; --day) {
                        newDays [day] = endEvent;
                    }

                    // To avoid race conditions, updating "this" with the new values has to happen on the UI thread.
                    // The completion action also has to be run on the UI thread.
                    NachoPlatform.InvokeOnUIThread.Instance.Invoke (delegate {
                        if (untilDate != finalDay) {
                            // ExtendEventMap was called while Refresh's task was in progress.
                            // The whole Refresh operation has to be redone.
                            Refresh (completionAction);
                        } else {
                            events = newEvents;
                            days = newDays;
                            if (null != completionAction) {
                                completionAction ();
                            }
                        }
                    });
                }
            }, "NcEventsCalendarMapCommonRefresh");
        }

        /// <summary>
        /// Return the midnight that is the same or later than the given date/time
        /// </summary>
        private DateTime NextMidnight (DateTime time)
        {
            DateTime midnight = time.ToLocalTime ().Date;
            if (midnight != time) {
                midnight = midnight.AddDays (1);
            }
            return midnight;
        }
    }

    /// <summary>
    /// A mapping between events and days on a calendar where all events are displayed.
    /// </summary>
    public class NcAllEventsCalendarMap : NcEventsCalendarMapCommon
    {
        protected override List<McEvent> GetEvents ()
        {
            return McEvent.QueryAllEventsInOrder ();
        }
    }
}
