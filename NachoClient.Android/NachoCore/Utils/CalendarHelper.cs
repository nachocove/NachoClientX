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
        public static DateTime EventStartTime(DDay.iCal.Event evt, McCalendar c)
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
        public static DateTime EventEndTime(DDay.iCal.Event evt, McCalendar c)
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
            if (null == iCal || string.IsNullOrEmpty(tzid)) {
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

        public static void SendInvites (McAccount account, McCalendar c, List<McAttendee> attendeeOverride, MimeEntity mimeBody)
        {
            var attendees = attendeeOverride ?? c.attendees;
            if (0 == attendees.Count) {
                // Nobody to invite.
                return;
            }
            var mimeMessage = new MimeMessage ();

            mimeMessage.From.Add (new MailboxAddress (Pretty.DisplayNameForAccount (account), account.EmailAddr));

            foreach (var a in attendees) {
                mimeMessage.To.Add (new MailboxAddress (a.Name, a.Email));
            }

            mimeMessage.Subject = Pretty.SubjectString (c.Subject);
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

        public static void SendMeetingResponse (McAccount account, McCalendar c, MimeEntity mimeBody, NcResponseType response)
        {
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
            var dupBody = McBody.InsertDuplicate (message.AccountId, message.BodyId);
            c.BodyId = dupBody.Id;
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

        // Type element is set to zero (0), meaning a daily occurrence.
        // If the DayOfWeek element is not set, the recurrence is a daily occurrence,
        // occurring n days apart, where n is the value of the Interval element.
        protected static DateTime GenerateEveryIntervalDays (McCalendar c, McRecurrence r, DateTime previousEntryInDatabase, DateTime generateAtLeastUntil)
        {
            NcAssert.True (0 == r.DayOfWeek);

            int occurrance = 0;

            DateTime eventStart = c.StartTime;
            DateTime eventEnd = c.EndTime;

            int daysToIncrement = (r.IntervalIsSet ? r.Interval : 1);

            while (!r.OccurencesIsSet || (occurrance <= r.Occurences)) {
                if ((DateTime.MinValue != r.Until) && (eventStart > r.Until)) {
                    return DateTime.MaxValue;
                }
                if (eventStart > previousEntryInDatabase) {
                    if (r.OccurencesIsSet) {
                        CreateEventRecord (c, eventStart, eventEnd);
                    } else {
                        CreateEventRecord (c, eventStart, eventEnd);
                    }
                }
                if (eventStart >= generateAtLeastUntil) {
                    return eventStart;
                }
                occurrance += 1;
                eventStart = eventStart.AddDays (daysToIncrement);
                eventEnd = eventEnd.AddDays (daysToIncrement);
            }
            // Generated all occurences
            return DateTime.MaxValue;
        }

        protected static bool DayInDayOfWeek (NcDayOfWeek a, NcDayOfWeek b)
        {
            return (0 != (((int)a) & ((int)b)));
        }

        // The type element is set to 0:
        // If the DayOfWeek element is set, the recurrence is a weekly occurrence,
        // occurring on the day specified by the DayOfWeek element, and the value
        // of the Interval element indicates the number of weeks between occurrences.
        // Or the Type element is set to 1, meaning a weekly occurrence.
        protected static DateTime GenerateEveryIntervalWeeks (McCalendar c, McRecurrence r, DateTime previousEntryInDatabase, DateTime generateAtLeastUntil)
        {
            int occurrance = 0;

            DateTime eventStart = c.StartTime;
            DateTime eventEnd = c.EndTime;

            // Interval of 0 is really 1
            int weeksToIncrement = (r.IntervalIsSet ? Math.Max (1, r.Interval) : 1);

            while (true) {
                for (int i = 0; i < 7; i++) {
                    if (r.OccurencesIsSet && (occurrance > r.Occurences)) {
                        return DateTime.MaxValue;
                    }
                    if ((DateTime.MinValue != r.Until) && (eventStart > r.Until)) {
                        return DateTime.MaxValue;
                    }
                    // Is eventStart a candidate date?
                    var shouldWe = false;
                    var dayOfWeek = eventStart.DayOfWeek;
                    if ((System.DayOfWeek.Sunday == dayOfWeek) && (DayInDayOfWeek (NcDayOfWeek.Sunday, r.DayOfWeek))) {
                        shouldWe = true;
                    }
                    if ((System.DayOfWeek.Monday == dayOfWeek) && (DayInDayOfWeek (NcDayOfWeek.Monday, r.DayOfWeek))) {
                        shouldWe = true;
                    }
                    if ((System.DayOfWeek.Tuesday == dayOfWeek) && (DayInDayOfWeek (NcDayOfWeek.Tuesday, r.DayOfWeek))) {
                        shouldWe = true;
                    }
                    if ((System.DayOfWeek.Wednesday == dayOfWeek) && (DayInDayOfWeek (NcDayOfWeek.Wednesday, r.DayOfWeek))) {
                        shouldWe = true;
                    }
                    if ((System.DayOfWeek.Thursday == dayOfWeek) && (DayInDayOfWeek (NcDayOfWeek.Thursday, r.DayOfWeek))) {
                        shouldWe = true;
                    }
                    if ((System.DayOfWeek.Friday == dayOfWeek) && (DayInDayOfWeek (NcDayOfWeek.Friday, r.DayOfWeek))) {
                        shouldWe = true;
                    }
                    if ((System.DayOfWeek.Saturday == dayOfWeek) && (DayInDayOfWeek (NcDayOfWeek.Saturday, r.DayOfWeek))) {
                        shouldWe = true;
                    }
                    if (shouldWe) {
                        if (eventStart > previousEntryInDatabase) {
                            if (r.OccurencesIsSet) {
                                CreateEventRecord (c, eventStart, eventEnd);
                            } else {
                                CreateEventRecord (c, eventStart, eventEnd);
                            }
                        }
                        occurrance += 1;
                    }
                    if (eventStart >= generateAtLeastUntil) {
                        return eventStart;
                    }
                    eventStart = eventStart.AddDays (1);
                    eventEnd = eventEnd.AddDays (1);
                }
                // Already incremented by a week
                NcAssert.True (0 < weeksToIncrement);
                eventStart = eventStart.AddDays (7 * (weeksToIncrement - 1));
                eventEnd = eventEnd.AddDays (7 * (weeksToIncrement - 1));
            }
        }

        // The Type element is set to 2, meaning a monthly occurrence,
        protected static DateTime GenerateEveryIntervalMonths (McCalendar c, McRecurrence r, DateTime previousEntryInDatabase, DateTime generateAtLeastUntil)
        {
            int occurrance = 0;

            DateTime eventStart = c.StartTime;
            DateTime eventEnd = c.EndTime;

            if (eventStart.Day != r.DayOfMonth) {
                Log.Error (Log.LOG_CALENDAR, "GenerateEveryIntervalMonths eventStart={0}, DayOfMonth={1}", eventStart.Day, r.DayOfMonth);
            }

            // Interval of 0 is really 1
            int Interval = (r.IntervalIsSet ? Math.Max (1, r.Interval) : 1);

            while (true) {
                if (r.OccurencesIsSet && (occurrance > r.Occurences)) {
                    return DateTime.MaxValue;
                }
                if ((DateTime.MinValue != r.Until) && (eventStart > r.Until)) {
                    return DateTime.MaxValue;
                }
                if (eventStart > previousEntryInDatabase) {
                    CreateEventRecord (c, eventStart, eventEnd);
                }
                occurrance += 1;
                if (eventStart >= generateAtLeastUntil) {
                    return eventStart;
                }
                NcAssert.True (0 < Interval);
                eventStart = eventStart.AddMonths (Interval);
                eventEnd = eventEnd.AddMonths (Interval);
            }
        }

        // The Type element is set to 5, meaning a yearly occurrence,
        protected static DateTime GenerateEveryIntervalYears (McCalendar c, McRecurrence r, DateTime previousEntryInDatabase, DateTime generateAtLeastUntil)
        {
            int occurrance = 0;

            DateTime eventStart = c.StartTime;
            DateTime eventEnd = c.EndTime;

            if (eventStart.Day != r.DayOfMonth) {
                Log.Error (Log.LOG_CALENDAR, "GenerateEveryIntervalYears eventStart={0}, DayOfMonth={1}", eventStart.Day, r.DayOfMonth);
            }
            if (eventStart.Month != r.MonthOfYear) {
                Log.Error (Log.LOG_CALENDAR, "GenerateEveryIntervalYears eventStart={0}, MonthOfYear={1}", eventStart.Month, r.MonthOfYear);
            }

            // Interval of 0 is really 1
            int Interval = (r.IntervalIsSet ? Math.Max (1, r.Interval) : 1);

            while (true) {
                if (r.OccurencesIsSet && (occurrance > r.Occurences)) {
                    return DateTime.MaxValue;
                }
                if ((DateTime.MinValue != r.Until) && (eventStart > r.Until)) {
                    return DateTime.MaxValue;
                }
                if (eventStart > previousEntryInDatabase) {
                    CreateEventRecord (c, eventStart, eventEnd);
                }
                occurrance += 1;
                if (eventStart >= generateAtLeastUntil) {
                    return eventStart;
                }
                NcAssert.True (0 < Interval);
                eventStart = eventStart.AddYears (Interval);
                eventEnd = eventEnd.AddYears (Interval);
            }
        }

        protected static DateTime ReplaceDayOfMonth (DateTime dt, int newDayOfMonth)
        {
            return new DateTime (
                dt.Year,
                dt.Month,
                newDayOfMonth,
                dt.Hour,
                dt.Minute,
                dt.Second,
                dt.Millisecond,
                dt.Kind);
        }

        // Type element set to 3.  Subcase:
        // If the DayOfWeek element is set to 127, the WeekOfMonth
        // element indicates the day of the month that the event occurs.
        protected static DateTime GenerateEveryNthDateOfMonth (McCalendar c, McRecurrence r, DateTime previousEntryInDatabase, DateTime generateAtLeastUntil)
        {
            int occurrance = 0;

            DateTime eventStart = c.StartTime;
            DateTime eventEnd = c.EndTime;

            NcAssert.True (127 == (int)r.DayOfWeek);

            // Interval of 0 is really 1
            int Interval = (r.IntervalIsSet ? Math.Max (1, r.Interval) : 1);

            while (true) {
                if (r.OccurencesIsSet && (occurrance > r.Occurences)) {
                    return DateTime.MaxValue;
                }
                if ((DateTime.MinValue != r.Until) && (eventStart > r.Until)) {
                    return DateTime.MaxValue;
                }
                if (eventStart > previousEntryInDatabase) {
                    var actualEventStart = ReplaceDayOfMonth (eventStart, r.WeekOfMonth);
                    var actualEventEnd = actualEventStart + (eventEnd - eventStart);
                    CreateEventRecord (c, actualEventStart, actualEventEnd);
                }
                occurrance += 1;
                if (eventStart >= generateAtLeastUntil) {
                    return eventStart;
                }
                NcAssert.True (0 < Interval);
                eventStart = eventStart.AddMonths (Interval);
                eventEnd = eventEnd.AddMonths (Interval);
            }
        }

        protected static DateTime ReplaceDayOfMonthWithNthWeekday (DateTime dt, int nthWeekday)
        {
            var t = new DateTime (dt.Year, dt.Month, 1);

            int newDayOfMonth = 0;

            while (nthWeekday > 0) {
                newDayOfMonth += 1;
                if (!((DayOfWeek.Saturday == t.DayOfWeek) || (DayOfWeek.Sunday == t.DayOfWeek))) {
                    nthWeekday -= 1;
                }
            }

            return new DateTime (
                dt.Year,
                dt.Month,
                newDayOfMonth,
                dt.Hour,
                dt.Minute,
                dt.Second,
                dt.Millisecond,
                dt.Kind);
        }

        protected static DateTime ReplaceDayOfMonthWithNthWeekendDay (DateTime dt, int nthWeekendDay)
        {
            var t = new DateTime (dt.Year, dt.Month, 1);

            int newDayOfMonth = 0;

            while (nthWeekendDay > 0) {
                newDayOfMonth += 1;
                if (!((DayOfWeek.Saturday == t.DayOfWeek) || (DayOfWeek.Sunday == t.DayOfWeek))) {
                    nthWeekendDay -= 1;
                }
            }

            return new DateTime (
                dt.Year,
                dt.Month,
                newDayOfMonth,
                dt.Hour,
                dt.Minute,
                dt.Second,
                dt.Millisecond,
                dt.Kind);
        }

        // Type element set to 3.  Subcase:
        // If the DayOfWeek element is set to 62, to specify weekdays,
        // the WeekOfMonth element indicates the nth weekday of the month,
        // where n is the value of WeekOfMonth element.
        protected static DateTime GenerateEveryNthWeekdayOfMonth (McCalendar c, McRecurrence r, DateTime previousEntryInDatabase, DateTime generateAtLeastUntil)
        {
            int occurrance = 0;

            DateTime eventStart = c.StartTime;
            DateTime eventEnd = c.EndTime;

            NcAssert.True (62 == (int)r.DayOfWeek);

            // Interval of 0 is really 1
            int Interval = (r.IntervalIsSet ? Math.Max (1, r.Interval) : 1);

            while (true) {
                if (r.OccurencesIsSet && (occurrance > r.Occurences)) {
                    return DateTime.MaxValue;
                }
                if ((DateTime.MinValue != r.Until) && (eventStart > r.Until)) {
                    return DateTime.MaxValue;
                }
                if (eventStart > previousEntryInDatabase) {
                    var actualEventStart = ReplaceDayOfMonthWithNthWeekday (eventStart, r.WeekOfMonth);
                    var actualEventEnd = actualEventStart + (eventEnd - eventStart);
                    CreateEventRecord (c, actualEventStart, actualEventEnd);
                }
                occurrance += 1;
                if (eventStart >= generateAtLeastUntil) {
                    return eventStart;
                }
                NcAssert.True (0 < Interval);
                eventStart = eventStart.AddMonths (Interval);
                eventEnd = eventEnd.AddMonths (Interval);
            }
        }


        // Type element set to 3.  Subcase:
        // If the DayOfWeek element is set to 65, to specify weekends,
        // the WeekOfMonth element indicates the nth weekend day of the month,
        // where n is the value of WeekOfMonth element.
        protected static DateTime GenerateEveryNthWeekendDayOfMonth (McCalendar c, McRecurrence r, DateTime previousEntryInDatabase, DateTime generateAtLeastUntil)
        {
            int occurrance = 0;

            DateTime eventStart = c.StartTime;
            DateTime eventEnd = c.EndTime;

            NcAssert.True (65 == (int)r.DayOfWeek);

            // Interval of 0 is really 1
            int Interval = (r.IntervalIsSet ? Math.Max (1, r.Interval) : 1);

            while (true) {
                if (r.OccurencesIsSet && (occurrance > r.Occurences)) {
                    return DateTime.MaxValue;
                }
                if ((DateTime.MinValue != r.Until) && (eventStart > r.Until)) {
                    return DateTime.MaxValue;
                }
                if (eventStart > previousEntryInDatabase) {
                    var actualEventStart = ReplaceDayOfMonthWithNthWeekendDay (eventStart, r.WeekOfMonth);
                    var actualEventEnd = actualEventStart + (eventEnd - eventStart);
                    CreateEventRecord (c, actualEventStart, actualEventEnd);
                }
                occurrance += 1;
                if (eventStart >= generateAtLeastUntil) {
                    return eventStart;
                }
                NcAssert.True (0 < Interval);
                eventStart = eventStart.AddMonths (Interval);
                eventEnd = eventEnd.AddMonths (Interval);
            }
        }

        protected static bool IsDayInDayOfWeek (System.DayOfWeek systemDOW, NcDayOfWeek ncDOW)
        {
            int shift = (int)systemDOW;
            NcAssert.True ((0 <= shift) && (6 >= shift));
            return (0 != ((1 << shift) & ((int)ncDOW)));
        }

        protected static DateTime ReplaceDayOfMonthWithNthDay (DateTime dt, NcDayOfWeek DayOfWeek, int WeekOfMonth)
        {
            var t = new DateTime (dt.Year, dt.Month, 1);

            // System.DayOfWeek 0..6
            // DayOfWeek 1,2,4,8,...
            while (!IsDayInDayOfWeek (t.DayOfWeek, DayOfWeek)) {
                t = t.AddDays (1);
            }

            NcAssert.True ((0 < WeekOfMonth) && (5 >= WeekOfMonth));
            t = t.AddDays (7 * (WeekOfMonth - 1));
            while (t.Month != dt.Month) {
                t = t.AddDays (-7);
            }

            return new DateTime (
                dt.Year,
                dt.Month,
                t.Day,
                dt.Hour,
                dt.Minute,
                dt.Second,
                dt.Millisecond,
                dt.Kind);
        }

        // Type element set to 3.  Subcase:
        // Or, by default, the Nth DayOfWeek, where nth is WeekOfMonth,
        // and WeekOfMonth is 1..5, where 5 is the last week of the month.
        // Like every 3rd Tuesday or the last Thursday of the month.
        protected static DateTime GenerateEveryNthDayOfMonth (McCalendar c, McRecurrence r, DateTime previousEntryInDatabase, DateTime generateAtLeastUntil)
        {
            int occurrance = 0;

            DateTime eventStart = c.StartTime;
            DateTime eventEnd = c.EndTime;

            // Interval of 0 is really 1
            int Interval = (r.IntervalIsSet ? Math.Max (1, r.Interval) : 1);

            while (true) {
                if (r.OccurencesIsSet && (occurrance > r.Occurences)) {
                    return DateTime.MaxValue;
                }
                if ((DateTime.MinValue != r.Until) && (eventStart > r.Until)) {
                    return DateTime.MaxValue;
                }
                if (eventStart > previousEntryInDatabase) {
                    var actualEventStart = ReplaceDayOfMonthWithNthDay (eventStart, r.DayOfWeek, r.WeekOfMonth);
                    var actualEventEnd = actualEventStart + (eventEnd - eventStart);
                    CreateEventRecord (c, actualEventStart, actualEventEnd);
                }
                occurrance += 1;
                if (eventStart >= generateAtLeastUntil) {
                    return eventStart;
                }
                NcAssert.True (0 < Interval);
                eventStart = eventStart.AddMonths (Interval);
                eventEnd = eventEnd.AddMonths (Interval);
            }
        }


        protected static DateTime ExpandRecurrences (McCalendar c, McRecurrence r, DateTime startingTime, DateTime endingTime)
        {
            if (NcRecurrenceType.Daily == r.Type) {
                if (0 == r.DayOfWeek) {
                    // Every 'interval' days
                    return GenerateEveryIntervalDays (c, r, startingTime, endingTime);
                } else {
                    // Every 'interval' weeks on R.DayOfWeek'
                    return GenerateEveryIntervalWeeks (c, r, startingTime, endingTime);
                }
            }
            if (NcRecurrenceType.Weekly == r.Type) {
                NcAssert.True (0 != r.DayOfWeek);
                // Every 'interval' weeks on R.DayOfWeek
                return GenerateEveryIntervalWeeks (c, r, startingTime, endingTime);
            }
            if (NcRecurrenceType.Monthly == r.Type) {
                NcAssert.True (0 != r.DayOfMonth);
                // Every 'interval' months on r.DayOfMonth
                return GenerateEveryIntervalMonths (c, r, startingTime, endingTime);
            }
            if (NcRecurrenceType.Yearly == r.Type) {
                // Every 'interval' years on the MonthOfYear & DayOfMonth
                return GenerateEveryIntervalYears (c, r, startingTime, endingTime);
            }

            if (NcRecurrenceType.MonthlyOnDay == r.Type) {
                // If the DayOfWeek element is set to 127, the WeekOfMonth
                // element indicates the day of the month that the event occurs.
                if (127 == (int)r.DayOfWeek) {
                    return GenerateEveryNthDateOfMonth (c, r, startingTime, endingTime);
                }
                // If the DayOfWeek element is set to 62, to specify weekdays,
                // the WeekOfMonth element indicates the nth weekday of the month,
                // where n is the value of WeekOfMonth element.
                if (62 == (int)r.DayOfWeek) {
                    return GenerateEveryNthWeekdayOfMonth (c, r, startingTime, endingTime);
                }
                // If the DayOfWeek element is set to 65, to specify weekends,
                // the WeekOfMonth element indicates the nth weekend day of the month,
                // where n is the value of WeekOfMonth element.
                if (65 == (int)r.DayOfWeek) {
                    return GenerateEveryNthWeekendDayOfMonth (c, r, startingTime, endingTime);
                }
                // Or, by default, the Nth DayOfWeek, where nth is WeekOfMonth,
                // and WeekOfMonth is 1..5, where 5 is the last week of the month.
                // Like every 3rd Tuesday or the last Thursday of the month.
                return GenerateEveryNthDayOfMonth (c, r, startingTime, endingTime);
            }

            Log.Error (Log.LOG_CALENDAR, "Unhandled recurrence type {0} c.Id={1} r.Id={2}", r.Type, c.Id, r.Id);
            return DateTime.MinValue;


//            if (NcRecurrenceType.Yearly == r.Type) {
//                // Every 'interval' years on the MonthOfYear & DayOfMonth.
//                return;
//            }
//            if (NcRecurrenceType.YearlyOnDay == r.Type) {
//                // Every 'interval' years
//                // In month MonthOfYear
//                if (127 == (int)r.DayOfWeek) {
//                    // on day WeekOfMonth
//                    return;
//                }
//                if (62 == (int)r.DayOfWeek) {
//                    // on the r.WeeksOfMonth day
//                    return;
//                }
//                if (65 == (int)r.DayOfWeek) {
//                    // on the r.WeekOfMonth weekend day of the month.
//                }
//                return;
//            }
        }

        protected static void ExpandAllDayEvent (McCalendar c)
        {
            // Create local events for an all-day calendar item.  Figure out the starting
            // day of the event in the organizer's time zone.  Create a local event that
            // starts at midnight local time on the same day.  If the original item is
            // more than one day long, repeat for subsequent days.

            TimeZoneInfo timeZone = new AsTimeZone (c.TimeZone).ConvertToSystemTimeZone ();
            DateTime organizersTime = TimeZoneInfo.ConvertTimeFromUtc (c.StartTime, timeZone);
            DateTime localStartTime = new DateTime (organizersTime.Year, organizersTime.Month, organizersTime.Day, 0, 0, 0, DateTimeKind.Local);
            double days = (c.EndTime - c.StartTime).TotalDays;
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
                        ExpandAllDayEvent (calendarItem);
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
            if (DateTime.UtcNow < notificationTime) {
                notifier.ScheduleNotif (e.Id, e.StartTime.AddMinutes (-reminder), Pretty.FormatAlert (reminder));
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

