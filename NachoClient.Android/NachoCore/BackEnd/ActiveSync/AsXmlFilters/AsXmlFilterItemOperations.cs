using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterItemOperations : NcXmlFilter
    {
        public AsXmlFilterItemOperations () : base ("ItemOperations")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // Store
            node1 = new NcXmlFilterNode ("Store", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Store
            // Range
            node1 = new NcXmlFilterNode ("Range", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Range
            // Total
            node1 = new NcXmlFilterNode ("Total", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Total
            // Properties
            node1 = new NcXmlFilterNode ("Properties", RedactionType.NONE, RedactionType.NONE);
            // Range
            node2 = new NcXmlFilterNode ("Range", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Properties -> Range
            // Total
            node2 = new NcXmlFilterNode ("Total", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Properties -> Total
            // Data
            node2 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Properties -> Data
            // Part
            node2 = new NcXmlFilterNode ("Part", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Properties -> Part
            // Version
            node2 = new NcXmlFilterNode ("Version", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Properties -> Version
            node0.Add(node1); // xml -> Properties
            // Data
            node1 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Data
            // Status
            node1 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Status
            // Version
            node1 = new NcXmlFilterNode ("Version", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Version
            // Schema
            node1 = new NcXmlFilterNode ("Schema", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Schema
            // Part
            node1 = new NcXmlFilterNode ("Part", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Part
            // DeleteSubFolders
            node1 = new NcXmlFilterNode ("DeleteSubFolders", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> DeleteSubFolders
            // UserName
            node1 = new NcXmlFilterNode ("UserName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> UserName
            // Password
            node1 = new NcXmlFilterNode ("Password", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Password
            // DstFldId
            node1 = new NcXmlFilterNode ("DstFldId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> DstFldId
            // ConversationId
            node1 = new NcXmlFilterNode ("ConversationId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ConversationId
            // MoveAlways
            node1 = new NcXmlFilterNode ("MoveAlways", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> MoveAlways
            
            Root = node0;
        }
    }
}
