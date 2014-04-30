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

            // Timezone
            node0 = new NcXmlFilterNode ("Timezone", RedactionType.FULL, RedactionType.FULL);
            // AllDayEvent
            node0 = new NcXmlFilterNode ("AllDayEvent", RedactionType.FULL, RedactionType.FULL);
            // BusyStatus
            node0 = new NcXmlFilterNode ("BusyStatus", RedactionType.FULL, RedactionType.FULL);
            // OrganizerName
            node0 = new NcXmlFilterNode ("OrganizerName", RedactionType.FULL, RedactionType.FULL);
            // OrganizerEmail
            node0 = new NcXmlFilterNode ("OrganizerEmail", RedactionType.FULL, RedactionType.FULL);
            // DtStamp
            node0 = new NcXmlFilterNode ("DtStamp", RedactionType.FULL, RedactionType.FULL);
            // EndTime
            node0 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            // Location
            node0 = new NcXmlFilterNode ("Location", RedactionType.FULL, RedactionType.FULL);
            // Reminder
            node0 = new NcXmlFilterNode ("Reminder", RedactionType.FULL, RedactionType.FULL);
            // Sensitivity
            node0 = new NcXmlFilterNode ("Sensitivity", RedactionType.FULL, RedactionType.FULL);
            // Subject
            node0 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            // StartTime
            node0 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            // UID
            node0 = new NcXmlFilterNode ("UID", RedactionType.FULL, RedactionType.FULL);
            // MeetingStatus
            node0 = new NcXmlFilterNode ("MeetingStatus", RedactionType.FULL, RedactionType.FULL);
            // Attendees
            node0 = new NcXmlFilterNode ("Attendees", RedactionType.NONE, RedactionType.NONE);
            // Attendee
            node1 = new NcXmlFilterNode ("Attendee", RedactionType.NONE, RedactionType.NONE);
            // Email
            node2 = new NcXmlFilterNode ("Email", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attendee -> Email
            // Name
            node2 = new NcXmlFilterNode ("Name", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attendee -> Name
            // AttendeeStatus
            node2 = new NcXmlFilterNode ("AttendeeStatus", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attendee -> AttendeeStatus
            // AttendeeType
            node2 = new NcXmlFilterNode ("AttendeeType", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attendee -> AttendeeType
            node0.Add(node1); // Attendees -> Attendee
            // Categories
            node0 = new NcXmlFilterNode ("Categories", RedactionType.NONE, RedactionType.NONE);
            // Category
            node1 = new NcXmlFilterNode ("Category", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Categories -> Category
            // Recurrence
            node0 = new NcXmlFilterNode ("Recurrence", RedactionType.NONE, RedactionType.NONE);
            // Type
            node1 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Type
            // Occurrences
            node1 = new NcXmlFilterNode ("Occurrences", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Occurrences
            // Interval
            node1 = new NcXmlFilterNode ("Interval", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Interval
            // WeekOfMonth
            node1 = new NcXmlFilterNode ("WeekOfMonth", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> WeekOfMonth
            // DayOfWeek
            node1 = new NcXmlFilterNode ("DayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> DayOfWeek
            // MonthOfYear
            node1 = new NcXmlFilterNode ("MonthOfYear", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> MonthOfYear
            // Until
            node1 = new NcXmlFilterNode ("Until", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Until
            // DayOfMonth
            node1 = new NcXmlFilterNode ("DayOfMonth", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> DayOfMonth
            // CalendarType
            node1 = new NcXmlFilterNode ("CalendarType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> CalendarType
            // IsLeapMonth
            node1 = new NcXmlFilterNode ("IsLeapMonth", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> IsLeapMonth
            // FirstDayOfWeek
            node1 = new NcXmlFilterNode ("FirstDayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> FirstDayOfWeek
            // Exceptions
            node0 = new NcXmlFilterNode ("Exceptions", RedactionType.NONE, RedactionType.NONE);
            // Exception
            node1 = new NcXmlFilterNode ("Exception", RedactionType.NONE, RedactionType.NONE);
            // Deleted
            node2 = new NcXmlFilterNode ("Deleted", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> Deleted
            // ExceptionStartTime
            node2 = new NcXmlFilterNode ("ExceptionStartTime", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> ExceptionStartTime
            // Subject
            node2 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> Subject
            // StartTime
            node2 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> StartTime
            // EndTime
            node2 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> EndTime
            // Location
            node2 = new NcXmlFilterNode ("Location", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> Location
            // Categories
            node2 = new NcXmlFilterNode ("Categories", RedactionType.NONE, RedactionType.NONE);
            // Category
            node3 = new NcXmlFilterNode ("Category", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Categories -> Category
            node1.Add(node2); // Exception -> Categories
            // Sensitivity
            node2 = new NcXmlFilterNode ("Sensitivity", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> Sensitivity
            // BusyStatus
            node2 = new NcXmlFilterNode ("BusyStatus", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> BusyStatus
            // AllDayEvent
            node2 = new NcXmlFilterNode ("AllDayEvent", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> AllDayEvent
            // Reminder
            node2 = new NcXmlFilterNode ("Reminder", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> Reminder
            // DtStamp
            node2 = new NcXmlFilterNode ("DtStamp", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> DtStamp
            // MeetingStatus
            node2 = new NcXmlFilterNode ("MeetingStatus", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> MeetingStatus
            // Attendees
            node2 = new NcXmlFilterNode ("Attendees", RedactionType.NONE, RedactionType.NONE);
            // Attendee
            node3 = new NcXmlFilterNode ("Attendee", RedactionType.NONE, RedactionType.NONE);
            // Email
            node4 = new NcXmlFilterNode ("Email", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Attendee -> Email
            // Name
            node4 = new NcXmlFilterNode ("Name", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Attendee -> Name
            // AttendeeStatus
            node4 = new NcXmlFilterNode ("AttendeeStatus", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Attendee -> AttendeeStatus
            // AttendeeType
            node4 = new NcXmlFilterNode ("AttendeeType", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Attendee -> AttendeeType
            node2.Add(node3); // Attendees -> Attendee
            node1.Add(node2); // Exception -> Attendees
            // AppointmentReplyTime
            node2 = new NcXmlFilterNode ("AppointmentReplyTime", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> AppointmentReplyTime
            // ResponseType
            node2 = new NcXmlFilterNode ("ResponseType", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> ResponseType
            // OnlineMeetingConfLink
            node2 = new NcXmlFilterNode ("OnlineMeetingConfLink", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> OnlineMeetingConfLink
            // OnlineMeetingExternalLink
            node2 = new NcXmlFilterNode ("OnlineMeetingExternalLink", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> OnlineMeetingExternalLink
            node0.Add(node1); // Exceptions -> Exception
            // ResponseRequested
            node0 = new NcXmlFilterNode ("ResponseRequested", RedactionType.FULL, RedactionType.FULL);
            // AppointmentReplyTime
            node0 = new NcXmlFilterNode ("AppointmentReplyTime", RedactionType.FULL, RedactionType.FULL);
            // ResponseType
            node0 = new NcXmlFilterNode ("ResponseType", RedactionType.FULL, RedactionType.FULL);
            // DisallowNewTimeProposal
            node0 = new NcXmlFilterNode ("DisallowNewTimeProposal", RedactionType.FULL, RedactionType.FULL);
            // OnlineMeetingConfLink
            node0 = new NcXmlFilterNode ("OnlineMeetingConfLink", RedactionType.FULL, RedactionType.FULL);
            // OnlineMeetingExternalLink
            node0 = new NcXmlFilterNode ("OnlineMeetingExternalLink", RedactionType.FULL, RedactionType.FULL);
            // Timezone
            node0 = new NcXmlFilterNode ("Timezone", RedactionType.FULL, RedactionType.FULL);
            // AllDayEvent
            node0 = new NcXmlFilterNode ("AllDayEvent", RedactionType.FULL, RedactionType.FULL);
            // BusyStatus
            node0 = new NcXmlFilterNode ("BusyStatus", RedactionType.FULL, RedactionType.FULL);
            // OrganizerName
            node0 = new NcXmlFilterNode ("OrganizerName", RedactionType.FULL, RedactionType.FULL);
            // OrganizerEmail
            node0 = new NcXmlFilterNode ("OrganizerEmail", RedactionType.FULL, RedactionType.FULL);
            // DtStamp
            node0 = new NcXmlFilterNode ("DtStamp", RedactionType.FULL, RedactionType.FULL);
            // EndTime
            node0 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            // Location
            node0 = new NcXmlFilterNode ("Location", RedactionType.FULL, RedactionType.FULL);
            // Reminder
            node0 = new NcXmlFilterNode ("Reminder", RedactionType.FULL, RedactionType.FULL);
            // Sensitivity
            node0 = new NcXmlFilterNode ("Sensitivity", RedactionType.FULL, RedactionType.FULL);
            // Subject
            node0 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            // StartTime
            node0 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            // UID
            node0 = new NcXmlFilterNode ("UID", RedactionType.FULL, RedactionType.FULL);
            // MeetingStatus
            node0 = new NcXmlFilterNode ("MeetingStatus", RedactionType.FULL, RedactionType.FULL);
            // Attendees
            node0 = new NcXmlFilterNode ("Attendees", RedactionType.NONE, RedactionType.NONE);
            // Attendee
            node1 = new NcXmlFilterNode ("Attendee", RedactionType.NONE, RedactionType.NONE);
            // Email
            node2 = new NcXmlFilterNode ("Email", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attendee -> Email
            // Name
            node2 = new NcXmlFilterNode ("Name", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attendee -> Name
            // AttendeeStatus
            node2 = new NcXmlFilterNode ("AttendeeStatus", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attendee -> AttendeeStatus
            // AttendeeType
            node2 = new NcXmlFilterNode ("AttendeeType", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attendee -> AttendeeType
            node0.Add(node1); // Attendees -> Attendee
            // Categories
            node0 = new NcXmlFilterNode ("Categories", RedactionType.NONE, RedactionType.NONE);
            // Category
            node1 = new NcXmlFilterNode ("Category", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Categories -> Category
            // Recurrence
            node0 = new NcXmlFilterNode ("Recurrence", RedactionType.NONE, RedactionType.NONE);
            // Type
            node1 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Type
            // Occurrences
            node1 = new NcXmlFilterNode ("Occurrences", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Occurrences
            // Interval
            node1 = new NcXmlFilterNode ("Interval", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Interval
            // WeekOfMonth
            node1 = new NcXmlFilterNode ("WeekOfMonth", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> WeekOfMonth
            // DayOfWeek
            node1 = new NcXmlFilterNode ("DayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> DayOfWeek
            // MonthOfYear
            node1 = new NcXmlFilterNode ("MonthOfYear", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> MonthOfYear
            // Until
            node1 = new NcXmlFilterNode ("Until", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Until
            // DayOfMonth
            node1 = new NcXmlFilterNode ("DayOfMonth", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> DayOfMonth
            // CalendarType
            node1 = new NcXmlFilterNode ("CalendarType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> CalendarType
            // IsLeapMonth
            node1 = new NcXmlFilterNode ("IsLeapMonth", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> IsLeapMonth
            // FirstDayOfWeek
            node1 = new NcXmlFilterNode ("FirstDayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> FirstDayOfWeek
            // Exceptions
            node0 = new NcXmlFilterNode ("Exceptions", RedactionType.NONE, RedactionType.NONE);
            // Exception
            node1 = new NcXmlFilterNode ("Exception", RedactionType.NONE, RedactionType.NONE);
            // Deleted
            node2 = new NcXmlFilterNode ("Deleted", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> Deleted
            // ExceptionStartTime
            node2 = new NcXmlFilterNode ("ExceptionStartTime", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> ExceptionStartTime
            // Subject
            node2 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> Subject
            // StartTime
            node2 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> StartTime
            // EndTime
            node2 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> EndTime
            // Location
            node2 = new NcXmlFilterNode ("Location", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> Location
            // Categories
            node2 = new NcXmlFilterNode ("Categories", RedactionType.NONE, RedactionType.NONE);
            // Category
            node3 = new NcXmlFilterNode ("Category", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Categories -> Category
            node1.Add(node2); // Exception -> Categories
            // Sensitivity
            node2 = new NcXmlFilterNode ("Sensitivity", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> Sensitivity
            // BusyStatus
            node2 = new NcXmlFilterNode ("BusyStatus", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> BusyStatus
            // AllDayEvent
            node2 = new NcXmlFilterNode ("AllDayEvent", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> AllDayEvent
            // Reminder
            node2 = new NcXmlFilterNode ("Reminder", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> Reminder
            // DtStamp
            node2 = new NcXmlFilterNode ("DtStamp", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> DtStamp
            // MeetingStatus
            node2 = new NcXmlFilterNode ("MeetingStatus", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> MeetingStatus
            // Attendees
            node2 = new NcXmlFilterNode ("Attendees", RedactionType.NONE, RedactionType.NONE);
            // Attendee
            node3 = new NcXmlFilterNode ("Attendee", RedactionType.NONE, RedactionType.NONE);
            // Email
            node4 = new NcXmlFilterNode ("Email", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Attendee -> Email
            // Name
            node4 = new NcXmlFilterNode ("Name", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Attendee -> Name
            // AttendeeStatus
            node4 = new NcXmlFilterNode ("AttendeeStatus", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Attendee -> AttendeeStatus
            // AttendeeType
            node4 = new NcXmlFilterNode ("AttendeeType", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Attendee -> AttendeeType
            node2.Add(node3); // Attendees -> Attendee
            node1.Add(node2); // Exception -> Attendees
            // AppointmentReplyTime
            node2 = new NcXmlFilterNode ("AppointmentReplyTime", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> AppointmentReplyTime
            // ResponseType
            node2 = new NcXmlFilterNode ("ResponseType", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> ResponseType
            // OnlineMeetingConfLink
            node2 = new NcXmlFilterNode ("OnlineMeetingConfLink", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> OnlineMeetingConfLink
            // OnlineMeetingExternalLink
            node2 = new NcXmlFilterNode ("OnlineMeetingExternalLink", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Exception -> OnlineMeetingExternalLink
            node0.Add(node1); // Exceptions -> Exception
            // ResponseRequested
            node0 = new NcXmlFilterNode ("ResponseRequested", RedactionType.FULL, RedactionType.FULL);
            // AppointmentReplyTime
            node0 = new NcXmlFilterNode ("AppointmentReplyTime", RedactionType.FULL, RedactionType.FULL);
            // ResponseType
            node0 = new NcXmlFilterNode ("ResponseType", RedactionType.FULL, RedactionType.FULL);
            // DisallowNewTimeProposal
            node0 = new NcXmlFilterNode ("DisallowNewTimeProposal", RedactionType.FULL, RedactionType.FULL);
            // OnlineMeetingConfLink
            node0 = new NcXmlFilterNode ("OnlineMeetingConfLink", RedactionType.FULL, RedactionType.FULL);
            // OnlineMeetingExternalLink
            node0 = new NcXmlFilterNode ("OnlineMeetingExternalLink", RedactionType.FULL, RedactionType.FULL);
            // Timezone
            node0 = new NcXmlFilterNode ("Timezone", RedactionType.FULL, RedactionType.FULL);
            // AllDayEvent
            node0 = new NcXmlFilterNode ("AllDayEvent", RedactionType.FULL, RedactionType.FULL);
            // BusyStatus
            node0 = new NcXmlFilterNode ("BusyStatus", RedactionType.FULL, RedactionType.FULL);
            // OrganizerName
            node0 = new NcXmlFilterNode ("OrganizerName", RedactionType.FULL, RedactionType.FULL);
            // OrganizerEmail
            node0 = new NcXmlFilterNode ("OrganizerEmail", RedactionType.FULL, RedactionType.FULL);
            // DtStamp
            node0 = new NcXmlFilterNode ("DtStamp", RedactionType.FULL, RedactionType.FULL);
            // EndTime
            node0 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            // Location
            node0 = new NcXmlFilterNode ("Location", RedactionType.FULL, RedactionType.FULL);
            // Reminder
            node0 = new NcXmlFilterNode ("Reminder", RedactionType.FULL, RedactionType.FULL);
            // Sensitivity
            node0 = new NcXmlFilterNode ("Sensitivity", RedactionType.FULL, RedactionType.FULL);
            // Subject
            node0 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            // StartTime
            node0 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            // UID
            node0 = new NcXmlFilterNode ("UID", RedactionType.FULL, RedactionType.FULL);
            // MeetingStatus
            node0 = new NcXmlFilterNode ("MeetingStatus", RedactionType.FULL, RedactionType.FULL);
            // Attendees
            node0 = new NcXmlFilterNode ("Attendees", RedactionType.FULL, RedactionType.FULL);
            // Categories
            node0 = new NcXmlFilterNode ("Categories", RedactionType.FULL, RedactionType.FULL);
            // Recurrence
            node0 = new NcXmlFilterNode ("Recurrence", RedactionType.FULL, RedactionType.FULL);
            // Exceptions
            node0 = new NcXmlFilterNode ("Exceptions", RedactionType.FULL, RedactionType.FULL);
            // DisallowNewTimeProposal
            node0 = new NcXmlFilterNode ("DisallowNewTimeProposal", RedactionType.FULL, RedactionType.FULL);
            // ResponseRequested
            node0 = new NcXmlFilterNode ("ResponseRequested", RedactionType.FULL, RedactionType.FULL);
            // Timezone
            node0 = new NcXmlFilterNode ("Timezone", RedactionType.FULL, RedactionType.FULL);
            // StartTime
            node0 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            // EndTime
            node0 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            // Subject
            node0 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            // Location
            node0 = new NcXmlFilterNode ("Location", RedactionType.FULL, RedactionType.FULL);
            // Reminder
            node0 = new NcXmlFilterNode ("Reminder", RedactionType.FULL, RedactionType.FULL);
            // AllDayEvent
            node0 = new NcXmlFilterNode ("AllDayEvent", RedactionType.FULL, RedactionType.FULL);
            // BusyStatus
            node0 = new NcXmlFilterNode ("BusyStatus", RedactionType.FULL, RedactionType.FULL);
            // Recurrence
            node0 = new NcXmlFilterNode ("Recurrence", RedactionType.FULL, RedactionType.FULL);
            // Sensitivity
            node0 = new NcXmlFilterNode ("Sensitivity", RedactionType.FULL, RedactionType.FULL);
            // DtStamp
            node0 = new NcXmlFilterNode ("DtStamp", RedactionType.FULL, RedactionType.FULL);
            // Attendees
            node0 = new NcXmlFilterNode ("Attendees", RedactionType.FULL, RedactionType.FULL);
            // Categories
            node0 = new NcXmlFilterNode ("Categories", RedactionType.FULL, RedactionType.FULL);
            // MeetingStatus
            node0 = new NcXmlFilterNode ("MeetingStatus", RedactionType.FULL, RedactionType.FULL);
            // OrganizerName
            node0 = new NcXmlFilterNode ("OrganizerName", RedactionType.FULL, RedactionType.FULL);
            // OrganizerEmail
            node0 = new NcXmlFilterNode ("OrganizerEmail", RedactionType.FULL, RedactionType.FULL);
            // UID
            node0 = new NcXmlFilterNode ("UID", RedactionType.FULL, RedactionType.FULL);
            // DisallowNewTimeProposal
            node0 = new NcXmlFilterNode ("DisallowNewTimeProposal", RedactionType.FULL, RedactionType.FULL);
            // ResponseRequested
            node0 = new NcXmlFilterNode ("ResponseRequested", RedactionType.FULL, RedactionType.FULL);
            // Exceptions
            node0 = new NcXmlFilterNode ("Exceptions", RedactionType.FULL, RedactionType.FULL);
            
            Root = node0;
        }
    }
}
