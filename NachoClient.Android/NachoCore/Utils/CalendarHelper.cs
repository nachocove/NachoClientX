//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using DDay.iCal;
using DDay.iCal.Serialization.iCalendar;
using MimeKit;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.ActiveSync;

namespace NachoCore.Utils
{
    public class CalendarHelper
    {
        public CalendarHelper ()
        {
        }

        /// <summary>
        /// Create a new McCalendar item.  The only fields that are initialized are the start time and end time.
        /// If no start or end dates are specified, then the events starts on the next half hour and runs for an
        /// hour.  If start and end dates are specified, then the event starts on the next half hour on that date
        /// and ends an hour later on the given end date.
        /// </summary>
        /// <param name="startDate">Start date. Only the date is significant; the time portion is ignored.</param>
        /// <param name="endDate">End date. Only the date is significant; the time portion is ignored.</param>
        public static McCalendar DefaultMeeting (DateTime startDate = default (DateTime), DateTime endDate = default (DateTime))
        {
            var cal = new McCalendar ();

            DateTime localNow = DateTime.Now;
            DateTime halfHour = new DateTime (localNow.Year, localNow.Month, localNow.Day, localNow.Hour, localNow.Minute >= 30 ? 30 : 0, 0, DateTimeKind.Local);
            DateTime localStart = halfHour.AddMinutes (30);

            if (default (DateTime) == startDate) {
                cal.StartTime = localStart.ToUniversalTime ();
            } else {
                cal.StartTime = new DateTime (startDate.Year, startDate.Month, startDate.Day, localStart.Hour, localStart.Minute, 0, DateTimeKind.Local).ToUniversalTime ();
            }

            if (default (DateTime) == endDate) {
                cal.EndTime = cal.StartTime.AddHours (1);
            } else {
                DateTime localEnd = localStart.AddHours (1);
                DateTime utcEnd = new DateTime (endDate.Year, endDate.Month, endDate.Day, localEnd.Hour, localEnd.Minute, 0, DateTimeKind.Local).ToUniversalTime ();
                if (utcEnd < cal.StartTime) {
                    // This can happen if the arguments startDate and endDate are on the same day
                    // and the local time is between 22:30 and 23:30.  In this case, behave as if
                    // the endDate had not been specified.
                    utcEnd = cal.StartTime.AddHours (1);
                }
                cal.EndTime = utcEnd;
            }

            return cal;
        }

        public static McTask DefaultTask ()
        {
            var t = new McTask ();
            return t;
        }

        public static bool IsOrganizer (string organizerEmail, string userEmail)
        {
            return organizerEmail == userEmail;
        }

        public static DateTime ReturnAllDayEventEndTime (DateTime date)
        {
            return date.AddDays (-1);
        }

        public static McCalendar GetMcCalendarRootForEvent (int eventId)
        {
            var e = McEvent.QueryById<McEvent> (eventId);
            if (null == e) {
                return null;  // may be deleted
            }
            return McCalendar.QueryById<McCalendar> (e.CalendarId);
        }

        public static void CancelOccurrence (McCalendar cal, DateTime occurrence)
        {
            var exceptions = new List<McException> (cal.exceptions);
            McException exception = null;
            foreach (var e in exceptions) {
                if (e.ExceptionStartTime == occurrence) {
                    exception = e;
                    break;
                }
            }
            if (null == exception) {
                exception = new McException () {
                    AccountId = cal.AccountId,
                    CalendarId = cal.Id,
                    ExceptionStartTime = occurrence,
                    Deleted = 1,
                };
                exceptions.Add (exception);
            } else {
                exception.Deleted = 1;
            }
            cal.exceptions = exceptions;
            cal.RecurrencesGeneratedUntil = DateTime.MinValue;
            cal.Update ();
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Status = NcResult.Info (NcResult.SubKindEnum.Info_CalendarChanged),
                Account = McAccount.QueryById<McAccount> (cal.AccountId),
            });
        }

        public static void MarkEventAsCancelled (McMeetingRequest eventInfo)
        {
            var cal = McCalendar.QueryByUID (eventInfo.AccountId, eventInfo.GetUID ());
            if (null == cal) {
                // Google servers delete the event as soon at is sees the cancellation notice.
                // Or this could be the cancelation notice for a meeting that the user has declined.
                // So this is a normal case and should not result in an error message.
                return;
            }
            var account = McAccount.QueryById<McAccount> (eventInfo.AccountId);
            if (null == account || account.EmailAddr == cal.OrganizerEmail) {
                // The user owns the meeting being processed.  Most likely, this is the outgoing cancelation
                // notice showing up in the Sent folder.  Processing this would normally have no effect, since
                // the meeting has already been deleted locally.  But there is a bug on some of the servers
                // that causes a bad effect.  When canceling a single occurrence of a recurring meeting, the
                // server leaves out the RecurrenceId from the MeetingRequest element.  This results in the
                // entire meeting being canceled rather than the one occurrence.  To work around this bug,
                // ignore messages where the user is the organizer.
                return;
            }
            if (DateTime.MinValue == eventInfo.RecurrenceId) {
                // Cancel the entire meeting, not just one occurrence.  This is the easy case.
                if (cal.MeetingStatusIsSet && (NcMeetingStatus.MeetingOrganizerCancelled == cal.MeetingStatus || NcMeetingStatus.MeetingAttendeeCancelled == cal.MeetingStatus)) {
                    // Someone, most likely the server, has already marked the meeting as cancelled.
                    // No need to update the item again.
                    return;
                }
                cal.MeetingStatusIsSet = true;
                cal.MeetingStatus = NcMeetingStatus.MeetingAttendeeCancelled;
                cal.Subject = "Canceled: " + cal.Subject;
            } else {
                // Only one occurrence of a recurring meeting has been cancelled.
                var allExceptions = new List<McException> (cal.exceptions);
                var exceptions = allExceptions.Where (x => x.ExceptionStartTime == eventInfo.RecurrenceId).ToList ();
                if (0 == exceptions.Count) {
                    var exception = new McException () {
                        AccountId = cal.AccountId,
                        ExceptionStartTime = eventInfo.RecurrenceId,
                        StartTime = eventInfo.StartTime,
                        EndTime = eventInfo.EndTime,
                        Subject = "Canceled: " + cal.Subject,
                        MeetingStatusIsSet = true,
                        MeetingStatus = NcMeetingStatus.MeetingAttendeeCancelled,
                    };
                    allExceptions.Add (exception);
                } else {
                    if (1 < exceptions.Count) {
                        Log.Error (Log.LOG_CALENDAR,
                            "{0} exceptions were found for calendar item {1} ({2}) with recurrence ID {3}. There should be at most one exception.",
                            exceptions.Count, cal.Id, cal.ServerId, eventInfo.RecurrenceId);
                    }
                    foreach (var exception in exceptions) {
                        if (0 == exception.Deleted &&
                            (!exception.MeetingStatusIsSet ||
                            (NcMeetingStatus.MeetingOrganizerCancelled != exception.MeetingStatus && NcMeetingStatus.MeetingAttendeeCancelled != exception.MeetingStatus))) {
                            exception.MeetingStatusIsSet = true;
                            exception.MeetingStatus = NcMeetingStatus.MeetingAttendeeCancelled;
                            exception.Subject = "Canceled: " + exception.GetSubject ();
                        }
                    }
                }
                cal.exceptions = allExceptions;
            }
            cal.RecurrencesGeneratedUntil = DateTime.MinValue;
            cal.Update ();
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Status = NcResult.Info (NcResult.SubKindEnum.Info_CalendarChanged),
                Account = McAccount.QueryById<McAccount> (cal.AccountId),
            });
        }

        private static void AddAttendeesAndOrganizerToiCalEvent (IEvent evt, McCalendar c, IList<McAttendee> attendees)
        {
            NcAssert.False (string.IsNullOrEmpty (c.OrganizerEmail), "OrganizerEmail is empty/null");
            evt.Organizer = new Organizer (c.OrganizerEmail);
            evt.Organizer.CommonName = c.OrganizerName;
            foreach (var mcAttendee in attendees) {
                try {
                    var iAttendee = new Attendee (EmailHelper.MailToUri (mcAttendee.Email));
                    NcAssert.True (null != mcAttendee.Name);
                    iAttendee.CommonName = mcAttendee.Name;
                    NcAssert.True (mcAttendee.AttendeeTypeIsSet);
                    switch (mcAttendee.AttendeeType) {
                    case NcAttendeeType.Required:
                        iAttendee.RSVP = c.ResponseRequestedIsSet && c.ResponseRequested;
                        iAttendee.Role = "REQ-PARTICIPANT";
                        iAttendee.ParticipationStatus = DDay.iCal.ParticipationStatus.NeedsAction;
                        iAttendee.Type = "INDIVIDUAL";
                        break;
                    case NcAttendeeType.Optional:
                        iAttendee.RSVP = c.ResponseRequestedIsSet && c.ResponseRequested;
                        iAttendee.Role = "OPT-PARTICIPANT";
                        iAttendee.ParticipationStatus = DDay.iCal.ParticipationStatus.NeedsAction;
                        iAttendee.Type = "INDIVIDUAL";
                        break;
                    case NcAttendeeType.Unknown:
                        iAttendee.Role = "NON-PARTICIPANT";
                        break;
                    }
                    evt.Attendees.Add (iAttendee);
                } catch (UriFormatException e) {
                    // The McAttendee somehow got an e-mail address that can't be parsed as an e-mail address.
                    // I'm not sure how that happened, but it shouldn't crash the app.  Just ignore this
                    // attendee.
                    Log.Error (Log.LOG_CALENDAR, "McAttendee email address {0} could not be parsed: {1}", mcAttendee.Email, e.Message);
                }
            }
        }

        /// <summary>
        /// Return the appropriate iCalendar PARTSTAT string for the given NcResponseType.
        /// </summary>
        private static string iCalResponseString (NcResponseType response)
        {
            switch (response) {
            case NcResponseType.Accepted:
                return DDay.iCal.ParticipationStatus.Accepted;
            case NcResponseType.Tentative:
                return DDay.iCal.ParticipationStatus.Tentative;
            case NcResponseType.Declined:
                return DDay.iCal.ParticipationStatus.Declined;
            default:
                Log.Error (Log.LOG_CALENDAR, "Unexpected response value: {0}. Should be Accepted, Tentative, or Declined.", response);
                return DDay.iCal.ParticipationStatus.NeedsAction;
            }
        }

        /// <summary>
        /// Return the appropriate prefix for the subject of the meeting response e-mail message.
        /// </summary>
        private static string ResponseSubjectPrefix (NcResponseType response)
        {
            // These are user-visible strings that should be translated.
            switch (response) {
            case NcResponseType.Accepted:
                return "Accepted";
            case NcResponseType.Tentative:
                return "Tentative";
            case NcResponseType.Declined:
                return "Declined";
            default:
                Log.Error (Log.LOG_CALENDAR, "Unexpected response value: {0}. Should be Accepted, Tentative, or Declined.", response);
                return "Unknown";
            }
        }

        private static void PopulateiCalTimeZoneInfo (ITimeZoneInfo tzi, System.TimeZoneInfo.TransitionTime transition, int year)
        {
            //            Calendar c = CultureInfo.CurrentCulture.Calendar;

            RecurrencePattern recurrence = new RecurrencePattern (FrequencyType.Yearly, 1);           
            recurrence.Frequency = FrequencyType.Yearly;
            recurrence.ByMonth.Add (transition.Month);
            recurrence.ByHour.Add (transition.TimeOfDay.Hour);
            recurrence.ByMinute.Add (transition.TimeOfDay.Minute);

            if (transition.IsFixedDateRule) {
                // TODO: why does this get an error?
                //                recurrence.ByMonthDay.Add(transition.Day);
                var dt = new DateTime (year, transition.Month, transition.Day);
                var dayOfWeek = dt.DayOfWeek;
                int week = 0;
                while (dt.Month == transition.Month) {
                    week += 1;
                    dt = dt.AddDays (-7);
                }
                recurrence.ByDay.Add (new WeekDay (dayOfWeek, week));

            } else {
                if (transition.Week != 5) {
                    recurrence.ByDay.Add (new WeekDay (transition.DayOfWeek, transition.Week));
                } else {
                    recurrence.ByDay.Add (new WeekDay (transition.DayOfWeek, -1));
                }
            }

            tzi.RecurrenceRules.Add (recurrence);
        }

        protected static iCalTimeZone FromSystemTimeZone (System.TimeZoneInfo tzinfo, DateTime earlistDateTimeToSupport, bool includeHistoricalData)
        {
            var adjustmentRules = tzinfo.GetAdjustmentRules ();
            var utcOffset = tzinfo.BaseUtcOffset;
            var dday_tz = new iCalTimeZone ();
            dday_tz.TZID = tzinfo.Id;

            DateTime y1901 = new DateTime (1901, 1, 1);
            if (includeHistoricalData || earlistDateTimeToSupport < y1901) {
                earlistDateTimeToSupport = y1901;
            }
            IDateTime earliest = new iCalDateTime (earlistDateTimeToSupport);

            foreach (var adjustmentRule in adjustmentRules) {
                // Only include historical data if asked to do so.  Otherwise,
                // use only the most recent adjustment rule available.
                if (adjustmentRule.DateEnd < earlistDateTimeToSupport) {
                    continue;
                }

                var delta = adjustmentRule.DaylightDelta;
                var dday_tzinfo_standard = new DDay.iCal.iCalTimeZoneInfo ();
                dday_tzinfo_standard.Name = "STANDARD";
                dday_tzinfo_standard.TimeZoneName = tzinfo.StandardName;
                IDateTime ruleStart = new iCalDateTime (adjustmentRule.DateStart);
                if (ruleStart.LessThan (earliest)) {
                    // The start time for this rule should be no earlier than one calendar
                    // year before the earliest supported date.
                    ruleStart = ruleStart.AddYears (Math.Max (0, earliest.Year - ruleStart.Year - 1));
                }
                dday_tzinfo_standard.Start = ruleStart;
                dday_tzinfo_standard.OffsetFrom = new UTCOffset (utcOffset + delta);
                dday_tzinfo_standard.OffsetTo = new UTCOffset (utcOffset);
                PopulateiCalTimeZoneInfo (dday_tzinfo_standard, adjustmentRule.DaylightTransitionEnd, adjustmentRule.DateStart.Year);

                // Add the "standard" time rule to the time zone
                dday_tz.AddChild (dday_tzinfo_standard);

                if (tzinfo.SupportsDaylightSavingTime) {
                    var dday_tzinfo_daylight = new DDay.iCal.iCalTimeZoneInfo ();
                    dday_tzinfo_daylight.Name = "DAYLIGHT";
                    dday_tzinfo_daylight.TimeZoneName = tzinfo.DaylightName;
                    dday_tzinfo_daylight.Start = ruleStart;
                    dday_tzinfo_daylight.OffsetFrom = new UTCOffset (utcOffset);
                    dday_tzinfo_daylight.OffsetTo = new UTCOffset (utcOffset + delta);
                    PopulateiCalTimeZoneInfo (dday_tzinfo_daylight, adjustmentRule.DaylightTransitionStart, adjustmentRule.DateStart.Year);

                    // Add the "daylight" time rule to the time zone
                    dday_tz.AddChild (dday_tzinfo_daylight);
                }                
            }

            // If no time zone information was recorded, at least
            // add a STANDARD time zone element to indicate the
            // base time zone information.
            if (dday_tz.TimeZoneInfos.Count == 0) {
                var dday_tzinfo_standard = new DDay.iCal.iCalTimeZoneInfo ();
                dday_tzinfo_standard.Name = "STANDARD";
                dday_tzinfo_standard.TimeZoneName = tzinfo.StandardName;
                dday_tzinfo_standard.Start = earliest;                
                dday_tzinfo_standard.OffsetFrom = new UTCOffset (utcOffset);
                dday_tzinfo_standard.OffsetTo = new UTCOffset (utcOffset);

                // Add the "standard" time rule to the time zone
                dday_tz.AddChild (dday_tzinfo_standard);
            }

            return dday_tz;
        }

        public static void SendInvites (McAccount account, McCalendar c, string subjectOverride, List<McAttendee> attendeeOverride, MimeEntity mimeBody, List<MailboxAddress> mailListOverride)
        {
            var attendees = attendeeOverride ?? c.attendees;
            if (0 == attendees.Count) {
                // Nobody to invite.
                return;
            }
            var mimeMessage = new MimeMessage ();

            mimeMessage.From.Add (new MailboxAddress (Pretty.OrganizerString (c.OrganizerName), c.OrganizerEmail));

            if (null != mailListOverride) {
                foreach (var m in mailListOverride) {
                    mimeMessage.To.Add (m);
                }
            } else {
                foreach (var a in attendees) {
                    mimeMessage.To.Add (new MailboxAddress (a.Name, a.Email));
                }
            }

            var subject = subjectOverride ?? Pretty.SubjectString (c.Subject);
            mimeMessage.Subject = subject;
            mimeMessage.Date = System.DateTime.UtcNow;
            mimeMessage.Body = mimeBody;

            var mcMessage = MimeHelpers.AddToDb (account.Id, mimeMessage);
            BackEnd.Instance.SendEmailCmd (mcMessage.AccountId, mcMessage.Id, c.Id);
        }

        //Used to send a single invite to one attendee at a time rather than all attendees of an event
        public static void SendInvite (McAccount account, McCalendar c, McAttendee a, MimeEntity mimeBody)
        {
            var mimeMessage = new MimeMessage ();

            mimeMessage.From.Add (new MailboxAddress (Pretty.UserNameForAccount (account), account.EmailAddr));
            mimeMessage.To.Add (new MailboxAddress (a.Name, a.Email));

            mimeMessage.Subject = Pretty.SubjectString (c.Subject);
            mimeMessage.Date = System.DateTime.UtcNow;
            mimeMessage.Body = mimeBody;

            var mcMessage = MimeHelpers.AddToDb (account.Id, mimeMessage);
            BackEnd.Instance.SendEmailCmd (mcMessage.AccountId, mcMessage.Id, c.Id);
        }

        public static void SendMeetingResponse (McAccount account, McCalendar c, MimeEntity mimeBody, NcResponseType response)
        {
            // Need to send this message to someone.  Fix this assertion upstream if you hit it.
            NcAssert.True (!String.IsNullOrEmpty (c.OrganizerName) || !String.IsNullOrEmpty (c.OrganizerEmail));

            var mimeMessage = new MimeMessage ();
            mimeMessage.From.Add (new MailboxAddress (Pretty.UserNameForAccount (account), account.EmailAddr));
            mimeMessage.To.Add (new MailboxAddress (c.OrganizerName, c.OrganizerEmail));
            mimeMessage.Subject = Pretty.SubjectString (ResponseSubjectPrefix (response) + ": " + c.Subject);
            mimeMessage.Date = DateTime.UtcNow;
            mimeMessage.Body = mimeBody;
            var mcMessage = MimeHelpers.AddToDb (account.Id, mimeMessage);
            BackEnd.Instance.SendEmailCmd (mcMessage.AccountId, mcMessage.Id, c.Id);
        }

        public static void SendMeetingResponse (McAccount account, MailboxAddress to, string subject, string token, MimeEntity mimeBody, NcResponseType response)
        {
            var mimeMessage = new MimeMessage ();
            mimeMessage.From.Add (new MailboxAddress (Pretty.UserNameForAccount (account), account.EmailAddr));
            mimeMessage.To.Add (to);
            mimeMessage.Subject = Pretty.SubjectString (ResponseSubjectPrefix (response) + ": " + subject);
            mimeMessage.Date = DateTime.UtcNow;
            mimeMessage.Body = mimeBody;
            var mcMessage = MimeHelpers.AddToDb (account.Id, mimeMessage);
            BackEnd.Instance.SendEmailCmd (mcMessage.AccountId, mcMessage.Id);
        }

        public static void SendMeetingCancelations (McAccount account, McCalendar c, string subjectOverride, MimeEntity mimeBody)
        {
            var mimeMessage = new MimeMessage ();
            mimeMessage.From.Add (new MailboxAddress (Pretty.OrganizerString (c.OrganizerName), account.EmailAddr));
            foreach (var a in c.attendees) {
                mimeMessage.To.Add (new MailboxAddress (a.Name, a.Email));
            }
            mimeMessage.Subject = Pretty.SubjectString (subjectOverride ?? "Canceled: " + c.GetSubject ());
            mimeMessage.Date = DateTime.UtcNow;
            mimeMessage.Body = mimeBody;
            var mcMessage = MimeHelpers.AddToDb (account.Id, mimeMessage);
            BackEnd.Instance.SendEmailCmd (mcMessage.AccountId, mcMessage.Id, c.Id);
        }

        private static iCalendar iCalendarCommon (McAbstrCalendarRoot cal)
        {
            var iCal = new iCalendar ();
            iCal.ProductID = "Nacho Mail";

            var vEvent = iCal.Create<DDay.iCal.Event> ();
            vEvent.UID = cal.GetUID ();
            vEvent.LastModified = new iCalDateTime (DateTime.UtcNow);
            vEvent.IsAllDay = cal.AllDayEvent;
            vEvent.Priority = 5;
            if (cal.AllDayEvent) {
                vEvent.Properties.Set ("X-MICROSOFT-CDO-ALLDAYEVENT", "TRUE");
                vEvent.Properties.Set ("X-MICROSOFT-CDO-INTENDEDSTATUS", "FREE");
            } else {
                vEvent.Properties.Set ("X-MICROSOFT-CDO-ALLDAYEVENT", "FALSE");
                vEvent.Properties.Set ("X-MICROSOFT-CDO-INTENDEDSTATUS", "BUSY");
            }
            vEvent.Properties.Set ("X-MICROSOFT-CDO-IMPORTANCE", 1);
            vEvent.Location = cal.GetLocation ();
            vEvent.Class = "PUBLIC";
            vEvent.Transparency = TransparencyType.Opaque;
            return iCal;
        }

        private static IICalendar iCalendarAddStartEndTimes (iCalendar iCal, McAbstrCalendarRoot cal, DateTime occurrence = default (DateTime))
        {
            var tzi = CalendarHelper.SimplifiedLocalTimeZone ();
            var timezone = FromSystemTimeZone (tzi, cal.StartTime.AddYears (-1), false);
            var localTimeZone = iCal.AddTimeZone (timezone);
            if (null != tzi.StandardName) {
                timezone.TZID = tzi.StandardName;
                localTimeZone.TZID = tzi.StandardName;
            }

            var vEvent = iCal.Events [0];
            vEvent.Start = new iCalDateTime (cal.StartTime.LocalT (), localTimeZone.TZID);
            vEvent.End = new iCalDateTime (cal.EndTime.LocalT (), localTimeZone.TZID);
            if (default (DateTime) != occurrence) {
                vEvent.RecurrenceID = new iCalDateTime (occurrence.LocalT (), localTimeZone.TZID);
            }
            return iCal;
        }

        private static IICalendar iCalendarAddStartEndTimes (iCalendar iCal, McEvent mcEvent, DateTime occurrence)
        {
            var tzi = CalendarHelper.SimplifiedLocalTimeZone ();
            var timezone = FromSystemTimeZone (tzi, mcEvent.StartTime.AddYears (-1), false);
            var localTimeZone = iCal.AddTimeZone (timezone);
            if (null != tzi.StandardName) {
                timezone.TZID = tzi.StandardName;
                localTimeZone.TZID = tzi.StandardName;
            }

            var vEvent = iCal.Events [0];
            vEvent.Start = new iCalDateTime (mcEvent.StartTime.LocalT (), localTimeZone.TZID);
            vEvent.End = new iCalDateTime (mcEvent.EndTime.LocalT (), localTimeZone.TZID);
            if (default (DateTime) != occurrence) {
                vEvent.RecurrenceID = new iCalDateTime (occurrence.LocalT (), localTimeZone.TZID);
            }
            return iCal;
        }

        /// <summary>
        /// Create an iCalendar from either a McCalendar object or a McMeetingRequest object.
        /// </summary>
        private static IICalendar iCalendarCommonFromAbstrCal (McAbstrCalendarRoot cal, DateTime occurrence = default (DateTime))
        {
            return iCalendarAddStartEndTimes (iCalendarCommon (cal), cal, occurrence);
        }

        /// <summary>
        /// Create an iCalendar meeting request from a McCalendar object.
        /// </summary>
        /// <returns>The calendar request from mc calendar.</returns>
        /// <param name="cal">Cal.</param>
        private static IICalendar iCalendarRequestFromMcCalendar (McCalendar cal)
        {
            var iCal = iCalendarCommonFromAbstrCal (cal);
            iCal.Method = DDay.iCal.CalendarMethods.Request;
            var evt = iCal.Events [0];
            evt.Status = EventStatus.Confirmed;
            evt.Summary = cal.Subject;
            AddAttendeesAndOrganizerToiCalEvent (evt, cal, cal.attendees);
            return iCal;
        }

        /// <summary>
        /// Set the fields in the iCalendar object that make it a meeting response.
        /// </summary>
        private static void FillOutICalResponse (McAccount account, IICalendar iCal, NcResponseType response, string subject)
        {
            iCal.Method = DDay.iCal.CalendarMethods.Reply;
            var evt = iCal.Events [0];
            evt.Status = EventStatus.Confirmed;
            evt.Summary = ResponseSubjectPrefix (response) + ": " + subject;
            var iAttendee = new Attendee (EmailHelper.MailToUri (account.EmailAddr));
            if (!string.IsNullOrEmpty (Pretty.UserNameForAccount (account))) {
                iAttendee.CommonName = Pretty.UserNameForAccount (account);
            }
            iAttendee.ParticipationStatus = iCalResponseString (response);
            evt.Attendees.Add (iAttendee);
        }

        /// <summary>
        /// Create an iCalendar meeting response from a McCalendar object.
        /// </summary>
        private static IICalendar iCalendarResponseFromMcCalendar (McCalendar cal, NcResponseType response, DateTime occurrence = default (DateTime))
        {
            var iCal = iCalendarCommonFromAbstrCal (cal, occurrence);
            FillOutICalResponse (McAccount.QueryById<McAccount> (cal.AccountId), iCal, response, cal.Subject);
            return iCal;
        }

        /// <summary>
        /// Create an iCalendar meeting cancelation notice for a McCalendar object.
        /// </summary>
        private static IICalendar iCalendarCancelFromMcCalendar (McCalendar cal)
        {
            var iCal = iCalendarCommonFromAbstrCal (cal);
            iCal.Method = DDay.iCal.CalendarMethods.Cancel;
            var evt = iCal.Events [0];
            evt.Status = EventStatus.Cancelled;
            evt.Summary = "Canceled: " + cal.Subject;
            AddAttendeesAndOrganizerToiCalEvent (evt, cal, cal.attendees);
            return iCal;
        }

        /// <summary>
        /// Create an iCalendar meeting cancelation notice for an occurrence of an event.
        /// </summary>
        private static IICalendar iCalendarCancelFromOccurrence (McCalendar root, McAbstrCalendarRoot cal, McEvent mcEvent, DateTime occurrence)
        {
            var iCal = iCalendarAddStartEndTimes (iCalendarCommon (cal), mcEvent, occurrence);
            iCal.Method = DDay.iCal.CalendarMethods.Cancel;
            var evt = iCal.Events [0];
            evt.Status = EventStatus.Cancelled;
            evt.Summary = "Canceled: " + cal.GetSubject ();
            AddAttendeesAndOrganizerToiCalEvent (evt, root, cal.attendees);
            return iCal;
        }

        /// <summary>
        /// Create an iCalendar meeting response from a McMeetingRequest object.
        /// </summary>
        private static IICalendar iCalendarResponseFromEmail (McMeetingRequest mail, NcResponseType response, string subject, DateTime occurrence = default (DateTime))
        {
            var iCal = iCalendarCommonFromAbstrCal (mail, occurrence);
            FillOutICalResponse (McAccount.QueryById<McAccount> (mail.AccountId), iCal, response, subject);
            return iCal;
        }

        /// <summary>
        /// Create a text/calendar MIME part that holds the given iCalendar event.
        /// </summary>
        private static TextPart MimeCalFromICalendar (IICalendar iCal)
        {
            var iCalPart = new TextPart ("calendar");
            iCalPart.ContentType.Parameters.Add ("METHOD", iCal.Method);
            using (var iCalStream = new MemoryStream ()) {
                var serializer = new iCalendarSerializer ();
                serializer.Serialize (iCal, iCalStream, System.Text.Encoding.ASCII);
                iCalStream.Seek (0, SeekOrigin.Begin);
                using (var textStream = new StreamReader (iCalStream)) {
                    iCalPart.Text = textStream.ReadToEnd ();
                }
            }
            iCalPart.ContentTransferEncoding = ContentEncoding.Base64;
            return iCalPart;
        }

        /// <summary>
        /// Using a McCalendar object, create a text/calendar MIME part that holds an iCalendar meeting request.
        /// </summary>
        public static TextPart MimeRequestFromCalendar (McCalendar cal)
        {
            return MimeCalFromICalendar (iCalendarRequestFromMcCalendar (cal));
        }

        /// <summary>
        /// Using a McCalendar object, create a text/calendar MIME part that holds an iCalendar meeting response.
        /// </summary>
        public static TextPart MimeResponseFromCalendar (McCalendar cal, NcResponseType response, DateTime occurrence = default (DateTime))
        {
            return MimeCalFromICalendar (iCalendarResponseFromMcCalendar (cal, response, occurrence));
        }

        /// <summary>
        /// Using a McCalendar object, create a text/calendar MIME part that holds an iCalendar meeting cancellation.
        /// </summary>
        public static TextPart MimeCancelFromCalendar (McCalendar cal)
        {
            return MimeCalFromICalendar (iCalendarCancelFromMcCalendar (cal));
        }

        public static TextPart MimeCancelFromOccurrence (McCalendar root, McAbstrCalendarRoot cal, McEvent evt, DateTime occurrence)
        {
            return MimeCalFromICalendar (iCalendarCancelFromOccurrence (root, cal, evt, occurrence));
        }

        /// <summary>
        /// Using a McMeetingRequest object attached to an e-mail message, create a text/calendar MIME part that
        /// holds an iCalendar meeting response.
        /// </summary>
        public static TextPart MimeResponseFromEmail (McMeetingRequest mail, NcResponseType response, string subject, DateTime occurrence = default (DateTime))
        {
            return MimeCalFromICalendar (iCalendarResponseFromEmail (mail, response, subject, occurrence));
        }

        public static MimeEntity CreateMime (string description, TextPart iCalPart, IList<McAttachment> attachments)
        {
            // attachments
            var attachmentCollection = new MimeKit.AttachmentCollection ();
            foreach (var a in attachments) {
                attachmentCollection.Add (a.GetFilePath ());
            }

            MimeEntity bodyPart = null;

            bodyPart = new TextPart ("plain") {
                Text = description,
            };

            if (null != iCalPart) {
                var alternative = new Multipart ("alternative");
                alternative.Add (bodyPart);
                alternative.Add (iCalPart);
                bodyPart = alternative;
            }

            if (0 == attachmentCollection.Count) {
                return bodyPart;
            }

            var mixed = new Multipart ("mixed");
            mixed.Add (bodyPart);

            foreach (var attachmentPart in attachmentCollection) {
                mixed.Add (attachmentPart);
            }
            return mixed;
        }

        protected static McAttendee CreateAttendee (int accountId, InternetAddress address, NcAttendeeType attendeeType)
        {
            var mailboxAddress = address as MailboxAddress;

            if (null == mailboxAddress) {
                return null;
            }

            var attendee = new McAttendee ();
            attendee.AccountId = accountId;
            attendee.Name = mailboxAddress.Name;
            attendee.Email = mailboxAddress.Address;
            attendee.AttendeeType = attendeeType;
            attendee.AttendeeTypeIsSet = true;
            return attendee;
        }

        protected static List<McAttendee> CreateAttendeeList (int accountId, string addressLine, NcAttendeeType attendeeType)
        {
            var addressList = NcEmailAddress.ParseAddressListString (addressLine);
            var attendeeList = new List<McAttendee> ();
            foreach (var address in addressList) {
                var addendee = CreateAttendee (accountId, address, attendeeType);
                if (null != addendee) {
                    attendeeList.Add (addendee);
                }
            }
            return attendeeList;
        }

        public static McCalendar CreateMeeting (McEmailMessage message, DateTime startDate = default(DateTime))
        {
            var c = DefaultMeeting (startDate);
            c.AccountId = message.AccountId;
            c.Subject = message.Subject;
//            var dupBody = McBody.InsertDuplicate (message.AccountId, message.BodyId);
//            c.BodyId = dupBody.Id;

            // Instead of grabbing the whole body from the email message, only the
            // text part (if there exists one) is added to the event description.
            // TODO Extract formatted text instead of just plain text.
            var textPart = MimeHelpers.ExtractTextPart (message.GetBody ());
            if (null != textPart) {
                c.SetDescription (textPart, McAbstrFileDesc.BodyTypeEnum.PlainText_1);
            }
            var attendees = new List<McAttendee> ();
            attendees.AddRange (CreateAttendeeList (message.AccountId, message.From, NcAttendeeType.Required));
            attendees.AddRange (CreateAttendeeList (message.AccountId, message.To, NcAttendeeType.Required));
            attendees.AddRange (CreateAttendeeList (message.AccountId, message.Cc, NcAttendeeType.Optional));
            c.attendees = attendees;
            return c;
        }

        public static McTask CreateTask (McEmailMessage message)
        {
            return DefaultTask ();
        }

        protected class RecurrenceValidationException : Exception
        {
            public RecurrenceValidationException (string message)
                : base (message)
            {
            }
        }

        /// <summary>
        /// Validate a meeting recurrence structure.  The server has probably done any necessary
        /// validation already, so it is not expected that this will ever find an error.  But we
        /// still want this function because anything that comes from the server is, technically,
        /// user input, which means we don't want to crash if it is malformed.  Doing all of
        /// these checks up front means we don't need any checks in the code that calculates the
        /// recurrences.
        /// </summary>
        /// <returns><c>true</c>, if recurrence is valid and supported, <c>false</c> otherwise.</returns>
        protected static bool ValidateRecurrence (McCalendar c, McRecurrence r)
        {
            try {
                if (r.IntervalIsSet && (0 > r.Interval || 999 < r.Interval)) {
                    throw new RecurrenceValidationException (string.Format (
                        "The Interval field has an invalid value of {0}. It must be 0 <= Interval <= 999.", r.Interval));
                }
                if (r.CalendarTypeIsSet && (NcCalendarType.Default != r.CalendarType && NcCalendarType.Gregorian != r.CalendarType)) {
                    throw new RecurrenceValidationException (string.Format (
                        "Unsupported calendar type: {0}. Only the Gregorian calendar is supported.", r.CalendarType));
                }
                switch (r.Type) {
                case NcRecurrenceType.Daily:
                    // Nothing else to check.
                    break;
                case NcRecurrenceType.Weekly:
                    if (!r.DayOfWeekIsSet) {
                        Log.Info (Log.LOG_CALENDAR, "DayOfWeekIsSet is false for weekly recurring event {0}. A reinstall of the app is needed to correct this.", c.Subject);
                        //throw new RecurrenceValidationException("The DayOfWeek field must be set in a weekly recurrence.");
                    }
                    if (1 > (int)r.DayOfWeek || 127 < (int)r.DayOfWeek) {
                        throw new RecurrenceValidationException (string.Format (
                            "The DayOfWeek field has an invalid value for a weekly recurrence: {0}. It must be 1 <= DayOfWeek <= 127.", (int)r.DayOfWeek));
                    }
                    if (r.FirstDayOfWeekIsSet && (0 > r.FirstDayOfWeek || 6 < r.FirstDayOfWeek)) {
                        throw new RecurrenceValidationException (string.Format (
                            "The FirstDayOfWeek field has an invalid value of {0}. It must be 0 <= FirstDayOfWeek <= 6.", r.FirstDayOfWeek));
                    }
                    break;
                case NcRecurrenceType.Monthly:
                    if (1 > r.DayOfMonth || 31 < r.DayOfMonth) {
                        throw new RecurrenceValidationException (string.Format (
                            "The DayOfMonth field has an invalid value of {0}. It must be 1 <= DayOfMonth <= 31.", r.DayOfMonth));
                    }
                    break;
                case NcRecurrenceType.Yearly:
                    if (1 > r.DayOfMonth || 31 < r.DayOfMonth) {
                        throw new RecurrenceValidationException (string.Format (
                            "The DayOfMonth field has an invalid value of {0}. It must be 1 <= DayOfMonth <= 31.", r.DayOfMonth));
                    }
                    if (1 > r.MonthOfYear || 12 < r.MonthOfYear) {
                        throw new RecurrenceValidationException (string.Format (
                            "The MonthOfYear field has an invalid value of {0}. It must be 1 <= MonthOfYear <= 12.", r.MonthOfYear));
                    }
                    break;
                case NcRecurrenceType.MonthlyOnDay:
                    if (1 > r.WeekOfMonth || 5 < r.WeekOfMonth) {
                        throw new RecurrenceValidationException (string.Format (
                            "The WeekOfMonth field has an invalid value of {0}. It must be 1 <= WeekOfMonth <= 5.", r.WeekOfMonth));
                    }
                    if (!r.DayOfWeekIsSet) {
                        Log.Info (Log.LOG_CALENDAR, "DayOfWeekIsSet is false for monthly recurring event {0}. A reinstall of the app is needed to correct this.", c.Subject);
                        // throw new RecurrenceValidationException("The DayOfWeek field must be set in a weekly recurrence.");
                    }
                    if (NcDayOfWeek.Sunday != r.DayOfWeek && NcDayOfWeek.Monday != r.DayOfWeek && NcDayOfWeek.Tuesday != r.DayOfWeek &&
                        NcDayOfWeek.Wednesday != r.DayOfWeek && NcDayOfWeek.Thursday != r.DayOfWeek && NcDayOfWeek.Friday != r.DayOfWeek &&
                        NcDayOfWeek.Saturday != r.DayOfWeek && NcDayOfWeek.Weekdays != r.DayOfWeek && NcDayOfWeek.WeekendDays != r.DayOfWeek &&
                        NcDayOfWeek.LastDayOfTheMonth != r.DayOfWeek) {
                        throw new RecurrenceValidationException (string.Format (
                            "The DayOfWeek field has an invalid value of {0} for a monthly recurrence. It must be one of: 1, 2, 4, 8, 16, 32, 62, 64, 65, 127.", (int)r.DayOfWeek));
                    }
                    if (NcDayOfWeek.LastDayOfTheMonth == r.DayOfWeek && 5 != r.WeekOfMonth) {
                        throw new RecurrenceValidationException (
                            "When the DayOfWeek field is 127, meaning the last day of the month, then the WeekOfMonth field must be 5.");
                    }
                    break;
                case NcRecurrenceType.YearlyOnDay:
                    if (1 > r.MonthOfYear || 12 < r.MonthOfYear) {
                        throw new RecurrenceValidationException (string.Format (
                            "The MonthOfYear field has an invalid value of {0}. It must be 1 <= MonthOfYear <= 12.", r.MonthOfYear));
                    }
                    if (1 > r.WeekOfMonth || 5 < r.WeekOfMonth) {
                        throw new RecurrenceValidationException (string.Format (
                            "The WeekOfMonth field has an invalid value of {0}. It must be 1 <= WeekOfMonth <= 5.", r.WeekOfMonth));
                    }
                    if (!r.DayOfWeekIsSet) {
                        Log.Info (Log.LOG_CALENDAR, "DayOfWeekIsSet is false for yearly recurring event {0}. A reinstall of the app is needed to correct this.", c.Subject);
                        // throw new RecurrenceValidationException("The DayOfWeek field must be set in a weekly recurrence.");
                    }
                    if (NcDayOfWeek.Sunday != r.DayOfWeek && NcDayOfWeek.Monday != r.DayOfWeek && NcDayOfWeek.Tuesday != r.DayOfWeek &&
                        NcDayOfWeek.Wednesday != r.DayOfWeek && NcDayOfWeek.Thursday != r.DayOfWeek && NcDayOfWeek.Friday != r.DayOfWeek &&
                        NcDayOfWeek.Saturday != r.DayOfWeek && NcDayOfWeek.Weekdays != r.DayOfWeek && NcDayOfWeek.WeekendDays != r.DayOfWeek &&
                        NcDayOfWeek.LastDayOfTheMonth != r.DayOfWeek) {
                        throw new RecurrenceValidationException (string.Format (
                            "The DayOfWeek field has an invalid value of {0} for a yearly recurrence. It must be one of: 1, 2, 4, 8, 16, 32, 62, 64, 65, 127.", (int)r.DayOfWeek));
                    }
                    if (NcDayOfWeek.LastDayOfTheMonth == r.DayOfWeek && 5 != r.WeekOfMonth) {
                        throw new RecurrenceValidationException (
                            "When the DayOfWeek field is 127, meaning the last day of the month, then the WeekOfMonth field must be 5.");
                    }
                    break;
                default:
                    throw new RecurrenceValidationException (string.Format ("Unsupported recurrence type: {0}", (int)r.Type));
                }
                return true;
            } catch (RecurrenceValidationException e) {
                Log.Error (Log.LOG_CALENDAR, 
                    "Invalid or unsupported recurrence for event {0}. Recurrences will not be generated for this event.: {1}", 
                    c.Subject, e.Message);
                return false;
            }
        }

        /// <summary>
        /// Convert from the "FirstDayOfWeek" field in a recurrence to a C# DayOfWeek value.
        /// </summary>
        protected static DayOfWeek ToCSharpDayOfWeek (int firstDayOfWeek)
        {
            switch (firstDayOfWeek) {
            case 0:
                return DayOfWeek.Sunday;
            case 1:
                return DayOfWeek.Monday;
            case 2:
                return DayOfWeek.Tuesday;
            case 3:
                return DayOfWeek.Wednesday;
            case 4:
                return DayOfWeek.Thursday;
            case 5:
                return DayOfWeek.Friday;
            case 6:
                return DayOfWeek.Saturday;
            default:
                NcAssert.CaseError (string.Format ("Unrecognized valued for McRecurrence.FirstDayOfWeek: {0}", firstDayOfWeek));
                return DayOfWeek.Sunday;
            }
        }

        /// <summary>
        /// Convert from a C# DayOfWeek value to the DayOfWeek field within a recurrence.
        /// </summary>
        public static NcDayOfWeek ToNcDayOfWeek (DayOfWeek day)
        {
            switch (day) {
            case DayOfWeek.Sunday:
                return NcDayOfWeek.Sunday;
            case DayOfWeek.Monday:
                return NcDayOfWeek.Monday;
            case DayOfWeek.Tuesday:
                return NcDayOfWeek.Tuesday;
            case DayOfWeek.Wednesday:
                return NcDayOfWeek.Wednesday;
            case DayOfWeek.Thursday:
                return NcDayOfWeek.Thursday;
            case DayOfWeek.Friday:
                return NcDayOfWeek.Friday;
            case DayOfWeek.Saturday:
                return NcDayOfWeek.Saturday;
            default:
                return NcDayOfWeek.LastDayOfTheMonth;
            }
        }

        /// <summary>
        /// Is the given day of the week selected in the recurrence's DayOfWeek field?
        /// </summary>
        protected static bool IsSelected (DayOfWeek day, NcDayOfWeek selectedDays)
        {
            return ((int)selectedDays & (int)ToNcDayOfWeek (day)) != 0;
        }

        /// <summary>
        /// Convert the given date/time to the same time on the last day of the month.
        /// </summary>
        protected static DateTime EndOfMonth (DateTime date)
        {
            return new DateTime (date.Year, date.Month, DateTime.DaysInMonth (date.Year, date.Month),
                date.Hour, date.Minute, date.Second, date.Millisecond);
        }

        /// <summary>
        /// Convert the given date/time to the same time on the first day of the month.
        /// </summary>
        protected static DateTime BeginningOfMonth (DateTime date)
        {
            return new DateTime (date.Year, date.Month, 1, date.Hour, date.Minute, date.Second, date.Millisecond);
        }

        /// <summary>
        /// Find the desired day within the month using the "weekOfMonth" and "selectedDays"
        /// rules.  For example, "the 4th Thursday".  The "month" argument can be any day
        /// within the month.  The year, month, and time of day are preserved.  Only the day
        /// of the month is changed.
        /// </summary>
        protected static DateTime FindDayWithinMonthByRule (DateTime month, int weekOfMonth, NcDayOfWeek selectedDays)
        {
            if (5 == weekOfMonth) {
                // Start at the end of the month and go backwards, looking for the
                // first day that matches.
                DateTime day = EndOfMonth (month);
                while (!IsSelected (day.DayOfWeek, selectedDays)) {
                    day = day.AddDays (-1);
                }
                return day;
            } else {
                // Start at the beginning of the month and go forwards, looking for
                // the nth matching day.
                DateTime day = BeginningOfMonth (month).AddDays (-1);
                int count = 0;
                while (count < weekOfMonth) {
                    day = day.AddDays (1);
                    if (IsSelected (day.DayOfWeek, selectedDays)) {
                        ++count;
                    }
                }
                return day;
            }
        }

        /// <summary>
        /// Given a recurrence rule and an existing occurrence, calculate and return
        /// the next occurrence in the series.
        /// </summary>
        protected static DateTime NextEvent (McRecurrence r, DateTime current)
        {
            int interval = r.IntervalIsSet && 0 != r.Interval ? r.Interval : 1;

            DateTime next;

            switch (r.Type) {

            case NcRecurrenceType.Daily:
                return current.AddDays (interval);

            case NcRecurrenceType.Weekly:
                DayOfWeek firstDayOfWeek = r.FirstDayOfWeekIsSet ? ToCSharpDayOfWeek (r.FirstDayOfWeek) : DayOfWeek.Sunday;
                next = current;
                for (int infiniteLoopGuard = 0; infiniteLoopGuard < 10; ++infiniteLoopGuard) {
                    next = next.AddDays (1);
                    if (1 < interval && next.DayOfWeek == firstDayOfWeek) {
                        // Reached the end of the week. Jump ahead some number of weeks.
                        next = next.AddDays (7 * (interval - 1));
                    }
                    if (IsSelected (next.DayOfWeek, r.DayOfWeek)) {
                        return next;
                    }
                }
                // Should never get here. A matching day should always be found within seven times
                // through the loop above.
                NcAssert.CaseError ("Failed to find the next occurrence for a weekly meeting.");
                return next;

            case NcRecurrenceType.Monthly:
                next = current.AddMonths (interval);
                // Calculations can get messed up if the day of the month is the 29th or later.
                if (next.Day != r.DayOfMonth) {
                    next = new DateTime (
                        next.Year, next.Month,
                        Math.Min (r.DayOfMonth, DateTime.DaysInMonth (next.Year, next.Month)),
                        next.Hour, next.Minute, next.Second, next.Millisecond);
                }
                return next;

            case NcRecurrenceType.Yearly:
                // Handle the case where the event is scheduled for Leap Day.
                int nextYear = current.Year + interval;
                return new DateTime (
                    nextYear, r.MonthOfYear,
                    Math.Min (r.DayOfMonth, DateTime.DaysInMonth (nextYear, r.MonthOfYear)),
                    current.Hour, current.Minute, current.Second, current.Millisecond);

            case NcRecurrenceType.MonthlyOnDay:
                return FindDayWithinMonthByRule (current.AddMonths (interval), r.WeekOfMonth, r.DayOfWeek);

            case NcRecurrenceType.YearlyOnDay:
                return FindDayWithinMonthByRule (new DateTime (current.Year + interval, r.MonthOfYear, 1, current.Hour, current.Minute, current.Second, current.Millisecond), r.WeekOfMonth, r.DayOfWeek);

            default:
                NcAssert.CaseError ("Unsupported recurrence type, which should have been caught earlier.");
                return DateTime.MaxValue;
            }
        }

        protected static DateTime ExpandRecurrences (McCalendar c, McRecurrence r, DateTime startingTime, DateTime endingTime)
        {
            if (!ValidateRecurrence (c, r)) {
                return DateTime.MinValue;
            }
            if (startingTime < NcEventManager.BeginningOfEventsOfInterest) {
                // Don't bother creating McEvents for times in the past that will never show up on the calendar.
                startingTime = NcEventManager.BeginningOfEventsOfInterest;
            }

            // All date/time calculations must be done in the event's original time zone.
            TimeZoneInfo timeZone = new AsTimeZone (c.TimeZone).ConvertToSystemTimeZone ();
            DateTime eventStart = ConvertTimeFromUtc (c.StartTime, timeZone);
            DateTime eventEnd = ConvertTimeFromUtc (c.EndTime, timeZone);
            var duration = eventEnd - eventStart;

            int maxOccurrences = r.OccurrencesIsSet ? r.Occurrences : int.MaxValue;
            DateTime lastOccurrence = r.Until == default(DateTime) ? DateTime.MaxValue : ConvertTimeFromUtc (r.Until, timeZone);

            int occurrence = 0;

            while (occurrence < maxOccurrences && eventStart <= lastOccurrence) {
                DateTime eventStartUtc = ConvertTimeToUtc (eventStart, timeZone);
                if (eventStartUtc > startingTime) {
                    DateTime eventEndUtc = ConvertTimeToUtc (eventStart + duration, timeZone);
                    if (eventStartUtc > eventEndUtc) {
                        // This can happen when the start falls within the missing hour at the transition from
                        // standard time to daylight time, but the end time is outside that window.  The start
                        // time gets pushed forward an hour in that case, possibly beyond the end time.
                        eventEndUtc = eventStartUtc + duration;
                    }
                    if (c.AllDayEvent) {
                        ExpandAllDayEvent (c, eventStartUtc, eventEndUtc);
                    } else {
                        CreateEventRecord (c, eventStartUtc, eventEndUtc);
                    }
                }
                if (eventStartUtc >= endingTime) {
                    return eventStartUtc;
                }
                eventStart = NextEvent (r, eventStart);
                ++occurrence;
            }

            return DateTime.MaxValue;
        }

        protected static void ExpandAllDayEvent (McCalendar c, DateTime start, DateTime end, McException exception = null)
        {
            if (null == exception) {
                // Look for any exceptions for this particular occurrence.
                var exceptions = McException.QueryForExceptionId (c.Id, start);
                if (0 < exceptions.Count) {
                    foreach (var ex in exceptions) {
                        DateTime exceptionStart = DateTime.MinValue == ex.StartTime ? start : ex.StartTime;
                        DateTime exceptionEnd = DateTime.MinValue == ex.EndTime ? end : ex.EndTime;
                        ExpandAllDayEvent (c, exceptionStart, exceptionEnd, ex);
                    }
                    return;
                }
            }

            // Create local events for an all-day calendar item.  Figure out the starting
            // day of the event in the organizer's time zone.  Create a local event that
            // starts at midnight local time on the same day.  If the original item is
            // more than one day long, repeat for subsequent days.

            TimeZoneInfo timeZone = new AsTimeZone (c.TimeZone).ConvertToSystemTimeZone ();
            DateTime organizersTime = ConvertTimeFromUtc (start, timeZone);
            DateTime dayStart = new DateTime (organizersTime.Year, organizersTime.Month, organizersTime.Day, 0, 0, 0, DateTimeKind.Utc);
            double days = (end - start).TotalDays;

            // Use a do/while loop so we will always create at least one event, even if
            // the original item is improperly less than one day long.  If the all-day
            // event spans the transition from daylight saving time to standard time,
            // then it will have an extra hour in its duration.  So the cutoff for
            // creating an extra event is a quarter of a day.
            McAbstrCalendarRoot reminderItem = (McAbstrCalendarRoot)exception ?? c;
            bool needsReminder = reminderItem.HasReminder ();
            int exceptionId = null == exception ? 0 : exception.Id;
            do {
                DateTime nextDay = dayStart.AddDays (1.0);
                var ev = McEvent.Create (c.AccountId, dayStart, nextDay, c.UID, true, c.Id, exceptionId);
                if (needsReminder) {
                    ev.SetReminder (reminderItem.GetReminder ());
                    needsReminder = false; // Only the first day should have a reminder.
                }
                dayStart = nextDay;
                days -= 1.0;
                NcTask.Cts.Token.ThrowIfCancellationRequested ();
            } while (days > 0.25);
        }

        private static object expandRecurrencesLock = new object ();

        /// <summary>
        /// Create McEvents as necessary so that McEvents are accurate up through the given date.  If there
        /// are any changes to the McEvents, an EventSetChanged status will be sent.  All work is done as a
        /// task on a background thread; this method always returns immediately.
        /// </summary>
        public static void ExpandRecurrences (DateTime untilDate)
        {
            NcTask.Run (delegate {

                // Duplicate events may result if two instances of ExpandRecurrences are running at the same time.
                lock (expandRecurrencesLock) {

                    // Fetch calendar entries that haven't been generated that far in advance
                    var list = McCalendar.QueryOutOfDateRecurrences (untilDate);

                    if (0 == list.Count) {
                        // McEvents are up to date.  Nothing to do.
                        return;
                    }

                    foreach (var calendarItem in list) {

                        NcTask.Cts.Token.ThrowIfCancellationRequested ();

                        // Delete any existing events that are later than calendarItem.RecurrencesGeneratedUntil.
                        // These events can exist for two reasons: (1) The app was killed while generating events
                        // for this item. (2) The calendar item was edited and its RecurrencesGeneratedUntil was
                        // reset.
                        foreach (var evt in McEvent.QueryEventsForCalendarItemAfter (calendarItem.Id, calendarItem.RecurrencesGeneratedUntil)) {
                            evt.Delete ();
                        }

                        if (0 == calendarItem.recurrences.Count) {

                            // Non-recurring event.  Create one McEvent.
                            if (calendarItem.AllDayEvent) {
                                ExpandAllDayEvent (calendarItem, calendarItem.StartTime, calendarItem.EndTime);
                            } else {
                                CreateEventRecord (calendarItem, calendarItem.StartTime, calendarItem.EndTime);
                            }
                            calendarItem.UpdateRecurrencesGeneratedUntil (DateTime.MaxValue);

                        } else {

                            // Recurring event.  Create McEvents up through untilDate.
                            var lastOneGeneratedAggregate = DateTime.MaxValue;
                            foreach (var recurrence in calendarItem.recurrences) {
                                var lastOneGenerated = ExpandRecurrences (
                                                           calendarItem, recurrence, calendarItem.RecurrencesGeneratedUntil, untilDate);
                                if (lastOneGeneratedAggregate > lastOneGenerated) {
                                    lastOneGeneratedAggregate = lastOneGenerated;
                                }
                            }
                            calendarItem.UpdateRecurrencesGeneratedUntil (lastOneGeneratedAggregate);
                        }
                    }
                }

                var el = NcModel.Instance.Db.Table<McEvent> ().Count ();
                Log.Info (Log.LOG_CALENDAR, "Events in db: {0}", el);

                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                    Status = NcResult.Info (NcResult.SubKindEnum.Info_EventSetChanged),
                    Account = ConstMcAccount.NotAccountSpecific,
                    Tokens = new string[] { DateTime.Now.ToString () },
                });
            }, "ExpandRecurrences");
        }

        protected static void CreateEventRecord (McCalendar c, DateTime startTime, DateTime endTime)
        {
            var exceptions = McException.QueryForExceptionId (c.Id, startTime);

            if ((null == exceptions) || (0 == exceptions.Count)) {
                var e = McEvent.Create (c.AccountId, startTime, endTime, c.UID, false, c.Id, 0);
                if (c.ReminderIsSet) {
                    e.SetReminder (c.Reminder);
                }
            } else {
                foreach (var exception in exceptions) {
                    if (0 == exception.Deleted) {
                        DateTime exceptionStart = exception.StartTime;
                        if (DateTime.MinValue == exceptionStart) {
                            exceptionStart = startTime;
                        }
                        DateTime exceptionEnd = exception.EndTime;
                        if (DateTime.MinValue == exceptionEnd) {
                            exceptionEnd = endTime;
                        }
                        var e = McEvent.Create (c.AccountId, exceptionStart, exceptionEnd, c.UID, false, c.Id, exception.Id);
                        if (exception.HasReminder ()) {
                            e.SetReminder (exception.GetReminder ());
                        }
                    }
                }
            }
        }

        // Looking at the characteristics of a calendar item, return a number that indicates the importance of the calendar item.
        // A higher number indicates more important.  The ranking, from highest to lowest:
        //   7. Meeting organizer
        //   6. Meeting that has been accepted
        //   5. Appointment
        //   4. Meeting that hasn't been responded to, or whose status is unknown
        //   3. Meeting that has been tentatively accepted
        //   2. Meeting that has been declined
        //   1. Meeting that has been cancelled
        // (Declined meetings and cancelled meetings should never get this far, but it was easy enough to check for them.)

        private static int MeetingStatusLevel (McAbstrCalendarRoot cal)
        {
            if (cal.HasMeetingStatus ()) {
                switch (cal.GetMeetingStatus ()) {
                case NcMeetingStatus.MeetingOrganizer:
                    return 7;
                case NcMeetingStatus.MeetingAttendee:
                    if (cal.HasResponseType ()) {
                        if (NcResponseType.Accepted == cal.GetResponseType ()) {
                            return 6;
                        } else if (NcResponseType.Tentative == cal.GetResponseType ()) {
                            return 3;
                        } else if (NcResponseType.Declined == cal.GetResponseType ()) {
                            return 2;
                        } else {
                            return 4;
                        }
                    }
                    return 4;
                case NcMeetingStatus.Appointment:
                    return 5;
                case NcMeetingStatus.MeetingOrganizerCancelled:
                case NcMeetingStatus.MeetingAttendeeCancelled:
                default:
                    return 1;
                }
            }
            // Treat it like an appointment.
            return 5;
        }

        /// <summary>
        /// Is the first event more important than the second event?
        /// </summary>
        private static bool FirstIsBetterMeetingStatus (McAbstrCalendarRoot a, McAbstrCalendarRoot b)
        {
            return MeetingStatusLevel (a) > MeetingStatusLevel (b);
        }

        /// <summary>
        /// Should the first event trump the second one when choosing the hot event, assuming both events are in the future?
        /// The primary factor is the start time, with earlier start times being preferred.
        /// </summary>
        private static bool FirstIsBetterFutureEvent (McEvent a, McAbstrCalendarRoot aCal, McEvent b, McAbstrCalendarRoot bCal)
        {
            if (a.GetStartTimeUtc () < b.GetStartTimeUtc ()) {
                return true;
            }
            if (a.GetStartTimeUtc () > b.GetStartTimeUtc ()) {
                return false;
            }
            if (!a.AllDayEvent && b.AllDayEvent) {
                return true;
            }
            if (a.AllDayEvent && !b.AllDayEvent) {
                return false;
            }
            return FirstIsBetterMeetingStatus (aCal, bCal);
        }

        /// <summary>
        /// Should the first event trump the second one when choosing the hot event, assuming both events are currently in progress?
        /// The primary factor is the start time, with the event that has started the most recently being preferred.
        /// </summary>
        private static bool FirstIsBetterCurrentEvent (McEvent a, McAbstrCalendarRoot aCal, McEvent b, McAbstrCalendarRoot bCal)
        {
            if (a.GetStartTimeUtc () > b.GetStartTimeUtc ()) {
                return true;
            }
            if (a.GetStartTimeUtc () < b.GetStartTimeUtc ()) {
                return false;
            }
            return FirstIsBetterMeetingStatus (aCal, bCal);
        }

        private static DateTime EarlierTime (DateTime a, DateTime b)
        {
            if (a < b) {
                return a;
            }
            return b;
        }

        /// How far into the future to look for the next event.
        private static TimeSpan NextEventWindow = TimeSpan.FromDays (7);

        /// The time span within which an upcoming event trumps an in-progress event when choosing the hot event.
        private static TimeSpan NearFutureSpan = TimeSpan.FromMinutes (30);

        /// The time span within which a regular event trumps an all-day event when choosing the hot event.
        private static TimeSpan MediumFutureSpan = TimeSpan.FromHours (24);

        /// <summary>
        /// Pick the event to display in the hot event view.  The goal is to pick the one event that the user is most interested in seeing
        /// </summary>
        /// <returns>The or next event.</returns>
        /// <param name="nextCheckTime">Next check time.</param>
        public static McEvent CurrentOrNextEvent (out DateTime nextCheckTime)
        {
            McEvent bestNearFuture = null;
            McAbstrCalendarRoot bestNearFutureCal = null;
            bool multipleInNearFuture = false;
            McEvent bestCurrent = null;
            McAbstrCalendarRoot bestCurrentCal = null;
            McEvent bestMediumFuture = null;
            McAbstrCalendarRoot bestMediumFutureCal = null;
            McEvent bestAllDay = null;
            McAbstrCalendarRoot bestAllDayCal = null;
            McEvent bestFarFuture = null;
            McAbstrCalendarRoot bestFarFutureCal = null;

            DateTime now = DateTime.UtcNow;

            foreach (var evt in McEvent.UpcomingEvents (NextEventWindow)) {

                if (evt.GetEndTimeUtc () < now) {
                    // Because of the way that all-day events are stored, it is possible for McEvent.UpcomingEvents() to return an
                    // all-day event that is already over.  Ignore such events.
                    continue;
                }
                var cal = evt.GetCalendarItemforEvent ();
                if (null == cal || NcMeetingStatus.MeetingOrganizerCancelled == cal.GetMeetingStatus () || NcMeetingStatus.MeetingAttendeeCancelled == cal.GetMeetingStatus ()) {
                    // Ignore events that don't have a corresponding calendar item or that have been cancelled.
                    continue;
                }

                if (!evt.AllDayEvent && evt.GetStartTimeUtc () > now && evt.GetStartTimeUtc () - now < NearFutureSpan) {
                    // Starts in the next 30 minutes
                    if (null == bestNearFuture || FirstIsBetterFutureEvent (evt, cal, bestNearFuture, bestNearFutureCal)) {
                        bestNearFuture = evt;
                        bestNearFutureCal = cal;
                    } else if (evt.GetStartTimeUtc () != bestNearFuture.GetStartTimeUtc ()) {
                        multipleInNearFuture = true;
                        // Events at at least two different times within the next 30 minutes.  Looking at more events won't be useful.
                        break;
                    }
                } else if (!evt.AllDayEvent && evt.GetStartTimeUtc () <= now) {
                    // Event is currently in progress
                    if (null == bestCurrent || FirstIsBetterCurrentEvent (evt, cal, bestCurrent, bestCurrentCal)) {
                        bestCurrent = evt;
                        bestCurrentCal = cal;
                    }
                } else if (!evt.AllDayEvent && evt.GetStartTimeUtc () - now < MediumFutureSpan) {
                    // Sarts in the next 24 hours
                    if (null == bestMediumFuture || FirstIsBetterFutureEvent (evt, cal, bestMediumFuture, bestMediumFutureCal)) {
                        bestMediumFuture = evt;
                        bestMediumFutureCal = cal;
                    } else if (evt.GetStartTimeUtc () != bestMediumFuture.GetStartTimeUtc ()) {
                        // Events at at least two different times within the next day.  Looking at more events won't be useful.
                        break;
                    }
                } else if (evt.AllDayEvent && evt.GetStartTimeUtc () - now < MediumFutureSpan) {
                    // All day event for today or tomorrow.
                    if (null == bestAllDay || FirstIsBetterFutureEvent (evt, cal, bestAllDay, bestAllDayCal)) {
                        bestAllDay = evt;
                        bestAllDayCal = cal;
                    }
                } else {
                    // Starts more than 24 hours in the future.  For this bucket, all-day events and regular events are treated the same.
                    if (null == bestFarFuture || FirstIsBetterFutureEvent (evt, cal, bestFarFuture, bestFarFutureCal)) {
                        bestFarFuture = evt;
                        bestFarFutureCal = cal;
                    } else if (evt.AllDayEvent != bestFarFuture.AllDayEvent || evt.GetStartTimeUtc () - bestFarFuture.GetStartTimeUtc () > TimeSpan.FromHours (14)) {
                        // Looking at more events won't be useful.  (The 14-hour time span deals with the issue of all-day events
                        // and regular events being returned out of order.
                        break;
                    }
                }
            }

            // Figure out when the hot event view should next call CurrentOrNextEvent().  Err on the side of returning a time span
            // that is too short rather than too long.  A time span that is too short will result in some extra computation.
            // A time span that is too long may result in the wrong event being displayed.
            if (multipleInNearFuture) {
                nextCheckTime = bestNearFuture.GetStartTimeUtc ();
            } else {
                nextCheckTime = now.AddHours (2);
                if (null != bestNearFuture) {
                    nextCheckTime = EarlierTime (nextCheckTime, bestNearFuture.GetEndTimeUtc ());
                }
                if (null != bestCurrent) {
                    nextCheckTime = EarlierTime (nextCheckTime, bestCurrent.GetEndTimeUtc ());
                }
                if (null != bestMediumFuture) {
                    nextCheckTime = EarlierTime (nextCheckTime, bestMediumFuture.GetStartTimeUtc ().Subtract (NearFutureSpan));
                }
                if (null != bestAllDay && null == bestNearFuture && null == bestCurrent && null == bestMediumFuture) {
                    nextCheckTime = EarlierTime (nextCheckTime, bestAllDay.GetEndTimeUtc ());
                    if (null != bestFarFuture) {
                        nextCheckTime = EarlierTime (nextCheckTime, bestFarFuture.GetStartTimeUtc ().Subtract (MediumFutureSpan));
                    }
                }
            }
            return bestNearFuture ?? bestCurrent ?? bestMediumFuture ?? bestAllDay ?? bestFarFuture;
        }

        /// <summary>
        /// Convert the local time zone information into a form that can be represented in
        /// Exchange's time zone format.
        /// </summary>
        /// <remarks>
        /// Exchange's time zone information has very limited flexability for specifying
        /// daylight saving rules.  There is room for just one floating rule, e.g. second
        /// Sunday in March.  Take the local time zone and simplify its rules to fit within
        /// Exchange's limitations.  If the current year has a fixed date rule, deduce a
        /// floating rule and assume that it applies to all years.
        /// </remarks>
        public static TimeZoneInfo SimplifiedLocalTimeZone ()
        {
            return SimplifiedTimeZone (TimeZoneInfo.Local);
        }

        /// <summary>
        /// Convert the given time zone information into a form that can be represented in
        /// Exchange's time zone format.
        /// </summary>
        /// <remarks>
        /// Exchange's time zone information has very limited flexability for specifying
        /// daylight saving rules.  There is room for just one floating rule, e.g. second
        /// Sunday in March.  Take the local time zone and simplify its rules to fit within
        /// Exchange's limitations.  If the current year has a fixed date rule, deduce a
        /// floating rule and assume that it applies to all years.
        /// </remarks>
        public static TimeZoneInfo SimplifiedTimeZone (TimeZoneInfo local)
        {
            if (!local.SupportsDaylightSavingTime) {
                // No problem.
                return local;
            }

            // Find the DST adjustment for right now.
            DateTime now = DateTime.Now;
            TimeZoneInfo.AdjustmentRule currentAdjustment = null;
            foreach (var adjustment in local.GetAdjustmentRules()) {
                if (adjustment.DateStart <= now && now < adjustment.DateEnd) {
                    currentAdjustment = adjustment;
                    break;
                }
                // If there are separate DST rules for each year, there can be a small window in between
                // the effective ranges of the rules. So we also look for an adjustment rule that will
                // be applicable within the near future.
                if (now < adjustment.DateStart && (adjustment.DateStart - now).TotalDays < 365 &&
                    (null == currentAdjustment || adjustment.DateStart < currentAdjustment.DateStart)) {
                    currentAdjustment = adjustment;
                }
            }

            if (null == currentAdjustment) {
                // No DST in effect in the near future.  Return a new TimeZoneInfo object that simply
                // has DST disabled.
                return TimeZoneInfo.CreateCustomTimeZone (
                    local.Id, local.BaseUtcOffset, local.DisplayName, local.StandardName);
            }

            TimeZoneInfo.TransitionTime dstStart = currentAdjustment.DaylightTransitionStart;
            if (dstStart.IsFixedDateRule) {
                dstStart = FlexibleRuleFromFixedDate (dstStart, currentAdjustment.DateEnd);
            }
            TimeZoneInfo.TransitionTime dstEnd = currentAdjustment.DaylightTransitionEnd;
            if (dstEnd.IsFixedDateRule) {
                dstEnd = FlexibleRuleFromFixedDate (dstEnd, currentAdjustment.DateEnd);
            }
            return TimeZoneInfo.CreateCustomTimeZone (
                local.Id, local.BaseUtcOffset, local.DisplayName, local.StandardName, local.DaylightName,
                new TimeZoneInfo.AdjustmentRule[] { TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule (
                        DateTime.MinValue.Date, DateTime.MaxValue.Date, currentAdjustment.DaylightDelta, dstStart, dstEnd)
                });
        }

        private static TimeZoneInfo.TransitionTime FlexibleRuleFromFixedDate (TimeZoneInfo.TransitionTime fixedTransition, DateTime endDate)
        {
            int month = fixedTransition.Month;
            int day = fixedTransition.Day;
            DateTime now = DateTime.Now;
            DateTime transitionDate = new DateTime (now.Year, month, day);
            if (transitionDate < now) {
                transitionDate = new DateTime (now.Year + 1, month, day);
            }
            while (endDate < transitionDate) {
                transitionDate = new DateTime (transitionDate.Year - 1, month, day);
            }
            // If the date is within the last seven days of the month, assume that
            // the rule is "last" rather than "4th".
            int week;
            if (day <= 7) {
                week = 1;
            } else if (day <= 14) {
                week = 2;
            } else if (day <= 21) {
                week = 3;
            } else if (day <= DateTime.DaysInMonth (transitionDate.Year, month) - 7) {
                week = 4;
            } else {
                week = 5;
            }
            return TimeZoneInfo.TransitionTime.CreateFloatingDateRule (
                fixedTransition.TimeOfDay, month, week, transitionDate.DayOfWeek);
        }

        /// <summary>
        /// Mono has a bug in its daylight saving code, where a transition to or from
        /// daylight saving time might happen a week early.  This function tests whether
        /// or not the runtime in use has this particular bug.
        /// </summary>
        /// <returns><c>true</c>, if the runtime is correct, <c>false</c> if the runtime has the bug.</returns>
        /// <remarks>
        /// The bug happens when (1) the transition is a floating rule rather than a
        /// fixed rule, (2) the transition happens in a week other than the first week,
        /// and (3) the day of the week of the first day of the month is later in the
        /// week than the day of the week that the transition happens on.  (Since most
        /// transitions happen on Sundays, which is the first day of the week, condition
        /// #3 almost always applies.)
        /// </remarks>
        private static bool DaylightSavingCorrectnessCheck ()
        {
            // Construct a custom time zone where daylight saving time starts on the
            // 2nd Sunday in March.
            var transitionToDaylight = TimeZoneInfo.TransitionTime.CreateFloatingDateRule (
                                           new DateTime (1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday);
            var transitionToStandard = TimeZoneInfo.TransitionTime.CreateFloatingDateRule (
                                           new DateTime (1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday);
            var adjustment = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule (
                                 DateTime.MinValue.Date, DateTime.MaxValue.Date, new TimeSpan (1, 0, 0),
                                 transitionToDaylight, transitionToStandard);
            var timeZone = TimeZoneInfo.CreateCustomTimeZone (
                               "BugCheck", new TimeSpan (-8, 0, 0), "Testing", "Testing Standard", "Testing Daylight",
                               new TimeZoneInfo.AdjustmentRule[] { adjustment });
            // See if March 7, 2014 is listed as being during daylight saving time.
            // If it is DST, then the runtime has the bug that we are looking for.
            return !timeZone.IsDaylightSavingTime (new DateTime (2014, 3, 7, 12, 0, 0, DateTimeKind.Unspecified));
        }

        private static bool daylightSavingIsCorrect = DaylightSavingCorrectnessCheck ();

        /// <summary>
        /// Convert from UTC to the specified time zone, working around a Mono bug if
        /// necessary.
        /// </summary>
        public static DateTime ConvertTimeFromUtc (DateTime utc, TimeZoneInfo timeZone)
        {
            if (daylightSavingIsCorrect || !timeZone.SupportsDaylightSavingTime) {
                return TimeZoneInfo.ConvertTimeFromUtc (utc, timeZone);
            }
            DateTime local = new DateTime (utc.Ticks + timeZone.BaseUtcOffset.Ticks, DateTimeKind.Unspecified);
            TimeZoneInfo.AdjustmentRule adjustment = FindAdjustmentRule (timeZone, local);
            if (null == adjustment || (!WorkaroundNeeded (local, adjustment.DaylightTransitionStart) && !WorkaroundNeeded (local, adjustment.DaylightTransitionEnd))) {
                return TimeZoneInfo.ConvertTimeFromUtc (utc, timeZone);
            }
            if (IsDaylightTime (local, adjustment)) {
                local = local.Add (adjustment.DaylightDelta);
            }
            return local;
        }

        /// <summary>
        /// Convert from a time in the specified time zone to UTC, working around a Mono
        /// bug if necessary.
        /// </summary>
        public static DateTime ConvertTimeToUtc (DateTime local, TimeZoneInfo timeZone)
        {
            try {
                if (daylightSavingIsCorrect || !timeZone.SupportsDaylightSavingTime) {
                    return TimeZoneInfo.ConvertTimeToUtc (local, timeZone);
                }
                TimeZoneInfo.AdjustmentRule adjustment = FindAdjustmentRule (timeZone, local);
                if (null == adjustment ||
                    (!WorkaroundNeeded (local, adjustment.DaylightTransitionStart) &&
                        !WorkaroundNeeded (local, adjustment.DaylightTransitionEnd)))
                {
                    return TimeZoneInfo.ConvertTimeToUtc (local, timeZone);
                }
                DateTime utc = new DateTime (local.Ticks - timeZone.BaseUtcOffset.Ticks, DateTimeKind.Utc);
                if (IsDaylightTime (local, adjustment)) {
                    utc = utc.Subtract (adjustment.DaylightDelta);
                }
                return utc;
            } catch (ArgumentException) {
                if (timeZone.IsInvalidTime (local)) {
                    // The time is within the missing hour at the transition from standard time to daylight time.
                    // Try with a time that is an hour later.
                    return ConvertTimeToUtc (local.AddHours (1), timeZone);
                } else {
                    // Something else is wrong, which can't be handled here.
                    throw;
                }
            }
        }

        /// <summary>
        /// Find the daylight adjustment rule that applies to the specified
        /// local time, if any.
        /// </summary>
        private static TimeZoneInfo.AdjustmentRule FindAdjustmentRule (TimeZoneInfo timeZone, DateTime local)
        {
            foreach (var adjustment in timeZone.GetAdjustmentRules()) {
                if (adjustment.DateStart < local && local <= adjustment.DateEnd) {
                    return adjustment;
                }
            }
            return null;
        }

        /// <summary>
        /// Is the workaround needed for the given time and the given transition rule?
        /// Return true only if the given time is within the month of the transition and
        /// the transition is suseptable to the bug.
        /// </summary>
        private static bool WorkaroundNeeded (DateTime local, TimeZoneInfo.TransitionTime rule)
        {
            return !rule.IsFixedDateRule && 1 != rule.Week && local.Month == rule.Month;
        }

        /// <summary>
        /// Return the exact time of the given transition for the given year.
        /// </summary>
        private static DateTime TransitionPoint (TimeZoneInfo.TransitionTime rule, int year)
        {
            DayOfWeek first = new DateTime (year, rule.Month, 1).DayOfWeek;
            int day = 1 + (rule.Week - 1) * 7 + ((rule.DayOfWeek - first) + 7) % 7;
            while (day > DateTime.DaysInMonth (year, rule.Month)) {
                day -= 7;
            }
            return new DateTime (year, rule.Month, day) + rule.TimeOfDay.TimeOfDay;
        }

        /// <summary>
        /// Is the given time within the daylight saving time period?  **NOTE**:
        /// This will give the correct answer only if the given time is in the same
        /// month as either of the transitions. It will return false for any other
        /// month.
        /// </summary>
        private static bool IsDaylightTime (DateTime local, TimeZoneInfo.AdjustmentRule adjustment)
        {
            return (local.Month == adjustment.DaylightTransitionStart.Month && local > TransitionPoint (adjustment.DaylightTransitionStart, local.Year)) ||
            (local.Month == adjustment.DaylightTransitionEnd.Month && local < TransitionPoint (adjustment.DaylightTransitionEnd, local.Year));
        }

        public static string GetFirstName (string displayName)
        {
            string[] names = displayName.Split (new char [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (0 == names.Length || names [0] == null) {
                return "";
            }
            if (names [0].Length > 1) {
                return char.ToUpper (names [0] [0]) + names [0].Substring (1);
            }
            return names [0].ToUpper ();
        }
    }
}

