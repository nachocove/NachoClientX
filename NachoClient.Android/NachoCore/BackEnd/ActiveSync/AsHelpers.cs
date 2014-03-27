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
        const string CompactDateTimeFmt1 = "yyyyMMddTHHmmssZ";
        const string CompactDateTimeFmt2 = "yyyyMMddTHHmmssfffZ";
        const string DateTimeFmt1 = "yyyy-MM-ddTHH:mm:ss.fffZ";
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
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.AllDayEvent));
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
            // TODO: BodyId not supported yet.

            if (0 != cal.attendees.Count) {
                var xmlAttendees = new XElement (CalendarNs + Xml.Calendar.Calendar_Attendees);
                foreach (var attendee in cal.attendees) {
                    var xmlAttendee = new XElement (CalendarNs + Xml.Calendar.Attendees.Attendee,
                                          new XElement (CalendarNs + Xml.Calendar.Email, attendee.Email),
                                          new XElement (CalendarNs + Xml.Calendar.Name, attendee.Name));
                    if (attendee.AttendeeTypeIsSet) {
                        xmlAttendee.Add (new XElement (CalendarNs + Xml.Calendar.AttendeeType, (uint)attendee.AttendeeType));
                    }
                    if (attendee.AttendeeStatusIsSet) {
                        xmlAttendee.Add (new XElement (CalendarNs + Xml.Calendar.AttendeeStatus, (uint)attendee.AttendeeStatus));
                    }
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

            xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.ResponseRequested, XmlFromBool (cal.ResponseRequested)));
            xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.DisallowNewTimeProposal, XmlFromBool (cal.DisallowNewTimeProposal)));
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
        public System.DateTime ParseAsDateTime (string dateTime)
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
            NachoCore.NachoAssert.True ((start + fieldLength) <= binaryData.Length);
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
            NachoCore.NachoAssert.True (null != attendees);
            NachoCore.NachoAssert.True (attendees.Name.LocalName.Equals (Xml.Calendar.Calendar_Attendees));

            var list = new List<McAttendee> ();

            foreach (var attendee in attendees.Elements()) {
                NachoCore.NachoAssert.True (attendee.Name.LocalName.Equals (Xml.Calendar.Attendees.Attendee));

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
            NachoCore.NachoAssert.True (null != categories);
            NachoCore.NachoAssert.True (categories.Name.LocalName.Equals (Xml.Calendar.Calendar_Categories));

            var list = new List<McCalendarCategory> ();

            foreach (var category in categories.Elements()) {
                NachoCore.NachoAssert.True (category.Name.LocalName.Equals (Xml.Calendar.Categories.Category));
                var n = new McCalendarCategory (category.Value);
                list.Add (n);
            }
            return list;
        }

        /// <summary>
        /// Parses the recurrence section of a calendar item.
        /// </summary>
        /// <returns>The recurrence record</returns>
        /// <param name="ns">XML namespace to use to fetch elements</param>
        /// <param name="recurrence">Recurrence element</param>
        public McRecurrence ParseRecurrence (XNamespace ns, XElement recurrence)
        {
            NachoCore.NachoAssert.True (null != recurrence);
            NachoCore.NachoAssert.True (recurrence.Name.LocalName.Equals (Xml.Calendar.Calendar_Recurrence));

            var r = new McRecurrence ();

            foreach (var child in recurrence.Elements()) {
                switch (child.Name.LocalName) {
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
            NachoCore.NachoAssert.True (null != exceptions);
            NachoCore.NachoAssert.True (exceptions.Name.LocalName.Equals (Xml.Calendar.Calendar_Exceptions));

            var l = new List<McException> ();

            Log.Info (Log.LOG_CALENDAR, "ParseExceptions\n{0}", exceptions);
            foreach (var exception in exceptions.Elements()) {
                NachoCore.NachoAssert.True (exception.Name.LocalName.Equals (Xml.Calendar.Exceptions.Exception));
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
                        var bodyElement = child.Element (m_baseNs + Xml.AirSyncBase.Data);
                        if (null != bodyElement) {
                            var saveAttr = bodyElement.Attributes ().SingleOrDefault (x => x.Name == "nacho-body-id");
                            if (null != saveAttr) {
                                e.BodyId = int.Parse (saveAttr.Value);
                            } else {
                                var body = new McBody ();
                                body.Body = bodyElement.Value; 
                                body.Insert ();
                                e.BodyId = body.Id;
                            }
                        } else {
                            e.BodyId = 0;
                            Console.WriteLine ("Truncated message from server.");
                        }
                        break;
                    case Xml.AirSyncBase.NativeBodyType:
                        NachoAssert.CaseError (); // Docs claim this doesn't exist
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
            NachoCore.NachoAssert.True (null != serverId);

            McCalendar c = new McCalendar ();
            c.ServerId = serverId.Value;

            c.attendees = new List<McAttendee> ();
            c.categories = new List<McCalendarCategory> ();
            c.exceptions = new List<McException> ();
            c.recurrences = new List<McRecurrence> ();

            XNamespace nsCalendar = "Calendar";
            // <ApplicationData>...</ApplicationData>
            var applicationData = command.Element (ns + Xml.AirSync.ApplicationData);
            NachoCore.NachoAssert.True (null != applicationData);

            Log.Info (Log.LOG_CALENDAR, "ParseCalendar\n{0}", applicationData);
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
                    var bodyElement = child.Element (m_baseNs + Xml.AirSyncBase.Data);
                    if (null != bodyElement) {
                        var saveAttr = bodyElement.Attributes ().SingleOrDefault (x => x.Name == "nacho-body-id");
                        if (null != saveAttr) {
                            c.BodyId = int.Parse (saveAttr.Value);
                        } else {
                            var body = new McBody ();
                            body.Body = bodyElement.Value; 
                            body.Insert ();
                            c.BodyId = body.Id;
                        }
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
//                case Xml.Calendar.Timezone:
//                    stringValue = child.Value;
//                    NcTimeZone tz = ParseAsTimeZone (stringValue);
//                    break;
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
                NachoCore.NachoAssert.True (null != prop);
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
                NachoCore.NachoAssert.True (null != prop);
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
                NachoCore.NachoAssert.True (null != prop);
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
                NachoCore.NachoAssert.True (null != prop);
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

