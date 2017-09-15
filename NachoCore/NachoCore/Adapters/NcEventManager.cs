//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    /// <summary>
    /// Listen for changes to the calendar and update McEvents accordingly.  Code that uses this class
    /// can indicate how far into the future the McEvents need to be kept accurate.
    /// </summary>
    public class NcEventManager
    {
        private NcEventManager () { }

        private static object lockObject = new object ();

        private static Dictionary<object, TimeSpan> eventWindows = new Dictionary<object, TimeSpan> ();
        private static TimeSpan maxDuration = TimeSpan.MinValue;
        private static DateTime latestEndDate = DateTime.MinValue;
        private static DateTime oldestEvent = DateTime.Now.Subtract (TimeSpan.FromDays (30)).Date.ToUniversalTime ();

        public static void Initialize ()
        {
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            // Always keep the events accurate for the next 30 days, so that local notifications will be correct.
            AddEventWindow (typeof (NcEventManager), TimeSpan.FromDays (30));
        }

        public static DateTime BeginningOfEventsOfInterest {
            get {
                return oldestEvent;
            }
        }

        /// <summary>
        /// Inform NcEventManager that events need to be maintained through the given date.
        /// The key should be unique for each caller and can be used later to change or remove
        /// the event window.
        /// </summary>
        public static void AddEventWindow (object key, DateTime endDate)
        {
            AddEventWindow (key, endDate - DateTime.UtcNow);
        }

        /// <summary>
        /// Inform NcEventManager that events need to be maintained for some period into the
        /// future.  The key should be unique for each caller and can be used later to change
        /// or remove the event window.
        /// </summary>
        public static void AddEventWindow (object key, TimeSpan duration)
        {
            lock (lockObject) {
                eventWindows [key] = duration;
                if (DateTime.UtcNow + duration > latestEndDate) {
                    RegenerateEvents ();
                }
            }
        }

        public static void RemoveEventWindow (object key)
        {
            lock (lockObject) {
                eventWindows.Remove (key);
            }
        }

        private static void RegenerateEvents ()
        {
            UpdateLatestEndDate ();
            CalendarHelper.ExpandRecurrences (latestEndDate);
        }

        public static void RegenerateEvents (McCalendar calendar)
        {
            UpdateLatestEndDate ();
            CalendarHelper.ExpandRecurrences (calendar, latestEndDate);
        }

        static void UpdateLatestEndDate ()
        {
            maxDuration = TimeSpan.MinValue;
            foreach (var duration in eventWindows.Values) {
                if (duration > maxDuration) {
                    maxDuration = duration;
                }
            }
            latestEndDate = DateTime.UtcNow + maxDuration;
        }

        private static void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            switch (s.Status.SubKind) {

            case NcResult.SubKindEnum.Info_CalendarChanged:
            case NcResult.SubKindEnum.Info_CalendarSetChanged:
                lock (lockObject) {
                    RegenerateEvents ();
                }
                break;

            // When the app comes into the foreground, check if it has been more than two days
            // since events were regenerated.
            case NcResult.SubKindEnum.Info_ExecutionContextChanged:
                if (NachoCore.NcApplication.ExecutionContextEnum.Foreground == (NachoCore.NcApplication.ExecutionContextEnum)s.Status.Value) {
                    lock (lockObject) {
                        if (DateTime.UtcNow + maxDuration > latestEndDate.AddDays (2)) {
                            RegenerateEvents ();
                        }
                    }
                }
                break;
            }
        }
    }
}

