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

        public int MaxScheduledCount { get { return 10000; /* FIXME */} }
        public int ScheduledCount { get { return 0; /* FIXME */} }

        public void ScheduleNotif (int handle, DateTime when, string message)
        {
            NcAssert.True (false);
        }

        public void CancelNotif (int handle)
        {
            NcAssert.True (false);
        }

        public void CancelNotif (List<int> handles)
        {
            NcAssert.True (false);
        }
    }
}
