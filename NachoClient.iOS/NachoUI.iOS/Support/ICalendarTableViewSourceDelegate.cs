//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface ICalendarTableViewSourceDelegate
    {

        void SendRunningLateMessage (int calendarIndex);

        void ForwardInvite (int calendarIndex);

        void CalendarTableViewScrollingEnded ();

        void ReturnToToday ();

        void CreateEvent (DateTime date);

        void ShowEvent (McEvent calendarEvent);

    }
}

