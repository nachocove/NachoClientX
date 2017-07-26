//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using Foundation;

namespace NachoPlatform
{

    public class Strings : IStrings
    {
        private static Strings _Instance;
        public static IStrings Instance {
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

        #region Date/Time

        public string FriendlyDateTimeTodayFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("Today - {0} (friendly datetime)", "friendly date/time format for today");
            }
        }

        public string FriendlyDateTimeYesterdayFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("Yesterday - {0} (friendly datetime)", "friendly date/time format for yesterday");
            }
        }

        public string FriendlyDateTimeOtherFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} - {1} (friendly datetime)", "friendly date/time format for any other date");
            }
        }

        public string DecreasingPrecisionTimeYesterday {
            get {
                return NSBundle.MainBundle.LocalizedString ("Yesterday (decreasing precision)", "Name for yesterday when showing dates in the message list");
            }
        }

        public string FutureDateToday {
            get {
                return NSBundle.MainBundle.LocalizedString ("Today (future date)", "Name for today when showing due dates");
            }
        }

        public string FutureDateTomorrow {
            get {
                return NSBundle.MainBundle.LocalizedString ("Tomrrow (future date)", "Name for tomorrow when showing due dates");
            }
        }

        public string FutureDateYesterday {
            get {
                return NSBundle.MainBundle.LocalizedString ("Yesterday (future date)", "Name for yesterday when showing due dates");
            }
        }

        public string VariableDateTimeYesterdayFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("Yesterday {0} (variable datetime)", "Format for yesterday when showing chat timestamps");
            }
        }

        public string VariableDateTimeOtherFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} {1} (variable datetime)", "Format for other days when showing chat timestamps");
            }
        }

        #endregion

        #region Events

        public string EventTimeNow {
            get {
                return NSBundle.MainBundle.LocalizedString ("now (event time)", "Name for right now when showing event time");
            }
        }

        public string EventTimeOneMinute {
            get {
                return NSBundle.MainBundle.LocalizedString ("in 1 minute (event time)", "Name for one minute from now when showing event time");
            }
        }

        public string EventTimeMinutesFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("in {0} minutes (event time)", "Format for x minutes from now when showing event time");
            }
        }

        public string EventTimeAtFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("at {0} (event time)", "Format for a specific time today when showing event time");
            }
        }

        public string EventTimeDateFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} {1} (event time)", "Format for a specific daty/time when showing event time");
            }
        }

        public string EventDayToday {
            get {
                return NSBundle.MainBundle.LocalizedString ("Today (event day)", "Name for today when showing an event's day");
            }
        }

        public string EventDayTomorrow {
            get {
                return NSBundle.MainBundle.LocalizedString ("Tomorrow (event day)", "Name for tomorrow when showing an event's day");
            }
        }

        public string EventDetailTimeThroughFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("through {0}", "Second line of an all day event's span");
            }
        }

        public string EventDetailTimeToFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} to {1} (event detail)", "Time span for an event, as shown in the detail screen");
            }
        }

        public string MeetingRequestTimeAllDayFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} through {1} (meeting request)", "Time span for an all day meeting request");
            }
        }

        public string MeetingRequestTimeSameHalfDayFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} from {1} to {2} (meeting request)", "Time span for a meeting request on the same day with the same am/pm");
            }
        }

        public string MeetingRequestTimeSameDayFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0}, {1} to {2} (meeting request)", "Time span for a meeting request on the same day");
            }
        }

        public string MeetingRequestTimeMultiDayFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} at {1} to {2} (meeting request)", "Time span for a meeting request on different days");
            }
        }

        public string EventNotificationDefaultTitle {
            get {
                return NSBundle.MainBundle.LocalizedString ("Event (event notification)", "The fallback title for an event notification");
            }
        }

        public string EventNotificationAllDayTimeFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} all day (event notification)", "The time of an all day event notification");
            }
        }

        public string EventNotificationTimeFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} - {1} (event notification)", "The time of an event notification");
            }
        }

        public string AttendeeStatusOptional {
            get {
                return NSBundle.MainBundle.LocalizedString ("Optional (attendee status)", "The name for the optional attendee status");
            }
        }

        public string AttendeeStatusRequired {
            get {
                return NSBundle.MainBundle.LocalizedString ("Required (attendee status)", "The name for the required attendee status");
            }
        }

        public string AttendeeStatusResource {
            get {
                return NSBundle.MainBundle.LocalizedString ("Resource (attendee status)", "The name for the resource attendee status");
            }
        }

        public string AttendeeStatusAccepted {
            get {
                return NSBundle.MainBundle.LocalizedString ("Accepted (attendee status)", "The name for the accepted attendee status");
            }
        }

        public string AttendeeStatusDeclined {
            get {
                return NSBundle.MainBundle.LocalizedString ("Declined (attendee status)", "The name for the declined attendee status");
            }
        }

        public string AttendeeStatusTentative {
            get {
                return NSBundle.MainBundle.LocalizedString ("Tentative (attendee status)", "The name for the tentative attendee status");
            }
        }

        public string AttendeeStatusNoResponse {
            get {
                return NSBundle.MainBundle.LocalizedString ("No Response (attendee status)", "The name for the no response attendee status");
            }
        }

        public string MeetingResponseAcceptedFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} has accepted the meeting.", "The status line indicating meeting request was accepted");
            }
        }

        public string MeetingResponseDeclinedFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} has declined the meeting.", "The status line indicating meeting request was declined");
            }
        }

        public string MeetingResponseTentativeFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} has tentatively accepted the meeting.", "The status line indicating meeting request was tentatively accepted");
            }
        }

        public string MeetingResponseUnknownFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("The status of {0} is unknown.", "The status line indicating an unknown meeting response");
            }
        }

        #endregion

        public string NoRecipientsFallback {
            get {
                return NSBundle.MainBundle.LocalizedString ("(No Recipients)", "Fallback text to use in a mesasge list when there are no recipients, like for a draft");
            }
        }

        #region Recurrence

        public string RecurrenceDoesNotRepeat {
            get {
                return NSBundle.MainBundle.LocalizedString ("does not repeat", "");
            }
        }

        public string RecurrenceDaily {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats daily", "");
            }
        }

        public string RecurrenceEveryXDaysFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} days", "");
            }
        }

        public string RecurrenceWeekdays {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats on weekdays", "");
            }
        }

        public string RecurrenceWeekends {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats on weekends", "");
            }
        }

        public string RecurrenceWeeklyFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats weekly on {0}", "");
            }
        }

        public string RecurrenceEveryXWeekdaysFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} weeks on weekdays", "");
            }
        }

        public string RecurrenceEveryXWeeksFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} weeks on {1}", "");
            }
        }

        public string RecurrenceMonthlyFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats monthly on day {0}", "");
            }
        }

        public string RecurrenceEveryXMonthsFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} months on day {1}", "");
            }
        }

        public string RecurrenceLastDayOfMonth {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats monthly on the last day of the month", "");
            }
        }

        public string RecurrenceLastWeekdayOfMonth {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats monthly on the last weekday of the month", "");
            }
        }

        public string RecurrenceLastWeekendDayOfMonth {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats monthly on the last weekend day of the month", "");
            }
        }

        public string RecurrenceWeekdaysInWeekOfMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats monthly on weekdays during week {0} of the month", "");
            }
        }

        public string RecurrenceWeekendDaysInWeekOfMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats monthly on weekend days during week {0} of the month", "");
            }
        }

        public string RecurrenceNamedDayInWeekOfMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats monthly on {0} in week {1} of the month", "");
            }
        }

        public string RecurrenceLastNamedDayOfMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats monthly on the last {0} of the month", "");
            }
        }

        public string RecurrenceLastDayOfEveryXMonthsFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} months on the last day of the month", "");
            }
        }

        public string RecurrenceLastWeekdayOfEveryXMonthsFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} months on the last weekday of the month", "");
            }
        }

        public string RecurrenceLastWeekendDayOfEveryXMonthsFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} months on the last weekend day of the month", "");
            }
        }

        public string RecurrenceLastNamedDayOfEveryXMonthsFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} months on the last {1} of the month", "");
            }
        }

        public string RecurrenceWeekdaysInWeekOfEveryXMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} months on the weekdays in week {1} of the month", "");
            }
        }

        public string RecurrenceWeekendDaysInWeekOfEveryXMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} months on the weekend days in week {1} of the month", "");
            }
        }

        public string RecurrenceNamedDayInWeekOfEveryXMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} months on {1} in week {2} of the month", "");
            }
        }

        public string RecurrenceLastDayOfNamedMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats yearly on the last day of {0}", "");
            }
        }

        public string RecurrenceLastWeekdayOfNamedMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats yearly on the last weekday of {0}", "");
            }
        }

        public string RecurrenceLastWeekendDayOfNamedMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats yearly on the last weekend day of {0}", "");
            }
        }

        public string RecurrenceLastNamedDayOfNamedMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats yearly on the last {0} of {1}", "");
            }
        }

        public string RecurrenceWeekdaysInWeekOfNamedMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats yearly on the weekdays in week {0} of {1}", "");
            }
        }

        public string RecurrenceWeekendDaysInWeekOfNamedMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats yearly on the weekend days in week {0} of {1}", "");
            }
        }

        public string RecurrenceNamedDayInWeekOfNamedMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats yearly on the {0} in week {1} of {2}", "");
            }
        }

        public string RecurrenceLastDayOfNamedMonthEveryXYearsFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} years on the last day of {1}", "");
            }
        }

        public string RecurrenceLastWeekdayOfNamedMonthEveryXYearsFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} years on the last weekday of {1}", "");
            }
        }

        public string RecurrenceLastWeekendDayOfNamedMonthEveryXYearsFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} years on the last weekend day of {1}", "");
            }
        }

        public string RecurrenceLastNamedDayOfNamedMonthEveryXYearsFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} years on the last {1} of {2}", "");
            }
        }

        public string RecurrenceWeekdaysInWeekOfNamedMonthEveryXYearsFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} years on the weekdays in week {1} of {2}", "");
            }
        }

        public string RecurrenceWeekendDaysInWeekOfNamedMonthEveryXYearsFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} years on the weekend days in week {1} of {2}", "");
            }
        }

        public string RecurrenceNamedDayInWeekOfNamedMonthEveryXYearsFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} years on the {1} in week {2} of {3}", "");
            }
        }

        public string RecurrenceYearlyFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every year on {0}", "");
            }
        }

        public string RecurrenceEveryXYearsFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats every {0} years on {1}", "");
            }
        }

        public string RecurrenceUnknownMonthFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("month {0} (recurrence)", "");
            }
        }

        public string RecurrenceUnknown {
            get {
                return NSBundle.MainBundle.LocalizedString ("repeats with an unknown frequency", "");
            }
        }

        public string RecurrenceListJoiner {
            get {
                return NSBundle.MainBundle.LocalizedString (", (recurrence)", "");
            }
        }

        public string RecurrenceListFinalJoiner {
            get {
                return NSBundle.MainBundle.LocalizedString (" and (recurrence)", "");
            }
        }

        #endregion

        #region Attachments

        public string AttachmentInlineUnknownFile {
            get {
                return NSBundle.MainBundle.LocalizedString ("Inline Unknown file", "detail label for inline attachment of unknown type");
            }
        }

        public string AttachmentInlineTypedFileFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("Inline {0} file", "detail label for inline attachment of known type");
            }
        }

        public string AttachmentUnknownFile {
            get {
                return NSBundle.MainBundle.LocalizedString ("Unknown file", "detail label for attachment of unknown type");
            }
        }

        public string AttachmentTypedFileFormat {
            get {
                return NSBundle.MainBundle.LocalizedString ("{0} file", "detail label for attachment of known type");
            }
        }

        #endregion

        #region Settings

        public string MaxAgeFilterAllMessages {
            get {
                return NSBundle.MainBundle.LocalizedString ("All messages (max age filter)", "label for max age filter setting for all messages");
            }
        }

        public string MaxAgeFilterOneDay {
            get {
                return NSBundle.MainBundle.LocalizedString ("One day (max age filter)", "label for max age filter setting for one day");
            }
        }

        public string MaxAgeFilterThreeDays {
            get {
                return NSBundle.MainBundle.LocalizedString ("Three days (max age filter)", "label for max age filter setting for three days");
            }
        }

        public string MaxAgeFilterOneWeek {
            get {
                return NSBundle.MainBundle.LocalizedString ("One week (max age filter)", "label for max age filter setting for one week");
            }
        }

        public string MaxAgeFilterTwoWeeks {
            get {
                return NSBundle.MainBundle.LocalizedString ("Two weeks (max age filter)", "label for max age filter setting for two weeks");
            }
        }

        public string MaxAgeFilterOneMonth {
            get {
                return NSBundle.MainBundle.LocalizedString ("One month (max age filter)", "label for max age filter setting for one month");
            }
        }

        public string MaxAgeFilterThreeMonths {
            get {
                return NSBundle.MainBundle.LocalizedString ("Three months (max age filter)", "label for max age filter setting for three months");
            }
        }

        public string MaxAgeFilterSixMonths {
            get {
                return NSBundle.MainBundle.LocalizedString ("Six months (max age filter)", "label for max age filter setting for six months");
            }
        }

        public string NotificationConfigurationHot {
            get {
                return NSBundle.MainBundle.LocalizedString ("Hot (notification config)", "Notification configuration setting label for hot");
            }
        }

        public string NotificationConfigurationVIPs {
            get {
                return NSBundle.MainBundle.LocalizedString ("VIPs (notification config)", "Notification configuration setting label for VIPs");
            }
        }

        public string NotificationConfigirationInbox {
            get {
                return NSBundle.MainBundle.LocalizedString ("Inbox (notification config)", "Notification configuration setting label for inbox");
            }
        }

        public string NotificationConfigirationNone {
            get {
                return NSBundle.MainBundle.LocalizedString ("None (notification config)", "Notification configuration setting label for none");
            }
        }

        #endregion

    }
}
