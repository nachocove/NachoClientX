using System;
using Foundation;

using NachoClient.iOS;

namespace NachoPlatform
{

    public static class DateConverter
    {
        public static NSDate ToLocalNSDate (this DateTime dateTime)
        {
            var local = dateTime.ToLocalTime ();
            return local.ToNSDate ();
        }
    }

    public class DateTimeFormatter : IDateTimeFormatter
    {
        public static DateTimeFormatter Instance { get; private set; } = new DateTimeFormatter ();

        private DateTimeFormatter ()
        {
        }

        private bool Is24HourPreferred {
            get {
                var format = NSDateFormatter.GetDateFormatFromTemplate ("j", 0, NSLocale.CurrentLocale);
                return format.Contains ("H") || format.Contains ("k");
            }
        }

        public string WeekdayName (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("EEEE");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string AbbreviatedWeekdayName (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("EEE");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string MonthName (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("MMMM");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string MonthNameWithYear (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("MMMM yyyy");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string ShortNumericDate (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("Md");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string ShortNumericDateWithYear (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("Mdyyyy");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string ShortNumericDateWithShortYear (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("Mdyy");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string AbbreviatedDate (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("MMMd");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string Date (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("MMMMd");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }


        public string AbbreviatedDateWithYear (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("MMMdyyyy");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string DateWithYear (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("MMMMdyyyy");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }


        public string AbbreviatedDateWithWeekday (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("EEEMMMd");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string DateWithWeekday (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("EEEEMMMMd");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }


        public string AbbreviatedDateWithWeekdayAndYear (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("EEEMMMdyyyy");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string DateWithWeekdayAndYear (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("EEEEMMMMdyyyy");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string MinutePrecisionTime (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            if (Is24HourPreferred) {
                formatter.SetLocalizedDateFormatFromTemplate ("j:mm");
            } else {
                formatter.SetLocalizedDateFormatFromTemplate ("j:mm a");
                if (formatter.AMSymbol == "AM") {
                    formatter.AMSymbol = "am";
                }
                if (formatter.PMSymbol == "PM") {
                    formatter.PMSymbol = "pm";
                }
            }
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string HourPrecisionTime (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            if (Is24HourPreferred) {
                formatter.SetLocalizedDateFormatFromTemplate ("j");
            } else {
                formatter.SetLocalizedDateFormatFromTemplate ("j a");
                if (formatter.AMSymbol == "AM") {
                    formatter.AMSymbol = "am";
                }
                if (formatter.PMSymbol == "PM") {
                    formatter.PMSymbol = "pm";
                }
            }
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string MinutePrecisionTimeWithoutAmPm (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("j:mm");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

        public string HourPrecisionTimeWithoutAmPm (DateTime dateTime)
        {
            var formatter = new NSDateFormatter ();
            formatter.SetLocalizedDateFormatFromTemplate ("j");
            return formatter.ToString (dateTime.ToLocalNSDate ());
        }

    }
}