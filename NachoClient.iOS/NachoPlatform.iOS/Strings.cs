//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using Foundation;

namespace NachoPlatform
{

    public class Strings : IStrings
    {
        private static Strings _Instance;
        public static Strings Instance {
            get {
                if (_Instance == null) {
                    _Instance = new Strings ();
                }
                return _Instance;
            }
        }

        private Strings ()
        {
        }

        #region Compact Durations

        public string CompactMinutesFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0}m", "compact duration in minutes");
            }
        }

        public string CompactHoursFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0}h", "compact duration in hours");
            }
        }

        public string CompactHourMinutesFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0}h{1}m", "compact duration in hours and minutes");
            }
        }

        public string CompactDayPlus {
            get {
                return NSBundle.MainBundle.LocalizedString ("1d+", "compact duration, greater than one day");
            }
        }

        #endregion

        #region Reminders

        public string ReminderNone { 
            get {
                return NSBundle.MainBundle.LocalizedString ("None (Reminder)", "no reminder is set");
            }
        }

        public string ReminderAtEvent { 
            get {
                return NSBundle.MainBundle.LocalizedString ("At time of event", "reminder when the event starts");
            }
        }

        public string ReminderOneMinute { 
            get {
                return NSBundle.MainBundle.LocalizedString ("1 minute before", "reminder 1 minute before the event starts");
            }
        }

        public string ReminderOneHour { 
            get {
                return NSBundle.MainBundle.LocalizedString ("1 hour before", "reminder 1 hour before the event starts");
            }
        }

        public string ReminderOneDay { 
            get {
                return NSBundle.MainBundle.LocalizedString ("1 day before", "reminder 1 day before the event starts");
            }
        }

        public string ReminderOneWeek { 
            get {
                return NSBundle.MainBundle.LocalizedString ("1 week before", "reminder 1 week before the event starts");
            }
        }

        public string ReminderWeeksFormat { 
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} weeks before", "reminder X weeks before the event starts");
            }
        }

        public string ReminderDaysFormat { 
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} days before", "reminder X days before the event starts");
            }
        }

        public string ReminderHoursFormat { 
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} hours before", "reminder X hours before the event starts");
            }
        }

        public string ReminderMinutesFormat { 
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} minutes", "reminder X minutes the event starts");
            }
        }

        #endregion
    }
}
