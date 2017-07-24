using System;
using Java.Text;
using Java.Util;
using Android.Content;

using NachoClient.AndroidClient;

namespace NachoPlatform
{

    public static class DateConverter
    {
        public static Java.Util.Date ToLocalJavaDate (this DateTime dateTime)
        {
            var local = dateTime.ToLocalTime ();
            var calendar = Calendar.Instance;
            calendar.Set (local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second);
            calendar.Set (CalendarField.Millisecond, local.Millisecond);
            return calendar.Time;
        }
    }

    public class DateTimeFormatter : IDateTimeFormatter
    {
        public static DateTimeFormatter Instance { get; private set; }

        public static void Init (Context context)
        {
            Instance = new DateTimeFormatter (context);
        }

        Context Context;

        private DateTimeFormatter (Context context)
        {
            Context = context;
        }

        private bool Is24HourPreferred {
            get {
                return Android.Text.Format.DateFormat.Is24HourFormat (Context);
            }
        }

        public string WeekdayName (DateTime dateTime)
        {
            return new SimpleDateFormat ("EEEE").Format (dateTime.ToLocalJavaDate ());
        }

        public string AbbreviatedWeekdayName (DateTime dateTime)
        {
            return new SimpleDateFormat ("EEE").Format (dateTime.ToLocalJavaDate ());
        }

        public string MonthName (DateTime dateTime)
        {
            return new SimpleDateFormat ("MMMM").Format (dateTime.ToLocalJavaDate ());
        }

        public string MonthNameWithYear (DateTime dateTime)
        {
            var pattern = Context.GetString (Resource.String.datetime_month_with_year_pattern);
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ());
        }

        public string ShortNumericDate (DateTime dateTime)
        {
            var pattern = Context.GetString (Resource.String.datetime_short_numeric_date_pattern);
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ());
        }

        public string ShortNumericDateWithYear (DateTime dateTime)
        {
            var pattern = Context.GetString (Resource.String.datetime_short_numeric_date_with_year_pattern);
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ());
        }

        public string ShortNumericDateWithShortYear (DateTime dateTime)
        {
            var pattern = Context.GetString (Resource.String.datetime_short_numeric_date_with_short_year_pattern);
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ());
        }


        public string AbbreviatedDate (DateTime dateTime)
        {
            var pattern = Context.GetString (Resource.String.datetime_abbr_date_pattern);
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ());
        }

        public string Date (DateTime dateTime)
        {
            var pattern = Context.GetString (Resource.String.datetime_date_pattern);
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ());
        }


        public string AbbreviatedDateWithYear (DateTime dateTime)
        {
            var pattern = Context.GetString (Resource.String.datetime_abbr_date_with_year_pattern);
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ());
        }

        public string DateWithYear (DateTime dateTime)
        {
            var pattern = Context.GetString (Resource.String.datetime_date_with_year_pattern);
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ());
        }


        public string AbbreviatedDateWithWeekday (DateTime dateTime)
        {
            var pattern = Context.GetString (Resource.String.datetime_abbr_date_with_weekday_pattern);
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ());
        }

        public string DateWithWeekday (DateTime dateTime)
        {
            var pattern = Context.GetString (Resource.String.datetime_date_with_weekday_pattern);
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ());
        }


        public string AbbreviatedDateWithWeekdayAndYear (DateTime dateTime)
        {
            var pattern = Context.GetString (Resource.String.datetime_abbr_date_with_weekday_and_year_pattern);
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ());
        }

        public string DateWithWeekdayAndYear (DateTime dateTime)
        {
            var pattern = Context.GetString (Resource.String.datetime_date_with_weekday_and_year_pattern);
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ());
        }

        public string MinutePrecisionTime (DateTime dateTime)
        {
            string pattern;
            if (Is24HourPreferred) {
                pattern = Context.GetString (Resource.String.datetime_minute_precision_time_24_pattern);
            } else {
                pattern = Context.GetString (Resource.String.datetime_minute_precision_time_pattern);
            }
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ()).ToLower ();
        }

        public string HourPrecisionTime (DateTime dateTime)
        {
            string pattern;
            if (Is24HourPreferred) {
                pattern = Context.GetString (Resource.String.datetime_hour_precision_time_24_pattern);
            } else {
                pattern = Context.GetString (Resource.String.datetime_hour_precision_time_pattern);
            }
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ()).ToLower ();
        }

        public string MinutePrecisionTimeWithoutAmPm (DateTime dateTime)
        {
            string pattern;
            if (Is24HourPreferred) {
                pattern = Context.GetString (Resource.String.datetime_minute_precision_time_24_pattern);
            } else {
                pattern = Context.GetString (Resource.String.datetime_minute_precision_time_noampm_pattern);
            }
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ());
        }

        public string HourPrecisionTimeWithoutAmPm (DateTime dateTime)
        {
            string pattern;
            if (Is24HourPreferred) {
                pattern = Context.GetString (Resource.String.datetime_hour_precision_time_24_pattern);
            } else {
                pattern = Context.GetString (Resource.String.datetime_hour_precision_time_noampm_pattern);
            }
            return new SimpleDateFormat (pattern).Format (dateTime.ToLocalJavaDate ());
        }

    }
}