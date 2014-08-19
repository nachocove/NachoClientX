//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using SQLite;

using NachoCore.Model;

namespace NachoCore.Model
{
    public class McEvent : McAbstrObjectPerAcc
    {
        public McEvent ()
        {
        }

        [Indexed]
        public DateTime StartTime { get; set; }

        [Indexed]
        public DateTime EndTime { get; set; }

        public int CalendarId { get; set; }

        static public void Create(int accountId, DateTime startTime, DateTime endTime, int calendarId)
        {
            var e = new McEvent ();
            e.AccountId = accountId;
            e.StartTime = startTime;
            e.EndTime = endTime;
            e.CalendarId = calendarId;
            e.Insert ();
            NachoCore.Utils.Log.Info (Utils.Log.LOG_DB, "McEvent create: {0} {1}", startTime, calendarId);
        }
    }
}

