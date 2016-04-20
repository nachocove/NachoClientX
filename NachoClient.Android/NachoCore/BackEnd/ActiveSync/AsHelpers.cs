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
        public static void ApplyAsXmlBody (this McAbstrItem item, XElement xmlBody)
        {
            var xmlType = xmlBody.ElementAnyNs (Xml.AirSyncBase.Type);
            var xmlData = xmlBody.ElementAnyNs (Xml.AirSyncBase.Data);
            var xmlEstimatedDataSize = xmlBody.ElementAnyNs (Xml.AirSyncBase.EstimatedDataSize);
            var xmlTruncated = xmlBody.ElementAnyNs (Xml.AirSyncBase.Truncated);
            var xmlPreview = xmlBody.ElementAnyNs (Xml.AirSyncBase.Preview);

            // An Exchange 2007 server might send a <Body> element without any <Data> element for calendar events
            // that have an empty description.  This should be treated as if it were an empty <Data> element.
            // The <Data> element can also be missing if a preview was requested.  We have to be careful to not
            // confuse the two cases.
            if (null == xmlData && null == xmlPreview &&
                null != xmlEstimatedDataSize && 0 == xmlEstimatedDataSize.Value.ToInt() &&
                (null == xmlTruncated || !ToBoolean(xmlTruncated.Value)))
            {
                // The easiest thing is to create an empty <Data> element, then let the normal processing happen.
                xmlData = new XElement (Xml.AirSyncBase.Data, "");
            }

            if (null != xmlPreview) {
                item.BodyPreview = xmlPreview.Value;
            }
            if (null != xmlData) {
                McBody body;
                var typeCode = xmlType.Value.ToEnum<Xml.AirSync.TypeCode> ();
                var bodyType = typeCode.ToBodyType ();
                var saveAttr = xmlData.Attributes ().Where (x => x.Name == "nacho-body-id").SingleOrDefault ();
                if (null != saveAttr) {
                    item.BodyId = int.Parse (saveAttr.Value);
                    body = McBody.QueryById<McBody> (item.BodyId);
                    NcAssert.NotNull (body);
                } else if (0 == item.BodyId) {
                    body = McBody.InsertFile (item.AccountId, bodyType, xmlData.Value); 
                    item.BodyId = body.Id;
                } else {
                    body = McBody.QueryById<McBody> (item.BodyId);
                    body.UpdateData (xmlData.Value);
                }
                body.BodyType = bodyType;
                if ((null != xmlTruncated) && ToBoolean (xmlTruncated.Value)) {
                    body.Truncated = true;
                    body.FilePresence = McAbstrFileDesc.FilePresenceEnum.Complete;
                } else {
                    body.Truncated = false;
                    body.FilePresence = McAbstrFileDesc.FilePresenceEnum.Complete;
                }
                if (null != xmlEstimatedDataSize) {
                    body.FileSize = xmlEstimatedDataSize.Value.ToInt ();
                    body.FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate;
                }
                body.Update ();

                MimeHelpers.PossiblyExtractAttachmentsFromBody (body, item);
            } else {
                item.BodyId = 0;
            }
        }

        /// <summary>
        /// Set the item's preview based on the Body element in the response.
        /// Use the Preview element if it exists.  Otherwise, extract the first
        /// 255 characters out of the body.  The item's Body field is not changed
        /// at all.  The body in the response is used only to generate a preview.
        /// It will not be treated as the real message body.
        /// </summary>
        public static void ExtractPreviewFromXmlBody (this McAbstrItem item, XElement xmlBody)
        {
            var xmlData = xmlBody.ElementAnyNs (Xml.AirSyncBase.Data);
            var xmlPreview = xmlBody.ElementAnyNs (Xml.AirSyncBase.Preview);

            if (null != xmlPreview) {
                item.BodyPreview = xmlPreview.Value;
            }
            if (null != xmlData) {
                string bodyText;
                var saveAttr = xmlData.Attributes ().Where (x => x.Name == "nacho-body-id").SingleOrDefault ();
                if (null != saveAttr) {
                    var body = McBody.QueryById<McBody> (int.Parse (saveAttr.Value));
                    NcAssert.NotNull (body);
                    bodyText = body.GetContentsString ();
                    // Now that we have looked at the contents, get rid of the McBody.
                    body.Delete ();
                } else {
                    bodyText = xmlData.Value;
                }
                if (null == xmlPreview) {
                    item.BodyPreview = bodyText;
                }
            }
            if (null != item.BodyPreview) {
                const int TruncationSize = 500;
                // AWS ignores trunc size & returns the full body
                if ((2*TruncationSize) < item.BodyPreview.Length) {
                    Log.Warn (Log.LOG_AS, "Body preview too big: {0}", item.BodyPreview.Length);
                    item.BodyPreview = item.BodyPreview.Substring (0, TruncationSize);
                }
            }
        }

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

        public static McAbstrFileDesc.BodyTypeEnum ToBodyType (this Xml.AirSync.TypeCode typeCode)
        {
            switch (typeCode) {
            case Xml.AirSync.TypeCode.PlainText_1:
                return McAbstrFileDesc.BodyTypeEnum.PlainText_1;
            case Xml.AirSync.TypeCode.Html_2:
                return McAbstrFileDesc.BodyTypeEnum.HTML_2;
            case Xml.AirSync.TypeCode.Rtf_3:
                return McAbstrFileDesc.BodyTypeEnum.RTF_3;
            case Xml.AirSync.TypeCode.Mime_4:
                return McAbstrFileDesc.BodyTypeEnum.MIME_4;
            default:
                NcAssert.CaseError ();
                return McAbstrFileDesc.BodyTypeEnum.PlainText_1;
            }
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

        public static XElement ToXmlApplicationData (McCalendar cal, IBEContext beContext, bool includeBody = true)
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
            if (cal.ReminderIsSet) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.Reminder, cal.Reminder));
            }

            xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.Timezone, cal.TimeZone));

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
            if (cal.MeetingStatusIsSet) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.MeetingStatus, (uint)cal.MeetingStatus));
            }
            if (null != cal.OnlineMeetingConfLink) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.OnlineMeetingConfLink, cal.OnlineMeetingConfLink));
            }
            if (null != cal.OnlineMeetingExternalLink) {
                xmlAppData.Add (new XElement (CalendarNs + Xml.Calendar.OnlineMeetingExternalLink, cal.OnlineMeetingExternalLink));
            }

            if (includeBody && 0 != cal.BodyId) {
                var body = McBody.QueryById<McBody> (cal.BodyId);
                NcAssert.True (null != body);
                xmlAppData.Add (new XElement (AirSyncBaseNs + Xml.AirSyncBase.Body,
                    new XElement (AirSyncBaseNs + Xml.AirSyncBase.Type, (uint)body.BodyType),
                    new XElement (AirSyncBaseNs + Xml.AirSyncBase.Data, body.GetContentsString ())));
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
                    xmlAttendees.Add (xmlAttendee);
                }
                xmlAppData.Add (xmlAttendees);
            }
            if (0 != cal.categories.Count) {
                var xmlCategories = new XElement (CalendarNs + Xml.Calendar.Calendar_Categories);
                foreach (var category in cal.categories) {
                    xmlCategories.Add (new XElement (CalendarNs + Xml.Calendar.Category, category.Name));
                }
                xmlAppData.Add (xmlCategories);
            }
            if (0 != cal.recurrences.Count) {
                foreach (var recurrence in cal.recurrences) {
                    var xmlRecurrence = new XElement (CalendarNs + Xml.Calendar.Calendar_Recurrence);
                    xmlRecurrence.Add (new XElement (CalendarNs + Xml.Calendar.Recurrence.Type, (int)recurrence.Type));
                    if (recurrence.IntervalIsSet) {
                        xmlRecurrence.Add (new XElement (CalendarNs + Xml.Calendar.Recurrence.Interval, recurrence.Interval));
                    }
                    if (recurrence.OccurrencesIsSet) {
                        xmlRecurrence.Add (new XElement (CalendarNs + Xml.Calendar.Recurrence.Occurrences, recurrence.Occurrences));
                    }
                    if (DateTime.MinValue != recurrence.Until) {
                        xmlRecurrence.Add (new XElement (CalendarNs + Xml.Calendar.Recurrence.Until, recurrence.Until.ToString (CompactDateTimeFmt1)));
                    }
                    if (NcRecurrenceType.Monthly == recurrence.Type || NcRecurrenceType.Yearly == recurrence.Type) {
                        xmlRecurrence.Add (new XElement (CalendarNs + Xml.Calendar.Recurrence.DayOfMonth, recurrence.DayOfMonth));
                    }
                    if (NcRecurrenceType.Yearly == recurrence.Type || NcRecurrenceType.YearlyOnDay == recurrence.Type) {
                        xmlRecurrence.Add (new XElement (CalendarNs + Xml.Calendar.Recurrence.MonthOfYear, recurrence.MonthOfYear));
                    }
                    if (NcRecurrenceType.MonthlyOnDay == recurrence.Type || NcRecurrenceType.YearlyOnDay == recurrence.Type) {
                        xmlRecurrence.Add (new XElement (CalendarNs + Xml.Calendar.Recurrence.WeekOfMonth, recurrence.WeekOfMonth));
                    }
                    if (recurrence.DayOfWeekIsSet) {
                        xmlRecurrence.Add (new XElement (CalendarNs + Xml.Calendar.Recurrence.DayOfWeek, (int)recurrence.DayOfWeek));
                    }
                    if (recurrence.FirstDayOfWeekIsSet) {
                        xmlRecurrence.Add (new XElement (CalendarNs + Xml.Calendar.Recurrence.FirstDayOfWeek, recurrence.FirstDayOfWeek));
                    }
                    xmlAppData.Add (xmlRecurrence);
                }
            }
            if (0 != cal.exceptions.Count) {
                var xmlExceptions = new XElement (CalendarNs + Xml.Calendar.Calendar_Exceptions);
                foreach (var exception in cal.exceptions) {
                    var xmlException = new XElement (CalendarNs + Xml.Calendar.Exceptions.Exception);
                    if (0 != exception.Deleted) {
                        // The exception for a deleted occurrence must contain only the Deleted and
                        // ExceptionStartTime elements.
                        xmlException.Add (new XElement (CalendarNs + Xml.Calendar.Exception.Deleted, "1"));
                        if (DateTime.MinValue != exception.ExceptionStartTime) {
                            xmlException.Add (new XElement (CalendarNs + Xml.Calendar.Exception.ExceptionStartTime, exception.ExceptionStartTime.ToString (CompactDateTimeFmt1)));
                        }
                    } else {
                        if (0 != exception.attendees.Count) {
                            var xmlAttendees = new XElement (CalendarNs + Xml.Calendar.Exception.Attendees);
                            foreach (var attendee in exception.attendees) {
                                var xmlAttendee = new XElement (CalendarNs + Xml.Calendar.Attendees.Attendee);
                                xmlAttendee.Add (new XElement (CalendarNs + Xml.Calendar.Email, attendee.Email));
                                xmlAttendee.Add (new XElement (CalendarNs + Xml.Calendar.Name, attendee.Name));
                                if (attendee.AttendeeTypeIsSet) {
                                    xmlAttendee.Add (new XElement (CalendarNs + Xml.Calendar.AttendeeType, (uint)attendee.AttendeeType));
                                }
                                xmlAttendees.Add (xmlAttendee);
                            }
                            xmlException.Add (xmlAttendees);
                        }
                        if (0 != exception.categories.Count) {
                            var xmlCategories = new XElement (CalendarNs + Xml.Calendar.Exception.Categories);
                            foreach (var category in exception.categories) {
                                xmlCategories.Add (new XElement (CalendarNs + Xml.Calendar.Category, category.Name));
                            }
                            xmlException.Add (xmlCategories);
                        }
                        if (exception.AllDayEventIsSet) {
                            xmlException.Add (new XElement (CalendarNs + Xml.Calendar.Exception.AllDayEvent, exception.AllDayEvent ? "1" : "0"));
                        }
                        if (exception.BusyStatusIsSet) {
                            xmlException.Add (new XElement (CalendarNs + Xml.Calendar.Exception.BusyStatus, (int)exception.BusyStatus));
                        }
                        if (0 != exception.Deleted) {
                            xmlException.Add (new XElement (CalendarNs + Xml.Calendar.Exception.Deleted, "1"));
                        }
                        if (exception.MeetingStatusIsSet) {
                            xmlException.Add (new XElement (CalendarNs + Xml.Calendar.Exception.MeetingStatus, (int)exception.MeetingStatus));
                        }
                        if (exception.ReminderIsSet) {
                            xmlException.Add (new XElement (CalendarNs + Xml.Calendar.Exception.Reminder, exception.Reminder));
                        }
                        if (exception.SensitivityIsSet) {
                            xmlException.Add (new XElement (CalendarNs + Xml.Calendar.Exception.Sensitivity, (int)exception.Sensitivity));
                        }
                        if (DateTime.MinValue != exception.DtStamp) {
                            xmlException.Add (new XElement (CalendarNs + Xml.Calendar.Exception.DtStamp, exception.DtStamp.ToString (CompactDateTimeFmt1)));
                        }
                        if (DateTime.MinValue != exception.StartTime) {
                            xmlException.Add (new XElement (CalendarNs + Xml.Calendar.Exception.StartTime, exception.StartTime.ToString (CompactDateTimeFmt1)));
                        }
                        if (DateTime.MinValue != exception.EndTime) {
                            xmlException.Add (new XElement (CalendarNs + Xml.Calendar.Exception.EndTime, exception.EndTime.ToString (CompactDateTimeFmt1)));
                        }
                        if (DateTime.MinValue != exception.ExceptionStartTime) {
                            xmlException.Add (new XElement (CalendarNs + Xml.Calendar.Exception.ExceptionStartTime, exception.ExceptionStartTime.ToString (CompactDateTimeFmt1)));
                        }
                        if (null != exception.Subject) {
                            xmlException.Add (new XElement (CalendarNs + Xml.Calendar.Exception.Subject, exception.Subject));
                        }
                        if (null != exception.Location) {
                            xmlException.Add (new XElement (CalendarNs + Xml.Calendar.Exception.Location, exception.Location));
                        }
                        // TODO Body.  Sending a body for an exception is complicated.  Just leave the body out for now,
                        // which means the exception will inherit its body from the main calendar event.
                    }
                    xmlExceptions.Add (xmlException);
                }
                xmlAppData.Add (xmlExceptions);
            }

            if (cal.ResponseRequestedIsSet && 14.0 <= Convert.ToDouble (beContext.ProtocolState.AsProtocolVersion, System.Globalization.CultureInfo.InvariantCulture)) {
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
        // TODO: Make sure we don't have extra fields
        public List<McAttendee> ParseAttendees (int accountId, XNamespace ns, XElement attendees)
        {
            NcAssert.True (null != attendees);
            NcAssert.True (attendees.Name.LocalName.Equals (Xml.Calendar.Calendar_Attendees));

            var list = new List<McAttendee> ();

            foreach (var attendee in attendees.Elements()) {
                NcAssert.True (attendee.Name.LocalName.Equals (Xml.Calendar.Attendees.Attendee));

                // Required.  But protect agains a misbehaving server.
                string name = "";
                var nameElement = attendee.Element (ns + Xml.Calendar.Attendee.Name);
                if (null != nameElement) {
                    name = nameElement.Value;
                }

                // Required.  But we have seen a case where it appears to be missing, so protect against that.
                string email = "";
                var emailElement = attendee.Element (ns + Xml.Calendar.Attendee.Email);
                if (null != emailElement) {
                    email = emailElement.Value;
                }

                // Optional
                NcAttendeeStatus status = NcAttendeeStatus.NotResponded;
                var statusElement = attendee.Element (ns + Xml.Calendar.Attendee.AttendeeStatus);
                if (null != statusElement) {
                    status = statusElement.Value.ToEnum<NcAttendeeStatus> ();
                }

                // Optional.  The default value is "Required".  (At least that's how GFE behaves.)
                NcAttendeeType type = NcAttendeeType.Required;
                var typeElement = attendee.Element (ns + Xml.Calendar.Attendee.AttendeeType);
                if (null != typeElement) {
                    type = typeElement.Value.ToEnum<NcAttendeeType> ();
                }

                // McAttendee barfs if both Name and Email are empty.
                if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(email)) {
                    name = "Unknown";
                }

                var a = new McAttendee (accountId, name, email, type, status);
                list.Add (a);
            }
            return list;
        }

        /// <returns>
        /// A list of categories not yet associated with an NcCalendar or NcException. Not null.
        /// </returns>
        public List<McCalendarCategory> ParseCategories (int accountId, XNamespace ns, XElement categories)
        {
            NcAssert.True (null != categories);
            NcAssert.True (categories.Name.LocalName.Equals (Xml.Calendar.Calendar_Categories));

            var list = new List<McCalendarCategory> ();

            foreach (var category in categories.Elements()) {
                NcAssert.True (category.Name.LocalName.Equals (Xml.Calendar.Categories.Category));
                var n = new McCalendarCategory (accountId, category.Value);
                list.Add (n);
            }
            return list;
        }

        public static List<McEmailMessageCategory> ParseEmailCategories (int accountId, XNamespace ns, XElement categories)
        {
            var list = new List<McEmailMessageCategory> ();
            if (categories.Elements ().Count () != 0) {

                NcAssert.True (null != categories);
                NcAssert.True (categories.Name.LocalName.Equals (Xml.Email.Categories));

                foreach (var category in categories.Elements()) {
                    NcAssert.True (category.Name.LocalName.Equals (Xml.Email.Category));
                    var n = new McEmailMessageCategory (accountId, category.Value);
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
        public McRecurrence ParseRecurrence (int accountId, XNamespace ns, XElement recurrence, string LocalName)
        {
            NcAssert.True (null != recurrence);
            NcAssert.True (recurrence.Name.LocalName.Equals (LocalName));

            var r = new McRecurrence (accountId);
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
                    r.DayOfWeekIsSet = true;
                    break;
                case Xml.Calendar.Recurrence.FirstDayOfWeek:
                    r.FirstDayOfWeek = child.Value.ToInt ();
                    r.FirstDayOfWeekIsSet = true;
                    break;
                case Xml.Calendar.Recurrence.Interval:
                    r.Interval = child.Value.ToInt ();
                    r.IntervalIsSet = true;
                    break;
                case Xml.Calendar.Recurrence.IsLeapMonth:
                    r.isLeapMonth = child.Value.ToBoolean ();
                    break;
                case Xml.Calendar.Recurrence.MonthOfYear:
                    r.MonthOfYear = child.Value.ToInt ();
                    break;
                case Xml.Calendar.Recurrence.Occurrences:
                    r.Occurrences = int.Parse (child.Value);
                    r.OccurrencesIsSet = true;
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

        public List<McException> ParseExceptions (int accountId, XNamespace ns, XElement exceptions)
        {
            NcAssert.True (null != exceptions);
            NcAssert.True (exceptions.Name.LocalName.Equals (Xml.Calendar.Calendar_Exceptions));

            var l = new List<McException> ();

            DateTime oldExceptionCutoff = DateTime.UtcNow - TimeSpan.FromDays (31);

            foreach (var exception in exceptions.Elements()) {
                if (Xml.Calendar.Exceptions.Exception != exception.Name.LocalName) {
                    Log.Warn (Log.LOG_AS, "ParseExceptions: Unexpected XML element <{0}>. Should be <{1}>.",
                        exception.Name.LocalName, Xml.Calendar.Exceptions.Exception);
                    continue;
                }
                var startTimeXml = exception.ElementAnyNs (Xml.Calendar.Exception.ExceptionStartTime);
                if (null != startTimeXml) {
                    var startTime = ParseAsCompactDateTime (startTimeXml.Value);
                    if (startTime < oldExceptionCutoff && startTime != DateTime.MinValue) {
                        // This exception is too old to be shown on the calendar.  Ignore it.
                        continue;
                    }
                }
                var e = new McException ();
                e.AccountId = accountId;
                var attendees = new List<McAttendee> ();
                var categories = new List<McCalendarCategory> ();
                foreach (var child in exception.Elements()) {
                    switch (child.Name.LocalName) {
                    // Containers
                    case Xml.Calendar.Exception.Attendees:
                        attendees.AddRange (ParseAttendees (accountId, ns, child));
                        break;
                    case Xml.Calendar.Exception.Categories:
                        categories.AddRange (ParseCategories (accountId, ns, child));
                        break;
                    // Elements
                    case Xml.Calendar.Exception.AllDayEvent:
                        e.AllDayEvent = child.Value.ToBoolean ();
                        e.AllDayEventIsSet = true;
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
                        if (!String.IsNullOrEmpty (child.Value)) {
                            e.ReminderIsSet = true;
                            e.Reminder = child.Value.ToUint ();
                        }
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
                        e.ApplyAsXmlBody (child);
                        if (0 != e.BodyId) {
                            McBody body = McBody.QueryById<McBody> (e.BodyId);
                            if (McBody.FilePresenceEnum.Complete == body.FilePresence && McBody.BodyTypeEnum.MIME_4 == body.BodyType) {
                                e.SetServerAttachments (MimeHelpers.LoadMessage (body));
                            }
                        }
                        break;
                    case Xml.AirSyncBase.NativeBodyType:
                        NcAssert.CaseError (); // Docs claim this doesn't exist
                        break;
                    default:
                        Log.Warn (Log.LOG_AS, "ParseExceptions: Unhandled element <{0}> with value={1}", child.Name.LocalName, child.Value);
                        break;
                    }
                }
                e.attendees = attendees;
                e.categories = categories;
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
        public NcResult ParseCalendar (int accountId, XNamespace ns, XElement command)
        {
            // <ServerId>..</ServerId>
            var serverId = command.Element (ns + Xml.AirSync.ServerId);
            NcAssert.True (null != serverId);

            McCalendar c = new McCalendar ();
            c.AccountId = accountId;
            c.ServerId = serverId.Value;

            var attendees = new List<McAttendee> ();
            var categories = new List<McCalendarCategory> ();
            var recurrences = new List<McRecurrence> ();
            List<McException> exceptions = null;

            XNamespace nsCalendar = "Calendar";
            // <ApplicationData>...</ApplicationData>
            var applicationData = command.Element (ns + Xml.AirSync.ApplicationData);
            NcAssert.True (null != applicationData);

            Log.Debug (Log.LOG_XML, "ParseCalendar\n{0}", applicationData);
            foreach (var child in applicationData.Elements()) {
                switch (child.Name.LocalName) {
                // Containers
                case Xml.Calendar.Calendar_Attendees:
                    attendees.AddRange (ParseAttendees (accountId, nsCalendar, child));
                    break;
                case Xml.Calendar.Calendar_Categories:
                    categories.AddRange (ParseCategories (accountId, nsCalendar, child));
                    break;
                case Xml.Calendar.Calendar_Exceptions:
                    if (null == exceptions) {
                        exceptions = new List<McException> ();
                    }
                    exceptions.AddRange (ParseExceptions (accountId, nsCalendar, child));
                    break;
                case Xml.Calendar.Calendar_Recurrence:
                    recurrences.Add (ParseRecurrence (accountId, nsCalendar, child, Xml.Calendar.Calendar_Recurrence));
                    break;
                case Xml.AirSyncBase.Body:
                    c.ApplyAsXmlBody (child);
                    if (0 != c.BodyId) {
                        McBody body = McBody.QueryById<McBody> (c.BodyId);
                        if (McBody.FilePresenceEnum.Complete == body.FilePresence && McBody.BodyTypeEnum.MIME_4 == body.BodyType) {
                            c.SetServerAttachments (MimeHelpers.LoadMessage (body));
                        }
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
                        c.ReminderIsSet = true;
                        c.Reminder = child.Value.ToUint ();
                    }
                    break;
                case Xml.Calendar.ResponseRequested:
                    c.ResponseRequested = child.Value.ToBoolean ();
                    c.ResponseRequestedIsSet = true;
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

            if (null == c.OrganizerEmail) {
                // AWS leaves out the OrganizerEmail field for calendar items owned by the current user.
                // Other servers always include the OrganizerEmail field.  The code assumes that OrganizerEmail
                // is always set (since AWS is the most recent server to be supported).  So make sure it
                // is always set.
                c.OrganizerEmail = McAccount.QueryById<McAccount>(accountId).EmailAddr;
            }

            c.attendees = attendees;
            c.categories = categories;
            c.recurrences = recurrences;
            if (null != exceptions) {
                c.exceptions = exceptions;
            }
            return NcResult.OK (c);
        }

        public NcResult ParseEmail (XNamespace ns, XElement command, McFolder folder)
        {
            var serverId = command.Element (ns + Xml.AirSync.ServerId);
            NcAssert.True (null != serverId);

            McEmailMessage emailMessage = McAbstrFolderEntry.QueryByServerId<McEmailMessage> (folder.AccountId, serverId.Value);

            if (null == emailMessage) {
                emailMessage = new McEmailMessage () {
                    ServerId = serverId.Value,
                    AccountId = folder.AccountId,
                };
            }

            var appData = command.Element (ns + Xml.AirSync.ApplicationData);
            NcAssert.NotNull (appData);

            XNamespace nsEmail = "Email";

            emailMessage.xmlAttachments = null;

            var categories = new List<McEmailMessageCategory> ();

            foreach (var child in appData.Elements()) {
                switch (child.Name.LocalName) {
                case Xml.AirSyncBase.Attachments:
                    emailMessage.xmlAttachments = child.Elements (m_baseNs + Xml.AirSyncBase.Attachment);
                    emailMessage.cachedHasAttachments = true;
                    break;
                case Xml.AirSyncBase.Body:
                    emailMessage.ExtractPreviewFromXmlBody (child);
                    break;

                case Xml.Email.Flag:
                    // Implicit deletes: If an element is not present within the Flag container element
                    // in a request or response, then the corresponding property is deleted. (MS-ASEMAIL)
                    emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Cleared;
                    emailMessage.FlagType = null;
                    emailMessage.FlagStartDate = DateTime.MinValue;
                    emailMessage.FlagUtcStartDate = DateTime.MinValue;
                    emailMessage.FlagDue = DateTime.MinValue;
                    emailMessage.FlagUtcDue = DateTime.MinValue;
                    emailMessage.FlagReminderSet = false;
                    emailMessage.FlagReminderTime = DateTime.MinValue;
                    emailMessage.FlagCompleteTime = DateTime.MinValue;
                    emailMessage.FlagDateCompleted = DateTime.MinValue;
                    emailMessage.FlagOrdinalDate = DateTime.MinValue;
                    emailMessage.FlagSubOrdinalDate = DateTime.MinValue;
                    if (child.HasElements) {
                        foreach (var flagPart in child.Elements()) {
                            switch (flagPart.Name.LocalName) {
                            case Xml.Email.Status:
                                try {
                                    uint statusValue = uint.Parse (flagPart.Value);
                                    if (2 < statusValue) {
                                        Log.Error (Log.LOG_AS, "Illegal Status value {0}", statusValue);
                                    } else {
                                        emailMessage.FlagStatus = statusValue;
                                    }
                                } catch {
                                    Log.Error (Log.LOG_AS, "Illegal Status value {0}", flagPart.Value);
                                }
                                break;

                            case Xml.Email.FlagType:
                                emailMessage.FlagType = flagPart.Value;
                                break;

                            case Xml.Tasks.StartDate:
                                try {
                                    emailMessage.FlagStartDate = ParseAsDateTime (flagPart.Value);
                                } catch {
                                    Log.Error (Log.LOG_AS, "Illegal StartDate value {0}", flagPart.Value);
                                }
                                break;

                            case Xml.Tasks.UtcStartDate:
                                try {
                                    emailMessage.FlagUtcStartDate = ParseAsDateTime (flagPart.Value);
                                } catch {
                                    Log.Error (Log.LOG_AS, "Illegal UtcStartDate value {0}", flagPart.Value);
                                }
                                break;

                            case Xml.Tasks.DueDate:
                                try {
                                    emailMessage.FlagDue = ParseAsDateTime (flagPart.Value);
                                } catch {
                                    Log.Error (Log.LOG_AS, "Illegal DueDate value {0}", flagPart.Value);
                                }
                                break;

                            case Xml.Tasks.UtcDueDate:
                                try {
                                    emailMessage.FlagUtcDue = ParseAsDateTime (flagPart.Value);
                                } catch {
                                    Log.Error (Log.LOG_AS, "Illegal UtcDueDate value {0}", flagPart.Value);
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
                                        Log.Error (Log.LOG_AS, "Illegal ReminderSet value {0}", flagPart.Value);
                                    }
                                } catch {
                                    Log.Error (Log.LOG_AS, "Illegal ReminderSet value {0}", flagPart.Value);
                                }
                                break;

                            case Xml.Tasks.Subject:
                                // Ignore. This SHOULD be the same as the message Subject.
                                break;

                            case Xml.Tasks.ReminderTime:
                                try {
                                    emailMessage.FlagReminderTime = ParseAsDateTime (flagPart.Value);
                                } catch {
                                    Log.Error (Log.LOG_AS, "Illegal ReminderTime value {0}", flagPart.Value);
                                }
                                break;

                            case Xml.Email.CompleteTime:
                                try {
                                    emailMessage.FlagCompleteTime = ParseAsDateTime (flagPart.Value);
                                } catch {
                                    Log.Error (Log.LOG_AS, "Illegal CompleteTime value {0}", flagPart.Value);
                                }
                                break;

                            case Xml.Tasks.DateCompleted:
                                try {
                                    emailMessage.FlagDateCompleted = ParseAsDateTime (flagPart.Value);
                                } catch {
                                    Log.Error (Log.LOG_AS, "Illegal DateCompleted value {0}", flagPart.Value);
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
                    if (!String.IsNullOrEmpty (emailMessage.Subject) && (emailMessage.Subject != child.Value)) {
                        Log.Error (Log.LOG_AS, "Subject overwritten with changed value: serverId={0} {1} {2}", emailMessage.ServerId, emailMessage.Subject, child.Value);
                    }
                    if (child.Value.StartsWith ("Synchronization with your") && child.Value.Contains ("failed for")) {
                        Log.Error (Log.LOG_AS, "Server reports that synchronization failed. The user was notified via an e-mail message.");
                    }
                    emailMessage.Subject = child.Value;
                    break;

                case Xml.Email.DateReceived:
                    try {
                        emailMessage.DateReceived = ParseAsDateTime (child.Value);
                    } catch {
                        Log.Error (Log.LOG_AS, "Illegal DateReceived value {0}", child.Value);
                    }
                    break;
                case Xml.Email.DisplayTo:
                    emailMessage.DisplayTo = child.Value;
                    break;
                case Xml.Email.Importance:
                    try {
                        emailMessage.Importance = (NcImportance)uint.Parse (child.Value);
                    } catch {
                        Log.Error (Log.LOG_AS, "Illegal Importance value {0}", child.Value);
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
                    categories.AddRange (AsHelpers.ParseEmailCategories (folder.AccountId, nsEmail, child));
                    break;
                case Xml.Email.MeetingRequest:
                    if (child.HasElements) {
                        var e = new  McMeetingRequest ();
                        var recurrences = new List<McRecurrence> ();
                        foreach (var meetingRequestPart in child.Elements()) {
                            switch (meetingRequestPart.Name.LocalName) {
                            case Xml.Email.AllDayEvent:
                                e.AllDayEvent = meetingRequestPart.Value.ToBoolean ();
                                break;
                            case Xml.Email.StartTime:
                            case Xml.Email.DtStamp:
                            case Xml.Email.EndTime:
                            case Xml.Email.RecurrenceId:
                                TrySetDateTimeFromXml (e, meetingRequestPart.Name.LocalName, meetingRequestPart.Value);
                                break;
                            case Xml.Email.InstanceType:
                                e.InstanceType = meetingRequestPart.Value.ParseInteger<NcInstanceType> ();
                                break;
                            case Xml.Email.Location:
                            case Xml.Email.Organizer:
                            case Xml.Email.GlobalObjId:
                                TrySetStringFromXml (e, meetingRequestPart.Name.LocalName, meetingRequestPart.Value);
                                break;
                            case Xml.Email.Reminder:
                                if (!String.IsNullOrEmpty (meetingRequestPart.Value)) {
                                    e.ReminderIsSet = true;
                                    e.Reminder = meetingRequestPart.Value.ToUint ();
                                }
                                break;
                            case Xml.Email.ResponseRequested:
                                e.ResponseRequested = meetingRequestPart.Value.ToBoolean ();
                                e.ResponseRequestedIsSet = true;
                                break;
                            case Xml.Email.Recurrences:
                                if (meetingRequestPart.HasElements) {
                                    foreach (var recurrencePart in meetingRequestPart.Elements()) {
                                        var recurrence = ParseRecurrence (folder.AccountId, nsEmail, recurrencePart, Xml.Email.Recurrence);
                                        recurrences.Add (recurrence);
                                    }
                                }
                                break;
                            case Xml.Email.Sensitivity:
                                e.Sensitivity = meetingRequestPart.Value.ParseInteger<NcSensitivity> ();
                                e.SensitivityIsSet = true;
                                break;
                            case Xml.Email.BusyStatus:
                                e.BusyStatus = meetingRequestPart.Value.ToEnum<NcBusyStatus> ();
                                e.BusyStatusIsSet = true;
                                break;
                            case Xml.Email.TimeZone:
                                e.TimeZone = meetingRequestPart.Value;
                                break;
                            case Xml.Email.DisallowNewTimeProposal:
                                e.DisallowNewTimeProposal = meetingRequestPart.Value.ToBoolean ();
                                break;
                            case Xml.Email.MeetingMessageType:
                                e.MeetingMessageType = meetingRequestPart.Value.ParseInteger<NcMeetingMessageType> ();
                                break;
                            default:
                                Log.Warn (Log.LOG_AS, "ProcessEmailItem MeetingRequest UNHANDLED: " + meetingRequestPart.Name.LocalName + " value=" + child.Value);
                                break;
                            }
                        }
                        e.recurrences = recurrences;
                        emailMessage.MeetingRequest = e;
                    }
                    break;
                case Xml.Email2.LastVerbExecuted:
                    emailMessage.LastVerbExecuted = child.Value.ToInt ();
                    break;
                case Xml.Email2.LastVerbExecutionTime:
                    emailMessage.LastVerbExecutionTime = ParseAsDateTime (child.Value);
                    break;
                default:
                    Log.Warn (Log.LOG_AS, "ProcessEmailItem UNHANDLED: " + child.Name.LocalName + " value=" + child.Value);
                    break;
                }
            }
            emailMessage.Categories = categories;
            if (null == emailMessage.ConversationId) {
                emailMessage.ConversationId = System.Guid.NewGuid ().ToString ();
            }
            if (null != emailMessage.MeetingRequest) {
                // AWS Exchange servers include several flags in all meeting request messages.
                // Those flags mess up the app's handling of the messages, causing the messages
                // to not show up in the inbox if the meeting is in the future.  (And having the
                // meeting request message appear only after the meeting is over is not user-friendly
                // behavior.)  To work around this quirky server behavior, ignore the flags in all
                // messages that have a MeetingRequest element.
                emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Cleared;
                emailMessage.FlagType = null;
                emailMessage.FlagStartDate = DateTime.MinValue;
                emailMessage.FlagUtcStartDate = DateTime.MinValue;
                emailMessage.FlagDue = DateTime.MinValue;
                emailMessage.FlagUtcDue = DateTime.MinValue;
                emailMessage.FlagReminderSet = false;
                emailMessage.FlagReminderTime = DateTime.MinValue;
                emailMessage.FlagCompleteTime = DateTime.MinValue;
                emailMessage.FlagDateCompleted = DateTime.MinValue;
                emailMessage.FlagOrdinalDate = DateTime.MinValue;
                emailMessage.FlagSubOrdinalDate = DateTime.MinValue;
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
            // TODO https://github.com/nachocove/NachoClientX/issues/473
            return true;
        }

        public static bool TimeOrLocationChanged (XElement command, string serverId)
        {
            // TODO https://github.com/nachocove/NachoClientX/issues/474
            return false;
        }

        public void InsertAttachments (McEmailMessage msg)
        {
            XNamespace email2Ns = Xml.Email2.Ns;
            if (null != msg.xmlAttachments) {
                foreach (XElement xmlAttachment in msg.xmlAttachments) {
                    // Create & save the attachment record.
                    var attachment = new McAttachment {
                        AccountId = msg.AccountId,
                        FileSize = long.Parse (xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.EstimatedDataSize).Value),
                        FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                        FileReference = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.FileReference).Value,
                        Method = uint.Parse (xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.Method).Value),
                    };
                    var displayName = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.DisplayName);
                    if (null != displayName) {
                        attachment.SetDisplayName (displayName.Value);
                    }
                    var contentLocation = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.ContentLocation);
                    if (null != contentLocation) {
                        attachment.ContentLocation = contentLocation.Value;
                    }
                    var contentId = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.ContentId);
                    if (null != contentId) {
                        attachment.ContentId = contentId.Value;
                    }
                    var contentType = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.ContentType);
                    if (null != contentType) {
                        attachment.ContentType = contentType.Value;
                    } else if (displayName != null) {
                        attachment.ContentType = MimeKit.MimeTypes.GetMimeType (displayName.Value);
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
                    NcModel.Instance.RunInTransaction (() => {
                        attachment.Insert ();
                        attachment.Link (msg);
                    });
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

        /// <summary>
        /// ActiveSync delivers RTF bodies in a base-64 compressed format. This method converts
        /// from that format to normal RTF.
        /// </summary>
        /// <returns>Normal uncompressed RTF.</returns>
        /// <param name="base64Compressed">The base-64 compressed RTF.</param>
        public static string Base64CompressedRtfToNormalRtf (string base64Compressed)
        {
            try {
                byte[] compressed = Convert.FromBase64String (base64Compressed);
                // The algorithm to decompress the RTF is too complex to be worth implementing ourselves.
                // Fortunately, MimeKit has a converter that is usable, even though it doesn't look like it
                // was designed to be used outside of MimeKit.
                var compressedToNormalConverter = new MimeKit.Tnef.RtfCompressedToRtf ();
                int normalIndex;
                int normalLength;
                byte[] normalBytes = compressedToNormalConverter.Filter (compressed, 0, compressed.Length, out normalIndex, out normalLength);
                return System.Text.Encoding.UTF8.GetString (normalBytes, normalIndex, normalLength);
            } catch (FormatException) {
                Log.Warn (Log.LOG_UTILS, "Base-64 compressed RTF string is not a valid base-64 format.");
                return base64Compressed;
            } catch (Exception e) {
                Log.Warn (Log.LOG_UTILS, "Conversion of base-64 compressed RTF to normal RTF failed: {0}", e.ToString ());
                return base64Compressed;
            }
        }
    }
}

