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
                    return Event.EventIdentifier;
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
                if (Event.IsDetached) {
                    // FIXME Figure out how to handle exceptions.  That's hard because the item for the
                    // exception is not directly linked to the main item for the recurring meeting.
                    // That makes it hard to process the items one at a time.
                    return NcResult.Error("Ignoring exception to recurring event from device calendar.");
                }

                var accountId = McAccount.GetDeviceAccount ().Id;
                var cal = new McCalendar () {
                    Source = McAbstrItem.ItemSource.Device,
                    ServerId = Event.EventIdentifier,
                    AccountId = accountId,
                    OwnerEpoch = SchemaRev,
                };

                cal.AllDayEvent = Event.AllDay;
                cal.DeviceLastUpdate = (null == Event.LastModifiedDate) ? default(DateTime) : Event.LastModifiedDate.ToDateTime ();
                cal.DeviceCreation = (null == Event.CreationDate) ? cal.DeviceLastUpdate : Event.CreationDate.ToDateTime ();
                cal.Subject = Event.Title;
                cal.Location = Event.Location;
                cal.UID = Event.CalendarItemExternalIdentifier;

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

                if (null != Event.Organizer) {
                    cal.OrganizerName = Event.Organizer.Name;
                    cal.OrganizerEmail = TryExtractEmailAddress (Event.Organizer);
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

                TimeZoneInfo timeZone = null;
                if (null != Event.TimeZone) {
                    // iOS's NSTimeZone does not expose the daylight saving transition rules, so there is no way
                    // to construct a TimeZoneInfo object from the NSTimeZone object.  Instead we have to look
                    // up the TimeZoneInfo by its ID.
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById (Event.TimeZone.Name);
                }
                if (null == timeZone) {
                    // If the iOS event didn't specify a time zone, or if a time zone with that ID could not be
                    // found, assume the local time zone.  Time zones only matter for all-day events and recurring
                    // events, so getting the wrong time zone won't be a problem for most events.
                    timeZone = TimeZoneInfo.Local;
                }
                cal.TimeZone = new AsTimeZone (CalendarHelper.SimplifiedTimeZone (timeZone), cal.StartTime).toEncodedTimeZone ();

                if (Event.HasRecurrenceRules) {
                    // The iOS documentation says that only one recurrence rule is supported for a calendar item,
                    // even though the API allows for multiple.  So the app only looks at the first recurrence rule
                    // from the device event.
                    var recurrences = new List<McRecurrence> ();
                    recurrences.Add (ConvertRecurrence (accountId, Event.RecurrenceRules [0],
                        TimeZoneInfo.ConvertTimeFromUtc (cal.StartTime, timeZone)));
                    cal.recurrences = recurrences;
                }

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

                if (null != rule.RecurrenceEnd) {
                    if (0 < rule.RecurrenceEnd.OccurrenceCount) {
                        result.Occurrences = (int)rule.RecurrenceEnd.OccurrenceCount;
                        result.OccurrencesIsSet = true;
                    }
                    if (null != rule.RecurrenceEnd.EndDate) {
                        result.Until = rule.RecurrenceEnd.EndDate.ToDateTime ();
                    }
                }

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

            // FIXME DAVID - we only go a week back and out 6 months. we probably need to expand based on cal view?
            var start = DateTime.UtcNow.AddDays (-7);
            var end = DateTime.UtcNow.AddMonths (6);
            var calendars = Es.GetCalendars (EKEntityType.Event);
            var retval = new List<PlatformCalendarRecordiOS> ();
            var predicate = Es.PredicateForEvents (start.ToNSDate (), end.ToNSDate (), calendars);
            var calEvents = Es.EventsMatching (predicate);
            if (null != calEvents) {
                foreach (var calEvent in calEvents) {
                    retval.Add (new PlatformCalendarRecordiOS () {
                        Event = calEvent,
                    });
                }
            }
            return retval;
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
