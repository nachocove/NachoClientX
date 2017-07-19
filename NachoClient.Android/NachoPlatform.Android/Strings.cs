//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using NachoClient.AndroidClient;
using Android.Content;

namespace NachoPlatform
{

    public class Strings : IStrings
    {
        public static Strings Instance { get; private set; }

        public static void Init (Context context)
        {
            Instance = new Strings (context);
        }

        Context Context;

        private Strings (Context context)
        {
            Context = context;
        }

        #region Compact Durations

        public string CompactMinutesFormat {
            get {
                return Context.GetString (Resource.String.pretty_compact_minutes_format);
            }
        }

        public string CompactHoursFormat {
            get {
                return Context.GetString (Resource.String.pretty_compact_hours_format);
            }
        }

        public string CompactHourMinutesFormat {
            get {
                return Context.GetString (Resource.String.pretty_compact_hours_minutes_format);
            }
        }

        public string CompactDayPlus {
            get {
                return Context.GetString (Resource.String.pretty_compact_day_plus);
            }
        }

        #endregion

        #region Reminders

        public string ReminderNone { 
            get {
                return Context.GetString (Resource.String.reminder_none);
            }
        }

        public string ReminderAtEvent { 
            get {
                return Context.GetString (Resource.String.reminder_at_event);
            }
        }

        public string ReminderOneMinute { 
            get {
                return Context.GetString (Resource.String.reminder_one_minute);
            }
        }

        public string ReminderOneHour { 
            get {
                return Context.GetString (Resource.String.reminder_one_hour);
            }
        }

        public string ReminderOneDay { 
            get {
                return Context.GetString (Resource.String.reminder_one_day);
            }
        }

        public string ReminderOneWeek { 
            get {
                return Context.GetString (Resource.String.reminder_one_week);
            }
        }

        public string ReminderWeeksFormat { 
            get {
                return Context.GetString (Resource.String.reminder_weeks_format);
            }
        }

        public string ReminderDaysFormat { 
            get {
                return Context.GetString (Resource.String.reminder_days_format);
            }
        }

        public string ReminderHoursFormat { 
            get {
                return Context.GetString (Resource.String.reminder_hours_format);
            }
        }

        public string ReminderMinutesFormat { 
            get {
                return Context.GetString (Resource.String.reminder_minutes_format);
            }
        }

        #endregion
    }
}
