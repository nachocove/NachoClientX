using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterMoveRequest : NcXmlFilter
    {
        public AsXmlFilterMoveRequest () : base ("Move")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // MoveItems
            node1 = new NcXmlFilterNode ("MoveItems", RedactionType.NONE, RedactionType.NONE);
            // Move
            node2 = new NcXmlFilterNode ("Move", RedactionType.NONE, RedactionType.NONE);
            // SrcMsgId
            node3 = new NcXmlFilterNode ("SrcMsgId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Move -> SrcMsgId
            // SrcFldId
            node3 = new NcXmlFilterNode ("SrcFldId", RedactionType.SHORT_HASH, RedactionType.NONE);
            node2.Add(node3); // Move -> SrcFldId
            // DstFldId
            node3 = new NcXmlFilterNode ("DstFldId", RedactionType.SHORT_HASH, RedactionType.NONE);
            node2.Add(node3); // Move -> DstFldId
            node1.Add(node2); // MoveItems -> Move
            node0.Add(node1); // xml -> MoveItems
            
            Root = node0;
        }
    }
}
