//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
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
        public AsHelpers ()
        {

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
            const string fmt1 = "yyyyMMddTHHmmssZ";
            const string fmt2 = "yyyyMMddTHHmmssfffZ";
            DateTimeStyles styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;

            if (DateTime.TryParseExact (compactDateTime, fmt1, CultureInfo.InvariantCulture, styles, out dateValue)) {
                return dateValue;
            }
            if (DateTime.TryParseExact (compactDateTime, fmt2, CultureInfo.InvariantCulture, styles, out dateValue)) {
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
            const string fmt1 = "yyyy-MM-ddTHH:mm:ss.fffZ";
            DateTimeStyles styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;
            if (DateTime.TryParseExact (dateTime, fmt1, CultureInfo.InvariantCulture, styles, out dateValue)) {
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
        public NcTimeZone ParseAsTimeZone (string encodedTimeZone)
        {
            // Convert the Base64 UUEncoded input into binary output. 
            byte[] binaryData;
            try {
                binaryData = System.Convert.FromBase64String (encodedTimeZone);
            } catch (System.ArgumentNullException) {
                System.Console.WriteLine ("Encoded TimeZone string is null.");
                return null;
            } catch (System.FormatException) {
                System.Console.WriteLine ("Encoded TimeZone string length is not 4 or is not an even multiple of 4.");
                return null;
            }
            if (binaryData.Length != (4 + 64 + 16 + 4 + 64 + 16 + 4)) {
                System.Console.WriteLine ("Decoded TimeZone string length is wrong: " + binaryData.Length.ToString ());
                return null;
            }
            string StandardName = ExtractStringFromAsTimeZone (binaryData, 4, 64);
            string DaylightName = ExtractStringFromAsTimeZone (binaryData, 4 + 64 + 16 + 4, 64);
            NcTimeZone tz = new NcTimeZone ();
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
            System.Diagnostics.Debug.Assert ((start + fieldLength) <= binaryData.Length);
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
        public List<NcAttendee> ParseAttendees (XNamespace ns, XElement attendees)
        {
            System.Diagnostics.Trace.Assert (null != attendees);
            System.Diagnostics.Trace.Assert (attendees.Name.LocalName.Equals (Xml.Calendar.Calendar_Attendees));

            var list = new List<NcAttendee> ();

            foreach (var attendee in attendees.Elements()) {
                System.Diagnostics.Debug.Assert (attendee.Name.LocalName.Equals (Xml.Calendar.Attendees.Attendee));

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

                var a = new NcAttendee (name, email, type, status);
                list.Add (a);
            }
            return list;
        }

        /// <returns>
        /// A list of categories not yet associated with an NcCalendar or NcException. Not null.
        /// </returns>
        // TODO: Handle missing name & email better
        // TODO: Make sure we don't have extra fields
        public List<NcCategory> ParseCategories (XNamespace ns, XElement categories)
        {
            System.Diagnostics.Trace.Assert (null != categories);
            System.Diagnostics.Trace.Assert (categories.Name.LocalName.Equals (Xml.Calendar.Calendar_Categories));

            var list = new List<NcCategory> ();

            foreach (var category in categories.Elements()) {
                System.Diagnostics.Debug.Assert (categories.Name.LocalName.Equals (Xml.Calendar.Categories.Category));
                var n = new NcCategory (category.Value);
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
        public NcRecurrence ParseRecurrence (XNamespace ns, XElement recurrence)
        {
            System.Diagnostics.Trace.Assert (null != recurrence);
            System.Diagnostics.Trace.Assert (recurrence.Name.LocalName.Equals (Xml.Calendar.Calendar_Recurrence));

            var r = new NcRecurrence ();

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
                    Console.WriteLine ("ParseRecurrence UNHANDLED: " + child.Name.LocalName + " value=" + child.Value);
                    break;
                }
            }
            return r;
        }

        public List<NcException> ParseExceptions (XNamespace ns, XElement exceptions)
        {
            System.Diagnostics.Trace.Assert (null != exceptions);
            System.Diagnostics.Trace.Assert (exceptions.Name.LocalName.Equals (Xml.Calendar.Calendar_Exceptions));

            var l = new List<NcException> ();

            Log.Info (Log.LOG_CALENDAR, "ParseExceptions\n{0}", exceptions.ToString ());
            foreach (var exception in exceptions.Elements()) {
                System.Diagnostics.Trace.Assert (exception.Name.LocalName.Equals (Xml.Calendar.Exceptions.Exception));
                var e = new NcException ();
                e.attendees = new List<NcAttendee> ();
                e.categories = new List<NcCategory> ();
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
                        break;
                    case Xml.Calendar.Exception.Deleted:
                        e.Deleted = child.Value.ToUint ();
                        break;
                    case Xml.Calendar.Exception.MeetingStatus:
                        e.MeetingStatus = child.Value.ParseInteger<NcMeetingStatus> ();
                        break;
                    case Xml.Calendar.Exception.Reminder:
                        e.Reminder = child.Value.ToUint ();
                        break;
                    case Xml.Calendar.Exception.ResponseType:
                        e.ResponseType = child.Value.ParseInteger<NcResponseType> ();
                        break;
                    case Xml.Calendar.Exception.Sensitivity:
                        e.Sensitivity = child.Value.ParseInteger<NcSensitivity> ();
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
//                  case Xml.Calendar.airsyncbase:Body:
//                      break
                    default:
                        Console.WriteLine ("CreateNcCalendarFromXML UNHANDLED: " + child.Name.LocalName + " value=" + child.Value);
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
        public NcResult ParseCalendar (XNamespace ns, XElement command, NcFolder folder)
        {
            // <ServerId>..</ServerId>
            var serverId = command.Element (ns + Xml.AirSync.ServerId);
            System.Diagnostics.Trace.Assert (null != serverId);

            // Folder must exist & have a key
            System.Diagnostics.Trace.Assert (null != folder);
            System.Diagnostics.Trace.Assert (folder.Id > 0);

            NcCalendar c = new NcCalendar ();
            c.ServerId = serverId.Value;
            c.FolderId = folder.Id;

            c.attendees = new List<NcAttendee> ();
            c.categories = new List<NcCategory> ();
            c.exceptions = new List<NcException> ();
            c.recurrences = new List<NcRecurrence> ();

            XNamespace nsCalendar = "Calendar";
            // <ApplicationData>...</ApplicationData>
            var applicationData = command.Element (ns + Xml.AirSync.ApplicationData);
            System.Diagnostics.Trace.Assert (null != applicationData);

            Log.Info (Log.LOG_CALENDAR, "ParseCalendar\n{0}", applicationData.ToString ());
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
//                case Xml.Calendar.airsyncbase:Body:
//                    break
//                case Xml.Calendar.airsyncbase:NativeBodyType:
//                    break;
                // Elements
                case Xml.Calendar.AllDayEvent:
                    c.AllDayEvent = child.Value.ToBoolean ();
                    break;
                case Xml.Calendar.BusyStatus:
                    c.BusyStatus = child.Value.ToEnum<NcBusyStatus> ();
                    break;
                case Xml.Calendar.DisallowNewTimeProposal:
                    c.DisallowNewTimeProposal = child.Value.ToBoolean ();
                    break;
                case Xml.Calendar.MeetingStatus:
                    c.MeetingStatus = child.Value.ParseInteger<NcMeetingStatus> ();
                    break;
                case Xml.Calendar.Reminder:
                    c.Reminder = child.Value.ToUint ();
                    break;
                case Xml.Calendar.ResponseRequested:
                    c.ResponseRequested = child.Value.ToBoolean ();
                    break;
                case Xml.Calendar.ResponseType:
                    c.ResponseType = child.Value.ParseInteger<NcResponseType> ();
                    break;
                case Xml.Calendar.Sensitivity:
                    c.Sensitivity = child.Value.ParseInteger<NcSensitivity> ();
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
                    Console.WriteLine ("ParseCalendar UNHANDLED: " + child.Name.LocalName + " value=" + child.Value);
                    break;
                }
            }
            return NcResult.OK (c);
        }

        public NcResult ParseContact (XNamespace ns, XElement command, NcFolder folder)
        {
            // <ServerId>..</ServerId>
            var serverId = command.Element (ns + Xml.AirSync.ServerId);
            System.Diagnostics.Trace.Assert (null != serverId);

            // Folder must exist & have a key
            System.Diagnostics.Trace.Assert (null != folder);
            System.Diagnostics.Trace.Assert (folder.Id > 0);

            var c = new NcContact ();
            c.ServerId = serverId.Value;
            c.FolderId = folder.Id;

            c.categories = new List<NcContactCategory> ();

//            XNamespace nsContact = "Contact";
            // <ApplicationData>...</ApplicationData>
            var applicationData = command.Element (ns + Xml.AirSync.ApplicationData);
            System.Diagnostics.Trace.Assert (null != applicationData);

            Log.Info (Log.LOG_CALENDAR, "ParseContact\n{0}", applicationData.ToString ());
            foreach (var child in applicationData.Elements()) {
                switch (child.Name.LocalName) {
                case Xml.Contacts.Anniversary:
                case Xml.Contacts.Birthday:
                    TrySetDateTimeFromXml (c, child.Name.LocalName, child.Value);
                    break;
//                case Xml.Contacts.categories;
//                    break;
                case Xml.Contacts.WeightedRank:
                    TrySetIntFromXml (c, child.Name.LocalName, child.Value);
                    break;
                case Xml.Contacts.Alias:
                case Xml.Contacts.AssistantName:
                case Xml.Contacts.AssistantPhoneNumber:
                case Xml.Contacts.Business2PhoneNumber:
                case Xml.Contacts.BusinessAddressCity:
                case Xml.Contacts.BusinessAddressCountry:
                case Xml.Contacts.BusinessAddressPostalCode:
                case Xml.Contacts.BusinessAddressState:
                case Xml.Contacts.BusinessAddressStreet:
                case Xml.Contacts.BusinessFaxNumber:
                case Xml.Contacts.BusinessPhoneNumber:
                case Xml.Contacts.CarPhoneNumber:
                case Xml.Contacts.Category:
                case Xml.Contacts.Children:
                case Xml.Contacts.CompanyName:
                case Xml.Contacts.Department:
                case Xml.Contacts.Email1Address:
                case Xml.Contacts.Email2Address:
                case Xml.Contacts.Email3Address:
                case Xml.Contacts.FileAs:
                case Xml.Contacts.FirstName:
                case Xml.Contacts.Home2PhoneNumber:
                case Xml.Contacts.HomeAddressCity:
                case Xml.Contacts.HomeAddressCountry:
                case Xml.Contacts.HomeAddressPostalCode:
                case Xml.Contacts.HomeAddressState:
                case Xml.Contacts.HomeAddressStreet:
                case Xml.Contacts.HomeFaxNumber:
                case Xml.Contacts.HomePhoneNumber:
                case Xml.Contacts.JobTitle:
                case Xml.Contacts.LastName:
                case Xml.Contacts.MiddleName:
                case Xml.Contacts.MobilePhoneNumber:
                case Xml.Contacts.OfficeLocation:
                case Xml.Contacts.OtherAddressCity:
                case Xml.Contacts.OtherAddressCountry:
                case Xml.Contacts.OtherAddressPostalCode:
                case Xml.Contacts.OtherAddressState:
                case Xml.Contacts.OtherAddressStreet:
                case Xml.Contacts.PagerNumber:
                case Xml.Contacts.Picture:
                case Xml.Contacts.RadioPhoneNumber:
                case Xml.Contacts.Spouse:
                case Xml.Contacts.Suffix:
                case Xml.Contacts.Title:
                case Xml.Contacts.WebPage:
                case Xml.Contacts.YomiCompanyName:
                case Xml.Contacts.YomiFirstName:
                case Xml.Contacts.YomiLastName:
                case Xml.Contacts2.AccountName:
                case Xml.Contacts2.CompanyMainPhone:
                case Xml.Contacts2.CustomerId:
                case Xml.Contacts2.GovernmentId:
                case Xml.Contacts2.IMAddress2:
                case Xml.Contacts2.IMAddress3:
                case Xml.Contacts2.IMAddress:
                case Xml.Contacts2.MMS:
                case Xml.Contacts2.ManagerName:
                case Xml.Contacts2.NickName:
                    TrySetStringFromXml (c, child.Name.LocalName, child.Value);
                    break;
                default:
                    Console.WriteLine ("ParseContact UNHANDLED: " + child.Name.LocalName + " value=" + child.Value);
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
                System.Diagnostics.Trace.Assert (null != prop);
                if (typeof(string) != prop.PropertyType) {
                    Console.WriteLine ("TrySetStringFromXml: Property {0} is not string.", targetProp);
                    return;
                }
                prop.SetValue (targetObj, value);
            } catch (Exception e) {
                Console.WriteLine ("TrySetStringFromXml: Bad value {0} or property {1}:\n{2}.", value, targetProp, e);
            }
        }

        /// <summary>
        /// Tries the set int from xml.
        /// </summary>
        /// <param name="targetObj">Target object.</param>
        /// <param name="targetProp">Target property.</param>
        /// <param name="value">Value.</param>
        private void TrySetIntFromXml (object targetObj, string targetProp, string value)
        {
            try {
                var prop = targetObj.GetType ().GetProperty (targetProp);
                System.Diagnostics.Trace.Assert (null != prop);
                if (typeof(int) != prop.PropertyType) {
                    Console.WriteLine ("TrySetIntFromXml: Property {0} is not int.", targetProp);
                    return;
                }
                var numValue = int.Parse (value);
                prop.SetValue (targetObj, numValue);
            } catch (Exception e) {
                Console.WriteLine ("TrySetIntFromXml: Bad value {0} or property {1}:\n{2}.", value, targetProp, e);
            }
        }

        /// <summary>
        /// Tries the set date time from xml.
        /// </summary>
        /// <param name="targetObj">Target object.</param>
        /// <param name="targetProp">Target property.</param>
        /// <param name="value">Value.</param>
        private void TrySetDateTimeFromXml (object targetObj, string targetProp, string value)
        {
            try {
                var prop = targetObj.GetType ().GetProperty (targetProp);
                System.Diagnostics.Trace.Assert(null != prop);
                if (typeof(DateTime) != prop.PropertyType) {
                    Console.WriteLine ("TrySetDateTimeFromXml: Property {0} is not int.", targetProp);
                    return;
                }
                var dt = ParseAsDateTime (value);
                prop.SetValue (targetObj, dt);
            } catch (Exception e) {
                Console.WriteLine ("TrySetDateTimeFromXml: Bad value {0} or property {1}:\n{2}.", value, targetProp, e);
            }
        }

        private void TrySetCompactDateTimeFromXml (object targetObj, string targetProp, string value)
        {
            try {
                var prop = targetObj.GetType ().GetProperty (targetProp);
                System.Diagnostics.Trace.Assert(null != prop);
                if (typeof(DateTime) != prop.PropertyType) {
                    Console.WriteLine ("TrySetCompactDateTimeFromXml: Property {0} is not int.", targetProp);
                    return;
                }
                var dt = ParseAsCompactDateTime (value);
                prop.SetValue (targetObj, dt);
            } catch (Exception e) {
                Console.WriteLine ("TrySetCompactDateTimeFromXml: Bad value {0} or property {1}:\n{2}.", value, targetProp, e);
            }
        }
    }
}

