//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;

namespace NachoClient.iOS
{
    public interface ICalendarTableViewSourceDelegate
    {
        void PerformSegueForDelegate (string identifier, NSObject sender);

        void SendRunningLateMessage (int calendarIndex);

        void ForwardInvite (int calendarIndex);

        void CalendarTableViewScrollingEnded ();

        void ReturnToToday ();

        void CreateEvent (DateTime date);

    }
}

