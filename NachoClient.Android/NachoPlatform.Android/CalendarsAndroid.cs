﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Database;
using Android.Provider;
using Android.Text.Format;
using NachoClient.AndroidClient;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoPlatform
{
    /// <summary>
    /// Access the Android device calendar database, other than synching all the events into Nacho Mail.
    /// </summary>
    public static class AndroidCalendars
    {
        private static string[] instancesProjection = new string[] {
            CalendarContract.Instances.EventId,
            CalendarContract.Instances.Begin,
            CalendarContract.Instances.End,
            CalendarContract.Instances.InterfaceConsts.AllDay,
            CalendarContract.Instances.InterfaceConsts.Uid2445,
        };
        private const int INSTANCES_EVENT_ID_INDEX = 0;
        private const int INSTANCES_BEGIN_INDEX = 1;
        private const int INSTANCES_END_INDEX = 2;
        private const int INSTANCES_ALL_DAY_INDEX = 3;
        private const int INSTANCES_UID_INDEX = 4;

        /// <summary>
        /// Create in-memory McEvent objects for all of the device events within the given date range.
        /// The McEvents that are crated will have a negative CalendarId, which is the negative value
        /// of the event's ID in the Android database.
        /// </summary>
        public static List<McEvent> GetDeviceEvents (DateTime startRange, DateTime endRange)
        {
            var resolver = MainApplication.Instance.ContentResolver;
            var uriBuilder = CalendarContract.Instances.ContentSearchUri.BuildUpon ();
            ContentUris.AppendId (uriBuilder, startRange.MillisecondsSinceEpoch ());
            ContentUris.AppendId (uriBuilder, endRange.MillisecondsSinceEpoch ());
            ICursor eventCursor;
            try {
                eventCursor = CalendarContract.Instances.Query (resolver, instancesProjection, startRange.MillisecondsSinceEpoch (), endRange.MillisecondsSinceEpoch ());
            } catch (Exception e) {
                Log.Error (Log.LOG_SYS, "Querying device events failed with {0}", e.ToString ());
                return new List<McEvent> ();
            }

            var deviceAccount = McAccount.GetDeviceAccount ().Id;

            var result = new List<McEvent> ();

            while (eventCursor.MoveToNext ()) {
                long eventId = eventCursor.GetLong (INSTANCES_EVENT_ID_INDEX);
                DateTime start = eventCursor.GetLong (INSTANCES_BEGIN_INDEX).JavaMsToDateTime ();
                DateTime end = eventCursor.GetLong (INSTANCES_END_INDEX).JavaMsToDateTime ();
                bool allDay = eventCursor.GetInt (INSTANCES_ALL_DAY_INDEX) != 0;
                string uid = eventCursor.GetString (INSTANCES_UID_INDEX);

                result.Add (new McEvent () {
                    AccountId = deviceAccount,
                    DeviceEventId = eventId,
                    StartTime = start,
                    EndTime = end,
                    AllDayEvent = allDay,
                    UID = uid,
                });
            }

            return result;
        }

        private static string[] eventSummaryProjection = new string[] {
            CalendarContract.EventsColumns.Title,
            CalendarContract.EventsColumns.EventLocation,
            CalendarContract.EventsColumns.DisplayColor,
        };
        private const int EVENT_SUMMARY_TITLE_INDEX = 0;
        private const int EVENT_SUMMARY_LOCATION_INDEX = 1;
        private const int EVENT_SUMMARY_DISPLAY_COLOR_INDEX = 2;

        /// <summary>
        /// Get some of the details for a particular event in the Android calendar database.
        /// </summary>
        public static bool GetEventDetails (long eventId, out string title, out string location, out int displayColor)
        {
            title = null;
            location = null;
            displayColor = 0;
            var resolver = MainApplication.Instance.ContentResolver;
            ICursor eventCursor;
            try {
                eventCursor = resolver.Query (
                    CalendarContract.Events.ContentUri,
                    eventSummaryProjection,
                    CalendarContract.Events.InterfaceConsts.Id + " = ?",
                    new string[] { eventId.ToString () },
                    null, null);
            } catch (Exception e) {
                Log.Error (Log.LOG_SYS, "Looking up device details failed with {0}", e.ToString ());
                return false;
            }
            if (!eventCursor.MoveToNext ()) {
                return false;
            }
            title = eventCursor.GetString (EVENT_SUMMARY_TITLE_INDEX);
            location = eventCursor.GetString (EVENT_SUMMARY_LOCATION_INDEX);
            displayColor = eventCursor.GetInt (EVENT_SUMMARY_DISPLAY_COLOR_INDEX);

            return true;
        }

        private static string[] eventDetailProjection = new string[] {
            CalendarContract.EventsColumns.Title,
            CalendarContract.EventsColumns.EventLocation,
            CalendarContract.EventsColumns.Description,
            CalendarContract.EventsColumns.Dtstart,
            CalendarContract.EventsColumns.Dtend,
            CalendarContract.EventsColumns.AllDay,
            CalendarContract.EventsColumns.EventTimezone,
            CalendarContract.EventsColumns.Uid2445,
            CalendarContract.EventsColumns.HasAlarm,
            CalendarContract.EventsColumns.HasAttendeeData,
            CalendarContract.CalendarColumns.CalendarDisplayName,
            CalendarContract.EventsColumns.Organizer,
            CalendarContract.EventsColumns.Availability,
            CalendarContract.EventsColumns.SelfAttendeeStatus,
            CalendarContract.EventsColumns.IsOrganizer,
            CalendarContract.EventsColumns.Status,
            CalendarContract.EventsColumns.Rrule,
            CalendarContract.SyncColumns.AccountName,
            CalendarContract.SyncColumns.AccountType,
            CalendarContract.EventsColumns.CalendarId,
        };
        private const int EVENT_DETAIL_TITLE_INDEX = 0;
        private const int EVENT_DETAIL_LOCATION_INDEX = 1;
        private const int EVENT_DETAIL_DESCRIPTION_INDEX = 2;
        private const int EVENT_DETAIL_START_INDEX = 3;
        private const int EVENT_DETAIL_END_INDEX = 4;
        private const int EVENT_DETAIL_ALL_DAY_INDEX = 5;
        private const int EVENT_DETAIL_TIME_ZONE_INDEX = 6;
        private const int EVENT_DETAIL_UID_INDEX = 7;
        private const int EVENT_DETAIL_HAS_ALARM_INDEX = 8;
        private const int EVENT_DETAIL_HAS_ATTENDEES_INDEX = 9;
        private const int EVENT_DETAIL_CALENDAR_NAME_INDEX = 10;
        private const int EVENT_DETAIL_ORGANIZER_INDEX = 11;
        private const int EVENT_DETAIL_AVAILABILITY_INDEX = 12;
        private const int EVENT_DETAIL_RESPONSE_TYPE_INDEX = 13;
        private const int EVENT_DETAIL_IS_ORGANIZER_INDEX = 14;
        private const int EVENT_DETAIL_MEETING_STATUS_INDEX = 15;
        private const int EVENT_DETAIL_RRULE_INDEX = 16;
        private const int EVENT_DETAIL_ACCOUNT_NAME_INDEX = 17;
        private const int EVENT_DETAIL_ACCOUNT_TYPE_INDEX = 18;
        private const int EVENT_DETAIL_CALENDAR_ID_INDEX = 19;

        private static string[] reminderProjection = new string[] {
            CalendarContract.RemindersColumns.Minutes,
        };
        private const int REMINDER_MINUTES_INDEX = 0;

        private static string[] attendeeProjection = new string[] {
            CalendarContract.AttendeesColumns.AttendeeEmail,
            CalendarContract.AttendeesColumns.AttendeeName,
            CalendarContract.AttendeesColumns.AttendeeRelationship,
            CalendarContract.AttendeesColumns.AttendeeStatus,
            CalendarContract.AttendeesColumns.AttendeeType,
        };
        private const int ATTENDEE_EMAIL_INDEX = 0;
        private const int ATTENDEE_NAME_INDEX = 1;
        private const int ATTENDEE_RELATIONSHIP_INDEX = 2;
        private const int ATTENDEE_STATUS_INDEX = 3;
        private const int ATTENDEE_TYPE_INDEX = 4;

        public static McCalendar GetEventDetails (long eventId, out AndroidDeviceCalendarFolder folder)
        {
            folder = null;

            var resolver = MainApplication.Instance.ContentResolver;
            ICursor eventCursor;
            try {
                eventCursor = resolver.Query (
                    CalendarContract.Events.ContentUri,
                    eventDetailProjection,
                    CalendarContract.Events.InterfaceConsts.Id + " = ?",
                    new string[] { eventId.ToString () },
                    null, null);
            } catch (Exception e) {
                Log.Error (Log.LOG_SYS, "Looking up device details failed with {0}", e.ToString ());
                return null;
            }
            if (!eventCursor.MoveToNext ()) {
                return null;
            }

            string accountName = eventCursor.GetString (EVENT_DETAIL_ACCOUNT_NAME_INDEX);
            string accountType = eventCursor.GetString (EVENT_DETAIL_ACCOUNT_TYPE_INDEX);
            string calendarSimpleName = eventCursor.GetString (EVENT_DETAIL_CALENDAR_NAME_INDEX);
            long calendarId = eventCursor.GetLong (EVENT_DETAIL_CALENDAR_ID_INDEX);
            folder = new AndroidDeviceCalendarFolder (calendarId, CalendarDisplayName (accountName, accountType, calendarSimpleName));

            var result = new McCalendar ();

            result.Subject = eventCursor.GetString (EVENT_DETAIL_TITLE_INDEX);
            result.Location = eventCursor.GetString (EVENT_DETAIL_LOCATION_INDEX);
            result.SetDescription (eventCursor.GetString (EVENT_DETAIL_DESCRIPTION_INDEX), McAbstrFileDesc.BodyTypeEnum.PlainText_1);
            result.AllDayEvent = eventCursor.GetInt (EVENT_DETAIL_ALL_DAY_INDEX) != 0;
            result.StartTime = eventCursor.GetLong (EVENT_DETAIL_START_INDEX).JavaMsToDateTime ();
            result.EndTime = eventCursor.GetLong (EVENT_DETAIL_END_INDEX).JavaMsToDateTime ();
            result.UID = eventCursor.GetString (EVENT_DETAIL_UID_INDEX);
            result.OrganizerEmail = eventCursor.GetString (EVENT_DETAIL_ORGANIZER_INDEX);

            bool isOrganizer = eventCursor.GetInt (EVENT_DETAIL_IS_ORGANIZER_INDEX) != 0;
            var meetingResponse = (CalendarAttendeesStatus)eventCursor.GetInt (EVENT_DETAIL_RESPONSE_TYPE_INDEX);
            result.ResponseTypeIsSet = true;
            result.ResponseType = ResponseType (meetingResponse, isOrganizer);
            var busyStatus = (EventsAvailability)eventCursor.GetInt (EVENT_DETAIL_AVAILABILITY_INDEX);
            result.BusyStatusIsSet = true;
            result.BusyStatus = BusyStatus (busyStatus);

            string timeZoneName = eventCursor.GetString (EVENT_DETAIL_TIME_ZONE_INDEX);
            TimeZoneInfo timeZone = TimeZoneInfo.Utc;
            if (null != timeZoneName) {
                try {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById (eventCursor.GetString (EVENT_DETAIL_TIME_ZONE_INDEX));
                } catch (Exception) {
                    Log.Error (Log.LOG_SYS, "Could not find time zone {0}", timeZoneName);
                }
            }
            result.TimeZone = new AsTimeZone (CalendarHelper.SimplifiedTimeZone (timeZone), result.StartTime).toEncodedTimeZone ();

            bool hasAlarm = eventCursor.GetInt (EVENT_DETAIL_HAS_ALARM_INDEX) != 0;
            if (hasAlarm) {
                ICursor reminderCursor = null;
                try {
                    reminderCursor = CalendarContract.Reminders.Query (resolver, eventId, reminderProjection);
                } catch (Exception e) {
                    Log.Error (Log.LOG_SYS, "Looking up reminders failed with {0}", e.ToString ());
                }
                if (null != reminderCursor) {
                    // If there are multiple alarms, pick the one that goes off the earliest.
                    int earliestReminder = 0;
                    while (reminderCursor.MoveToNext ()) {
                        int thisReminder = reminderCursor.GetInt (REMINDER_MINUTES_INDEX);
                        earliestReminder = Math.Max (earliestReminder, thisReminder);
                    }
                    result.ReminderIsSet = true;
                    result.Reminder = (uint)earliestReminder;
                }
            }

            bool hasAttendees = eventCursor.GetInt (EVENT_DETAIL_HAS_ATTENDEES_INDEX) != 0;
            if (hasAttendees) {
                ICursor attendeeCursor = null;
                try {
                    attendeeCursor = CalendarContract.Attendees.Query (resolver, eventId, attendeeProjection);
                } catch (Exception e) {
                    Log.Error (Log.LOG_SYS, "Looking up attendees failed with {0}", e.ToString ());
                }
                if (null != attendeeCursor) {
                    int deviceAccountId = McAccount.GetDeviceAccount ().Id;
                    var attendees = new List<McAttendee> (attendeeCursor.Count);
                    while (attendeeCursor.MoveToNext ()) {
                        var relationship = (CalendarAttendeesRelationship)attendeeCursor.GetInt (ATTENDEE_RELATIONSHIP_INDEX);
                        if (relationship != CalendarAttendeesRelationship.None && relationship != CalendarAttendeesRelationship.Organizer) {
                            var attendee = new McAttendee (deviceAccountId,
                                attendeeCursor.GetString (ATTENDEE_NAME_INDEX),
                                attendeeCursor.GetString (ATTENDEE_EMAIL_INDEX),
                                AttendeeType ((CalendarAttendeesColumn)attendeeCursor.GetInt (ATTENDEE_TYPE_INDEX)),
                                AttendeeStatus ((CalendarAttendeesStatus)attendeeCursor.GetInt (ATTENDEE_STATUS_INDEX)));
                            attendees.Add (attendee);
                        }
                    }
                    result.attendees = attendees;
                }
            }

            var recurrence = ParseRecurrence (eventCursor.GetString (EVENT_DETAIL_RRULE_INDEX));
            if (null != recurrence) {
                var recurrences = new List<McRecurrence> (1);
                recurrences.Add (recurrence);
                result.recurrences = recurrences;
            }

            bool isCancelled = (EventsStatus)eventCursor.GetInt (EVENT_DETAIL_MEETING_STATUS_INDEX) == EventsStatus.Canceled;

            result.MeetingStatusIsSet = true;
            if (0 == result.attendees.Count) {
                result.MeetingStatus = NcMeetingStatus.Appointment;
            } else {
                if (isOrganizer) {
                    if (isCancelled) {
                        result.MeetingStatus = NcMeetingStatus.MeetingOrganizerCancelled;
                    } else {
                        result.MeetingStatus = NcMeetingStatus.MeetingOrganizer;
                    }
                } else {
                    if (isCancelled) {
                        result.MeetingStatus = NcMeetingStatus.MeetingAttendeeCancelled;
                    } else {
                        result.MeetingStatus = NcMeetingStatus.MeetingAttendee;
                    }
                }
            }

            return result;
        }

        static string CalendarDisplayName (string accountName, string accountType, string calendarName)
        {
            string accountPart = accountName;
            if (accountName == calendarName) {
                accountPart = accountType;
                if (CalendarContract.AccountTypeLocal == accountType) {
                    accountPart = "Local";
                } else if ("com.google" == accountType) {
                    accountPart = "Gmail";
                } else if ("com.android.exchange" == accountType) {
                    accountPart = "Exchange";
                }
            }
            return string.Format ("{0} : {1}", accountPart, calendarName);
        }

        static NcResponseType ResponseType (CalendarAttendeesStatus response, bool isOrganizer)
        {
            if (isOrganizer) {
                return NcResponseType.Organizer;
            }
            switch (response) {
            case CalendarAttendeesStatus.Accepted:
                return NcResponseType.Accepted;
            case CalendarAttendeesStatus.Tentative:
                return NcResponseType.Tentative;
            case CalendarAttendeesStatus.Declined:
                return NcResponseType.Declined;
            case CalendarAttendeesStatus.Invited:
                return NcResponseType.NotResponded;
            case CalendarAttendeesStatus.None:
                return NcResponseType.None;
            }
            return NcResponseType.None;
        }

        static NcBusyStatus BusyStatus (EventsAvailability busy) {
            switch (busy) {
            case EventsAvailability.Busy:
                return NcBusyStatus.Busy;
            case EventsAvailability.Free:
                return NcBusyStatus.Free;
            case EventsAvailability.Tentative:
                return NcBusyStatus.Tentative;
            }
            return NcBusyStatus.Busy;
        }

        static NcAttendeeType AttendeeType (CalendarAttendeesColumn attendeeType)
        {
            switch (attendeeType) {
            case CalendarAttendeesColumn.None:
                return NcAttendeeType.Unknown;
            case CalendarAttendeesColumn.Optional:
                return NcAttendeeType.Optional;
            case CalendarAttendeesColumn.Required:
                return NcAttendeeType.Required;
            case CalendarAttendeesColumn.Resource:
                return NcAttendeeType.Resource;
            }
            return NcAttendeeType.Unknown;
        }

        static NcAttendeeStatus AttendeeStatus (CalendarAttendeesStatus attendeeStatus) {
            switch (attendeeStatus) {
            case CalendarAttendeesStatus.Accepted:
                return NcAttendeeStatus.Accept;
            case CalendarAttendeesStatus.Tentative:
                return NcAttendeeStatus.Tentative;
            case CalendarAttendeesStatus.Declined:
                return NcAttendeeStatus.Decline;
            case CalendarAttendeesStatus.Invited:
                return NcAttendeeStatus.NotResponded;
            case CalendarAttendeesStatus.None:
                return NcAttendeeStatus.ResponseUnknown;
            }
            return NcAttendeeStatus.ResponseUnknown;
        }

        static McRecurrence ParseRecurrence (string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) {
                return null;
            }

            var result = new McRecurrence ();
            var rule = new DDay.iCal.RecurrencePattern (pattern);

            result.IntervalIsSet = true;
            result.Interval = rule.Interval;

            switch (rule.Frequency) {

            case DDay.iCal.FrequencyType.None:
            case DDay.iCal.FrequencyType.Secondly:
            case DDay.iCal.FrequencyType.Minutely:
            case DDay.iCal.FrequencyType.Hourly:
                Log.Error (Log.LOG_CALENDAR, "Unsupported recurrence type from Android device calendar item: {0}", rule.Frequency.ToString ());
                result.Type = NcRecurrenceType.Daily;
                result.Interval = 1;
                break;

            case DDay.iCal.FrequencyType.Daily:
                result.Type = NcRecurrenceType.Daily;
                break;

            case DDay.iCal.FrequencyType.Weekly:
                result.Type = NcRecurrenceType.Weekly;
                result.DayOfWeekIsSet = true;
                result.DayOfWeek = ToNcDayOfWeek (rule.ByDay);
                result.FirstDayOfWeekIsSet = true;
                result.FirstDayOfWeek = (int)rule.FirstDayOfWeek;
                break;

            case DDay.iCal.FrequencyType.Monthly:
                if (0 < rule.ByMonthDay.Count) {
                    result.Type = NcRecurrenceType.Monthly;
                    result.DayOfMonth = rule.ByMonthDay [0];
                } else if (0 < rule.ByDay.Count) {
                    result.Type = NcRecurrenceType.MonthlyOnDay;
                    int weekOfMonth = rule.ByDay [0].Offset;
                    if (weekOfMonth < 1 || 5 < weekOfMonth) {
                        weekOfMonth = 5;
                    }
                    result.WeekOfMonth = weekOfMonth;
                    result.DayOfWeek = CalendarHelper.ToNcDayOfWeek (rule.ByDay [0].DayOfWeek);
                } else {
                    result.Type = NcRecurrenceType.Monthly;
                    result.DayOfMonth = 1;
                }
                break;

            case DDay.iCal.FrequencyType.Yearly:
                if (0 < rule.ByMonth.Count && 0 < rule.ByMonthDay.Count) {
                    result.Type = NcRecurrenceType.Yearly;
                    result.MonthOfYear = rule.ByMonth [0];
                    result.DayOfMonth = rule.ByMonthDay [0];
                } else if (0 < rule.ByMonth.Count && 0 < rule.ByDay.Count) {
                    result.Type = NcRecurrenceType.YearlyOnDay;
                    result.MonthOfYear = rule.ByMonth [0];
                    int weekOfMonth = rule.ByDay [0].Offset;
                    if (weekOfMonth < 1 || 5 < weekOfMonth) {
                        weekOfMonth = 5;
                    }
                    result.WeekOfMonth = weekOfMonth;
                    result.DayOfWeek = CalendarHelper.ToNcDayOfWeek (rule.ByDay [0].DayOfWeek);
                } else {
                    result.Type = NcRecurrenceType.Yearly;
                    result.MonthOfYear = 1;
                    result.DayOfMonth = 1;
                }
                break;
            }

            return result;
        }

        static NcDayOfWeek ToNcDayOfWeek (IList<DDay.iCal.IWeekDay> icalDays)
        {
            var result = (NcDayOfWeek)0;
            foreach (var icalDay in icalDays) {
                result = result | CalendarHelper.ToNcDayOfWeek (icalDay.DayOfWeek);
            }
            if (0 == result) {
                result = NcDayOfWeek.Sunday;
            }
            return result;
        }

        private static ContentValues McCalendarToAndroidEvent (McCalendar cal, long calendarId)
        {
            var values = new ContentValues ();

            SetIfExists (values, CalendarContract.EventsColumns.Title, cal.Subject);
            SetIfExists (values, CalendarContract.EventsColumns.EventLocation, cal.Location);
            SetIfExists (values, CalendarContract.EventsColumns.Description, cal.Description);
            SetIfExists (values, CalendarContract.EventsColumns.Uid2445, cal.UID);
            SetIfExists (values, CalendarContract.EventsColumns.Organizer, cal.OrganizerEmail);

            if (0 != calendarId) {
                values.Put (CalendarContract.EventsColumns.CalendarId, calendarId);
            }

            values.Put (CalendarContract.EventsColumns.AllDay, cal.AllDayEvent);
            if (cal.AllDayEvent) {
                // Android requires that all-day events be in the UTC time zone.
                values.Put (CalendarContract.EventsColumns.Dtstart, DateTime.SpecifyKind (cal.StartTime, DateTimeKind.Utc).MillisecondsSinceEpoch ());
                values.Put (CalendarContract.EventsColumns.Dtend, DateTime.SpecifyKind (cal.EndTime, DateTimeKind.Utc).MillisecondsSinceEpoch ());
                values.Put (CalendarContract.EventsColumns.EventTimezone, Time.TimezoneUtc);
            } else {
                values.Put (CalendarContract.EventsColumns.Dtstart, cal.StartTime.MillisecondsSinceEpoch ());
                values.Put (CalendarContract.EventsColumns.Dtend, cal.EndTime.MillisecondsSinceEpoch ());
                values.Put (CalendarContract.EventsColumns.EventTimezone, Time.CurrentTimezone);
            }

            if (cal.BusyStatusIsSet) {
                values.Put (CalendarContract.EventsColumns.Availability, (int)BusyStatus (cal.BusyStatus));
            }

            return values;
        }

        private static string[] reminderIdProjection = new string[] {
            CalendarContract.Reminders.InterfaceConsts.Id,
        };
        private const int REMINDER_ID_INDEX = 0;

        public static void UpdateDeviceEvent (McCalendar cal, long eventId, long calendarId)
        {
            var resolver = MainApplication.Instance.ContentResolver;
            var values = McCalendarToAndroidEvent (cal, calendarId);
            resolver.Update (ContentUris.AppendId (CalendarContract.Events.ContentUri.BuildUpon (), eventId).Build (), values, null, null);

            UpdateEventReminder (cal, eventId, removeExisting: true);
        }

        public static void InsertDeviceEvent (McCalendar cal, long calendarId)
        {
            var resolver = MainApplication.Instance.ContentResolver;
            var values = McCalendarToAndroidEvent (cal, calendarId);
            var newEventUri = resolver.Insert (CalendarContract.Events.ContentUri, values);

            UpdateEventReminder (cal, ContentUris.ParseId (newEventUri), removeExisting: false);
        }

        public static void UpdateEventReminder (McAbstrCalendarRoot cal, long eventId, bool removeExisting)
        {
            var resolver = MainApplication.Instance.ContentResolver;

            if (removeExisting) {
                ICursor reminderCursor = null;
                try {
                    reminderCursor = CalendarContract.Reminders.Query (resolver, eventId, reminderIdProjection);
                } catch (Exception e) {
                    Log.Error (Log.LOG_SYS, "Looking up reminders failed with {0}", e.ToString ());
                }
                if (null != reminderCursor) {
                    while (reminderCursor.MoveToNext ()) {
                        long reminderId = reminderCursor.GetLong (REMINDER_ID_INDEX);
                        resolver.Delete (ContentUris.AppendId (CalendarContract.Reminders.ContentUri.BuildUpon (), reminderId).Build (), null, null);
                    }
                }
            }
            if (cal.HasReminder ()) {
                var reminderValues = new ContentValues ();
                reminderValues.Put (CalendarContract.RemindersColumns.EventId, eventId);
                reminderValues.Put (CalendarContract.RemindersColumns.Minutes, (int)cal.GetReminder ());
                reminderValues.Put (CalendarContract.RemindersColumns.Method, (int)RemindersMethod.Default);
                resolver.Insert (CalendarContract.Reminders.ContentUri, reminderValues);
            }
        }

        static void SetIfExists (ContentValues values, string fieldName, string value)
        {
            if (!string.IsNullOrEmpty (value)) {
                values.Put (fieldName, value);
            }
        }

        static EventsAvailability BusyStatus (NcBusyStatus busy)
        {
            switch (busy) {
            case NcBusyStatus.Busy:
            case NcBusyStatus.OutOfOffice:
                return EventsAvailability.Busy;
            case NcBusyStatus.Free:
                return EventsAvailability.Free;
            case NcBusyStatus.Tentative:
                return EventsAvailability.Tentative;
            }
            return EventsAvailability.Busy;
        }

        static CalendarAttendeesStatus ResponseType (NcResponseType response)
        {
            switch (response) {
            case NcResponseType.Accepted:
                return CalendarAttendeesStatus.Accepted;
            case NcResponseType.Tentative:
                return CalendarAttendeesStatus.Tentative;
            case NcResponseType.Declined:
                return CalendarAttendeesStatus.Declined;
            case NcResponseType.NotResponded:
                return CalendarAttendeesStatus.Invited;
            }
            return CalendarAttendeesStatus.None;
        }

        public static void DeleteEvent (long eventId)
        {
            var resolver = MainApplication.Instance.ContentResolver;
            resolver.Delete (ContentUris.AppendId (CalendarContract.Events.ContentUri.BuildUpon (), eventId).Build (), null, null);
        }

        private static string[] calendarsExistProjection = {
            CalendarContract.Calendars.InterfaceConsts.Id,
        };

        public static bool DeviceCalendarsExist ()
        {
            try {
                var cursor = MainApplication.Instance.ContentResolver.Query (CalendarContract.Calendars.ContentUri, calendarsExistProjection, null, null, null);
                return null != cursor && 0 < cursor.Count;
            } catch (Exception) {
                return false;
            }
        }

        private static string[] calendarProjection = {
            CalendarContract.Calendars.InterfaceConsts.Id,
            CalendarContract.CalendarColumns.CalendarDisplayName,
            CalendarContract.SyncColumns.AccountName,
            CalendarContract.SyncColumns.AccountType,
        };
        private const int CALENDAR_ID_INDEX = 0;
        private const int CALENDAR_DISPLAY_NAME_INDEX = 1;
        private const int CALENDAR_ACCOUNT_NAME_INDEX = 2;
        private const int CALENDAR_ACCOUNT_TYPE_INDEX = 3;

        public static List<McFolder> GetCalendarFolders ()
        {
            var resolver = MainApplication.Instance.ContentResolver;
            ICursor calendarCursor = null;
            try {
                calendarCursor = resolver.Query (CalendarContract.Calendars.ContentUri, calendarProjection, null, null, null);
            } catch (Exception e) {
                Log.Error (Log.LOG_SYS, "Looking up calendars failed with {0}", e.ToString ());
                return new List<McFolder> ();
            }
            if (null == calendarCursor) {
                return new List<McFolder> ();
            }

            var result = new List<McFolder> ();
            while (calendarCursor.MoveToNext ()) {
                string accountName = calendarCursor.GetString (CALENDAR_ACCOUNT_NAME_INDEX);
                string accountType = calendarCursor.GetString (CALENDAR_ACCOUNT_TYPE_INDEX);
                string calendarName = calendarCursor.GetString (CALENDAR_DISPLAY_NAME_INDEX);
                long calendarId = calendarCursor.GetLong (CALENDAR_ID_INDEX);
                result.Add (new AndroidDeviceCalendarFolder (calendarId, CalendarDisplayName (accountName, accountType, calendarName)));
            }
            return result;
        }
    }

    public class AndroidDeviceCalendarFolder : McFolder
    {
        public long DeviceCalendarId { get; set; }

        public AndroidDeviceCalendarFolder (long calendarId, string displayName)
        {
            this.AccountId = McAccount.GetDeviceAccount ().Id;
            this.DeviceCalendarId = calendarId;
            this.DisplayName = displayName;
            this.Type = Xml.FolderHierarchy.TypeCode.UserCreatedCal_13;
        }
    }

    public sealed class Calendars : IPlatformCalendars
    {
        private const int SchemaRev = 0;
        private static volatile Calendars instance;
        private static object syncRoot = new Object ();

        private Calendars ()
        {
        }

        public static Calendars Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new Calendars ();
                        }
                    }
                }
                return instance;
            }
        }

        public void AskForPermission (Action<bool> result)
        {
            // Permissions are controlled by the app's manifest.  They aren't changed at runtime.
        }

        public void GetCalendars (out IEnumerable<PlatformCalendarFolderRecord> folders, out IEnumerable<PlatformCalendarRecord> events)
        {
            // On Android, calendars are not synched.  Calendar items are accessed on demand.
            folders = null;
            events = null;
        }

        public event EventHandler ChangeIndicator;

        public NcResult Add (McCalendar contact)
        {
            return NcResult.Error ("Android Calendars.Add not yet implemented.");
        }

        public NcResult Delete (string serverId)
        {
            return NcResult.Error ("Android Calendars.Delete not yet implemented.");
        }

        public NcResult Change (McCalendar contact)
        {
            return NcResult.Error ("Android Calendars.Change not yet implemented.");
        }

        public bool AuthorizationStatus {
            get {
                return false;
            }
        }

        public void DeviceCalendarChanged ()
        {
            if (null != ChangeIndicator) {
                ChangeIndicator (this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// A listener for changes to the device calendar.
    /// </summary>
    [BroadcastReceiver (Enabled = true)]
    [IntentFilter (new[] { Intent.ActionProviderChanged }, DataScheme="content", DataHost="com.android.calendar")]
    public class AndroidCalendarChangeListener : BroadcastReceiver
    {
        public override void OnReceive (Context context, Intent intent)
        {
            Calendars.Instance.DeviceCalendarChanged ();
        }
    }
}

