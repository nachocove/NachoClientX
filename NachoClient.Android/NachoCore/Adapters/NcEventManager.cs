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
        private NcEventManager() { }

        private static object lockObject = new object ();

        private static Dictionary<object, DateTime> endDates = new Dictionary<object, DateTime> ();
        private static DateTime latestEndDate = DateTime.MinValue;

        public static void Initialize ()
        {
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            // Always keep the events accurate for the next 30 days, so that local notifications will be correct.
            AddEndDate (typeof(NcEventManager), DateTime.Now.AddDays (30));

            // ... and update that 30-day marker every two days
            TimeSpan twoDays = new TimeSpan (2, 0, 0, 0);
            new NcTimer ("NcEventManager", ((object state) => {
                AddEndDate (typeof(NcEventManager), DateTime.Now.AddDays (30));
            }), null, twoDays, twoDays);
        }

        /// <summary>
        /// Inform NcEventManager that events need to be maintained through the given date.
        /// The key should be unique for each caller and can be used later to change or remove
        /// the end date.
        /// </summary>
        public static void AddEndDate (object key, DateTime endDate)
        {
            lock (lockObject) {
                endDates [key] = endDate;
                if (endDate > latestEndDate) {
                    latestEndDate = endDate;
                    RegenerateEvents ();
                }
            }
        }

        public static void RemoveEndDate (object key)
        {
            lock (lockObject) {
                endDates.Remove (key);
            }
        }

        private static void RegenerateEvents ()
        {
            latestEndDate = DateTime.MinValue;
            foreach (var sentinel in endDates.Values) {
                if (sentinel > latestEndDate) {
                    latestEndDate = sentinel;
                }
            }
            CalendarHelper.ExpandRecurrences (latestEndDate);
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
            }
        }
    }
}

