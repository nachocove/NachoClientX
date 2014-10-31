using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterMeetingResponseResponse : NcXmlFilter
    {
        public AsXmlFilterMeetingResponseResponse () : base ("MeetingResponse")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // MeetingResponse
            node1 = new NcXmlFilterNode ("MeetingResponse", RedactionType.NONE, RedactionType.NONE);
            // Result
            node2 = new NcXmlFilterNode ("Result", RedactionType.NONE, RedactionType.NONE);
            // RequestId
            node3 = new NcXmlFilterNode ("RequestId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Result -> RequestId
            // Status
            node3 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Result -> Status
            // CalendarId
            node3 = new NcXmlFilterNode ("CalendarId", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Result -> CalendarId
            node1.Add(node2); // MeetingResponse -> Result
            node0.Add(node1); // xml -> MeetingResponse
            
            Root = node0;
        }
    }
}
