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

            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // UmCallerID
            node1 = new NcXmlFilterNode ("UmCallerID", RedactionType.FULL, RedactionType.FULL);
            // UmUserNotes
            node1 = new NcXmlFilterNode ("UmUserNotes", RedactionType.FULL, RedactionType.FULL);
            // UmAttDuration
            node1 = new NcXmlFilterNode ("UmAttDuration", RedactionType.FULL, RedactionType.FULL);
            // UmAttOrder
            node1 = new NcXmlFilterNode ("UmAttOrder", RedactionType.FULL, RedactionType.FULL);
            // ConversationId
            node1 = new NcXmlFilterNode ("ConversationId", RedactionType.FULL, RedactionType.FULL);
            // ConversationIndex
            node1 = new NcXmlFilterNode ("ConversationIndex", RedactionType.FULL, RedactionType.FULL);
            // LastVerbExecuted
            node1 = new NcXmlFilterNode ("LastVerbExecuted", RedactionType.FULL, RedactionType.FULL);
            // LastVerbExecutionTime
            node1 = new NcXmlFilterNode ("LastVerbExecutionTime", RedactionType.FULL, RedactionType.FULL);
            // ReceivedAsBcc
            node1 = new NcXmlFilterNode ("ReceivedAsBcc", RedactionType.FULL, RedactionType.FULL);
            // Sender
            node1 = new NcXmlFilterNode ("Sender", RedactionType.FULL, RedactionType.FULL);
            // CalendarType
            node1 = new NcXmlFilterNode ("CalendarType", RedactionType.FULL, RedactionType.FULL);
            // IsLeapMonth
            node1 = new NcXmlFilterNode ("IsLeapMonth", RedactionType.FULL, RedactionType.FULL);
            // AccountId
            node1 = new NcXmlFilterNode ("AccountId", RedactionType.FULL, RedactionType.FULL);
            // FirstDayOfWeek
            node1 = new NcXmlFilterNode ("FirstDayOfWeek", RedactionType.FULL, RedactionType.FULL);
            // MeetingMessageType
            node1 = new NcXmlFilterNode ("MeetingMessageType", RedactionType.FULL, RedactionType.FULL);
            // UmCallerID
            node1 = new NcXmlFilterNode ("UmCallerID", RedactionType.FULL, RedactionType.FULL);
            // UmUserNotes
            node1 = new NcXmlFilterNode ("UmUserNotes", RedactionType.FULL, RedactionType.FULL);
            // UmAttDuration
            node1 = new NcXmlFilterNode ("UmAttDuration", RedactionType.FULL, RedactionType.FULL);
            // UmAttOrder
            node1 = new NcXmlFilterNode ("UmAttOrder", RedactionType.FULL, RedactionType.FULL);
            // ConversationId
            node1 = new NcXmlFilterNode ("ConversationId", RedactionType.FULL, RedactionType.FULL);
            // ConversationIndex
            node1 = new NcXmlFilterNode ("ConversationIndex", RedactionType.FULL, RedactionType.FULL);
            // LastVerbExecuted
            node1 = new NcXmlFilterNode ("LastVerbExecuted", RedactionType.FULL, RedactionType.FULL);
            // LastVerbExecutionTime
            node1 = new NcXmlFilterNode ("LastVerbExecutionTime", RedactionType.FULL, RedactionType.FULL);
            // ReceivedAsBcc
            node1 = new NcXmlFilterNode ("ReceivedAsBcc", RedactionType.FULL, RedactionType.FULL);
            // Sender
            node1 = new NcXmlFilterNode ("Sender", RedactionType.FULL, RedactionType.FULL);
            // CalendarType
            node1 = new NcXmlFilterNode ("CalendarType", RedactionType.FULL, RedactionType.FULL);
            // IsLeapMonth
            node1 = new NcXmlFilterNode ("IsLeapMonth", RedactionType.FULL, RedactionType.FULL);
            // AccountId
            node1 = new NcXmlFilterNode ("AccountId", RedactionType.FULL, RedactionType.FULL);
            // FirstDayOfWeek
            node1 = new NcXmlFilterNode ("FirstDayOfWeek", RedactionType.FULL, RedactionType.FULL);
            // MeetingMessageType
            node1 = new NcXmlFilterNode ("MeetingMessageType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1);
            
            Root = node0;
        }
    }
}
