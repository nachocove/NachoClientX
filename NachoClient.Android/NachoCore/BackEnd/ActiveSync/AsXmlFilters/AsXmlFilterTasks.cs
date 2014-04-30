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

            // Subject
            node0 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            // Importance
            node0 = new NcXmlFilterNode ("Importance", RedactionType.FULL, RedactionType.FULL);
            // UtcStartDate
            node0 = new NcXmlFilterNode ("UtcStartDate", RedactionType.FULL, RedactionType.FULL);
            // StartDate
            node0 = new NcXmlFilterNode ("StartDate", RedactionType.FULL, RedactionType.FULL);
            // UtcDueDate
            node0 = new NcXmlFilterNode ("UtcDueDate", RedactionType.FULL, RedactionType.FULL);
            // DueDate
            node0 = new NcXmlFilterNode ("DueDate", RedactionType.FULL, RedactionType.FULL);
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
            // Start
            node1 = new NcXmlFilterNode ("Start", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Start
            // Until
            node1 = new NcXmlFilterNode ("Until", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Until
            // Occurrences
            node1 = new NcXmlFilterNode ("Occurrences", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Occurrences
            // Interval
            node1 = new NcXmlFilterNode ("Interval", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Interval
            // DayOfWeek
            node1 = new NcXmlFilterNode ("DayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> DayOfWeek
            // DayOfMonth
            node1 = new NcXmlFilterNode ("DayOfMonth", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> DayOfMonth
            // WeekOfMonth
            node1 = new NcXmlFilterNode ("WeekOfMonth", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> WeekOfMonth
            // MonthOfYear
            node1 = new NcXmlFilterNode ("MonthOfYear", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> MonthOfYear
            // Regenerate
            node1 = new NcXmlFilterNode ("Regenerate", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Regenerate
            // DeadOccur
            node1 = new NcXmlFilterNode ("DeadOccur", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> DeadOccur
            // CalendarType
            node1 = new NcXmlFilterNode ("CalendarType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> CalendarType
            // IsLeapMonth
            node1 = new NcXmlFilterNode ("IsLeapMonth", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> IsLeapMonth
            // FirstDayOfWeek
            node1 = new NcXmlFilterNode ("FirstDayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> FirstDayOfWeek
            // Complete
            node0 = new NcXmlFilterNode ("Complete", RedactionType.FULL, RedactionType.FULL);
            // DateCompleted
            node0 = new NcXmlFilterNode ("DateCompleted", RedactionType.FULL, RedactionType.FULL);
            // Sensitivity
            node0 = new NcXmlFilterNode ("Sensitivity", RedactionType.FULL, RedactionType.FULL);
            // ReminderTime
            node0 = new NcXmlFilterNode ("ReminderTime", RedactionType.FULL, RedactionType.FULL);
            // ReminderSet
            node0 = new NcXmlFilterNode ("ReminderSet", RedactionType.FULL, RedactionType.FULL);
            // OrdinalDate
            node0 = new NcXmlFilterNode ("OrdinalDate", RedactionType.FULL, RedactionType.FULL);
            // SubOrdinalDate
            node0 = new NcXmlFilterNode ("SubOrdinalDate", RedactionType.FULL, RedactionType.FULL);
            // Subject
            node0 = new NcXmlFilterNode ("Subject", RedactionType.FULL, RedactionType.FULL);
            // Importance
            node0 = new NcXmlFilterNode ("Importance", RedactionType.FULL, RedactionType.FULL);
            // UtcStartDate
            node0 = new NcXmlFilterNode ("UtcStartDate", RedactionType.FULL, RedactionType.FULL);
            // StartDate
            node0 = new NcXmlFilterNode ("StartDate", RedactionType.FULL, RedactionType.FULL);
            // UtcDueDate
            node0 = new NcXmlFilterNode ("UtcDueDate", RedactionType.FULL, RedactionType.FULL);
            // DueDate
            node0 = new NcXmlFilterNode ("DueDate", RedactionType.FULL, RedactionType.FULL);
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
            // Start
            node1 = new NcXmlFilterNode ("Start", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Start
            // Until
            node1 = new NcXmlFilterNode ("Until", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Until
            // Occurrences
            node1 = new NcXmlFilterNode ("Occurrences", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Occurrences
            // Interval
            node1 = new NcXmlFilterNode ("Interval", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Interval
            // DayOfWeek
            node1 = new NcXmlFilterNode ("DayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> DayOfWeek
            // DayOfMonth
            node1 = new NcXmlFilterNode ("DayOfMonth", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> DayOfMonth
            // WeekOfMonth
            node1 = new NcXmlFilterNode ("WeekOfMonth", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> WeekOfMonth
            // MonthOfYear
            node1 = new NcXmlFilterNode ("MonthOfYear", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> MonthOfYear
            // Regenerate
            node1 = new NcXmlFilterNode ("Regenerate", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> Regenerate
            // DeadOccur
            node1 = new NcXmlFilterNode ("DeadOccur", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> DeadOccur
            // CalendarType
            node1 = new NcXmlFilterNode ("CalendarType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> CalendarType
            // IsLeapMonth
            node1 = new NcXmlFilterNode ("IsLeapMonth", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> IsLeapMonth
            // FirstDayOfWeek
            node1 = new NcXmlFilterNode ("FirstDayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Recurrence -> FirstDayOfWeek
            // Complete
            node0 = new NcXmlFilterNode ("Complete", RedactionType.FULL, RedactionType.FULL);
            // DateCompleted
            node0 = new NcXmlFilterNode ("DateCompleted", RedactionType.FULL, RedactionType.FULL);
            // Sensitivity
            node0 = new NcXmlFilterNode ("Sensitivity", RedactionType.FULL, RedactionType.FULL);
            // ReminderTime
            node0 = new NcXmlFilterNode ("ReminderTime", RedactionType.FULL, RedactionType.FULL);
            // ReminderSet
            node0 = new NcXmlFilterNode ("ReminderSet", RedactionType.FULL, RedactionType.FULL);
            // OrdinalDate
            node0 = new NcXmlFilterNode ("OrdinalDate", RedactionType.FULL, RedactionType.FULL);
            // SubOrdinalDate
            node0 = new NcXmlFilterNode ("SubOrdinalDate", RedactionType.FULL, RedactionType.FULL);
            
            Root = node0;
        }
    }
}
