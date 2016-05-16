using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterMoveResponse : NcXmlFilter
    {
        public AsXmlFilterMoveResponse () : base ("Move")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // MoveItems
            node1 = new NcXmlFilterNode ("MoveItems", RedactionType.NONE, RedactionType.NONE);
            // Response
            node2 = new NcXmlFilterNode ("Response", RedactionType.NONE, RedactionType.NONE);
            // SrcMsgId
            node3 = new NcXmlFilterNode ("SrcMsgId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Response -> SrcMsgId
            // Status
            node3 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Response -> Status
            // DstMsgId
            node3 = new NcXmlFilterNode ("DstMsgId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Response -> DstMsgId
            node1.Add(node2); // MoveItems -> Response
            node0.Add(node1); // xml -> MoveItems
            
            Root = node0;
        }
    }
}
