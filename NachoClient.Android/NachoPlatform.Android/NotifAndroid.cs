//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
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

        // Badge number doesn't display on Android.
        public int BadgeNumber { get; set; }

        public void ScheduleNotif (int handle, DateTime when, string message, bool sound)
        {
            NcAssert.True (false);
        }

        public void CancelNotif (int handle)
        {
            NcAssert.True (false);
        }
    }
}
