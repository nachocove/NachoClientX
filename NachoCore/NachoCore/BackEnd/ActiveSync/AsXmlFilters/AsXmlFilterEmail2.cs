using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterEmail2 : NcXmlFilter
    {
        public AsXmlFilterEmail2 () : base ("Email2")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // UmCallerID
            node1 = new NcXmlFilterNode ("UmCallerID", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> UmCallerID
            // UmUserNotes
            node1 = new NcXmlFilterNode ("UmUserNotes", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> UmUserNotes
            // UmAttDuration
            node1 = new NcXmlFilterNode ("UmAttDuration", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> UmAttDuration
            // UmAttOrder
            node1 = new NcXmlFilterNode ("UmAttOrder", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> UmAttOrder
            // ConversationId
            node1 = new NcXmlFilterNode ("ConversationId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ConversationId
            // ConversationIndex
            node1 = new NcXmlFilterNode ("ConversationIndex", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ConversationIndex
            // LastVerbExecuted
            node1 = new NcXmlFilterNode ("LastVerbExecuted", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> LastVerbExecuted
            // LastVerbExecutionTime
            node1 = new NcXmlFilterNode ("LastVerbExecutionTime", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> LastVerbExecutionTime
            // ReceivedAsBcc
            node1 = new NcXmlFilterNode ("ReceivedAsBcc", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ReceivedAsBcc
            // Sender
            node1 = new NcXmlFilterNode ("Sender", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Sender
            // CalendarType
            node1 = new NcXmlFilterNode ("CalendarType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> CalendarType
            // IsLeapMonth
            node1 = new NcXmlFilterNode ("IsLeapMonth", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> IsLeapMonth
            // AccountId
            node1 = new NcXmlFilterNode ("AccountId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> AccountId
            // FirstDayOfWeek
            node1 = new NcXmlFilterNode ("FirstDayOfWeek", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> FirstDayOfWeek
            // MeetingMessageType
            node1 = new NcXmlFilterNode ("MeetingMessageType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> MeetingMessageType
            
            Root = node0;
        }
    }
}
