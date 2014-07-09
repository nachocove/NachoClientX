//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using System.Xml;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public static class AsExtensions
    {
        public static T ToEnum<T> (this string enumString)
        {
            return (T)Enum.Parse (typeof(T), enumString);
        }

        public static T ParseInteger<T> (this string intString)
        {
            int i = int.Parse (intString);
            return (T)Enum.ToObject (typeof(T), i);
        }

        public static Boolean ToBoolean (this string intString)
        {
            int i = int.Parse (intString);
            return System.Convert.ToBoolean (i);
        }

        public static int ToInt (this string intString)
        {
            return int.Parse (intString);
        }

        public static uint ToUint (this string intString)
        {
            return uint.Parse (intString);
        }
    }

    public class AsHelpers
    {
        public const string CompactDateTimeFmt1 = "yyyyMMddTHHmmssZ";
        public const string CompactDateTimeFmt2 = "yyyyMMddTHHmmssfffZ";
        public const string DateTimeFmt1 = "yyyy-MM-ddTHH:mm:ss.fffZ";
        protected XNamespace m_baseNs = Xml.AirSyncBase.Ns;

        private static string XmlFromBool (bool value)
        {
            return (value) ? "1" : "0";
        }

        public static XElement ToXmlApplicationData (McCalendar cal)
        {
            XNamespace AirSyncNs = Xml.AirSync.Ns;
            XNamespace CalendarNs = Xml.Calendar.Ns;
            XNamespace AirSyncBaseNs = Xml.AirSyncBase.Ns;

            var xmlAppData = new XElement (AirSyncNs + Xml.AirSync.ApplicationData);
            if (cal.AllDayEvent) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.AllDayEvent, "1"));
            }
            if (DateTime.MinValue != cal.DtStamp) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.DtStamp,
                    cal.DtStamp.ToString (CompactDateTimeFmt1)));
            }
            if (DateTime.MinValue != cal.StartTime) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.StartTime,
                    cal.StartTime.ToString (CompactDateTimeFmt1)));
            }
            if (DateTime.MinValue != cal.EndTime) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.EndTime,
                    cal.EndTime.ToString (CompactDateTimeFmt1)));
            }
            if (0 != cal.Reminder) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.Reminder, cal.Reminder));
            }

            // TODO TimeZoneId - TimeZone table not implemented yet.
            xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.Timezone, "4AEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAsAAAABAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMAAAACAAMAAAAAAAAAxP///w=="));//FIXME.

            if (null != cal.Subject) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.Subject, cal.Subject));
            }
            if (!string.IsNullOrEmpty (cal.Location)) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.Location, cal.Location));
            }
            if (cal.SensitivityIsSet) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.Sensitivity, (uint)cal.Sensitivity));
            }
            if (cal.BusyStatusIsSet) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.BusyStatus, (uint)cal.BusyStatus));
            }
            if (cal.ResponseTypeIsSet) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.ResponseType, (uint)cal.ResponseType));
            }
            if (cal.MeetingStatusIsSet) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.MeetingStatus, (uint)cal.MeetingStatus));
            }
            if (DateTime.MinValue != cal.AppointmentReplyTime) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.AppointmentReplyTime,
                    cal.AppointmentReplyTime.ToString (DateTimeFmt1)));
            }
            if (null != cal.OnlineMeetingConfLink) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.OnlineMeetingConfLink, cal.OnlineMeetingConfLink));
            }
            if (null != cal.OnlineMeetingExternalLink) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.OnlineMeetingExternalLink, cal.OnlineMeetingExternalLink));
            }

            if (0 != cal.BodyId) {
                var body = McBody.QueryById<McBody> (cal.BodyId);
                NcAssert.True (null != body);
                xmlAppData.Add (new XElement (AirSyncBaseNs + Xml.AirSyncBase.Body,
                    new XElement (AirSyncBaseNs + Xml.AirSyncBase.Type, (uint)Xml.AirSync.TypeCode.Mime_4),
                    new XElement (AirSyncBaseNs + Xml.AirSyncBase.Data, body.Body)));
            }

            if (0 != cal.attendees.Count) {
                var xmlAttendees = new XElement (CalendarNs + Xml.Calendar.Calendar_Attendees);
                foreach (var attendee in cal.attendees) {
                    var xmlAttendee = new XElement (CalendarNs + Xml.Calendar.Attendees.Attendee);
                    xmlAttendee.Add (new XElement (CalendarNs + Xml.Calendar.Email, attendee.Email));
                    xmlAttendee.Add (new XElement (CalendarNs + Xml.Calendar.Name, attendee.Name));
                    if (attendee.AttendeeTypeIsSet) {
                        xmlAttendee.Add (new XElement (CalendarNs + Xml.Calendar.AttendeeType, (uint)attendee.AttendeeType));
                    }
                    if (attendee.AttendeeStatusIsSet) {
                        xmlAttendee.Add (new XElement (CalendarNs + Xml.Calendar.AttendeeStatus, (uint)attendee.AttendeeStatus));
                    }
                    xmlAttendees.Add (xmlAttendee);
                }
                xmlAppData.Add (xmlAttendees);
            }
            if (0 != cal.categories.Count) {
                var xmlCategories = new XElement (CalendarNs + Xml.Calendar.Calendar_Categories);
                foreach (var category in cal.categories) {
                    xmlCategories.Add (new XElement (CalendarNs + Xml.Calendar.Category, category.Name));
                }
            }
            // TODO: exceptions.
            // TODO recurrences.

            if (cal.ResponseRequestedIsSet) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.ResponseRequested, XmlFromBool (cal.ResponseRequested)));
            }
            if (cal.DisallowNewTimeProposalIsSet) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.DisallowNewTimeProposal, XmlFromBool (cal.DisallowNewTimeProposal)));
            }
            if (null != cal.OrganizerName) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.OrganizerName, cal.OrganizerName));
            }
            if (null != cal.OrganizerEmail) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.OrganizerEmail, cal.OrganizerEmail));
            }
            if (null != cal.UID) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.UID, cal.UID));
            }
            if (0 != cal.NativeBodyType) {
                xmlAppData.Add (new XElement (AirSyncBaseNs + Xml.AirSyncBase.NativeBodyType, cal.NativeBodyType));
            }
            return xmlAppData;
        }
        // ParseAsCompactDataType
        // A Compact DateTime value is a representation of a UTC date and time.
        // The format of a Compact DateTime value is specified by the following
        // Augmented Backus-Naur Form (ABNF) notation.
        // ￼
        // date_string = year month day "T" hour minute seconds [milliseconds] "Z"
        // year = 4*DIGIT
        // month = ("0" DIGIT) / "10" / "11" / "12"
        // day = ("0" DIGIT) / ("1" DIGIT) / ("2" DIGIT ) / "30" / "31"
        // hour = ("0" DIGIT) / ("1" DIGIT) / "20" / "21" / "22" / "23"
        // minute = ("0" DIGIT) / ("1" DIGIT) / ("2" DIGIT) / ("3" DIGIT) / ("4" DIGIT) / ("5" DIGIT)
        // seconds = ("0" DIGIT) / ("1" DIGIT) / ("2" DIGIT) / ("3" DIGIT) / ("4" DIGIT) / ("5" DIGIT)
        // milliseconds  = 1*3DIGIT
        //
        // E.g. 20131123T190243Z
        //
        public System.DateTime ParseAsCompactDateTime (string compactDateTime)
        {
            DateTime dateValue;
            DateTimeStyles styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;
            if (DateTime.TryParseExact (compactDateTime, CompactDateTimeFmt1, CultureInfo.InvariantCulture, styles, out dateValue)) {
                return dateValue;
            }
            if (DateTime.TryParseExact (compactDateTime, CompactDateTimeFmt2, CultureInfo.InvariantCulture, styles, out dateValue)) {
                return dateValue;
            }
            return DateTime.MinValue;
        }

        /// <summary>
        /// Parses as date time.
        /// <A:DateReceived>2009-11-12T00:45:06.000Z</A:DateReceived>
        /// </summary>
        /// <returns>The as date time.</returns>
        /// <param name="dateTime">Date time.</param>
        public static System.DateTime ParseAsDateTime (string dateTime)
        {
            DateTime dateValue;
            DateTimeStyles styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;
            if (DateTime.TryParseExact (dateTime, DateTimeFmt1, CultureInfo.InvariantCulture, styles, out dateValue)) {
                return dateValue;
            }
            return DateTime.MinValue;
        }

        /// <summary>
        /// Parses a time zone string.
        /// </summary>
        /// <returns>The time zone record.</returns>
        /// <param name="encodedTimeZone">Encoded time zone.</param>
        // TODO: The bias fields of the timezone
        public McTimeZone ParseAsTimeZone (string encodedTimeZone)
        {
            // Convert the Base64 UUEncoded input into binary output. 
            byte[] binaryData;
            try {
                binaryData = System.Convert.FromBase64String (encodedTimeZone);
            } catch (System.ArgumentNullException) {
                Log.Warn (Log.LOG_AS, "Encoded TimeZone string is null.");
                return null;
            } catch (System.FormatException) {
                Log.Warn (Log.LOG_AS, "Encoded TimeZone string length is not 4 or is not an even multiple of 4.");
                return null;
            }
            if (binaryData.Length != (4 + 64 + 16 + 4 + 64 + 16 + 4)) {
                Log.Warn (Log.LOG_AS, "Decoded TimeZone string length is wrong: " + binaryData.Length.ToString ());
                return null;
            }
            string StandardName = ExtractStringFromAsTimeZone (binaryData, 4, 64);
            string DaylightName = ExtractStringFromAsTimeZone (binaryData, 4 + 64 + 16 + 4, 64);
            McTimeZone tz = new McTimeZone ();
            tz.StandardName = StandardName;
            tz.DaylightName = DaylightName;
            return tz;     
        }

        /// <summary>
        /// Extracts a string field from a TimeZone record.
        /// The value of this field is an array of 32 WCHARs
        /// Any unused WCHARs in the array MUST be set to 0x0000.
        /// </summary>
        /// <returns>The string from the time zone.</returns>
        /// <param name="binaryData">The packaged string</param>
        /// <param name="start">Starting offset of the first character</param>
        /// <param name="fieldLength">Length of the field.</param>
        public string ExtractStringFromAsTimeZone (byte[] binaryData, int start, int fieldLength)
        {
            NcAssert.True ((start + fieldLength) <= binaryData.Length);
            String field = System.Text.UnicodeEncoding.Unicode.GetString (binaryData, start, fieldLength);
            int index = field.IndexOf (System.Convert.ToChar (0)); // trim trailing null padding
            if (index < 0) { // no nulls
                return field;
            } else {
                return field.Substring (0, index);
            }
        }

        /// <returns>
        /// A list of attendees not yet associated with an NcCalendar or NcException. Not null.
        /// </returns>
        // TODO: Handle missing name & email better
        // TODO: Make sure we don't have extra fields
        public List<McAttendee> ParseAttendees (XNamespace ns, XElement attendees)
        {
            NcAssert.True (null != attendees);
            NcAssert.True (attendees.Name.LocalName.Equals (Xml.Calendar.Calendar_Attendees));

            var list = new List<McAttendee> ();

            foreach (var attendee in attendees.Elements()) {
                NcAssert.True (attendee.Name.LocalName.Equals (Xml.Calendar.Attendees.Attendee));

                // Required
                var nameElement = attendee.Element (ns + Xml.Calendar.Attendee.Name);
                string name = nameElement.Value;

                // Required
                var emailElement = attendee.Element (ns + Xml.Calendar.Attendee.Email);
                string email = emailElement.Value;

                // Optional
                NcAttendeeStatus status = NcAttendeeStatus.NotResponded;
                var statusElement = attendee.Element (ns + Xml.Calendar.Attendee.AttendeeStatus);
                if (null != statusElement) {
                    status = statusElement.Value.ToEnum<NcAttendeeStatus> ();
                }

                // Optional
                NcAttendeeType type = NcAttendeeType.Unknown;
                var typeElement = attendee.Element (ns + Xml.Calendar.Attendee.AttendeeType);
                if (null != typeElement) {
                    type = typeElement.Value.ToEnum<NcAttendeeType> ();
                }

                var a = new McAttendee (name, email, type, status);
                list.Add (a);
            }
            return list;
        }

        /// <returns>
        /// A list of categories not yet associated with an NcCalendar or NcException. Not null.
        /// </returns>
        // TODO: Handle missing name & email better
        // TODO: Make sure we don't have extra fields
        public List<McCalendarCategory> ParseCategories (XNamespace ns, XElement categories)
        {
            NcAssert.True (null != categories);
            NcAssert.True (categories.Name.LocalName.Equals (Xml.Calendar.Calendar_Categories));

            var list = new List<McCalendarCategory> ();

            foreach (var category in categories.Elements()) {
                NcAssert.True (category.Name.LocalName.Equals (Xml.Calendar.Categories.Category));
                var n = new McCalendarCategory (category.Value);
                list.Add (n);
            }
            return list;
        }

        public static List<McEmailMessageCategory> ParseEmailCategories (XNamespace ns, XElement categories)
        {
            var list = new List<McEmailMessageCategory> ();
            if (categories.Elements ().Count () != 0) {

                NcAssert.True (null != categories);
                NcAssert.True (categories.Name.LocalName.Equals (Xml.Email.Categories));

                foreach (var category in categories.Elements()) {
                    NcAssert.True (category.Name.LocalName.Equals (Xml.Email.Category));
                    var n = new McEmailMessageCategory (category.Value);
                    list.Add (n);
                }
            }
            return list;
        }

        /// <summary>
        /// Parses the recurrence section of a calendar item, or the recurrence section of a task item.
        /// </summary>
        /// <returns>The recurrence record</returns>
        /// <param name="ns">XML namespace to use to fetch elements</param>
        /// <param name="recurrence">Recurrence element</param>
        public McRecurrence ParseRecurrence (XNamespace ns, XElement recurrence)
        {
            NcAssert.True (null != recurrence);
            NcAssert.True (recurrence.Name.LocalName.Equals (Xml.Calendar.Calendar_Recurrence));

            var r = new McRecurrence ();

            foreach (var child in recurrence.Elements()) {
                switch (child.Name.LocalName) {
                // Note: LocalNames and values are the same in the Calendar and Task namespaces.
                case Xml.Calendar.Recurrence.CalendarType:
                    r.CalendarType = child.Value.ToEnum<NcCalendarType> ();
                    break;
                case Xml.Calendar.Recurrence.DayOfMonth:
                    r.DayOfMonth = child.Value.ToInt ();
                    break;
                case Xml.Calendar.Recurrence.DayOfWeek:
                    r.DayOfWeek = child.Value.ToEnum<NcDayOfWeek> ();
                    break;
                case Xml.Calendar.Recurrence.FirstDayOfWeek:
                    r.FirstDayOfWeek = child.Value.ToInt ();
                    break;
                case Xml.Calendar.Recurrence.Interval:
                    r.Interval = child.Value.ToInt ();
                    break;
                case Xml.Calendar.Recurrence.IsLeapMonth:
                    r.isLeapMonth = child.Value.ToBoolean ();
                    break;
                case Xml.Calendar.Recurrence.MonthOfYear:
                    r.MonthOfYear = child.Value.ToInt ();
                    break;
                case Xml.Calendar.Recurrence.Occurrences:
                    r.Occurences = int.Parse (child.Value);
                    break;
                case Xml.Calendar.Recurrence.Type:
                    r.Type = child.Value.ParseInteger<NcRecurrenceType> ();
                    break;
                case Xml.Calendar.Recurrence.Until:
                    r.Until = ParseAsCompactDateTime (child.Value);
                    break;
                case Xml.Calendar.Recurrence.WeekOfMonth:
                    r.WeekOfMonth = child.Value.ToInt ();
                    break;
                default:
                    Log.Warn (Log.LOG_AS, "ParseRecurrence UNHANDLED: " + child.Name.LocalName + " value=" + child.Value);
                    break;
                }
            }
            return r;
        }

        public List<McException> ParseExceptions (XNamespace ns, XElement exceptions)
        {
            NcAssert.True (null != exceptions);
            NcAssert.True (exceptions.Name.LocalName.Equals (Xml.Calendar.Calendar_Exceptions));

            var l = new List<McException> ();

            Log.Info (Log.LOG_CALENDAR, "ParseExceptions\n{0}", exceptions);
            foreach (var exception in exceptions.Elements()) {
                NcAssert.True (exception.Name.LocalName.Equals (Xml.Calendar.Exceptions.Exception));
                var e = new McException ();
                e.attendees = new List<McAttendee> ();
                e.categories = new List<McCalendarCategory> ();
                foreach (var child in exception.Elements()) {
                    switch (child.Name.LocalName) {
                    // Containers
                    case Xml.Calendar.Exception.Attendees:
                        var attendees = ParseAttendees (ns, child);
                        if (null == e.attendees) {
                            e.attendees = attendees;
                        } else {
                            e.attendees.AddRange (attendees);
                        }
                        break;
                    case Xml.Calendar.Exception.Categories:
                        var categories = ParseCategories (ns, child);
                        if (null == e.categories) {
                            e.categories = categories;
                        } else {
                            e.categories.AddRange (categories);
                        }
                        break;
                    // Elements
                    case Xml.Calendar.Exception.AllDayEvent:
                        e.AllDayEvent = child.Value.ToBoolean ();
                        break;
                    case Xml.Calendar.Exception.BusyStatus:
                        e.BusyStatus = child.Value.ToEnum<NcBusyStatus> ();
                        e.BusyStatusIsSet = true;
                        break;
                    case Xml.Calendar.Exception.Deleted:
                        e.Deleted = child.Value.ToUint ();
                        break;
                    case Xml.Calendar.Exception.MeetingStatus:
                        e.MeetingStatus = child.Value.ParseInteger<NcMeetingStatus> ();
                        e.MeetingStatusIsSet = true;
                        break;
                    case Xml.Calendar.Exception.Reminder:
                        e.Reminder = child.Value.ToUint ();
                        break;
                    case Xml.Calendar.Exception.ResponseType:
                        e.ResponseType = child.Value.ParseInteger<NcResponseType> ();
                        e.ResponseTypeIsSet = true;
                        break;
                    case Xml.Calendar.Exception.Sensitivity:
                        e.Sensitivity = child.Value.ParseInteger<NcSensitivity> ();
                        e.SensitivityIsSet = true;
                        break;
                    case Xml.Calendar.Exception.AppointmentReplyTime:
                    case Xml.Calendar.Exception.DtStamp:
                    case Xml.Calendar.Exception.EndTime:
                    case Xml.Calendar.Exception.ExceptionStartTime:
                    case Xml.Calendar.Exception.StartTime:
                        TrySetCompactDateTimeFromXml (e, child.Name.LocalName, child.Value);
                        break;
                    case Xml.Calendar.Exception.Location:
                    case Xml.Calendar.Exception.OnlineMeetingConfLink:
                    case Xml.Calendar.Exception.OnlineMeetingExternalLink:
                    case Xml.Calendar.Exception.Subject:
                        TrySetStringFromXml (e, child.Name.LocalName, child.Value);
                        break;
                    case Xml.AirSyncBase.Body:
                        var bodyType = child.Element (m_baseNs + Xml.AirSyncBase.Type).Value.ToInt ();
                        var bodyElement = child.Element (m_baseNs + Xml.AirSyncBase.Data);
                        if (null != bodyElement) {
                            var saveAttr = bodyElement.Attributes ().Where (x => x.Name == "nacho-body-id").SingleOrDefault ();
                            if (null != saveAttr) {
                                e.BodyId = int.Parse (saveAttr.Value);
                            } else {
                                var body = McBody.Save (bodyElement.Value); 
                                e.BodyId = body.Id;
                            }
                            e.BodyType = bodyType;
                        } else {
                            e.BodyId = 0;
                            Console.WriteLine ("Truncated message from server.");
                        }
                        break;
                    case Xml.AirSyncBase.NativeBodyType:
                        NcAssert.CaseError (); // Docs claim this doesn't exist
                        break;
                    default:
                        Log.Warn (Log.LOG_AS, "CreateNcCalendarFromXML UNHANDLED: " + child.Name.LocalName + " value=" + child.Value);
                        break;
                    }
                }
                l.Add (e);
            }
            return l;
        }
        // CreateNcCalendarFromXML
        // <Body xmlns="AirSyncBase:"> <Type> 1 </Type> <Data> </Data> </Body>
        // <DtStamp xmlns="Calendar:"> 20131123T190243Z </DtStamp>
        // <StartTime xmlns="Calendar:"> 20131123T223000Z </StartTime>
        // <EndTime xmlns="Calendar:"> 20131123T233000Z </EndTime>
        // <Location xmlns="Calendar:"> the Dogg House!  </Location>
        // <Subject xmlns="Calendar:"> Big dog party at the Dogg House!  </Subject>
        // <UID xmlns="Calendar:"> 3rrr5stn6eld9qmv8dviolj3u0@google.com </UID>
        // <Sensitivity xmlns="Calendar:"> 0 </Sensitivity>
        // <BusyStatus xmlns="Calendar:"> 2 </BusyStatus>
        // <AllDayEvent xmlns="Calendar:"> 0 </AllDayEvent>
        // <Reminder xmlns="Calendar:"> 10 </Reminder>
        // <MeetingStatus xmlns="Calendar:"> 0 </MeetingStatus>
        // <TimeZone xmlns="Calendar:"> LAEAAEUAUw...P///w== </TimeZone>
        // <Organizer_Email xmlns="Calendar:"> steves@nachocove.com </Organizer_Email>
        // <Organizer_Name xmlns="Calendar:"> Steve Scalpone </Organizer_Name>
        public NcResult ParseCalendar (XNamespace ns, XElement command)
        {
            // <ServerId>..</ServerId>
            var serverId = command.Element (ns + Xml.AirSync.ServerId);
            NcAssert.True (null != serverId);

            McCalendar c = new McCalendar ();
            c.ServerId = serverId.Value;

            c.attendees = new List<McAttendee> ();
            c.categories = new List<McCalendarCategory> ();
            c.exceptions = new List<McException> ();
            c.recurrences = new List<McRecurrence> ();

            XNamespace nsCalendar = "Calendar";
            // <ApplicationData>...</ApplicationData>
            var applicationData = command.Element (ns + Xml.AirSync.ApplicationData);
            NcAssert.True (null != applicationData);

            Log.Debug (Log.LOG_XML, "ParseCalendar\n{0}", applicationData);
            foreach (var child in applicationData.Elements()) {
                switch (child.Name.LocalName) {
                // Containers
                case Xml.Calendar.Calendar_Attendees:
                    var attendees = ParseAttendees (nsCalendar, child);
                    c.attendees.AddRange (attendees);
                    break;
                case Xml.Calendar.Calendar_Categories:
                    var categories = ParseCategories (nsCalendar, child);
                    c.categories.AddRange (categories);
                    break;
                case Xml.Calendar.Calendar_Exceptions:
                    var exceptions = ParseExceptions (nsCalendar, child);
                    c.exceptions.AddRange (exceptions);
                    break;
                case Xml.Calendar.Calendar_Recurrence:
                    var recurrence = ParseRecurrence (nsCalendar, child);
                    c.recurrences.Add (recurrence);
                    break;
                case Xml.AirSyncBase.Body:
                    var bodyType = child.Element (m_baseNs + Xml.AirSyncBase.Type).Value.ToInt ();
                    var bodyElement = child.Element (m_baseNs + Xml.AirSyncBase.Data);
                    if (null != bodyElement) {
                        var saveAttr = bodyElement.Attributes ().Where (x => x.Name == "nacho-body-id").SingleOrDefault ();
                        if (null != saveAttr) {
                            c.BodyId = int.Parse (saveAttr.Value);
                        } else {
                            var body = McBody.Save (bodyElement.Value); 
                            c.BodyId = body.Id;
                        }
                        c.BodyType = bodyType;
                    } else {
                        c.BodyId = 0;
                        Log.Info (Log.LOG_AS, "Truncated or zero-length message from server.");
                    }
                    break;
                case Xml.AirSyncBase.NativeBodyType:
                    c.NativeBodyType = child.Value.ToInt ();
                    break;
                // Elements
                case Xml.Calendar.AllDayEvent:
                    c.AllDayEvent = child.Value.ToBoolean ();
                    break;
                case Xml.Calendar.BusyStatus:
                    c.BusyStatus = child.Value.ToEnum<NcBusyStatus> ();
                    c.BusyStatusIsSet = true;
                    break;
                case Xml.Calendar.DisallowNewTimeProposal:
                    c.DisallowNewTimeProposal = child.Value.ToBoolean ();
                    break;
                case Xml.Calendar.MeetingStatus:
                    c.MeetingStatus = child.Value.ParseInteger<NcMeetingStatus> ();
                    c.MeetingStatusIsSet = true;
                    break;
                case Xml.Calendar.Reminder:
                    if (string.IsNullOrEmpty (child.Value)) {
                        // TODO: add support for top-level Reminder element.
                    } else {
                        c.Reminder = child.Value.ToUint ();
                    }
                    break;
                case Xml.Calendar.ResponseRequested:
                    c.ResponseRequested = child.Value.ToBoolean ();
                    break;
                case Xml.Calendar.ResponseType:
                    c.ResponseType = child.Value.ParseInteger<NcResponseType> ();
                    c.ResponseTypeIsSet = true;
                    break;
                case Xml.Calendar.Sensitivity:
                    c.Sensitivity = child.Value.ParseInteger<NcSensitivity> ();
                    c.SensitivityIsSet = true;
                    break;
                case Xml.Calendar.AppointmentReplyTime:
                case Xml.Calendar.DtStamp:
                case Xml.Calendar.EndTime:
                case Xml.Calendar.StartTime:
                    TrySetCompactDateTimeFromXml (c, child.Name.LocalName, child.Value);
                    break;
                case Xml.Calendar.Timezone:
                    c.TimeZone = child.Value;
                    break;
                case Xml.Calendar.Location:
                case Xml.Calendar.OnlineMeetingConfLink:
                case Xml.Calendar.OnlineMeetingExternalLink:
                case Xml.Calendar.OrganizerEmail:
                case Xml.Calendar.OrganizerName:
                case Xml.Calendar.Subject:
                case Xml.Calendar.UID:
                    TrySetStringFromXml (c, child.Name.LocalName, child.Value);
                    break;
                default:
                    Log.Warn (Log.LOG_AS, "ParseCalendar UNHANDLED: " + child.Name.LocalName + " value=" + child.Value);
                    break;
                }
            }
            return NcResult.OK (c);
        }

        public NcResult ParseEmail (XNamespace ns, XElement command)
        {

            var serverId = command.Element (ns + Xml.AirSync.ServerId);
            NcAssert.True (null != serverId);

            McEmailMessage emailMessage = new McEmailMessage ();
            emailMessage.ServerId = serverId.Value;
            var appData = command.Element (ns + Xml.AirSync.ApplicationData);
            NcAssert.NotNull (appData);


            emailMessage.xmlAttachments = null;

            foreach (var child in appData.Elements()) {
                switch (child.Name.LocalName) {
                case Xml.AirSyncBase.Attachments:
                    emailMessage.xmlAttachments = child.Elements (m_baseNs + Xml.AirSyncBase.Attachment);
                    emailMessage.cachedHasAttachments = true;
                    break;
                case Xml.AirSyncBase.Body:
                    var bodyType = child.Element (m_baseNs + Xml.AirSyncBase.Type).Value.ToInt ();
                    var bodyElement = child.Element (m_baseNs + Xml.AirSyncBase.Data);
                    // NOTE: We have seen EstimatedDataSize of 0 and no Truncate here.
                    if (null != bodyElement) {
                        var saveAttr = bodyElement.Attributes ().SingleOrDefault (x => x.Name == "nacho-body-id");
                        if (null != saveAttr) {
                            emailMessage.BodyId = int.Parse (saveAttr.Value);
                        } else {
                            var body = McBody.Save (bodyElement.Value); 
                            emailMessage.BodyId = body.Id;
                        }
                        emailMessage.BodyType = bodyType;
                    } else {
                        emailMessage.BodyId = 0;
                        Console.WriteLine ("Truncated message from server.");
                    }
                    break;

                case Xml.Email.Flag:
                    if (!child.HasElements) {
                        // This is the clearing of the Flag.
                        emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Cleared;
                    } else {
                        foreach (var flagPart in child.Elements()) {
                            switch (flagPart.Name.LocalName) {
                            case Xml.Email.Status:
                                try {
                                    uint statusValue = uint.Parse (flagPart.Value);
                                    if (2 < statusValue) {
                                        // FIXME log.
                                    } else {
                                        emailMessage.FlagStatus = statusValue;
                                    }
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Email.FlagType:
                                emailMessage.FlagType = flagPart.Value;
                                break;

                            case Xml.Tasks.StartDate:
                                try {
                                    emailMessage.FlagDeferUntil = DateTime.Parse (flagPart.Value);
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Tasks.UtcStartDate:
                                try {
                                    emailMessage.FlagUtcDeferUntil = DateTime.Parse (flagPart.Value);
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Tasks.DueDate:
                                try {
                                    emailMessage.FlagDue = DateTime.Parse (flagPart.Value);
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Tasks.UtcDueDate:
                                try {
                                    emailMessage.FlagUtcDue = DateTime.Parse (flagPart.Value);
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Tasks.ReminderSet:
                                try {
                                    int boolInt = int.Parse (flagPart.Value);
                                    if (0 == boolInt) {
                                        emailMessage.FlagReminderSet = false;
                                    } else if (1 == boolInt) {
                                        emailMessage.FlagReminderSet = true;
                                    } else {
                                        // FIXME log.
                                    }
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Tasks.Subject:
                                // Ignore. This SHOULD be the same as the message Subject.
                                break;

                            case Xml.Tasks.ReminderTime:
                                try {
                                    emailMessage.FlagReminderTime = DateTime.Parse (flagPart.Value);
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Email.CompleteTime:
                                try {
                                    emailMessage.FlagCompleteTime = DateTime.Parse (flagPart.Value);
                                } catch {
                                    // FIXME log.
                                }
                                break;

                            case Xml.Tasks.DateCompleted:
                                try {
                                    emailMessage.FlagDateCompleted = DateTime.Parse (flagPart.Value);
                                } catch {
                                    // FIXME log.
                                }
                                break;
                            }
                        }
                    }
                    break;

                case Xml.Email.To:
                    // TODO: Append
                    emailMessage.To = child.Value;
                    break;

                case Xml.Email.Cc:
                    // TODO: Append
                    emailMessage.Cc = child.Value;
                    break;

                case Xml.Email.From:
                    emailMessage.From = child.Value;
                    break;

                case Xml.Email.ReplyTo:
                    emailMessage.ReplyTo = child.Value;
                    break;

                case Xml.Email.Subject:
                    emailMessage.Subject = child.Value;
                    break;

                case Xml.Email.DateReceived:
                    try {
                        emailMessage.DateReceived = DateTime.Parse (child.Value);
                    } catch {
                        // FIXME - just log it.
                    }
                    break;
                case Xml.Email.DisplayTo:
                    emailMessage.DisplayTo = child.Value;
                    break;
                case Xml.Email.Importance:
                    try {
                        emailMessage.Importance = (NcImportance)uint.Parse (child.Value);
                    } catch {
                        // FIXME - just log it.
                    }
                    break;
                case Xml.Email.Read:
                    if ("1" == child.Value) {
                        emailMessage.IsRead = true;
                    } else {
                        emailMessage.IsRead = false;
                    }
                    break;
                case Xml.Email.MessageClass:
                    emailMessage.MessageClass = child.Value;
                    break;
                case Xml.Email.ThreadTopic:
                    emailMessage.ThreadTopic = child.Value;
                    break;
                case Xml.Email.Sender:
                    emailMessage.Sender = child.Value;
                    break;
                case Xml.Email2.ReceivedAsBcc:
                    if ("1" == child.Value) {
                        emailMessage.ReceivedAsBcc = true;
                    } else {
                        emailMessage.ReceivedAsBcc = false;
                    }
                    break;
                case Xml.Email2.ConversationIndex:
                    byte[] bytes = new byte[child.Value.Length * sizeof(char)];
                    System.Buffer.BlockCopy (child.Value.ToCharArray (), 0, bytes, 0, bytes.Length);
                    emailMessage.ConversationIndex = bytes;
                    break;
                case Xml.Email2.ConversationId:
                    emailMessage.ConversationId = child.Value;
                    break;
                case Xml.AirSyncBase.NativeBodyType:
                    emailMessage.NativeBodyType = (byte)child.Value.ToInt ();
                    break;
                case  Xml.Email.InternetCPID:
                    emailMessage.InternetCPID = child.Value;
                    break;
                case Xml.Email.ContentClass:
                    emailMessage.ContentClass = child.Value;
                    break;
                case Xml.Email.Categories:
                    XNamespace nsEmail = "Email";
                    var categories = AsHelpers.ParseEmailCategories (nsEmail, child);
                    if (0 == emailMessage.Categories.Count) {
                        emailMessage.Categories = categories;
                    } else {
                        emailMessage.Categories.AddRange (categories);
                    }
                    break;
                case Xml.Email2.LastVerbExecuted:
                    emailMessage.LastVerbExecuted = child.Value.ToInt ();
                    break;
                case Xml.Email2.LastVerbExecutionTime:
                    emailMessage.LastVerbExecutionTime = DateTime.Parse (child.Value);
                    break;
                default:
                    Log.Warn (Log.LOG_AS, "ProcessEmailItem UNHANDLED: " + child.Name.LocalName + " value=" + child.Value);
                    break;
                }
            }
            return NcResult.OK (emailMessage);
        }

        private static bool ParseXmlBoolean (XElement bit)
        {
            if (bit.IsEmpty) {
                return true;
            }
            switch (bit.Value) {
            case "0":
                return false;
            case "1":
                return true;
            default:
                throw new Exception ();
            }
        }

        public static bool EmailMessageHasAttachment (XElement command, int attachId)
        {
            // FIXME - need to implement.
            return true;
        }

        public static bool TimeOrLocationChanged (XElement command, string serverId)
        {
            // FIXME - need to implement.
            return false;
        }

        public void InsertAttachments (McEmailMessage msg)
        {
            XNamespace email2Ns = Xml.Email2.Ns;
            if (null != msg.xmlAttachments) {

                if (null != msg.xmlAttachments) {
                    foreach (XElement xmlAttachment in msg.xmlAttachments) {
                        // Create & save the attachment record.
                        var attachment = new McAttachment {
                            AccountId = msg.AccountId,
                            EmailMessageId = msg.Id,
                            IsDownloaded = false,
                            PercentDownloaded = 0,
                            IsInline = false,
                            EstimatedDataSize = uint.Parse (xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.EstimatedDataSize).Value),
                            FileReference = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.FileReference).Value,
                            Method = uint.Parse (xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.Method).Value),
                        };
                        var displayName = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.DisplayName);
                        if (null != displayName) {
                            attachment.DisplayName = displayName.Value;
                        }
                        var contentLocation = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.ContentLocation);
                        if (null != contentLocation) {
                            attachment.ContentLocation = contentLocation.Value;
                        }
                        var contentId = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.ContentId);
                        if (null != contentId) {
                            attachment.ContentId = contentId.Value;
                        }
                        var isInline = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.IsInline);
                        if (null != isInline) {
                            attachment.IsInline = ParseXmlBoolean (isInline);
                        }
                        var xmlUmAttDuration = xmlAttachment.Element (email2Ns + Xml.Email2.UmAttDuration);
                        if (null != xmlUmAttDuration) {
                            attachment.VoiceSeconds = uint.Parse (xmlUmAttDuration.Value);
                        }
                        var xmlUmAttOrder = xmlAttachment.Element (email2Ns + Xml.Email2.UmAttOrder);
                        if (null != xmlUmAttOrder) {
                            attachment.VoiceOrder = int.Parse (xmlUmAttOrder.Value);
                        }
                        attachment.Insert ();
                    }
                }
            }
        }

        /// <summary>
        /// Tries the set string from xml.
        /// </summary>
        /// <param name="targetObj">Target object.</param>
        /// <param name="targetProp">Target property.</param>
        /// <param name="value">Value.</param>
        public void TrySetStringFromXml (object targetObj, string targetProp, string value)
        {
            try {
                var prop = targetObj.GetType ().GetProperty (targetProp);
                NcAssert.True (null != prop);
                if (typeof(string) != prop.PropertyType) {
                    Log.Warn (Log.LOG_AS, "TrySetStringFromXml: Property {0} is not string.", targetProp);
                    return;
                }
                prop.SetValue (targetObj, value);
            } catch (Exception e) {
                Log.Warn (Log.LOG_AS, "TrySetStringFromXml: Bad value {0} or property {1}:\n{2}.", value, targetProp, e);
            }
        }

        /// <summary>
        /// Tries the set int from xml.
        /// </summary>
        /// <param name="targetObj">Target object.</param>
        /// <param name="targetProp">Target property.</param>
        /// <param name="value">Value.</param>
        public void TrySetIntFromXml (object targetObj, string targetProp, string value)
        {
            try {
                var prop = targetObj.GetType ().GetProperty (targetProp);
                NcAssert.True (null != prop);
                if (typeof(int) != prop.PropertyType) {
                    Log.Warn (Log.LOG_AS, "TrySetIntFromXml: Property {0} is not int.", targetProp);
                    return;
                }
                var numValue = int.Parse (value);
                prop.SetValue (targetObj, numValue);
            } catch (Exception e) {
                Log.Warn (Log.LOG_AS, "TrySetIntFromXml: Bad value {0} or property {1}:\n{2}.", value, targetProp, e);
            }
        }

        /// <summary>
        /// Tries the set date time from xml.
        /// </summary>
        /// <param name="targetObj">Target object.</param>
        /// <param name="targetProp">Target property.</param>
        /// <param name="value">Value.</param>
        public void TrySetDateTimeFromXml (object targetObj, string targetProp, string value)
        {
            try {
                var prop = targetObj.GetType ().GetProperty (targetProp);
                NcAssert.True (null != prop);
                if (typeof(DateTime) != prop.PropertyType) {
                    Log.Warn (Log.LOG_AS, "TrySetDateTimeFromXml: Property {0} is not int.", targetProp);
                    return;
                }
                var dt = ParseAsDateTime (value);
                prop.SetValue (targetObj, dt);
            } catch (Exception e) {
                Log.Warn (Log.LOG_AS, "TrySetDateTimeFromXml: Bad value {0} or property {1}:\n{2}.", value, targetProp, e);
            }
        }

        public void TrySetCompactDateTimeFromXml (object targetObj, string targetProp, string value)
        {
            try {
                var prop = targetObj.GetType ().GetProperty (targetProp);
                NcAssert.True (null != prop);
                if (typeof(DateTime) != prop.PropertyType) {
                    Log.Warn (Log.LOG_AS, "TrySetCompactDateTimeFromXml: Property {0} is not int.", targetProp);
                    return;
                }
                var dt = ParseAsCompactDateTime (value);
                prop.SetValue (targetObj, dt);
            } catch (Exception e) {
                Log.Warn (Log.LOG_AS, "TrySetCompactDateTimeFromXml: Bad value {0} or property {1}:\n{2}.", value, targetProp, e);
            }
        }
    }
}

