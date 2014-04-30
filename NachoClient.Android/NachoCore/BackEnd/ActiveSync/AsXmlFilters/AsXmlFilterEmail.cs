using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterEmail : NcXmlFilter
    {
        public AsXmlFilterEmail () : base ("Email")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;

            // To
            node0 = new NcXmlFilterNode ("To", RedactionType.FULL, RedactionType.FULL);
            // Cc
            node0 = new NcXmlFilterNode ("Cc", RedactionType.FULL, RedactionType.FULL);
            // From
            node0 = new NcXmlFilterNode ("From", RedactionType.FULL, RedactionType.FULL);
            // Subject
            node0 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            // ReplyTo
            node0 = new NcXmlFilterNode ("ReplyTo", RedactionType.FULL, RedactionType.FULL);
            // DateReceived
            node0 = new NcXmlFilterNode ("DateReceived", RedactionType.FULL, RedactionType.FULL);
            // DisplayTo
            node0 = new NcXmlFilterNode ("DisplayTo", RedactionType.FULL, RedactionType.FULL);
            // ThreadTopic
            node0 = new NcXmlFilterNode ("ThreadTopic", RedactionType.FULL, RedactionType.FULL);
            // Importance
            node0 = new NcXmlFilterNode ("Importance", RedactionType.FULL, RedactionType.FULL);
            // Read
            node0 = new NcXmlFilterNode ("Read", RedactionType.FULL, RedactionType.FULL);
            // MessageClass
            node0 = new NcXmlFilterNode ("MessageClass", RedactionType.FULL, RedactionType.FULL);
            // MeetingRequest
            node0 = new NcXmlFilterNode ("MeetingRequest", RedactionType.NONE, RedactionType.NONE);
            // AllDayEvent
            node1 = new NcXmlFilterNode ("AllDayEvent", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> AllDayEvent
            // StartTime
            node1 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> StartTime
            // DtStamp
            node1 = new NcXmlFilterNode ("DtStamp", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> DtStamp
            // EndTime
            node1 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> EndTime
            // InstanceType
            node1 = new NcXmlFilterNode ("InstanceType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> InstanceType
            // Location
            node1 = new NcXmlFilterNode ("Location", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> Location
            // Organizer
            node1 = new NcXmlFilterNode ("Organizer", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> Organizer
            // RecurrenceId
            node1 = new NcXmlFilterNode ("RecurrenceId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> RecurrenceId
            // Reminder
            node1 = new NcXmlFilterNode ("Reminder", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> Reminder
            // ResponseRequested
            node1 = new NcXmlFilterNode ("ResponseRequested", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> ResponseRequested
            // Recurrences
            node1 = new NcXmlFilterNode ("Recurrences", RedactionType.NONE, RedactionType.NONE);
            // Recurrence
            node2 = new NcXmlFilterNode ("Recurrence", RedactionType.NONE, RedactionType.NONE);
            // Type
            node3 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> Type
            // Interval
            node3 = new NcXmlFilterNode ("Interval", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> Interval
            // Until
            node3 = new NcXmlFilterNode ("Until", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> Until
            // Occurrences
            node3 = new NcXmlFilterNode ("Occurrences", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> Occurrences
            // WeekOfMonth
            node3 = new NcXmlFilterNode ("WeekOfMonth", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> WeekOfMonth
            // DayOfMonth
            node3 = new NcXmlFilterNode ("DayOfMonth", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> DayOfMonth
            // DayOfWeek
            node3 = new NcXmlFilterNode ("DayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> DayOfWeek
            // MonthOfYear
            node3 = new NcXmlFilterNode ("MonthOfYear", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> MonthOfYear
            node1.Add(node2); // Recurrences -> Recurrence
            node0.Add(node1); // MeetingRequest -> Recurrences
            // Sensitivity
            node1 = new NcXmlFilterNode ("Sensitivity", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> Sensitivity
            // BusyStatus
            node1 = new NcXmlFilterNode ("BusyStatus", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> BusyStatus
            // TimeZone
            node1 = new NcXmlFilterNode ("TimeZone", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> TimeZone
            // GlobalObjId
            node1 = new NcXmlFilterNode ("GlobalObjId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> GlobalObjId
            // DisallowNewTimeProposal
            node1 = new NcXmlFilterNode ("DisallowNewTimeProposal", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> DisallowNewTimeProposal
            // InternetCPID
            node0 = new NcXmlFilterNode ("InternetCPID", RedactionType.FULL, RedactionType.FULL);
            // Flag
            node0 = new NcXmlFilterNode ("Flag", RedactionType.NONE, RedactionType.NONE);
            // Status
            node1 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Flag -> Status
            // FlagType
            node1 = new NcXmlFilterNode ("FlagType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Flag -> FlagType
            // CompleteTime
            node1 = new NcXmlFilterNode ("CompleteTime", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Flag -> CompleteTime
            // ContentClass
            node0 = new NcXmlFilterNode ("ContentClass", RedactionType.FULL, RedactionType.FULL);
            // Categories
            node0 = new NcXmlFilterNode ("Categories", RedactionType.NONE, RedactionType.NONE);
            // Category
            node1 = new NcXmlFilterNode ("Category", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Categories -> Category
            // To
            node0 = new NcXmlFilterNode ("To", RedactionType.FULL, RedactionType.FULL);
            // Cc
            node0 = new NcXmlFilterNode ("Cc", RedactionType.FULL, RedactionType.FULL);
            // From
            node0 = new NcXmlFilterNode ("From", RedactionType.FULL, RedactionType.FULL);
            // Subject
            node0 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            // ReplyTo
            node0 = new NcXmlFilterNode ("ReplyTo", RedactionType.FULL, RedactionType.FULL);
            // DateReceived
            node0 = new NcXmlFilterNode ("DateReceived", RedactionType.FULL, RedactionType.FULL);
            // DisplayTo
            node0 = new NcXmlFilterNode ("DisplayTo", RedactionType.FULL, RedactionType.FULL);
            // ThreadTopic
            node0 = new NcXmlFilterNode ("ThreadTopic", RedactionType.FULL, RedactionType.FULL);
            // Importance
            node0 = new NcXmlFilterNode ("Importance", RedactionType.FULL, RedactionType.FULL);
            // Read
            node0 = new NcXmlFilterNode ("Read", RedactionType.FULL, RedactionType.FULL);
            // MessageClass
            node0 = new NcXmlFilterNode ("MessageClass", RedactionType.FULL, RedactionType.FULL);
            // MeetingRequest
            node0 = new NcXmlFilterNode ("MeetingRequest", RedactionType.NONE, RedactionType.NONE);
            // AllDayEvent
            node1 = new NcXmlFilterNode ("AllDayEvent", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> AllDayEvent
            // StartTime
            node1 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> StartTime
            // DtStamp
            node1 = new NcXmlFilterNode ("DtStamp", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> DtStamp
            // EndTime
            node1 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> EndTime
            // InstanceType
            node1 = new NcXmlFilterNode ("InstanceType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> InstanceType
            // Location
            node1 = new NcXmlFilterNode ("Location", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> Location
            // Organizer
            node1 = new NcXmlFilterNode ("Organizer", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> Organizer
            // RecurrenceId
            node1 = new NcXmlFilterNode ("RecurrenceId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> RecurrenceId
            // Reminder
            node1 = new NcXmlFilterNode ("Reminder", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> Reminder
            // ResponseRequested
            node1 = new NcXmlFilterNode ("ResponseRequested", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> ResponseRequested
            // Recurrences
            node1 = new NcXmlFilterNode ("Recurrences", RedactionType.NONE, RedactionType.NONE);
            // Recurrence
            node2 = new NcXmlFilterNode ("Recurrence", RedactionType.NONE, RedactionType.NONE);
            // Type
            node3 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> Type
            // Interval
            node3 = new NcXmlFilterNode ("Interval", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> Interval
            // Until
            node3 = new NcXmlFilterNode ("Until", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> Until
            // Occurrences
            node3 = new NcXmlFilterNode ("Occurrences", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> Occurrences
            // WeekOfMonth
            node3 = new NcXmlFilterNode ("WeekOfMonth", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> WeekOfMonth
            // DayOfMonth
            node3 = new NcXmlFilterNode ("DayOfMonth", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> DayOfMonth
            // DayOfWeek
            node3 = new NcXmlFilterNode ("DayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> DayOfWeek
            // MonthOfYear
            node3 = new NcXmlFilterNode ("MonthOfYear", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Recurrence -> MonthOfYear
            node1.Add(node2); // Recurrences -> Recurrence
            node0.Add(node1); // MeetingRequest -> Recurrences
            // Sensitivity
            node1 = new NcXmlFilterNode ("Sensitivity", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> Sensitivity
            // BusyStatus
            node1 = new NcXmlFilterNode ("BusyStatus", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> BusyStatus
            // TimeZone
            node1 = new NcXmlFilterNode ("TimeZone", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> TimeZone
            // GlobalObjId
            node1 = new NcXmlFilterNode ("GlobalObjId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> GlobalObjId
            // DisallowNewTimeProposal
            node1 = new NcXmlFilterNode ("DisallowNewTimeProposal", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // MeetingRequest -> DisallowNewTimeProposal
            // InternetCPID
            node0 = new NcXmlFilterNode ("InternetCPID", RedactionType.FULL, RedactionType.FULL);
            // Flag
            node0 = new NcXmlFilterNode ("Flag", RedactionType.NONE, RedactionType.NONE);
            // Status
            node1 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Flag -> Status
            // FlagType
            node1 = new NcXmlFilterNode ("FlagType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Flag -> FlagType
            // CompleteTime
            node1 = new NcXmlFilterNode ("CompleteTime", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Flag -> CompleteTime
            // ContentClass
            node0 = new NcXmlFilterNode ("ContentClass", RedactionType.FULL, RedactionType.FULL);
            // Categories
            node0 = new NcXmlFilterNode ("Categories", RedactionType.NONE, RedactionType.NONE);
            // Category
            node1 = new NcXmlFilterNode ("Category", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Categories -> Category
            // To
            node0 = new NcXmlFilterNode ("To", RedactionType.FULL, RedactionType.FULL);
            // Cc
            node0 = new NcXmlFilterNode ("Cc", RedactionType.FULL, RedactionType.FULL);
            // From
            node0 = new NcXmlFilterNode ("From", RedactionType.FULL, RedactionType.FULL);
            // ReplyTo
            node0 = new NcXmlFilterNode ("ReplyTo", RedactionType.FULL, RedactionType.FULL);
            // DateReceived
            node0 = new NcXmlFilterNode ("DateReceived", RedactionType.FULL, RedactionType.FULL);
            // Subject
            node0 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            // DisplayTo
            node0 = new NcXmlFilterNode ("DisplayTo", RedactionType.FULL, RedactionType.FULL);
            // Importance
            node0 = new NcXmlFilterNode ("Importance", RedactionType.FULL, RedactionType.FULL);
            // Read
            node0 = new NcXmlFilterNode ("Read", RedactionType.FULL, RedactionType.FULL);
            // MessageClass
            node0 = new NcXmlFilterNode ("MessageClass", RedactionType.FULL, RedactionType.FULL);
            // MeetingRequest
            node0 = new NcXmlFilterNode ("MeetingRequest", RedactionType.FULL, RedactionType.FULL);
            // ThreadTopic
            node0 = new NcXmlFilterNode ("ThreadTopic", RedactionType.FULL, RedactionType.FULL);
            // InternetCPID
            node0 = new NcXmlFilterNode ("InternetCPID", RedactionType.FULL, RedactionType.FULL);
            // DateReceived
            node0 = new NcXmlFilterNode ("DateReceived", RedactionType.FULL, RedactionType.FULL);
            
            Root = node0;
        }
    }
}
