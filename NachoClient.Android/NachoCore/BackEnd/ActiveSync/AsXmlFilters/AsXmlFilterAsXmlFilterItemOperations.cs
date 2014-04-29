using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterAsXmlFilterItemOperations : NcXmlFilter
    {
        public AsXmlFilterAsXmlFilterItemOperations () : base ("ItemOperations")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;

            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // Store
            node1 = new NcXmlFilterNode ("Store", RedactionType.FULL, RedactionType.FULL);
            // Range
            node1 = new NcXmlFilterNode ("Range", RedactionType.FULL, RedactionType.FULL);
            // Total
            node1 = new NcXmlFilterNode ("Total", RedactionType.FULL, RedactionType.FULL);
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
            // Data
            node1 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            // Status
            node1 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            // Version
            node1 = new NcXmlFilterNode ("Version", RedactionType.FULL, RedactionType.FULL);
            // Schema
            node1 = new NcXmlFilterNode ("Schema", RedactionType.FULL, RedactionType.FULL);
            // Part
            node1 = new NcXmlFilterNode ("Part", RedactionType.FULL, RedactionType.FULL);
            // DeleteSubFolders
            node1 = new NcXmlFilterNode ("DeleteSubFolders", RedactionType.FULL, RedactionType.FULL);
            // UserName
            node1 = new NcXmlFilterNode ("UserName", RedactionType.FULL, RedactionType.FULL);
            // Password
            node1 = new NcXmlFilterNode ("Password", RedactionType.FULL, RedactionType.FULL);
            // DstFldId
            node1 = new NcXmlFilterNode ("DstFldId", RedactionType.FULL, RedactionType.FULL);
            // ConversationId
            node1 = new NcXmlFilterNode ("ConversationId", RedactionType.FULL, RedactionType.FULL);
            // MoveAlways
            node1 = new NcXmlFilterNode ("MoveAlways", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1);
            
            Root = node0;
        }
    }
}
