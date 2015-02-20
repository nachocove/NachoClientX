//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore
{
    /// <summary>
    /// By default, there is a list of days that have events associated with them.
    /// The function NumberOfDays returns the size of this table.
    /// Given a day index, you can get the date of that day using GetDateUsingDayIndex.
    /// Given a day index, you can get the number of events for the day using NumberOfItemsForDay.
    /// Given a day index and an item index, you can get an McCalendar using GetCalendarItem.
    /// </summary>
    public interface INcEventProvider
    {
        void Refresh (Action completionAction);

        int NumberOfDays ();

        int NumberOfItemsForDay (int i);

        int IndexOfDate (DateTime target);

        DateTime GetDateUsingDayIndex (int day);

        McEvent GetEvent (int day, int item);

        McAbstrCalendarRoot GetEventDetail (int day, int item);

        int ExtendEventMap (DateTime untilDate);

        bool FindEventNearestTo (DateTime date, out int item, out int section);

        void StopTrackingEventChanges ();
    }
}