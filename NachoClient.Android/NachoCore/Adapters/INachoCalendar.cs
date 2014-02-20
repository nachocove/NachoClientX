//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore
{
    public interface INachoCalendar
    {
        int Count ();
        void Refresh ();
        McCalendar GetCalendarItem (int i);
    }
}