//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using DDay.iCal;
using DDay.iCal.Serialization.iCalendar;
using MimeKit;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.Utils
{
    public class CalendarHelper
    {
        public CalendarHelper ()
        {
        }

        public static void ExtrapolateTimes (ref DDay.iCal.Event evt)
        {
//            if (evt.End == null && evt.Start != null && evt.Duration != default(TimeSpan)) {
//                evt.End = evt.Start.Add (evt.Duration);
//            } else if (evt.Duration == default(TimeSpan) && evt.Start != null && evt.End != null) {
//                evt.Duration = evt.DTEnd.Subtract (evt.Start);
//            } else if (evt.Start == null && evt.Duration != default(TimeSpan) && evt.End != null) {
//                evt.Start = evt.End.Subtract (evt.Duration);
//            }
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

        public static IICalendar iCalendarFromMcCalendar (McAccount account, McCalendar c, string tzid)
        {
            var iCal = new iCalendar ();
            iCal.ProductID = "Nacho Mail";

            var tzi = TimeZoneInfo.FindSystemTimeZoneById (tzid);
            var timezone = FromSystemTimeZone (tzi, c.StartTime.AddYears (-1), false);
            var localTimeZone = iCal.AddTimeZone (timezone);

            if (null != tzi.StandardName) {
                timezone.TZID = tzi.StandardName;
                localTimeZone.TZID = tzi.StandardName;
            }

            var evt = iCal.Create<DDay.iCal.Event> ();
            evt.UID = c.UID;
            evt.Summary = c.Subject;
            evt.LastModified = new iCalDateTime (DateTime.UtcNow);
            evt.Start = new iCalDateTime (c.StartTime.ToLocalTime (), localTimeZone.TZID);
            evt.End = new iCalDateTime (c.EndTime.ToLocalTime (), localTimeZone.TZID);
            NachoCore.Utils.CalendarHelper.ExtrapolateTimes (ref evt);
            evt.IsAllDay = c.AllDayEvent;
            evt.Priority = 5;
            if (c.AllDayEvent) {
                evt.Properties.Set ("X-MICROSOFT-CDO-ALLDAYEVENT", "TRUE");
                evt.Properties.Set ("X-MICROSOFT-CDO-INTENDEDSTATUS", "FREE");
            } else {
                evt.Properties.Set ("X-MICROSOFT-CDO-ALLDAYEVENT", "FALSE");
                evt.Properties.Set ("X-MICROSOFT-CDO-INTENDEDSTATUS", "BUSY");
            }
            evt.Location = c.Location;
            var emailAddress = account.EmailAddr;
            evt.Organizer = new Organizer (emailAddress);
            evt.Organizer.CommonName = "";
            evt.Organizer.SentBy = new Uri ("MAILTO:" + emailAddress);
            evt.Status = EventStatus.Confirmed;
            evt.Class = "PUBLIC";
            evt.Transparency = TransparencyType.Opaque;
            foreach (var a in c.attendees) {
                var iAttendee = new Attendee ("MAILTO:" + a.Email);
                NcAssert.True (null != a.Name);
                iAttendee.CommonName = a.Name;
                NcAssert.True (a.AttendeeTypeIsSet);
                switch (a.AttendeeType) {
                case NcAttendeeType.Required:
                    iAttendee.RSVP = c.ResponseRequestedIsSet && c.ResponseRequested;
                    iAttendee.Role = "REQ-PARTICIPANT";
                    iAttendee.ParticipationStatus = "NEEDS-ACTION";
                    iAttendee.Type = "INDIVIDUAL";
                    break;
                case NcAttendeeType.Optional:
                    iAttendee.RSVP = c.ResponseRequestedIsSet && c.ResponseRequested;
                    iAttendee.Role = "OPT-PARTICIPANT";
                    iAttendee.ParticipationStatus = "NEEDS-ACTION";
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

            IDateTime earliest = new iCalDateTime (earlistDateTimeToSupport);
            foreach (var adjustmentRule in adjustmentRules) {
                // Only include historical data if asked to do so.  Otherwise,
                // use only the most recent adjustment rule available.
                if (!includeHistoricalData && adjustmentRule.DateEnd < earlistDateTimeToSupport)
                    continue;

                var delta = adjustmentRule.DaylightDelta;
                var dday_tzinfo_standard = new DDay.iCal.iCalTimeZoneInfo ();
                dday_tzinfo_standard.Name = "STANDARD";
                dday_tzinfo_standard.TimeZoneName = tzinfo.StandardName;
                dday_tzinfo_standard.Start = new iCalDateTime (new DateTime (adjustmentRule.DateStart.Year, adjustmentRule.DaylightTransitionEnd.Month, adjustmentRule.DaylightTransitionEnd.Day, adjustmentRule.DaylightTransitionEnd.TimeOfDay.Hour, adjustmentRule.DaylightTransitionEnd.TimeOfDay.Minute, adjustmentRule.DaylightTransitionEnd.TimeOfDay.Second).AddDays (1));
                if (dday_tzinfo_standard.Start.LessThan (earliest))
                    dday_tzinfo_standard.Start = dday_tzinfo_standard.Start.AddYears (earliest.Year - dday_tzinfo_standard.Start.Year);
                dday_tzinfo_standard.OffsetFrom = new UTCOffset (utcOffset + delta);
                dday_tzinfo_standard.OffsetTo = new UTCOffset (utcOffset);
                PopulateiCalTimeZoneInfo (dday_tzinfo_standard, adjustmentRule.DaylightTransitionEnd, adjustmentRule.DateStart.Year);

                // Add the "standard" time rule to the time zone
                dday_tz.AddChild (dday_tzinfo_standard);

                if (tzinfo.SupportsDaylightSavingTime) {
                    var dday_tzinfo_daylight = new DDay.iCal.iCalTimeZoneInfo ();
                    dday_tzinfo_daylight.Name = "DAYLIGHT";
                    dday_tzinfo_daylight.TimeZoneName = tzinfo.DaylightName;
                    dday_tzinfo_daylight.Start = new iCalDateTime (new DateTime (adjustmentRule.DateStart.Year, adjustmentRule.DaylightTransitionStart.Month, adjustmentRule.DaylightTransitionStart.Day, adjustmentRule.DaylightTransitionStart.TimeOfDay.Hour, adjustmentRule.DaylightTransitionStart.TimeOfDay.Minute, adjustmentRule.DaylightTransitionStart.TimeOfDay.Second));
                    if (dday_tzinfo_daylight.Start.LessThan (earliest))
                        dday_tzinfo_daylight.Start = dday_tzinfo_daylight.Start.AddYears (earliest.Year - dday_tzinfo_daylight.Start.Year);
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

        public static void SendInvites (McAccount account, McCalendar c, string tzid)
        {

            IICalendar iCal = CalendarHelper.iCalendarFromMcCalendar (account, c, tzid);

            var mimeMessage = new MimeMessage ();

            mimeMessage.From.Add (new MailboxAddress (Pretty.DisplayNameForAccount (account), account.EmailAddr));

            foreach (var a in c.attendees) {
                mimeMessage.To.Add (new MailboxAddress (a.Name, a.Email));
            }
            if (null != c.Subject) {
                mimeMessage.Subject = c.Subject;
            }
            mimeMessage.Date = System.DateTime.UtcNow;

            var body = new TextPart ("calendar");
            body.ContentType.Parameters.Add ("METHOD", "REQUEST");
            iCal.Method = "REQUEST";
            using (var iCalStream = new MemoryStream ()) {
                iCalendarSerializer serializer = new iCalendarSerializer ();
                serializer.Serialize (iCal, iCalStream, System.Text.Encoding.ASCII);
                iCalStream.Seek (0, SeekOrigin.Begin);
                using (var textStream = new StreamReader (iCalStream)) {
                    body.Text = textStream.ReadToEnd ();
                }
            }
            body.ContentTransferEncoding = ContentEncoding.Base64;

            var textPart = new TextPart ("plain") {
                Text = ""
            };

            var alternative = new Multipart ("alternative");
            alternative.Add (textPart);
            alternative.Add (body);

            mimeMessage.Body = alternative;

            MimeHelpers.SendEmail (account.Id, mimeMessage, c.Id);
        }

        public static void SendInvite (McAccount account, McCalendar c, McAttendee attendee, string tzid)
        {

            IICalendar iCal = CalendarHelper.iCalendarFromMcCalendar (account, c, tzid);

            var mimeMessage = new MimeMessage ();

            mimeMessage.From.Add (new MailboxAddress (Pretty.DisplayNameForAccount (account), account.EmailAddr));

            mimeMessage.To.Add (new MailboxAddress (attendee.Name, attendee.Email));

            if (null != c.Subject) {
                mimeMessage.Subject = c.Subject;
            }
            mimeMessage.Date = System.DateTime.UtcNow;

            var body = new TextPart ("calendar");
            body.ContentType.Parameters.Add ("METHOD", "REQUEST");
            iCal.Method = "REQUEST";
            using (var iCalStream = new MemoryStream ()) {
                iCalendarSerializer serializer = new iCalendarSerializer ();
                serializer.Serialize (iCal, iCalStream, System.Text.Encoding.ASCII);
                iCalStream.Seek (0, SeekOrigin.Begin);
                using (var textStream = new StreamReader (iCalStream)) {
                    body.Text = textStream.ReadToEnd ();
                }
            }
            body.ContentTransferEncoding = ContentEncoding.Base64;

            var textPart = new TextPart ("plain") {
                Text = ""
            };

            var alternative = new Multipart ("alternative");
            alternative.Add (textPart);
            alternative.Add (body);

            mimeMessage.Body = alternative;

            MimeHelpers.SendEmail (account.Id, mimeMessage, c.Id);
        }

        protected static McAttendee CreateAttendee (InternetAddress address, NcAttendeeType attendeeType)
        {
            var mailboxAddress = address as MailboxAddress;

            if (null == mailboxAddress) {
                return null;
            }

            var attendee = new McAttendee ();
            attendee.Name = mailboxAddress.Name;
            attendee.Email = mailboxAddress.Address;
            attendee.AttendeeType = attendeeType;
            attendee.AttendeeTypeIsSet = true;
            return attendee;
        }

        protected static List<McAttendee> CreateAttendeeList (string addressLine, NcAttendeeType attendeeType)
        {
            var addressList = NcEmailAddress.ParseAddressListString (addressLine);
            var attendeeList = new List<McAttendee> ();
            foreach (var address in addressList) {
                var addendee = CreateAttendee (address, attendeeType);
                if (null != addendee) {
                    attendeeList.Add (addendee);
                }
            }
            return attendeeList;
        }

        public static McCalendar CreateMeeting (McEmailMessage message)
        {
            var c = DefaultMeeting ();
            c.Subject = message.Subject;
            c.BodyId = McBody.Duplicate (message.BodyId);
            c.attendees = new System.Collections.Generic.List<McAttendee> ();
            c.attendees.AddRange (CreateAttendeeList (message.From, NcAttendeeType.Required));
            c.attendees.AddRange (CreateAttendeeList (message.To, NcAttendeeType.Required));
            c.attendees.AddRange (CreateAttendeeList (message.Cc, NcAttendeeType.Optional));
            return c;
        }

        public static McTask CreateTask (McEmailMessage message)
        {
            return DefaultTask ();
        }
    }
}

