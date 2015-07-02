using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterMeetingResponseRequest : NcXmlFilter
    {
        public AsXmlFilterMeetingResponseRequest () : base ("MeetingResponse")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // MeetingResponse
            node1 = new NcXmlFilterNode ("MeetingResponse", RedactionType.NONE, RedactionType.NONE);
            // Request
            node2 = new NcXmlFilterNode ("Request", RedactionType.NONE, RedactionType.NONE);
            // UserResponse
            node3 = new NcXmlFilterNode ("UserResponse", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Request -> UserResponse
            // CollectionId
            node3 = new NcXmlFilterNode ("CollectionId", RedactionType.SHORT_HASH, RedactionType.NONE);
            node2.Add(node3); // Request -> CollectionId
            // RequestId
            node3 = new NcXmlFilterNode ("RequestId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Request -> RequestId
            // InstanceId
            node3 = new NcXmlFilterNode ("InstanceId", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Request -> InstanceId
            node1.Add(node2); // MeetingResponse -> Request
            node0.Add(node1); // xml -> MeetingResponse
            
            Root = node0;
        }
    }
}
