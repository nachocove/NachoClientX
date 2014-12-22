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

        public static McCalendar DefaultMeeting ()
        {
            var c = new McCalendar ();
            var start = DateTime.UtcNow.AddMinutes (30.0);
            if (start.Minute >= 30.0) {
                c.StartTime = new DateTime (start.Year, start.Month, start.Day, start.Hour, 30, 0, DateTimeKind.Utc);
            } else {
                c.StartTime = new DateTime (start.Year, start.Month, start.Day, start.Hour, 0, 0, DateTimeKind.Utc);
            }
            c.EndTime = c.StartTime.AddMinutes (60.0);
            return c;
        }

        public static McTask DefaultTask ()
        {
            var t = new McTask ();
            return t;
        }

        /// <summary>
        /// Return the starting time for the event in UTC, without triggering the DDay.iCal
        /// time zone code that would cause the app to crash.
        /// </summary>
        public static DateTime EventStartTime (DDay.iCal.Event evt, McCalendar c)
        {
            if (null != c) {
                return c.StartTime;
            }
            // This isn't an error.  But we want to log a message to track how often this
            // happens.  If it happens a lot, me might invest more in calculating correct
            // times.
            Log.Error (Log.LOG_CALENDAR, "Extracting the time from the iCalendar object in the message because the corresponding McCalendar object could not be found.");
            return TimeZoneAdjustment (
                evt.Parent as IICalendar,
                evt.Start.Parameters.Get ("TZID"),
                evt.Start.Value);
        }

        /// <summary>
        /// Return the ending time for the event in UTC, without triggering the DDay.iCal
        /// time zone code that would cause the app to crash.
        /// </summary>
        public static DateTime EventEndTime (DDay.iCal.Event evt, McCalendar c)
        {
            if (null != c) {
                return c.EndTime;
            }
            return TimeZoneAdjustment (
                evt.Parent as IICalendar,
                evt.End.Parameters.Get ("TZID"),
                evt.End.Value);
        }

        /// <summary>
        /// Make a basic attempt to adjust the event time to UTC using the time zone
        /// information in the calendar item, without using DDay.iCal's time zone
        /// calculations. Calling Event.Start.UTC will cause a crash on iOS devices
        /// due to a <a href="http://developer.xamarin.com/guides/ios/advanced_topics/limitations/">limitation</a>
        /// in the way Mono runs on iOS. The calculation should be correct most of the
        /// time, but it might be off by an hour or two when it guesses incorrectly
        /// about whether or not the time in question is daylight saving time.
        /// </summary>
        private static DateTime TimeZoneAdjustment (IICalendar iCal, string tzid, DateTime time)
        {
            if (null == iCal || string.IsNullOrEmpty (tzid)) {
                return time;
            }
            bool isDaylight = DateTime.SpecifyKind (time, DateTimeKind.Unspecified).IsDaylightSavingTime ();
            foreach (var timeZone in iCal.TimeZones) {
                if (timeZone.TZID == tzid) {
                    foreach (var tzi in timeZone.TimeZoneInfos) {
                        if (tzi.Name == (isDaylight ? "DAYLIGHT" : "STANDARD")) {
                            var offset = tzi.OffsetTo;
                            return DateTime.SpecifyKind (
                                time.AddHours (offset.Positive ? -offset.Hours : offset.Hours)
                                    .AddMinutes (offset.Positive ? -offset.Minutes : offset.Minutes)
                                    .AddSeconds (offset.Positive ? -offset.Seconds : offset.Seconds),
                                DateTimeKind.Utc);
                        }
                    }
                }
            }
            return time;
        }

        public static bool IsOrganizer (string organizerEmail, string userEmail)
        {
            if (organizerEmail == userEmail) {
                return true;
            }
            return false;
        }

        public static DateTime AdjustAllDayEventEndTime (DateTime date)
        {
            return date.AddDays (-1);
        }

        public static McCalendar GetMcCalendarRootForEvent (int eventId)
        {
            var e = McEvent.QueryById<McEvent> (eventId);
            if (null == e) {
                return null;  // may be deleted
            }
            var c = McCalendar.QueryById<McCalendar> (e.CalendarId);
            if (null == c) {
                return null; // may be deleted
            }
            return c;
        }

        public static IICalendar iCalendarFromMcCalendarWithResponse (McAccount account, McCalendar c, NcResponseType response)
        {
            var iCal = iCalendarFromMcCalendarCommon (c);
            iCal.Method = DDay.iCal.CalendarMethods.Reply;
            var vEvent = iCal.Events [0];
            var iAttendee = new Attendee ("MAILTO:" + account.EmailAddr);
            if (!string.IsNullOrEmpty (account.DisplayName)) {
                iAttendee.CommonName = account.DisplayName;
            }
            iAttendee.ParticipationStatus = iCalResponseString (response);
            vEvent.Attendees.Add (iAttendee);
            vEvent.Summary = ResponseSubjectPrefix (response) + ": " + c.Subject;
            return iCal;
        }

        public static IICalendar iCalendarFromMcCalendar (McAccount account, McCalendar c)
        {
            var iCal = iCalendarFromMcCalendarCommon (c);
            iCal.Method = DDay.iCal.CalendarMethods.Request;

            var evt = iCal.Events [0];
            evt.Summary = c.Subject;
            evt.Organizer = new Organizer (account.EmailAddr);
            evt.Organizer.SentBy = new Uri ("MAILTO:" + account.EmailAddr);
            if (!string.IsNullOrEmpty (account.DisplayName)) {
                evt.Organizer.CommonName = account.DisplayName;
            }

            foreach (var mcAttendee in c.attendees) {
                var iAttendee = new Attendee ("MAILTO:" + mcAttendee.Email);
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
            }
            return iCal;
        }

        /// <summary>
        /// The parts of iCalendar that are common to both meeting requests and meeting responses.
        /// </summary>
        private static IICalendar iCalendarFromMcCalendarCommon (McCalendar c)
        {
            var iCal = new iCalendar ();
            iCal.ProductID = "Nacho Mail";

            var tzi = CalendarHelper.SimplifiedLocalTimeZone ();
            var timezone = FromSystemTimeZone (tzi, c.StartTime.AddYears (-1), false);
            var localTimeZone = iCal.AddTimeZone (timezone);
            if (null != tzi.StandardName) {
                timezone.TZID = tzi.StandardName;
                localTimeZone.TZID = tzi.StandardName;
            }

            var vEvent = iCal.Create<DDay.iCal.Event> ();
            vEvent.UID = c.UID;
            vEvent.LastModified = new iCalDateTime (DateTime.UtcNow);
            vEvent.Start = new iCalDateTime (c.StartTime.LocalT (), localTimeZone.TZID);
            vEvent.End = new iCalDateTime (c.EndTime.LocalT (), localTimeZone.TZID);
            vEvent.IsAllDay = c.AllDayEvent;
            vEvent.Priority = 5;
            if (c.AllDayEvent) {
                vEvent.Properties.Set ("X-MICROSOFT-CDO-ALLDAYEVENT", "TRUE");
                vEvent.Properties.Set ("X-MICROSOFT-CDO-INTENDEDSTATUS", "FREE");
            } else {
                vEvent.Properties.Set ("X-MICROSOFT-CDO-ALLDAYEVENT", "FALSE");
                vEvent.Properties.Set ("X-MICROSOFT-CDO-INTENDEDSTATUS", "BUSY");
            }
            vEvent.Location = c.Location;
            vEvent.Status = EventStatus.Confirmed;
            vEvent.Class = "PUBLIC";
            vEvent.Transparency = TransparencyType.Opaque;
            return iCal;
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
            // TODO: Subtle ugliness. Id is passed to BE, ref-count is ++ in the DB.
            // The object here still has ref-count of 0, so interlock is lost, and delete really happens in the DB.
            // BE goes to reference the object later on, and it is missing.
            mcMessage = McEmailMessage.QueryById<McEmailMessage> (mcMessage.Id);
            mcMessage.Delete ();
        }

        //Used to send a single invite to one attendee at a time rather than all attendees of an event
        public static void SendInvite (McAccount account, McCalendar c, McAttendee a, MimeEntity mimeBody)
        {
            var mimeMessage = new MimeMessage ();

            mimeMessage.From.Add (new MailboxAddress (Pretty.DisplayNameForAccount (account), account.EmailAddr));
            mimeMessage.To.Add (new MailboxAddress (a.Name, a.Email));

            mimeMessage.Subject = Pretty.SubjectString (c.Subject);
            mimeMessage.Date = System.DateTime.UtcNow;
            mimeMessage.Body = mimeBody;

            var mcMessage = MimeHelpers.AddToDb (account.Id, mimeMessage);
            BackEnd.Instance.SendEmailCmd (mcMessage.AccountId, mcMessage.Id, c.Id);
            mcMessage = McEmailMessage.QueryById<McEmailMessage> (mcMessage.Id);
            mcMessage.Delete ();
        }

        public static void SendMeetingResponse (McAccount account, McCalendar c, MimeEntity mimeBody, NcResponseType response)
        {
            // Need to send this message to someone.  Fix this assertion upstream if you hit it.
            NcAssert.True (!String.IsNullOrEmpty (c.OrganizerName) || !String.IsNullOrEmpty (c.OrganizerEmail));

            var mimeMessage = new MimeMessage ();
            mimeMessage.From.Add (new MailboxAddress (Pretty.DisplayNameForAccount (account), account.EmailAddr));
            mimeMessage.To.Add (new MailboxAddress (c.OrganizerName, c.OrganizerEmail));
            mimeMessage.Subject = Pretty.SubjectString (ResponseSubjectPrefix (response) + ": " + c.Subject);
            mimeMessage.Date = DateTime.UtcNow;
            mimeMessage.Body = mimeBody;
            var mcMessage = MimeHelpers.AddToDb (account.Id, mimeMessage);
            BackEnd.Instance.SendEmailCmd (mcMessage.AccountId, mcMessage.Id, c.Id);
            mcMessage = McEmailMessage.QueryById<McEmailMessage> (mcMessage.Id);
            mcMessage.Delete ();
        }

        /// <summary>
        /// Create a text/calendar MIME part with a meeting request for the given event.
        /// </summary>
        public static TextPart iCalToMimePart (McAccount account, McCalendar c)
        {
            return iCalToMimePartCommon (
                CalendarHelper.iCalendarFromMcCalendar (account, c),
                DDay.iCal.CalendarMethods.Request);
        }

        /// <summary>
        /// Create a text/calendar MIME part with a meeting response for the given event.
        /// </summary>
        public static TextPart iCalResponseToMimePart (McAccount account, McCalendar c, NcResponseType response)
        {
            return iCalToMimePartCommon (
                CalendarHelper.iCalendarFromMcCalendarWithResponse (account, c, response),
                DDay.iCal.CalendarMethods.Reply);
        }

        /// <summary>
        /// Create a text/calendar MIME part from the given iCalendar object.
        /// </summary>
        private static TextPart iCalToMimePartCommon (IICalendar iCal, string method)
        {
            var iCalPart = new TextPart ("calendar");
            iCalPart.ContentType.Parameters.Add ("METHOD", method);
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

        public static MimeEntity CreateMime (string description, TextPart iCalPart, List<McAttachment> attachments)
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

        public static McCalendar CreateMeeting (McEmailMessage message)
        {
            var c = DefaultMeeting ();
            c.AccountId = message.AccountId;
            c.Subject = message.Subject;
//            var dupBody = McBody.InsertDuplicate (message.AccountId, message.BodyId);
//            c.BodyId = dupBody.Id;

            //Instead of grabbing the whole body from the email message, only the
            //text part (if there exists one) is added to the event description.
            if (null != MimeHelpers.ExtractTextPart (message.GetBody ())) {
                c.Description = MimeHelpers.ExtractTextPart (message.GetBody ());
            }
            c.attendees = new System.Collections.Generic.List<McAttendee> ();
            c.attendees.AddRange (CreateAttendeeList (message.AccountId, message.From, NcAttendeeType.Required));
            c.attendees.AddRange (CreateAttendeeList (message.AccountId, message.To, NcAttendeeType.Required));
            c.attendees.AddRange (CreateAttendeeList (message.AccountId, message.Cc, NcAttendeeType.Optional));
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
                    if (NcDayOfWeek.Sunday != r.DayOfWeek && NcDayOfWeek.Monday != r.DayOfWeek && NcDayOfWeek.Tuesday != r.DayOfWeek && NcDayOfWeek.Wednesday != r.DayOfWeek &&
                        NcDayOfWeek.Thursday != r.DayOfWeek && NcDayOfWeek.Friday != r.DayOfWeek && NcDayOfWeek.Weekdays != r.DayOfWeek && NcDayOfWeek.WeekendDays != r.DayOfWeek &&
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
                    if (NcDayOfWeek.Sunday != r.DayOfWeek && NcDayOfWeek.Monday != r.DayOfWeek && NcDayOfWeek.Tuesday != r.DayOfWeek && NcDayOfWeek.Wednesday != r.DayOfWeek &&
                        NcDayOfWeek.Thursday != r.DayOfWeek && NcDayOfWeek.Friday != r.DayOfWeek && NcDayOfWeek.Weekdays != r.DayOfWeek && NcDayOfWeek.WeekendDays != r.DayOfWeek &&
                        NcDayOfWeek.LastDayOfTheMonth != r.DayOfWeek) {
                        throw new RecurrenceValidationException (string.Format (
                            "The DayOfWeek field has an invalid value of {0} for a monthly recurrence. It must be one of: 1, 2, 4, 8, 16, 32, 62, 64, 65, 127.", (int)r.DayOfWeek));
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
        protected static NcDayOfWeek ToNcDayOfWeek (DayOfWeek day)
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
            // All date/time calculations must be done in the event's original time zone.
            TimeZoneInfo timeZone = new AsTimeZone (c.TimeZone).ConvertToSystemTimeZone ();
            DateTime eventStart = TimeZoneInfo.ConvertTimeFromUtc (c.StartTime, timeZone);
            DateTime eventEnd = TimeZoneInfo.ConvertTimeFromUtc (c.EndTime, timeZone);
            var duration = eventEnd - eventStart;

            int maxOccurrences = r.OccurencesIsSet ? r.Occurences : int.MaxValue;
            DateTime lastOccurence = r.Until == default(DateTime) ? DateTime.MaxValue : TimeZoneInfo.ConvertTimeFromUtc (r.Until, timeZone);

            int occurrence = 0;

            while (occurrence < maxOccurrences && eventStart <= lastOccurence) {
                DateTime eventStartUtc = TimeZoneInfo.ConvertTimeToUtc (eventStart, timeZone);
                if (eventStartUtc > startingTime) {
                    DateTime eventEndUtc = TimeZoneInfo.ConvertTimeToUtc (eventStart + duration, timeZone);
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

        protected static void ExpandAllDayEvent (McCalendar c, DateTime start, DateTime end)
        {
            // Create local events for an all-day calendar item.  Figure out the starting
            // day of the event in the organizer's time zone.  Create a local event that
            // starts at midnight local time on the same day.  If the original item is
            // more than one day long, repeat for subsequent days.

            TimeZoneInfo timeZone = new AsTimeZone (c.TimeZone).ConvertToSystemTimeZone ();
            DateTime organizersTime = TimeZoneInfo.ConvertTimeFromUtc (start, timeZone);
            DateTime localStartTime = new DateTime (organizersTime.Year, organizersTime.Month, organizersTime.Day, 0, 0, 0, DateTimeKind.Local);
            double days = (end - start).TotalDays;
            // Use a do/while loop so we will always create at least one event, even if
            // the original item is improperly less than one day long.  If the all-day
            // event spans the transition from daylight saving time to standard time,
            // then it will have an extra hour in its duration.  So the cutoff for
            // creating an extra event is a quarter of a day.
            do {
                DateTime nextDay = localStartTime.AddDays (1.0);
                CreateEventRecord (c, localStartTime.ToUniversalTime (), nextDay.ToUniversalTime ());
                localStartTime = nextDay;
                days -= 1.0;
            } while (days > 0.25);
        }

        public static bool ExpandRecurrences (DateTime untilDate)
        {
//            // Debug
//            NcModel.Instance.Db.DeleteAll<McEvent> ();
//            FIXME - if we choose to use DeleteAll, we need to add support for it in NcModel.
//
//            // Debug
//            var l = NcModel.Instance.Db.Table<McCalendar> ().ToList ();
//            foreach (var e in l) {
//                e.RecurrencesGeneratedUntil = DateTime.MinValue;
//                e.Update ();
//            }
//
            // Decide how long into the future we are going to generate recurrences
            DateTime GenerateUntil = untilDate;

            // Fetch calendar entries that haven't been generated that far in advance
            var list = McCalendar.QueryOutOfDateRecurrences (GenerateUntil);

            // Abandon if nothing to do
            if ((null == list) || (0 == list.Count)) {
                return false;
            }

            // Loop thru 'em, generating recurrences
            foreach (var calendarItem in list) {
                // Just add entries that don't have recurrences
                if (0 == calendarItem.recurrences.Count) {
                    if (calendarItem.AllDayEvent) {
                        ExpandAllDayEvent (calendarItem, calendarItem.StartTime, calendarItem.EndTime);
                    } else {
                        CreateEventRecord (calendarItem, calendarItem.StartTime, calendarItem.EndTime);
                    }
                    calendarItem.RecurrencesGeneratedUntil = DateTime.MaxValue;
                    calendarItem.Update ();
                    continue;
                }
                var lastOneGeneratedAggregate = DateTime.MaxValue;
                foreach (var recurrence in calendarItem.recurrences) {
                    var lastOneGenerated = ExpandRecurrences (calendarItem, recurrence, calendarItem.RecurrencesGeneratedUntil, GenerateUntil);
                    if (lastOneGeneratedAggregate > lastOneGenerated) {
                        lastOneGeneratedAggregate = lastOneGenerated;
                    }
                }
                calendarItem.RecurrencesGeneratedUntil = lastOneGeneratedAggregate;
                calendarItem.Update ();
            }

            var el = NcModel.Instance.Db.Table<McEvent> ().ToList ();
            Log.Info (Log.LOG_CALENDAR, "Events in db: {0}", el.Count);

            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_EventSetChanged),
                Account = NachoCore.Model.ConstMcAccount.NotAccountSpecific,
                Tokens = new String[] { DateTime.Now.ToString () },
            });

            return true;
        }

        protected static void CreateEventRecord (McCalendar c, DateTime startTime, DateTime endTime)
        {
            var exceptions = McException.QueryForExceptionId (c.Id, startTime);

            if ((null == exceptions) || (0 == exceptions.Count)) {
                var e = McEvent.Create (c.AccountId, startTime, endTime, c.Id, 0);
                if (c.ReminderIsSet) {
                    ScheduleNotification (e, c.Reminder);
                }
            } else {
                foreach (var exception in exceptions) {
                    var e = McEvent.Create (c.AccountId, exception.StartTime, exception.EndTime, c.Id, exception.Id);
                    if (exception.ReminderIsSet) {
                        ScheduleNotification (e, exception.Reminder);
                    }
                }
            }
        }

        public static void UpdateRecurrences (McCalendar c)
        {
            c.DeleteRelatedEvents ();
            c.RecurrencesGeneratedUntil = DateTime.MinValue;
            c.Update ();
            NcEventManager.Instance.ExpandRecurrences ();
        }

        /// Note that McEvent Ids are not immutable; they change often as the
        /// calendar event changes. Thus we push the immutable calendar ID into
        /// the notification. The 'calendar view' event will show the proper view.
        protected static void ScheduleNotification (McEvent e, uint reminder)
        {
            var notifier = NachoPlatform.Notif.Instance;
            notifier.CancelNotif (e.Id);
            var notificationTime = e.StartTime.AddMinutes (-reminder);
            var calendarItem = e.GetCalendarItemforEvent ();
            var message = Pretty.Join (Pretty.SubjectString (calendarItem.Subject), Pretty.FormatAlert (reminder));
            if (DateTime.UtcNow < notificationTime) {
                notifier.ScheduleNotif (e.Id, e.StartTime.AddMinutes (-reminder), message);
            }
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
            TimeZoneInfo local = TimeZoneInfo.Local;

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

    }
}

