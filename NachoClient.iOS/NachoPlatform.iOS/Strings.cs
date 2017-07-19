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
    }
}
