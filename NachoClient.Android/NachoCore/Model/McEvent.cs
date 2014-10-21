//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using SQLite;

using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;

namespace NachoCore.Model
{
    public class McEvent : McAbstrObjectPerAcc
    {
        [Indexed]
        public DateTime StartTime { get; set; }

        [Indexed]
        public DateTime EndTime { get; set; }

        [Indexed]
        public DateTime NotifTime { get; set; }

        [Indexed]
        public bool IsScheduled { get; set; }

        public int CalendarId { get; set; }

        public int ExceptionId { get; set; }

        public string NotifMessage { get; set; }

        static object LockObj = new object ();

        static NcTimer Invoker;

        public const int EVENT_NOTIF_PERIOD = 180;

        static public McEvent Create (int accountId, DateTime startTime, DateTime endTime, int reminder, string message, int calendarId, int exceptionId)
        {
            // Save the event
            var e = new McEvent ();
            e.AccountId = accountId;
            e.StartTime = startTime;
            e.EndTime = endTime;
            e.NotifTime = e.StartTime.AddMinutes (-reminder);
            e.NotifMessage = message;
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
            Notif.Instance.CancelNotif (Id);
            return base.Delete ();
        }

        public static void EnsureSoonestNotifsScheduled ()
        {
            lock (LockObj) {
                // Clear the IsScheduled flag for anthing with a NotifTime in the past.
                var alreadyFired = NcModel.Instance.Db.Table<McEvent> ().Where (x => DateTime.UtcNow > x.NotifTime && x.IsScheduled);
                foreach (var already in alreadyFired) {
                    already.IsScheduled = false;
                    already.Update ();
                }

                // Check to see if the device may have lost scheduled notifications (android cold boot case):
                // If the DB reports more scheduled notifs that the device, then cancel all notifs and reset all DB IsScheduled flags.
                var scheduled = NcModel.Instance.Db.Table<McEvent> ().Where (x => x.IsScheduled);
                if (scheduled.Count () > Notif.Instance.ScheduledCount) {
                    foreach (var sched in scheduled) {
                        Notif.Instance.CancelNotif (sched.Id);
                        sched.IsScheduled = false;
                        sched.Update ();
                    }
                }

                // Query the McEvent ordered by NotifTime limit MaxScheduledCount. Count them.
                var soonest = NcModel.Instance.Db.Table<McEvent> ().OrderBy (x => x.NotifTime).OrderBy (x => x.Id).Take (Notif.Instance.MaxScheduledCount).ToList ();

                // Cancel any already-scheduled McEvent with NotifTime later than the last of the first MaxScheduledCount, and clear IsScheduled flag.
                if (soonest.Count == Notif.Instance.MaxScheduledCount) {
                    var caboose = soonest.Last ();
                    var later = NcModel.Instance.Db.Table<McEvent> ().Where (x => 
                        x.IsScheduled && 
                        (caboose.NotifTime < x.NotifTime ||
                            (caboose.NotifTime == x.NotifTime && caboose.Id < x.Id)));
                    foreach (var behind in later) {
                        Notif.Instance.CancelNotif (behind.Id);
                        behind.IsScheduled = false;
                        behind.Update ();
                    }
                }
                
                // Ensure each McEvent in the query response is scheduled.
                foreach (var lucky in soonest.Where (x => false == x.IsScheduled)) {
                    Notif.Instance.ScheduleNotif (lucky.Id, lucky.NotifTime, lucky.NotifMessage);
                    lucky.IsScheduled = true;
                    lucky.Update ();
                }
            }
        }

        private static void TimerCallback (Object state)
        {
            EnsureSoonestNotifsScheduled ();
        }

        private static void EventCallback (object sender, EventArgs ea)
        {
            var siea = (StatusIndEventArgs)ea;
            if (NcResult.SubKindEnum.Info_CalendarSetChanged == siea.Status.SubKind) {
                EnsureSoonestNotifsScheduled ();
            }            
        }

        public static void EnableNotifScheduling ()
        {
            if (null != Invoker) {
                Invoker.Dispose ();
                Invoker = null;
            }
            Invoker = new NcTimer ("NcContactGleaner", TimerCallback, null,
                TimeSpan.Zero, new TimeSpan (0, 0, EVENT_NOTIF_PERIOD));
            Invoker.Stfu = true;
            NcTask.Run (() => {
                EnsureSoonestNotifsScheduled ();
            }, "EnableNotifScheduling");
            NcApplication.Instance.StatusIndEvent += EventCallback;
        }

        public static void DisableNotifScheduling ()
        {
            if (null != Invoker) {
                Invoker.Dispose ();
                Invoker = null;
            }
            NcApplication.Instance.StatusIndEvent -= EventCallback;
        }
    }
}
