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
            NcXmlFilterNode node4 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // To
            node1 = new NcXmlFilterNode ("To", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> To
            // Cc
            node1 = new NcXmlFilterNode ("Cc", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Cc
            // From
            node1 = new NcXmlFilterNode ("From", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> From
            // Subject
            node1 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Subject
            // ReplyTo
            node1 = new NcXmlFilterNode ("ReplyTo", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ReplyTo
            // DateReceived
            node1 = new NcXmlFilterNode ("DateReceived", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> DateReceived
            // DisplayTo
            node1 = new NcXmlFilterNode ("DisplayTo", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> DisplayTo
            // ThreadTopic
            node1 = new NcXmlFilterNode ("ThreadTopic", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ThreadTopic
            // Importance
            node1 = new NcXmlFilterNode ("Importance", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Importance
            // Read
            node1 = new NcXmlFilterNode ("Read", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Read
            // MessageClass
            node1 = new NcXmlFilterNode ("MessageClass", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> MessageClass
            // MeetingRequest
            node1 = new NcXmlFilterNode ("MeetingRequest", RedactionType.NONE, RedactionType.NONE);
            // AllDayEvent
            node2 = new NcXmlFilterNode ("AllDayEvent", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> AllDayEvent
            // StartTime
            node2 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> StartTime
            // DtStamp
            node2 = new NcXmlFilterNode ("DtStamp", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> DtStamp
            // EndTime
            node2 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> EndTime
            // InstanceType
            node2 = new NcXmlFilterNode ("InstanceType", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> InstanceType
            // Location
            node2 = new NcXmlFilterNode ("Location", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> Location
            // Organizer
            node2 = new NcXmlFilterNode ("Organizer", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> Organizer
            // RecurrenceId
            node2 = new NcXmlFilterNode ("RecurrenceId", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> RecurrenceId
            // Reminder
            node2 = new NcXmlFilterNode ("Reminder", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> Reminder
            // ResponseRequested
            node2 = new NcXmlFilterNode ("ResponseRequested", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> ResponseRequested
            // Recurrences
            node2 = new NcXmlFilterNode ("Recurrences", RedactionType.NONE, RedactionType.NONE);
            // Recurrence
            node3 = new NcXmlFilterNode ("Recurrence", RedactionType.NONE, RedactionType.NONE);
            // Type
            node4 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Recurrence -> Type
            // Interval
            node4 = new NcXmlFilterNode ("Interval", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Recurrence -> Interval
            // Until
            node4 = new NcXmlFilterNode ("Until", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Recurrence -> Until
            // Occurrences
            node4 = new NcXmlFilterNode ("Occurrences", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Recurrence -> Occurrences
            // WeekOfMonth
            node4 = new NcXmlFilterNode ("WeekOfMonth", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Recurrence -> WeekOfMonth
            // DayOfMonth
            node4 = new NcXmlFilterNode ("DayOfMonth", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Recurrence -> DayOfMonth
            // DayOfWeek
            node4 = new NcXmlFilterNode ("DayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Recurrence -> DayOfWeek
            // MonthOfYear
            node4 = new NcXmlFilterNode ("MonthOfYear", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Recurrence -> MonthOfYear
            node2.Add(node3); // Recurrences -> Recurrence
            node1.Add(node2); // MeetingRequest -> Recurrences
            // Sensitivity
            node2 = new NcXmlFilterNode ("Sensitivity", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> Sensitivity
            // BusyStatus
            node2 = new NcXmlFilterNode ("BusyStatus", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> BusyStatus
            // TimeZone
            node2 = new NcXmlFilterNode ("TimeZone", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> TimeZone
            // GlobalObjId
            node2 = new NcXmlFilterNode ("GlobalObjId", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> GlobalObjId
            // DisallowNewTimeProposal
            node2 = new NcXmlFilterNode ("DisallowNewTimeProposal", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // MeetingRequest -> DisallowNewTimeProposal
            node0.Add(node1); // xml -> MeetingRequest
            // InternetCPID
            node1 = new NcXmlFilterNode ("InternetCPID", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> InternetCPID
            // Flag
            node1 = new NcXmlFilterNode ("Flag", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Flag -> Status
            // FlagType
            node2 = new NcXmlFilterNode ("FlagType", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Flag -> FlagType
            // CompleteTime
            node2 = new NcXmlFilterNode ("CompleteTime", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Flag -> CompleteTime
            node0.Add(node1); // xml -> Flag
            // ContentClass
            node1 = new NcXmlFilterNode ("ContentClass", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ContentClass
            // Categories
            node1 = new NcXmlFilterNode ("Categories", RedactionType.NONE, RedactionType.NONE);
            // Category
            node2 = new NcXmlFilterNode ("Category", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Categories -> Category
            node0.Add(node1); // xml -> Categories
            
            Root = node0;
        }
    }
}
