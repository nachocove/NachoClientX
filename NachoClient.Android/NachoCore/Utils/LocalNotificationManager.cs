//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using System.Collections.Generic;
using NachoPlatform;
using System.Threading;

namespace NachoCore.Utils
{
    /// <summary>
    /// Manage local notifications for the app.  Local notifications are the pop-ups
    /// that appear when the app is not in the foreground to let the user know about
    /// an event that is about to happen.
    /// 
    /// It is assumed that the only use for local notifications is to implement
    /// reminders for upcoming calendar events.  If local notifications are to be
    /// used for anything else, then this class must be changed.
    /// </summary>
    public class LocalNotificationManager
    {
        // The number of notifications that are scheduled at any moment depends on how
        // often meetings happen.  Try to schedule notifications for all the meetings
        // in the next four days, but never schedule more than 50 at a time.  If the
        // next four days aren't packed solid, then schedule notifications for the next
        // 20 meetings, but never schedule a notification more than 30 days in the future.
        private const int MINIMUM_NUM_EVENTS = 20;
        private const int MAXIMUM_NUM_EVENTS = 50;
        private static TimeSpan MINIMUM_WINDOW = new TimeSpan (4 * TimeSpan.TicksPerDay);
        private static TimeSpan MAXIMUM_WINDOW = new TimeSpan (30 * TimeSpan.TicksPerDay);

        private static object lockObject = new object ();

        // The handles of all the scheduled notifications.  Keeping our own list
        // reduces the amount of work that needs to be done on the UI thread.
        private static HashSet<int> scheduledEvents = null;

        // If a new event is created with a reminder before "scheduledThrough",
        // then the schedule of notifications needs to be redone.
        private static DateTime scheduledThrough = DateTime.MaxValue;

        // Has there been a new event in near future?
        private static bool reschedulingNeeded = false;

        public static void InitializeLocalNotifications ()
        {
            lock (lockObject) {
                NcAssert.True (null == scheduledEvents, "LocalNotificationManager.InitializeLocalNotifications() was called twice.");
                scheduledEvents = new HashSet<int> ();
                var notifications = new List<McEvent> ();
                int count = 0;
                DateTime now = DateTime.UtcNow;
                scheduledThrough = now + MAXIMUM_WINDOW;
                foreach (var ev in McEvent.QueryEventsWithRemindersInRange(now, now + MAXIMUM_WINDOW)) {
                    if (MAXIMUM_NUM_EVENTS <= count || (MINIMUM_NUM_EVENTS <= count && ev.ReminderTime - now > MINIMUM_WINDOW)) {
                        // More than the maximum total notifications,
                        // or more than the minimum number and past the minimum window.
                        // Record when we left off and stop.
                        scheduledThrough = ev.ReminderTime;
                        break;
                    }
                    notifications.Add (ev);
                    scheduledEvents.Add (ev.Id);
                    ++count;
                }
                reschedulingNeeded = false;
                NachoPlatform.Notif.Instance.ScheduleNotifications (notifications);

                NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            }
        }

        public static void ScheduleNotifications ()
        {
            // Before starting a background task, check to see if any work needs to be
            // done.  But don't block while trying to figure that out.  If the lock is
            // not available right away, then just go ahead and schedule the task.
            if (Monitor.TryEnter (lockObject)) {
                try {
                    if (!ShouldReschedule ()) {
                        return;
                    }
                } finally {
                    Monitor.Exit (lockObject);
                }
            }
            NcTask.Run (DoScheduling, "ScheduleNotifications");
        }

        private static void DoScheduling ()
        {
            lock (lockObject) {
                NcAssert.NotNull (scheduledEvents, "LocalNotificationManager.ScheduleNotifications() was called before InitializeLocalNotifications().");
                if (!ShouldReschedule ()) {
                    return;
                }
                Log.Info (Log.LOG_CALENDAR, "LocalNotificationManager: Adjusting the notifications...");
                int count = 0;
                DateTime now = DateTime.UtcNow;
                bool limitReached = false;
                foreach (var ev in McEvent.QueryEventsWithRemindersInRange(now, now + MAXIMUM_WINDOW)) {
                    if (!limitReached) {
                        limitReached = MAXIMUM_NUM_EVENTS <= count || (MINIMUM_NUM_EVENTS <= count && ev.ReminderTime - now > MINIMUM_WINDOW);
                        if (limitReached) {
                            scheduledThrough = ev.ReminderTime;
                        }
                    }
                    if (!limitReached) {
                        if (scheduledEvents.Add (ev.Id)) {
                            Log.Info (Log.LOG_CALENDAR, "Adding notification for event {0}", ev.Id);
                            NachoPlatform.Notif.Instance.ScheduleNotification (ev);
                        }
                    } else {
                        if (scheduledEvents.Remove (ev.Id)) {
                            Log.Info (Log.LOG_CALENDAR, "Removing notification for event {0}", ev.Id);
                            NachoPlatform.Notif.Instance.CancelNotification (ev.Id);
                        }
                    }
                    ++count;
                }
                if (!limitReached) {
                    scheduledThrough = now + MAXIMUM_WINDOW;
                }
                reschedulingNeeded = false;
            }
        }

        /// <summary>
        /// Make note of a new event.  If the event has a reminder in the near future,
        /// set a flag so that its notification will be scheduled on the next
        /// Info_EventSetChanged event.
        /// </summary>
        public static void ScheduleNotification (McEvent ev)
        {
            if (DateTime.MinValue == ev.ReminderTime) {
                // The event doesn't have a reminder.
                return;
            }
            DateTime now = DateTime.UtcNow;
            if (ev.ReminderTime < now) {
                if (ev.GetEndTimeUtc () > now) {
                    // The reminder time has passed, but the event isn't over yet.
                    // Notify the user right now.
                    NachoPlatform.Notif.Instance.ImmediateNotification (ev);
                }
                return;
            }
            lock (lockObject) {
                if (ev.ReminderTime < scheduledThrough) {
                    // The actual scheduling of the notification doesn't happen
                    // until ScheduleNotifications() is called.
                    reschedulingNeeded = true;
                }
            }
        }

        public static void CancelNotification (McEvent evt)
        {
            lock (lockObject) {
                NcAssert.NotNull (scheduledEvents, "LocalNotificationManager.CancelNotification() was called before InitializeLocalNotifications().");
                if (scheduledEvents.Remove (evt.Id)) {
                    NachoPlatform.Notif.Instance.CancelNotification (evt.Id);
                }
            }
        }

        public static void CancelNotifications (List<NcEventIndex> events)
        {
            lock (lockObject) {
                foreach (var eventId in events) {
                    if (scheduledEvents.Remove (eventId.Id)) {
                        NachoPlatform.Notif.Instance.CancelNotification (eventId.Id);
                    }
                }
            }
        }

        private static bool ShouldReschedule ()
        {
            return reschedulingNeeded || scheduledThrough - DateTime.UtcNow < MINIMUM_WINDOW;
        }

        private static void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var args = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_EventSetChanged == args.Status.SubKind) {
                ScheduleNotifications ();
            }
        }
    }
}

