using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterCalendar : NcXmlFilter
    {
        public AsXmlFilterCalendar () : base ("Calendar")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;
            NcXmlFilterNode node4 = null;
            NcXmlFilterNode node5 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // Timezone
            node1 = new NcXmlFilterNode ("Timezone", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Timezone
            // AllDayEvent
            node1 = new NcXmlFilterNode ("AllDayEvent", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> AllDayEvent
            // BusyStatus
            node1 = new NcXmlFilterNode ("BusyStatus", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> BusyStatus
            // OrganizerName
            node1 = new NcXmlFilterNode ("OrganizerName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> OrganizerName
            // OrganizerEmail
            node1 = new NcXmlFilterNode ("OrganizerEmail", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> OrganizerEmail
            // DtStamp
            node1 = new NcXmlFilterNode ("DtStamp", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> DtStamp
            // EndTime
            node1 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> EndTime
            // Location
            node1 = new NcXmlFilterNode ("Location", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Location
            // Reminder
            node1 = new NcXmlFilterNode ("Reminder", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Reminder
            // Sensitivity
            node1 = new NcXmlFilterNode ("Sensitivity", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Sensitivity
            // Subject
            node1 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Subject
            // StartTime
            node1 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> StartTime
            // UID
            node1 = new NcXmlFilterNode ("UID", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> UID
            // MeetingStatus
            node1 = new NcXmlFilterNode ("MeetingStatus", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> MeetingStatus
            // Attendees
            node1 = new NcXmlFilterNode ("Attendees", RedactionType.NONE, RedactionType.NONE);
            // Attendee
            node2 = new NcXmlFilterNode ("Attendee", RedactionType.NONE, RedactionType.NONE);
            // Email
            node3 = new NcXmlFilterNode ("Email", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attendee -> Email
            // Name
            node3 = new NcXmlFilterNode ("Name", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attendee -> Name
            // AttendeeStatus
            node3 = new NcXmlFilterNode ("AttendeeStatus", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attendee -> AttendeeStatus
            // AttendeeType
            node3 = new NcXmlFilterNode ("AttendeeType", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attendee -> AttendeeType
            node1.Add(node2); // Attendees -> Attendee
            node0.Add(node1); // xml -> Attendees
            // Categories
            node1 = new NcXmlFilterNode ("Categories", RedactionType.NONE, RedactionType.NONE);
            // Category
            node2 = new NcXmlFilterNode ("Category", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Categories -> Category
            node0.Add(node1); // xml -> Categories
            // Recurrence
            node1 = new NcXmlFilterNode ("Recurrence", RedactionType.NONE, RedactionType.NONE);
            // Type
            node2 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> Type
            // Occurrences
            node2 = new NcXmlFilterNode ("Occurrences", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> Occurrences
            // Interval
            node2 = new NcXmlFilterNode ("Interval", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> Interval
            // WeekOfMonth
            node2 = new NcXmlFilterNode ("WeekOfMonth", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> WeekOfMonth
            // DayOfWeek
            node2 = new NcXmlFilterNode ("DayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> DayOfWeek
            // MonthOfYear
            node2 = new NcXmlFilterNode ("MonthOfYear", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> MonthOfYear
            // Until
            node2 = new NcXmlFilterNode ("Until", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> Until
            // DayOfMonth
            node2 = new NcXmlFilterNode ("DayOfMonth", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> DayOfMonth
            // CalendarType
            node2 = new NcXmlFilterNode ("CalendarType", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> CalendarType
            // IsLeapMonth
            node2 = new NcXmlFilterNode ("IsLeapMonth", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> IsLeapMonth
            // FirstDayOfWeek
            node2 = new NcXmlFilterNode ("FirstDayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> FirstDayOfWeek
            node0.Add(node1); // xml -> Recurrence
            // Exceptions
            node1 = new NcXmlFilterNode ("Exceptions", RedactionType.NONE, RedactionType.NONE);
            // Exception
            node2 = new NcXmlFilterNode ("Exception", RedactionType.NONE, RedactionType.NONE);
            // Deleted
            node3 = new NcXmlFilterNode ("Deleted", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> Deleted
            // ExceptionStartTime
            node3 = new NcXmlFilterNode ("ExceptionStartTime", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> ExceptionStartTime
            // Subject
            node3 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> Subject
            // StartTime
            node3 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> StartTime
            // EndTime
            node3 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> EndTime
            // Location
            node3 = new NcXmlFilterNode ("Location", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> Location
            // Categories
            node3 = new NcXmlFilterNode ("Categories", RedactionType.NONE, RedactionType.NONE);
            // Category
            node4 = new NcXmlFilterNode ("Category", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Categories -> Category
            node2.Add(node3); // Exception -> Categories
            // Sensitivity
            node3 = new NcXmlFilterNode ("Sensitivity", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> Sensitivity
            // BusyStatus
            node3 = new NcXmlFilterNode ("BusyStatus", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> BusyStatus
            // AllDayEvent
            node3 = new NcXmlFilterNode ("AllDayEvent", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> AllDayEvent
            // Reminder
            node3 = new NcXmlFilterNode ("Reminder", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> Reminder
            // DtStamp
            node3 = new NcXmlFilterNode ("DtStamp", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> DtStamp
            // MeetingStatus
            node3 = new NcXmlFilterNode ("MeetingStatus", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> MeetingStatus
            // Attendees
            node3 = new NcXmlFilterNode ("Attendees", RedactionType.NONE, RedactionType.NONE);
            // Attendee
            node4 = new NcXmlFilterNode ("Attendee", RedactionType.NONE, RedactionType.NONE);
            // Email
            node5 = new NcXmlFilterNode ("Email", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Attendee -> Email
            // Name
            node5 = new NcXmlFilterNode ("Name", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Attendee -> Name
            // AttendeeStatus
            node5 = new NcXmlFilterNode ("AttendeeStatus", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Attendee -> AttendeeStatus
            // AttendeeType
            node5 = new NcXmlFilterNode ("AttendeeType", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Attendee -> AttendeeType
            node3.Add(node4); // Attendees -> Attendee
            node2.Add(node3); // Exception -> Attendees
            // AppointmentReplyTime
            node3 = new NcXmlFilterNode ("AppointmentReplyTime", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> AppointmentReplyTime
            // ResponseType
            node3 = new NcXmlFilterNode ("ResponseType", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> ResponseType
            // OnlineMeetingConfLink
            node3 = new NcXmlFilterNode ("OnlineMeetingConfLink", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> OnlineMeetingConfLink
            // OnlineMeetingExternalLink
            node3 = new NcXmlFilterNode ("OnlineMeetingExternalLink", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Exception -> OnlineMeetingExternalLink
            node1.Add(node2); // Exceptions -> Exception
            node0.Add(node1); // xml -> Exceptions
            // ResponseRequested
            node1 = new NcXmlFilterNode ("ResponseRequested", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ResponseRequested
            // AppointmentReplyTime
            node1 = new NcXmlFilterNode ("AppointmentReplyTime", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> AppointmentReplyTime
            // ResponseType
            node1 = new NcXmlFilterNode ("ResponseType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ResponseType
            // DisallowNewTimeProposal
            node1 = new NcXmlFilterNode ("DisallowNewTimeProposal", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> DisallowNewTimeProposal
            // OnlineMeetingConfLink
            node1 = new NcXmlFilterNode ("OnlineMeetingConfLink", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> OnlineMeetingConfLink
            // OnlineMeetingExternalLink
            node1 = new NcXmlFilterNode ("OnlineMeetingExternalLink", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> OnlineMeetingExternalLink
            
            Root = node0;
        }
    }
}
