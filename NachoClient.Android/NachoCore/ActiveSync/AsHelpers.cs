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

        public static int ToInt(this string intString)
        {
            return int.Parse (intString);
        }

        public static uint ToUint(this string intString)
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
        // DecodeAsTimeZone
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
        // ExtractStringFromAsTimeZone
        // The value of this field is an array of 32 WCHARs
        // It contains an optional description for standard time.
        // Any unused WCHARs in the array MUST be set to 0x0000.
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

        // TODO: Handle missing name & email better
        // TODO: Make sure we don't have extra fields
        public List<NcAttendee> ParseAttendees(XNamespace ns, XElement attendees)
        {
            System.Diagnostics.Debug.Assert (attendees.Name.LocalName.Equals (Xml.Calendar.Attendees.ElementName));

            var list = new List<NcAttendee> ();

            foreach(var attendee in attendees.Elements()) {
                System.Diagnostics.Debug.Assert(attendee.Name.LocalName.Equals(Xml.Calendar.Attendee.ElementName));

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
                var typeElement = attendee.Element(ns + Xml.Calendar.Attendee.AttendeeType);
                if (null != typeElement) {
                    type = typeElement.Value.ToEnum<NcAttendeeType> ();
                }

                var a = new NcAttendee (0, name, email);
                a.AttendeeStatus = status;
                a.AttendeeType = type;
                list.Add (a);
            }
            return list;
        }

        public NcRecurrenceType ToNcRecurrenceType (int i)
        {
            return (NcRecurrenceType)i;
        }
        // CreateNcCalendarFromXML
        // <Body xmlns="AirSyncBase:"> <Type> 1 </Type> <Data> </Data> </Body>
        // <DTStamp xmlns="Calendar:"> 20131123T190243Z </DTStamp>
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
        public NcResult CreateNcCalendarFromXML (XNamespace ns, XElement applicationData)
        {
            NcCalendar c = new NcCalendar ();

            Log.Info (Log.LOG_CALENDAR, "CreateNcCalendarFromXML\n{0}", applicationData.ToString ());
            foreach (var child in applicationData.Elements()) {
                switch (child.Name.LocalName) {
                case Xml.Calendar.AllDayEvent:
                    c.AllDayEvent = child.Value.ToBoolean ();
                    break;
                case Xml.Calendar.AppointmentReplyTime:
                    c.AppointmentReplyTime = ParseAsCompactDateTime (child.Value);
                    break;
                case Xml.Calendar.Attendees.ElementName:
                    c.attendees = ParseAttendees (child.GetDefaultNamespace(), child);
                    break;
//                case Xml.Calendar.airsyncbase:Body:
//                    break
                case Xml.Calendar.BusyStatus:
                    c.BusyStatus = child.Value.ToEnum<NcBusyStatus> ();
                    break;
                case Xml.Calendar.CalendarType:
                    c.CalendarType = child.Value.ToEnum<NcCalendarType> ();
                    break;
//                case Xml.Calendar.Categories:
//                    break;
                case Xml.Calendar.DayOfMonth:
                    c.DayOfMonth = child.Value.ToInt ();
                    break;
                case Xml.Calendar.DayOfWeek:
                    c.DayOfWeek = child.Value.ToEnum<NcDayOfWeek> ();
                    break;
                case Xml.Calendar.Deleted:
                    c.Deleted = child.Value.ToUint ();
                    break;
                case Xml.Calendar.DisallowNewTimeProposal:
                    c.DisallowNewTimeProposal = child.Value.ToBoolean ();
                    break;
                case Xml.Calendar.DtStamp:
                    c.DTStamp = ParseAsCompactDateTime (child.Value);
                    break;
                case Xml.Calendar.EndTime:
                    c.EndTime = ParseAsCompactDateTime (child.Value);
                    break;
//                case Xml.Calendar.Exception:
//                    break;
                case Xml.Calendar.ExceptionStartTime:
                    c.ExceptionStartTime = ParseAsCompactDateTime (child.Value);
                    break;
//                case Xml.Calendar.Exceptions:
//                    break;
                case Xml.Calendar.FirstDayOfWeek:
                    c.FirstDayOfWeek = child.Value.ToInt ();
                    break;
                case Xml.Calendar.Interval:
                    c.Interval = child.Value.ToInt ();
                    break;
                case Xml.Calendar.IsLeapMonth:
                    c.isLeapMonth = child.Value.ToBoolean ();
                    break;
                case Xml.Calendar.Location:
                    c.Location = child.Value;
                    break;
                case Xml.Calendar.MeetingStatus:
                    c.MeetingStatus = child.Value.ParseInteger<NcMeetingStatus> ();
                    break;
                case Xml.Calendar.MonthOfYear:
                    c.MonthOfYear = child.Value.ToInt ();
                    break;
//                case Xml.Calendar.airsyncbase:NativeBodyType:
//                    break;
                case Xml.Calendar.Occurrences:
                    c.Occurences = int.Parse (child.Value);
                    break;
                case Xml.Calendar.OnlineMeetingConfLink:
                    c.OnlineMeetingConfLink = child.Value;
                    break;
                case Xml.Calendar.OnlineMeetingExternalLink:
                    c.OnlineMeetingExternalLink = child.Value;
                    break;
                case Xml.Calendar.OrganizerEmail:
                    c.OrganizerEmail = child.Value;
                    break;
                case Xml.Calendar.OrganizerName:
                    c.OrganizerName = child.Value;
                    break;
//                case Xml.Calendar.Recurrence:
//                    break;
                case Xml.Calendar.Reminder:
                    c.Reminder = child.Value.ToUint();
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
                case Xml.Calendar.StartTime:
                    c.StartTime = ParseAsCompactDateTime (child.Value);
                    break;
                case Xml.Calendar.Subject:
                    c.Subject = child.Value;
                    break;
//                case Xml.Calendar.Timezone:
//                    stringValue = child.Value;
//                    NcTimeZone tz = ParseAsTimeZone (stringValue);
//                    break;
                case Xml.Calendar.Type:
                    c.Type = child.Value.ParseInteger<NcRecurrenceType> ();
                    break;
                case Xml.Calendar.UID:
                    c.UID = child.Value;
                    break;
                case Xml.Calendar.Until:
                    c.Until = ParseAsCompactDateTime (child.Value);
                    break;
                case Xml.Calendar.WeekOfMonth:
                    c.WeekOfMonth = child.Value.ToInt();
                    break;
                default:
                    Console.WriteLine ("CreateNcCalendarFromXML UNHANDLED: " + child.Name.LocalName + " value=" + child.Value);
                    break;
                }
            }


            return NcResult.OK (c);
        }
    }
}

