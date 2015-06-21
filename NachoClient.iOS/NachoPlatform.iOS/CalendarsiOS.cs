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
        }

        public bool AuthorizationStatus { get { 
                return EKAuthorizationStatus.Authorized == EKEventStore.GetAuthorizationStatus (EKEntityType.Event); 
            } }

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
