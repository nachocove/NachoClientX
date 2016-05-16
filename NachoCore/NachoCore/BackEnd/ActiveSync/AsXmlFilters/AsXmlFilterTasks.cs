using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterTasks : NcXmlFilter
    {
        public AsXmlFilterTasks () : base ("Tasks")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // Subject
            node1 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Subject
            // Importance
            node1 = new NcXmlFilterNode ("Importance", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Importance
            // UtcStartDate
            node1 = new NcXmlFilterNode ("UtcStartDate", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> UtcStartDate
            // StartDate
            node1 = new NcXmlFilterNode ("StartDate", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> StartDate
            // UtcDueDate
            node1 = new NcXmlFilterNode ("UtcDueDate", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> UtcDueDate
            // DueDate
            node1 = new NcXmlFilterNode ("DueDate", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> DueDate
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
            // Start
            node2 = new NcXmlFilterNode ("Start", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> Start
            // Until
            node2 = new NcXmlFilterNode ("Until", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> Until
            // Occurrences
            node2 = new NcXmlFilterNode ("Occurrences", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> Occurrences
            // Interval
            node2 = new NcXmlFilterNode ("Interval", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> Interval
            // DayOfWeek
            node2 = new NcXmlFilterNode ("DayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> DayOfWeek
            // DayOfMonth
            node2 = new NcXmlFilterNode ("DayOfMonth", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> DayOfMonth
            // WeekOfMonth
            node2 = new NcXmlFilterNode ("WeekOfMonth", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> WeekOfMonth
            // MonthOfYear
            node2 = new NcXmlFilterNode ("MonthOfYear", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> MonthOfYear
            // Regenerate
            node2 = new NcXmlFilterNode ("Regenerate", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> Regenerate
            // DeadOccur
            node2 = new NcXmlFilterNode ("DeadOccur", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Recurrence -> DeadOccur
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
            // Complete
            node1 = new NcXmlFilterNode ("Complete", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Complete
            // DateCompleted
            node1 = new NcXmlFilterNode ("DateCompleted", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> DateCompleted
            // Sensitivity
            node1 = new NcXmlFilterNode ("Sensitivity", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Sensitivity
            // ReminderTime
            node1 = new NcXmlFilterNode ("ReminderTime", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ReminderTime
            // ReminderSet
            node1 = new NcXmlFilterNode ("ReminderSet", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ReminderSet
            // OrdinalDate
            node1 = new NcXmlFilterNode ("OrdinalDate", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> OrdinalDate
            // SubOrdinalDate
            node1 = new NcXmlFilterNode ("SubOrdinalDate", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> SubOrdinalDate
            
            Root = node0;
        }
    }
}
