//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using EventKit;
using Foundation;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.ActiveSync;

namespace NachoPlatform
{
    public sealed class Calendars : IPlatformCalendars
    {
        private const int SchemaRev = 0;
        private static volatile Calendars instance;
        private static object syncRoot = new Object ();
        private EKEventStore Es;
        private NSObject NotifToken = null;

        public event EventHandler ChangeIndicator;

        private Calendars ()
        {
            EKEventStoreCreate ();
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

        private void Dispatch (NSNotification dummy)
        {
            if (null != ChangeIndicator) {
                ChangeIndicator (this, EventArgs.Empty);
            }
        }

        public class PlatformCalendarRecordiOS : PlatformCalendarRecord
        {
            public EKEvent Event { get; set; }

            public override string ServerId {
                get {
                    if (string.IsNullOrEmpty (Event.EventIdentifier)) {
                        return "";
                    }
                    if (Event.IsDetached || !Event.HasRecurrenceRules) {
                        return Event.EventIdentifier;
                    }
                    // A regular occurrence of a recurring meeting.  Add a date stamp to the ID to distinguish it
                    // from any other occurrences of the same event.  Use the same format for the date stamp that
                    // iOS uses for exceptional occurrences.
                    return string.Format ("{0}/RID={1}", Event.EventIdentifier, (long)Event.StartDate.SecondsSinceReferenceDate);
                }
            }
            public override DateTime LastUpdate {
                get {
                    return (null == Event.LastModifiedDate) ? default(DateTime) : Event.LastModifiedDate.ToDateTime ();
                }
            }

            private string TryExtractEmailAddress (EKParticipant who)
            {
                if (null == who) {
                    return null;
                }
                string maybeEmailAddr = null;
                if (null != who.Url) {
                    maybeEmailAddr = who.Url.ResourceSpecifier;
                    if (null != maybeEmailAddr && EmailHelper.IsValidEmail (maybeEmailAddr)) {
                        return maybeEmailAddr;
                    }
                }
                if (null != who.Description) {
                    var parms = who.Description.Split (';');
                    foreach (var parm in parms) {
                        if (parm.Trim ().StartsWith ("email")) {
                            var kvp = parm.Split ('=');
                            if (2 == kvp.Length) {
                                maybeEmailAddr = kvp [1].Trim ();
                                if (EmailHelper.IsValidEmail (maybeEmailAddr)) {
                                    return maybeEmailAddr;
                                }
                            }
                        }
                    }
                }
                return null;
            }

            public override NcResult ToMcCalendar ()
            {
                var accountId = McAccount.GetDeviceAccount ().Id;

                var cal = new McCalendar ();

                cal.Source = McAbstrItem.ItemSource.Device;
                cal.ServerId = this.ServerId;
                cal.DeviceLastUpdate = (null == Event.LastModifiedDate) ? default(DateTime) : Event.LastModifiedDate.ToDateTime ();
                cal.DeviceCreation = (null == Event.CreationDate) ? cal.DeviceLastUpdate : Event.CreationDate.ToDateTime ();
                cal.UID = Event.CalendarItemExternalIdentifier;
                if (null != Event.Organizer) {
                    cal.OrganizerName = Event.Organizer.Name;
                    cal.OrganizerEmail = TryExtractEmailAddress (Event.Organizer);
                }

                cal.AccountId = accountId;
                cal.OwnerEpoch = SchemaRev;

                cal.AllDayEvent = Event.AllDay;
                cal.Subject = Event.Title;
                cal.Location = Event.Location;

                cal.BusyStatusIsSet = true;
                switch (Event.Availability) {
                case EKEventAvailability.Busy:
                    cal.BusyStatus = NcBusyStatus.Busy;
                    break;
                case EKEventAvailability.Free:
                    cal.BusyStatus = NcBusyStatus.Free;
                    break;
                case EKEventAvailability.Tentative:
                    cal.BusyStatus = NcBusyStatus.Tentative;
                    break;
                case EKEventAvailability.Unavailable:
                    cal.BusyStatus = NcBusyStatus.OutOfOffice;
                    break;
                case EKEventAvailability.NotSupported:
                    // Just don't set it.
                    cal.BusyStatusIsSet = false;
                    break;
                default:
                    cal.BusyStatusIsSet = false;
                    break;
                }

                cal.SetDescription (Event.Notes ?? "", McBody.BodyTypeEnum.PlainText_1);

                cal.ResponseTypeIsSet = true;
                switch (Event.Status) {
                case EKEventStatus.None:
                    cal.ResponseType = NcResponseType.None;
                    break;
                case EKEventStatus.Cancelled:
                    cal.ResponseType = NcResponseType.Declined;
                    break;
                case EKEventStatus.Confirmed:
                    cal.ResponseType = NcResponseType.Accepted;
                    break;
                case EKEventStatus.Tentative:
                    cal.ResponseType = NcResponseType.Tentative;
                    break;
                default:
                    cal.ResponseTypeIsSet = false;
                    break;
                }

                cal.StartTime = Event.StartDate.ToDateTime ();
                cal.EndTime = Event.EndDate.ToDateTime ();
                if (Event.AllDay) {
                    // iOS Calendar stores the end time for an all-day event at one second before midnight.
                    // Nacho Mail wants the end time to be midnight at the end of the last day.  Adjust the
                    // end time to be an integral number of days after the start time.
                    cal.EndTime = cal.StartTime.AddDays (Math.Round ((cal.EndTime - cal.StartTime).TotalDays));
                }

                TimeZoneInfo eventTimeZone = null;
                if (null != Event.TimeZone) {
                    // iOS's NSTimeZone does not expose the daylight saving transition rules, so there is no way
                    // to construct a TimeZoneInfo object from the NSTimeZone object.  Instead we have to look
                    // up the TimeZoneInfo by its ID.
                    try {
                        eventTimeZone = TimeZoneInfo.FindSystemTimeZoneById (Event.TimeZone.Name);
                    } catch (TimeZoneNotFoundException) {
                        Log.Warn (Log.LOG_CALENDAR, "Device event has unknown time zone: {0}", Event.TimeZone.Name);
                        // Leave eventTimeZone set to null, so it will be set to the local time zone below.
                    }
                }
                if (null == eventTimeZone) {
                    // If the iOS event didn't specify a time zone, or if a time zone with that ID could not be
                    // found, assume the local time zone.  Time zones only matter for all-day events and recurring
                    // events, so getting the wrong time zone won't be a problem for most events.
                    eventTimeZone = TimeZoneInfo.Local;
                }
                cal.TimeZone = new AsTimeZone (CalendarHelper.SimplifiedTimeZone (eventTimeZone), cal.StartTime).toEncodedTimeZone ();

                if (null == Event.Organizer) {
                    cal.MeetingStatus = NcMeetingStatus.Appointment;
                } else {
                    if (Event.Organizer.IsCurrentUser) {
                        if (EKEventStatus.Cancelled == Event.Status) {
                            cal.MeetingStatus = NcMeetingStatus.MeetingOrganizerCancelled;
                        } else {
                            cal.MeetingStatus = NcMeetingStatus.MeetingOrganizer;
                        }
                    } else {
                        if (EKEventStatus.Cancelled == Event.Status) {
                            cal.MeetingStatus = NcMeetingStatus.MeetingAttendeeCancelled;
                        } else {
                            cal.MeetingStatus = NcMeetingStatus.MeetingAttendee;
                        }
                    }
                }
                cal.MeetingStatusIsSet = true;

                var attendees = new List<McAttendee> ();
                var ekAttendees = Event.Attendees;
                if (null != ekAttendees) {
                    foreach (var ekAttendee in ekAttendees) {
                        var attendee = new McAttendee () {
                            AccountId = accountId,
                        };

                        attendee.Name = ekAttendee.Name;

                        attendee.AttendeeTypeIsSet = true;
                        switch (ekAttendee.ParticipantRole) {
                        case EKParticipantRole.Chair:
                        case EKParticipantRole.Required:
                            attendee.AttendeeType = NcAttendeeType.Required;
                            break;
                        case EKParticipantRole.Optional:
                            attendee.AttendeeType = NcAttendeeType.Optional;
                            break;
                        case EKParticipantRole.NonParticipant:
                        case EKParticipantRole.Unknown:
                            if (EKParticipantType.Room == ekAttendee.ParticipantType ||
                                EKParticipantType.Resource == ekAttendee.ParticipantType) {
                                attendee.AttendeeType = NcAttendeeType.Resource;
                            } else {
                                attendee.AttendeeType = NcAttendeeType.Unknown;
                            }
                            break;
                        default:
                            attendee.AttendeeTypeIsSet = false;
                            break;
                        }

                        attendee.AttendeeStatusIsSet = true;
                        switch (ekAttendee.ParticipantStatus) {
                        case EKParticipantStatus.Accepted:
                            attendee.AttendeeStatus = NcAttendeeStatus.Accept;
                            break;
                        case EKParticipantStatus.Declined:
                            attendee.AttendeeStatus = NcAttendeeStatus.Decline;
                            break;
                        case EKParticipantStatus.Tentative:
                            attendee.AttendeeStatus = NcAttendeeStatus.Tentative;
                            break;
                        case EKParticipantStatus.Pending:
                            attendee.AttendeeStatus = NcAttendeeStatus.NotResponded;
                            break;
                        case EKParticipantStatus.Completed:
                        case EKParticipantStatus.Delegated:
                        case EKParticipantStatus.InProcess:
                        case EKParticipantStatus.Unknown:
                            attendee.AttendeeStatus = NcAttendeeStatus.ResponseUnknown;
                            break;
                        default:
                            attendee.AttendeeStatusIsSet = false;
                            break;
                        }
                        attendee.Email = TryExtractEmailAddress (ekAttendee);
                        attendees.Add (attendee);
                    }
                    if (0 != attendees.Count) {
                        cal.attendees = attendees;
                    }
                }

                if (!Event.IsDetached && Event.HasRecurrenceRules) {
                    // The iOS documentation says that only one recurrence rule is supported for a calendar item,
                    // even though the API allows for multiple.  So the app only looks at the first recurrence rule
                    // from the device event.
                    var recurrences = new List<McRecurrence> ();
                    recurrences.Add (ConvertRecurrence (McAccount.GetDeviceAccount ().Id, Event.RecurrenceRules [0],
                        TimeZoneInfo.ConvertTimeFromUtc (cal.StartTime, eventTimeZone)));
                    cal.recurrences = recurrences;
                }

                return NcResult.OK (cal);
            }

            /// <summary>
            /// Convert from an iOS recurrence rule to a McRecurrence (which is based on the ActiveSync recurrence
            /// rule).  The two systems represent recurrences differently, so this is not a trivial task.  The iOS
            /// rules allow for more flexibility, so there may be some rules that are not converted correctly.
            /// But the code does not try to detect or report those cases, since there is nothing useful that can
            /// be done with them.  The iOS rule keeps some of the information in the start date of the event, so
            /// the start date and time zone need to be passed in to this method.
            /// </summary>
            private McRecurrence ConvertRecurrence (int accountId, EKRecurrenceRule rule, DateTime localStartTime)
            {
                var result = new McRecurrence ();
                result.AccountId = accountId;

                // The interval is the easiest thing to convert.
                result.Interval = (int)rule.Interval;
                result.IntervalIsSet = true;

                switch (rule.Frequency) {

                case EKRecurrenceFrequency.Daily:
                    // Daily recurrences don't require any extra information.
                    result.Type = NcRecurrenceType.Daily;
                    break;

                case EKRecurrenceFrequency.Weekly:
                    result.Type = NcRecurrenceType.Weekly;
                    if (null == rule.DaysOfTheWeek || 0 == rule.DaysOfTheWeek.Length) {
                        // If the days of the week is not specified, then it is taken from the start date of the event.
                        result.DayOfWeek = CalendarHelper.ToNcDayOfWeek (localStartTime.DayOfWeek);
                    } else {
                        result.DayOfWeek = ConvertDaysOfWeek (rule.DaysOfTheWeek);
                    }
                    result.DayOfWeekIsSet = true;
                    if (EKDay.NotSet != rule.FirstDayOfTheWeek) {
                        result.FirstDayOfWeek = ConvertFirstDayOfWeek (rule.FirstDayOfTheWeek);
                        result.FirstDayOfWeekIsSet = true;
                    }
                    break;

                case EKRecurrenceFrequency.Monthly:
                    if (null == rule.DaysOfTheWeek || 0 == rule.DaysOfTheWeek.Length) {
                        // Monthly event on a numerical day of the month (e.g. "the 25th"), which is taken
                        // from the start date.
                        result.Type = NcRecurrenceType.Monthly;
                        result.DayOfMonth = localStartTime.Day;
                    } else {
                        // Monthly event that happens on a particular day of the week (e.g. "first Monday"
                        // or "fourth Thursday").
                        result.Type = NcRecurrenceType.MonthlyOnDay;
                        result.DayOfWeek = ConvertDaysOfWeek (rule.DaysOfTheWeek);
                        result.DayOfWeekIsSet = true;
                        if (null == rule.SetPositions || 0 == rule.SetPositions.Length) {
                            result.WeekOfMonth = 1;
                        } else {
                            // iOS allows for multiple occurrences in one rule.  ActiveSync does not.  So only the
                            // first occurrence is used.
                            int week = (int)(NSNumber)rule.SetPositions [0];
                            if (0 >= week || 5 <= week) {
                                // iOS allows "next to last" or "3rd from last".  But ActiveSync only allows for
                                // 1st, 2nd, 3rd, 4th, or last.  So anything that isn't 1st, 2nd, 3nd, or 4th gets
                                // translated as "last", which ActiveSync represents as "week 5".
                                result.WeekOfMonth = 5;
                            } else {
                                result.WeekOfMonth = week;
                            }
                        }
                    }
                    break;

                case EKRecurrenceFrequency.Yearly:
                    if (null == rule.DaysOfTheWeek || 0 == rule.DaysOfTheWeek.Length) {
                        // Yearly on a specific month and day, which are taken from the start time of the event.
                        result.Type = NcRecurrenceType.Yearly;
                        result.MonthOfYear = localStartTime.Month;
                        result.DayOfMonth = localStartTime.Day;
                    } else {
                        // Yearly on a particular day of the week within a month.  For reasons that do not have a
                        // good explanation, the week within the month is stored in the DaysOfTheWeek field rather
                        // than the SetPositions field.
                        result.Type = NcRecurrenceType.YearlyOnDay;
                        result.DayOfWeek = ConvertDaysOfWeek (rule.DaysOfTheWeek);
                        result.DayOfWeekIsSet = true;
                        int week = (int)rule.DaysOfTheWeek [0].WeekNumber;
                        if (0 >= week || 5 <= week) {
                            result.WeekOfMonth = 5;
                        } else {
                            result.WeekOfMonth = week;
                        }
                        if (null == rule.MonthsOfTheYear || 0 == rule.MonthsOfTheYear.Length) {
                            result.MonthOfYear = 1;
                        } else {
                            result.MonthOfYear = (int)rule.MonthsOfTheYear [0];
                        }
                    }
                    break;
                }

                // iOS gives us a separate EKEvent for each occurrence of a recurring event, so the app creates a
                // separate McCalendar for each one.  The McRecurrence exists so the recurring nature of the event
                // is shown in the event detail view.  It is not used to generate all of the McEvents.  To get that
                // behavior, set the number of occurrences to 1, no matter what the EKEvent's recurrence rule states.
                result.Occurrences = 1;
                result.OccurrencesIsSet = true;

                return result;
            }

            private NcDayOfWeek ConvertDaysOfWeek (EKRecurrenceDayOfWeek[] daysOfWeek)
            {
                NcDayOfWeek result = (NcDayOfWeek)0;
                foreach (var dow in daysOfWeek) {
                    result = (NcDayOfWeek)((int)result | (1 << ((int)dow.DayOfTheWeek - 1)));
                }
                return result;
            }

            private int ConvertFirstDayOfWeek (EKDay day)
            {
                switch (day) {
                case EKDay.Sunday:
                    return 0;
                case EKDay.Monday:
                    return 1;
                case EKDay.Tuesday:
                    return 2;
                case EKDay.Wednesday:
                    return 3;
                case EKDay.Thursday:
                    return 4;
                case EKDay.Friday:
                    return 5;
                case EKDay.Saturday:
                    return 6;
                default:
                    return 0;
                }
            }
        }

        public bool AuthorizationStatus {
            get {
                return EKAuthorizationStatus.Authorized == EKEventStore.GetAuthorizationStatus (EKEntityType.Event); 
            }
        }

        public bool ShouldWeBotherToAsk ()
        {
            if (EKAuthorizationStatus.NotDetermined == EKEventStore.GetAuthorizationStatus (EKEntityType.Event)) {
                return true;
            }
            // EKAuthorizationStatus.Authorized -- The user already said yes
            // EKAuthorizationStatus.Denied -- The user already said no
            // EKAuthorizationStatus.Restricted -- E.g. parental controls
            return false;
        }

        private void EKEventStoreCreate ()
        {
            if (null != NotifToken) {
                NSNotificationCenter.DefaultCenter.RemoveObserver (NotifToken);
                NotifToken = null;
            }
            Es = new EKEventStore ();
            if (null == Es) {
                Log.Error (Log.LOG_SYS, "new EKEventStore failed");
            }
            // setup external change.
            if (null != Es) {
                NotifToken = NSNotificationCenter.DefaultCenter.AddObserver (EKEventStore.ChangedNotification, Dispatch, Es);
            }
        }

        public void AskForPermission (Action<bool> result)
        {
            EKEventStoreCreate ();
            if (null == Es) {
                result (false);
                return;
            }
            Es.RequestAccess (EKEntityType.Event,
                (granted, reqErr) => {
                    if (null != reqErr) {
                        Log.Error (Log.LOG_SYS, "EKEventStore.RequestAccess: {0}", Contacts.GetNSErrorString (reqErr));
                        result (false);
                    }
                    if (granted) {
                        Log.Info (Log.LOG_SYS, "EKEventStore.RequestAccess authorized.");
                    } else {
                        Log.Info (Log.LOG_SYS, "EKEventStore.RequestAccess not authorized.");
                    }
                    result (granted);
                });
        }

        public IEnumerable<PlatformCalendarRecord> GetCalendars ()
        {
            if (EKEventStore.GetAuthorizationStatus (EKEntityType.Event) != EKAuthorizationStatus.Authorized) {
                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                    Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_NeedCalPermission),
                    Account = ConstMcAccount.NotAccountSpecific,
                });
                return null;
            }

            if (null == Es) {
                return null;
            }

            // Ask for all events from one month ago (which is as far back as the calendar normally goes) until
            // one year into the future.  There is not a way to find out how far into the future the user has
            // scrolled the calendar, nor is there a way for the calendar view to ask for more McCalendar items
            // as the user scrolls.  So we pick what seems like a reasonable time frame for synching device
            // events.  If the user scrolls more than a year into the future, the calendar simply won't have
            // any events from the device calendar.
            var result = new List<PlatformCalendarRecordiOS> ();
            var allCalendars = Es.GetCalendars (EKEntityType.Event);
            var predicate = Es.PredicateForEvents (DateTime.UtcNow.AddDays (-31).ToNSDate (), DateTime.UtcNow.AddYears (1).ToNSDate (), allCalendars);
            var deviceEvents = Es.EventsMatching (predicate);
            if (null != deviceEvents) {
                var cancellationToken = NcTask.Cts.Token;
                foreach (var deviceEvent in deviceEvents) {
                    cancellationToken.ThrowIfCancellationRequested ();
                    result.Add (new PlatformCalendarRecordiOS () {
                        Event = deviceEvent,
                    });
                }
            }
            return result;
        }

        public NcResult Add (McCalendar cal)
        {
            EKEvent ekEvent = null;
            try {
                ekEvent = EKEvent.FromStore (Es);
                ekEvent.StartDate = cal.StartTime.ToNSDate ();
                ekEvent.EndDate = cal.EndTime.ToNSDate ();
                ekEvent.Title = cal.Subject;
                // FIXME DAVID - need full translator here. Also we may need to think about how we target the correct
                // device calendar. worst case would be making it so that there is a device-based nacho account PER 
                // device synced calendar (maybe ugly but not impossible).
                ekEvent.Calendar = Es.DefaultCalendarForNewEvents;
                NSError err;
                Es.SaveEvent ( ekEvent, EKSpan.ThisEvent, out err);
                if (null != err) {
                    Log.Error (Log.LOG_SYS, "Add:Es.SaveEvent: {0}", Contacts.GetNSErrorString (err));
                    return NcResult.Error ("Es.SaveEvent");
                }
                cal.ServerId = ekEvent.EventIdentifier;
                cal.LastModified = ekEvent.LastModifiedDate.ToDateTime ();
                cal.IsAwaitingCreate = false;
                cal.Update ();
                return NcResult.OK ();
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "Calendar.Add: {0}", ex.ToString ());
                return NcResult.Error ("Calendar.Add");
            }
        }

        public NcResult Delete (string serverId)
        {
            try {
                var ekEvent = Es.EventFromIdentifier (serverId);
                if (null == ekEvent) {
                    return NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                }
                NSError err;
                Es.RemoveEvent (ekEvent, EKSpan.ThisEvent, true, out err);
                if (null != err) {
                    Log.Error (Log.LOG_SYS, "Calendar.Delete: {0}", Contacts.GetNSErrorString (err));
                    return NcResult.Error ("Calendar.Delete");
                }
                return NcResult.OK ();
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "Calendar.Delete: {0}", ex.ToString ());
                return NcResult.Error ("Calendar.Delete");
            }
        }

        public NcResult Change (McCalendar cal)
        {
            try {
                var ekEvent = Es.EventFromIdentifier ( cal.ServerId );
                if (null == ekEvent) {
                    return NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                }
                ekEvent.StartDate = cal.StartTime.ToNSDate ();
                // FIXME DAVID - need to fully translate McCalendar - to the extent that we can.
                NSError err;
                Es.SaveEvent ( ekEvent, EKSpan.ThisEvent, out err);
                if (null != err) {
                    Log.Error (Log.LOG_SYS, "Change:Es.SaveEvent: {0}", Contacts.GetNSErrorString (err));
                    return NcResult.Error ("Es.SaveEvent");
                }
                return NcResult.OK ();
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "Calendar.Change: {0}", ex.ToString ());
                return NcResult.Error ("Calendar.Change");
            }
        }
    }
}
