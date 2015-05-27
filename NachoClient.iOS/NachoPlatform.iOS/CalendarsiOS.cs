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
        private EKEventStore EventStore;

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

        public class PlatformCalendarRecordiOS : PlatformCalendarRecord
        {
            public EKEvent Event { get; set; }

            public override string UniqueId { get { return Event.EventIdentifier + Event.StartDate.ToString (); } }

            public override DateTime LastUpdate { get { return Event.LastModifiedDate.ToDateTime (); } }

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
                    ServerId = "NachoDeviceCalendar:" + UniqueId,
                    AccountId = accountId,
                    OwnerEpoch = SchemaRev,
                };

                cal.AllDayEvent = Event.AllDay;
                cal.DeviceLastUpdate = Event.LastModifiedDate.ToDateTime ();
                cal.DeviceCreation = (null == Event.CreationDate) ? cal.DeviceLastUpdate : Event.CreationDate.ToDateTime ();
                cal.DeviceUniqueId = UniqueId;

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

                if (null != Event.Organizer) {
                    cal.OrganizerName = Event.Organizer.Name;
                    cal.OrganizerEmail = TryExtractEmailAddress (Event.Organizer);
                }

                var body = McBody.InsertFile (accountId, McAbstrFileDesc.BodyTypeEnum.PlainText_1, Event.Notes ?? "");
                cal.BodyId = body.Id;

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

        public void AskForPermission (Action<bool> result)
        {
            var eventStore = new EKEventStore ();
            if (null == eventStore) {
                result (false);
                return;
            }
            eventStore.RequestAccess (EKEntityType.Event,
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

            if (null == EventStore) {
                EventStore = new EKEventStore ();
            }

            // TODO: progressive pull with limits.
            var start = DateTime.Now.AddDays (-7);
            var end = DateTime.Now.AddMonths (1);
            var calendars = EventStore.GetCalendars (EKEntityType.Event);
            var retval = new List<PlatformCalendarRecordiOS> ();
            var ignoredSources = new List<string> ();
            foreach (var calendar in calendars) {
                var account = McAccount.QueryByEmailAddr (calendar.Title.Trim ()).FirstOrDefault ();
                if (null != calendar.Title && null != account && 
                    McAccount.AccountCapabilityEnum.CalReader == 
                    (account.AccountCapability & McAccount.AccountCapabilityEnum.CalReader)) {
                    // This is probably one of our accounts - note it as a source we want to ignore.
                    if (null != calendar.Source && null != calendar.Source.Title) {
                        ignoredSources.Add (calendar.Source.Title.Trim ());
                    } else {
                        Log.Warn (Log.LOG_SYS, "GetCalendars: could not exclude calendar source.");
                    }
                }
            }
            calendars = calendars.Where (x => null == x.Source || null == x.Source.Title ||
            !ignoredSources.Contains (x.Source.Title.Trim ())).ToArray ();
            var predicate = EventStore.PredicateForEvents (start.ToNSDate (), end.ToNSDate (), calendars);
            var calEvents = EventStore.EventsMatching (predicate);
            if (null != calEvents) {
                foreach (var calEvent in calEvents) {
                    retval.Add (new PlatformCalendarRecordiOS () {
                        Event = calEvent,
                    });
                }
            }
            return retval;
        }
    }
}

