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

        private Action refreshAction = null;

        private object thisLock = new object ();

        private int refreshCount = 0;
        private object refreshCountLock = new object ();

        /// <summary>
        /// Get the events that should be displayed.  The list of events must be in chronological
        /// order.
        /// </summary>
        protected abstract List<McEvent> GetEventsWithDuplicates (DateTime start, DateTime end);

        protected NcEventsCalendarMapCommon (DateTime end)
        {
            events = new List<McEvent> ();
            days = new int[1] { 0 };
            firstDay = NcEventManager.BeginningOfEventsOfInterest.ToLocalTime ().Date;
            finalDay = firstDay;
            ExtendEventMap (end.ToLocalTime ());
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public Action UiRefresh {
            set {
                refreshAction = value;
            }
        }

        public int IndexOfDate (DateTime date)
        {
            if (firstDay <= date && date < finalDay) {
                return UnsafeIndexOfDate (date);
            } else {
                return -1;
            }
        }

        private int UnsafeIndexOfDate (DateTime date)
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

            NcEventManager.AddEventWindow (this, untilDate.ToUniversalTime ());

            int numDays = UnsafeIndexOfDate (untilDate);
            int numNewDays = numDays - NumberOfDays ();

            int[] newDays = new int[numDays + 1];
            Array.Copy (days, newDays, days.Length);
            for (int day = days.Length; day < newDays.Length; ++day) {
                newDays [day] = int.MaxValue;
            }
            int endEvent = events.Count;
            for (int e = days [NumberOfDays ()]; e < events.Count; ++e) {
                DateTime start = events [e].GetStartTimeLocal ();
                if (start >= untilDate) {
                    endEvent = e;
                    break;
                }
                if (firstDay <= start) {
                    for (int day = UnsafeIndexOfDate (start); 0 <= day && int.MaxValue == newDays [day]; --day) {
                        newDays [day] = e;
                    }
                }
            }
            for (int day = numDays; 0 <= day && int.MaxValue == newDays [day]; --day) {
                newDays [day] = endEvent;
            }

            finalDay = untilDate;
            days = newDays;

            Refresh ();

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

        public void IndexToDayItem (int index, out int day, out int item)
        {
            index += days [0];
            for (int i = 0; i < days.Length - 1; ++i) {
                if (index <= i + days [i + 1]) {
                    day = i;
                    item = index - (i + days [i] + 1);
                    return;
                }
            }
            day = 0;
            item = -1;
        }

        public int IndexFromDayItem (int day, int item)
        {
            return day + days [day] + item + 1 - days [0];
        }

        public int NumberOfEvents()
        {
            // Events before the starting date or after the ending date are not counted.
            return days [days.Length - 1] - days [0];
        }

        public DateTime GetDateUsingDayIndex (int day)
        {
            return firstDay.AddDays (day);
        }

        public McEvent GetEvent (int day, int item)
        {
            return events [days [day] + item];
        }

        public bool FindEventNearestTo (DateTime date, out int item, out int section)
        {
            date = date.ToLocalTime ();
            int day = IndexOfDate (date);
            if (0 <= day) {
                for (int i = days [day]; i < events.Count; ++i) {
                    if (date <= events [i].GetStartTimeLocal ()) {
                        while (days [day + 1] <= i) {
                            ++day;
                        }
                        section = day;
                        item = i - days [day];
                        return true;
                    }
                }
            }
            section = -1;
            item = -1;
            return false;
        }

        private void Refresh ()
        {
            lock (refreshCountLock) {
                if (2 <= refreshCount) {
                    // There is already one refresh running and one queued up to start.
                    // There is no point in starting yet another one.
                    return;
                }
                ++refreshCount;
            }

            // Most of the work needs to happen on a background thread, because GetEvents() can
            // take a long time.
            NcTask.Run (delegate {

                lock (thisLock) {

                    try {
                        // Make a copy of the end date, in case it changes while the events are being processed.
                        DateTime untilDate = finalDay;

                        PauseOrCancel ();

                        // Find all the events that are to be displayed.
                        var newEvents = GetEvents (firstDay, untilDate);

                        PauseOrCancel ();

                        // The start times for all-day events are stored differently from the start times
                        // for regular events.  This can result in the database returning the events in a
                        // slightly different order than what we want.  So the events need to be sorted.
                        // Hopefully this is fast, since the events should be in almost the correct order.
                        newEvents.Sort ((McEvent x, McEvent y) => {
                            TimeSpan diff = x.GetStartTimeUtc () - y.GetStartTimeUtc ();
                            if (0 > diff.Ticks) {
                                return -1;
                            } else if (0 < diff.Ticks) {
                                return 1;
                            } else {
                                return 0;
                            }
                        });

                        PauseOrCancel ();

                        int numDays = UnsafeIndexOfDate (untilDate);
                        int[] newDays = new int[numDays + 1];
                        for (int day = 0; day < newDays.Length; ++day) {
                            newDays [day] = int.MaxValue;
                        }
                        int endEvent = newEvents.Count;

                        // Iterate over all the events, figuring out on which day they occur.
                        for (int e = 0; e < newEvents.Count; ++e) {
                            DateTime start = newEvents [e].GetStartTimeLocal ();
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

                        PauseOrCancel ();

                        // To avoid race conditions, updating "this" with the new values has to happen on the UI thread.
                        // The completion action also has to be run on the UI thread.
                        NachoPlatform.InvokeOnUIThread.Instance.Invoke (delegate {
                            if (untilDate != finalDay) {
                                // ExtendEventMap was called while Refresh's task was in progress.
                                // The whole Refresh operation has to be redone.
                                Refresh ();
                            } else {
                                events = newEvents;
                                days = newDays;
                                if (null != refreshAction) {
                                    refreshAction ();
                                }
                            }
                        });
                    } finally {
                        lock (refreshCountLock) {
                            --refreshCount;
                        }
                    }
                }
            }, "NcEventsCalendarMapCommonRefresh");
        }

        private void PauseOrCancel ()
        {
            NcTask.Cts.Token.ThrowIfCancellationRequested ();
            if (null == refreshAction) {
                NcAbate.PauseWhileAbated ();
            }
        }

        private List<McEvent> GetEvents (DateTime start, DateTime end)
        {
            var deviceAccount = McAccount.GetDeviceAccount ().Id;
            var currentAccount = NcApplication.Instance.Account.Id;

            var result = GetEventsWithDuplicates (start, end);

            // Go through the list of events, removing duplicates.  In the initial pass, set any duplicate events
            // to null.  Then remove any null events in a single call to RemoveAll().  This two pass approach
            // simplifies the bookkeeping during the first pass and reduces the amount of copying that needs to
            // happen.
            for (int i = 0; i < result.Count; ++i) {
                var event1 = result [i];
                if (null == event1 || null == event1.UID) {
                    continue;
                }
                for (int j = i + 1; j < result.Count && (null == result[j] || event1.StartTime == result[j].StartTime); ++j) {
                    var event2 = result [j];
                    if (null != event2 && event1.UID == event2.UID) {
                        // Two events start at the same time and have the same UID.  Get rid of one of them.
                        // Prefer a non-device account over the device account.
                        // Prefer the currently active account over other accounts.
                        // If they are still tied, pick the first one in the list.
                        if ((deviceAccount == event1.AccountId && deviceAccount != event2.AccountId) ||
                            (currentAccount != event1.AccountId && currentAccount == event2.AccountId))
                        {
                            result [i] = null;
                            break;
                        } else {
                            result [j] = null;
                        }
                    }
                }
            }

            result.RemoveAll (delegate(McEvent obj) {
                return null == obj;
            });

            return result;
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {

            case NcResult.SubKindEnum.Info_EventSetChanged:
            case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                Refresh ();
                break;
            }
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
        public NcAllEventsCalendarMap (DateTime end)
            : base(end)
        {
        }

        protected override List<McEvent> GetEventsWithDuplicates (DateTime start, DateTime end)
        {
            return McEvent.QueryEventsInRangeInOrder (start, end);
        }
    }
}
