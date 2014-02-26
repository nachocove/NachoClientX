//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore
{
    public interface INachoCalendar
    {
        void Refresh ();

        int NumberOfDays ();

        int NumberOfItemsForDay (int i);

        int IndexOfDate (DateTime target);

        DateTime GetDayDate (int day);

        McCalendar GetCalendarItem (int day, int item);
    }
}