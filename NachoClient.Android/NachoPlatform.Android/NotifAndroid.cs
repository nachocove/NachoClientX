//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoPlatform
{
    public sealed class Notif : IPlatformNotif
    {
        private static volatile Notif instance;
        private static object syncRoot = new Object();

        private Notif ()
        {
        }

        public static Notif Instance
        {
            get 
            {
                if (instance == null) 
                {
                    lock (syncRoot) 
                    {
                        if (instance == null) 
                            instance = new Notif ();
                    }
                }
                return instance;
            }
        }

        public void ImmediateNotification (int handle, string message)
        {
            NcAssert.True (false);
        }

        public void ScheduleNotification (int handle, DateTime when, string message)
        {
            Log.Info (Log.LOG_CALENDAR, "ScheduleNotification not implemented for Android.");
        }

        public void ScheduleNotification (NotificationInfo notification)
        {
            ScheduleNotification (notification.Handle, notification.When, notification.Message);
        }

        public void ScheduleNotifications (List<NotificationInfo> notifications)
        {
            foreach (var notification in notifications) {
                ScheduleNotification (notification);
            }
        }

        public void CancelNotification (int handle)
        {
            NcAssert.True (false);
        }

        public static void DumpNotifications ()
        {
        }
    }
}
