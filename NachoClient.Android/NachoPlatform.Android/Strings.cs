//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using NachoClient.AndroidClient;
using Android.Content;

namespace NachoPlatform
{

    public class Strings : IStrings
    {
        public static IStrings Instance { get; private set; }

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

        #region Date/Time

        public string FriendlyDateTimeTodayFormat {
            get {
                return Context.GetString (Resource.String.friendly_datetime_today_format);
            }
        }

        public string FriendlyDateTimeYesterdayFormat {
            get {
                return Context.GetString (Resource.String.friendly_datetime_yesterday_format);
            }
        }

        public string FriendlyDateTimeOtherFormat {
            get {
                return Context.GetString (Resource.String.friendly_datetime_other_format);
            }
        }

        public string DecreasingPrecisionTimeYesterday {
            get {
                return Context.GetString (Resource.String.decreasing_precision_time_yesterday);
            }
        }

        public string FutureDateToday {
            get {
                return Context.GetString (Resource.String.future_date_today);
            }
        }

        public string FutureDateTomorrow {
            get {
                return Context.GetString (Resource.String.future_date_tomorrow);
            }
        }

        public string FutureDateYesterday {
            get {
                return Context.GetString (Resource.String.future_date_yesterday);
            }
        }

        public string VariableDateTimeYesterdayFormat {
            get {
                return Context.GetString (Resource.String.variable_date_yesterday_format);
            }
        }

        public string VariableDateTimeOtherFormat {
            get {
                return Context.GetString (Resource.String.variable_date_other_format);
            }
        }

        #endregion

        #region Events

        public string EventTimeNow {
            get {
                return Context.GetString (Resource.String.event_time_now);
            }
        }

        public string EventTimeOneMinute {
            get {
                return Context.GetString (Resource.String.event_time_one_minute);
            }
        }

        public string EventTimeMinutesFormat {
            get {
                return Context.GetString (Resource.String.event_time_minutes_format);
            }
        }

        public string EventTimeAtFormat {
            get {
                return Context.GetString (Resource.String.event_time_at_format);
            }
        }

        public string EventTimeDateFormat {
            get {
                return Context.GetString (Resource.String.event_time_date_format);
            }
        }

        public string EventDayToday {
            get {
                return Context.GetString (Resource.String.event_day_today);
            }
        }

        public string EventDayTomorrow {
            get {
                return Context.GetString (Resource.String.event_day_tomorrow);
            }
        }

        public string EventDetailTimeThroughFormat {
            get {
                return Context.GetString (Resource.String.event_detail_time_through_format);
            }
        }

        public string EventDetailTimeToFormat {
            get {
                return Context.GetString (Resource.String.event_detail_time_to_format);
            }
        }

        public string MeetingRequestTimeAllDayFormat {
            get {
                return Context.GetString (Resource.String.meeting_request_time_all_day_format);
            }
        }

        public string MeetingRequestTimeSameHalfDayFormat {
            get {
                return Context.GetString (Resource.String.meeting_request_time_same_half_day_format);
            }
        }

        public string MeetingRequestTimeSameDayFormat {
            get {
                return Context.GetString (Resource.String.meeting_request_time_same_day_format);
            }
        }

        public string MeetingRequestTimeMultiDayFormat {
            get {
                return Context.GetString (Resource.String.meeting_request_time_multi_day_format);
            }
        }

        public string EventNotificationDefaultTitle {
            get {
                return Context.GetString (Resource.String.event_notification_default_title);
            }
        }

        public string EventNotificationAllDayTimeFormat {
            get {
                return Context.GetString (Resource.String.event_notification_all_day_time_format);
            }
        }

        public string EventNotificationTimeFormat {
            get {
                return Context.GetString (Resource.String.event_notification_time_format);
            }
        }

        public string AttendeeStatusOptional {
            get {
                return Context.GetString (Resource.String.attendee_status_optional);
            }
        }

        public string AttendeeStatusRequired {
            get {
                return Context.GetString (Resource.String.attendee_status_required);
            }
        }

        public string AttendeeStatusResource {
            get {
                return Context.GetString (Resource.String.attendee_status_resource);
            }
        }

        public string AttendeeStatusAccepted {
            get {
                return Context.GetString (Resource.String.attendee_status_accepted);
            }
        }

        public string AttendeeStatusDeclined {
            get {
                return Context.GetString (Resource.String.attendee_status_declined);
            }
        }

        public string AttendeeStatusTentative {
            get {
                return Context.GetString (Resource.String.attendee_status_tentative);
            }
        }

        public string AttendeeStatusNoResponse {
            get {
                return Context.GetString (Resource.String.attendee_status_no_response);
            }
        }

        public string MeetingResponseAcceptedFormat {
            get {
                return Context.GetString (Resource.String.meeting_response_accepted_format);
            }
        }

        public string MeetingResponseDeclinedFormat {
            get {
                return Context.GetString (Resource.String.meeting_response_declined_format);
            }
        }

        public string MeetingResponseTentativeFormat {
            get {
                return Context.GetString (Resource.String.meeting_response_tentative_format);
            }
        }

        public string MeetingResponseUnknownFormat {
            get {
                return Context.GetString (Resource.String.meeting_response_unknown_format);
            }
        }

        #endregion

        public string NoRecipientsFallback {
            get {
                return Context.GetString (Resource.String.no_recipients_fallback);
            }
        }

        #region Recurrence

        public string RecurrenceDoesNotRepeat {
            get {
                return Context.GetString (Resource.String.recurrence_does_not_repeat);
            }
        }

        public string RecurrenceDaily {
            get {
                return Context.GetString (Resource.String.recurrence_daily);
            }
        }

        public string RecurrenceEveryXDaysFormat {
            get {
                return Context.GetString (Resource.String.recurrence_every_x_days_format);
            }
        }

        public string RecurrenceWeekdays {
            get {
                return Context.GetString (Resource.String.recurrence_weekdays);
            }
        }

        public string RecurrenceWeekends {
            get {
                return Context.GetString (Resource.String.recurrence_weekends);
            }
        }

        public string RecurrenceWeeklyFormat {
            get {
                return Context.GetString (Resource.String.recurrence_weekly_format);
            }
        }

        public string RecurrenceEveryXWeekdaysFormat {
            get {
                return Context.GetString (Resource.String.recurrence_every_x_weekdays_format);
            }
        }

        public string RecurrenceEveryXWeeksFormat {
            get {
                return Context.GetString (Resource.String.recurrence_every_x_weeks_format);
            }
        }

        public string RecurrenceMonthlyFormat {
            get {
                return Context.GetString (Resource.String.recurrence_monthly_format);
            }
        }

        public string RecurrenceEveryXMonthsFormat {
            get {
                return Context.GetString (Resource.String.recurrence_every_x_months_format);
            }
        }

        public string RecurrenceLastDayOfMonth {
            get {
                return Context.GetString (Resource.String.recurrence_last_day_of_month);
            }
        }

        public string RecurrenceLastWeekdayOfMonth {
            get {
                return Context.GetString (Resource.String.recurrence_last_weekday_of_month);
            }
        }

        public string RecurrenceLastWeekendDayOfMonth {
            get {
                return Context.GetString (Resource.String.recurrence_last_weekend_day_of_month);
            }
        }

        public string RecurrenceWeekdaysInWeekOfMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_weekdays_in_week_of_month_format);
            }
        }

        public string RecurrenceWeekendDaysInWeekOfMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_weekend_days_in_week_of_month_format);
            }
        }

        public string RecurrenceNamedDayInWeekOfMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_named_day_in_week_of_month_format);
            }
        }

        public string RecurrenceLastNamedDayOfMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_last_named_day_of_month_format);
            }
        }

        public string RecurrenceLastDayOfEveryXMonthsFormat {
            get {
                return Context.GetString (Resource.String.recurrence_last_day_of_every_x_months_format);
            }
        }

        public string RecurrenceLastWeekdayOfEveryXMonthsFormat {
            get {
                return Context.GetString (Resource.String.recurrence_last_weekday_of_every_x_months_format);
            }
        }

        public string RecurrenceLastWeekendDayOfEveryXMonthsFormat {
            get {
                return Context.GetString (Resource.String.recurrence_last_weekend_day_of_every_x_months_format);
            }
        }

        public string RecurrenceLastNamedDayOfEveryXMonthsFormat {
            get {
                return Context.GetString (Resource.String.recurrence_last_named_day_of_every_x_months_format);
            }
        }

        public string RecurrenceWeekdaysInWeekOfEveryXMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_weekdays_in_week_of_every_x_month_format);
            }
        }

        public string RecurrenceWeekendDaysInWeekOfEveryXMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_weekend_days_in_week_of_every_x_month_format);
            }
        }

        public string RecurrenceNamedDayInWeekOfEveryXMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_named_day_in_week_of_every_x_month_format);
            }
        }

        public string RecurrenceLastDayOfNamedMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_last_day_of_named_month);
            }
        }

        public string RecurrenceLastWeekdayOfNamedMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_last_weekday_of_named_month_format);
            }
        }

        public string RecurrenceLastWeekendDayOfNamedMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_last_weekend_day_of_named_month_format);
            }
        }

        public string RecurrenceLastNamedDayOfNamedMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_last_named_day_of_named_month_format);
            }
        }

        public string RecurrenceWeekdaysInWeekOfNamedMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_weekdays_in_week_of_named_month_format);
            }
        }

        public string RecurrenceWeekendDaysInWeekOfNamedMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_weekend_days_in_week_of_named_month_format);
            }
        }

        public string RecurrenceNamedDayInWeekOfNamedMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_named_day_in_week_of_named_month_format);
            }
        }

        public string RecurrenceLastDayOfNamedMonthEveryXYearsFormat {
            get {
                return Context.GetString (Resource.String.recurrence_last_day_of_named_month_every_x_years);
            }
        }

        public string RecurrenceLastWeekdayOfNamedMonthEveryXYearsFormat {
            get {
                return Context.GetString (Resource.String.recurrence_last_weekday_of_named_month_every_x_years_format);
            }
        }

        public string RecurrenceLastWeekendDayOfNamedMonthEveryXYearsFormat {
            get {
                return Context.GetString (Resource.String.recurrence_last_weekend_day_of_named_month_every_x_years_format);
            }
        }

        public string RecurrenceLastNamedDayOfNamedMonthEveryXYearsFormat {
            get {
                return Context.GetString (Resource.String.recurrence_last_named_day_of_named_month_every_x_years_format);
            }
        }

        public string RecurrenceWeekdaysInWeekOfNamedMonthEveryXYearsFormat {
            get {
                return Context.GetString (Resource.String.recurrence_weekdays_in_week_of_named_month_every_x_years_format);
            }
        }

        public string RecurrenceWeekendDaysInWeekOfNamedMonthEveryXYearsFormat {
            get {
                return Context.GetString (Resource.String.recurrence_weekend_days_in_week_of_named_month_every_x_years_format);
            }
        }

        public string RecurrenceNamedDayInWeekOfNamedMonthEveryXYearsFormat {
            get {
                return Context.GetString (Resource.String.recurrence_named_day_in_week_of_named_month_every_x_years_format);
            }
        }

        public string RecurrenceYearlyFormat {
            get {
                return Context.GetString (Resource.String.recurrence_yearly_format);
            }
        }

        public string RecurrenceEveryXYearsFormat {
            get {
                return Context.GetString (Resource.String.recurrence_every_x_years_format);
            }
        }

        public string RecurrenceUnknownMonthFormat {
            get {
                return Context.GetString (Resource.String.recurrence_unknown_month_format);
            }
        }

        public string RecurrenceUnknown {
            get {
                return Context.GetString (Resource.String.recurrence_unknown);
            }
        }

        public string RecurrenceListJoiner {
            get {
                return Context.GetString (Resource.String.recurrence_list_joiner);
            }
        }

        public string RecurrenceListFinalJoiner {
            get {
                return Context.GetString (Resource.String.recurrence_list_final_joiner);
            }
        }

        #endregion
    }
}
