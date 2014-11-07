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

        public int ExceptionId { get; set; }

        static public McEvent Create (int accountId, DateTime startTime, DateTime endTime, int calendarId, int exceptionId)
        {
            // Save the event
            var e = new McEvent ();
            e.AccountId = accountId;
            e.StartTime = startTime;
            e.EndTime = endTime;
            e.CalendarId = calendarId;
            e.ExceptionId = exceptionId;
            e.Insert ();
            NachoCore.Utils.Log.Info (Utils.Log.LOG_DB, "McEvent create: {0} {1}", startTime, calendarId);
            if (0 != exceptionId) {
                NachoCore.Utils.Log.Info (Utils.Log.LOG_DB, "McException found: eventId={0} exceptionId={1}", e.Id, exceptionId);
            }
            return e;
        }

        public override int Delete ()
        {
            var notifier = NachoPlatform.Notif.Instance;
            notifier.CancelNotif (this.Id);
            return base.Delete ();
        }

        public McAbstrCalendarRoot GetCalendarItemforEvent()
        {
            if (0 != ExceptionId) {
                return McException.QueryById<McException> (ExceptionId);
            } else {
                return McCalendar.QueryById<McCalendar> (CalendarId);
            }
        }
    }
}

