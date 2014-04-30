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

            // Store
            node0 = new NcXmlFilterNode ("Store", RedactionType.FULL, RedactionType.FULL);
            // Range
            node0 = new NcXmlFilterNode ("Range", RedactionType.FULL, RedactionType.FULL);
            // Total
            node0 = new NcXmlFilterNode ("Total", RedactionType.FULL, RedactionType.FULL);
            // Properties
            node0 = new NcXmlFilterNode ("Properties", RedactionType.NONE, RedactionType.NONE);
            // Range
            node1 = new NcXmlFilterNode ("Range", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Properties -> Range
            // Total
            node1 = new NcXmlFilterNode ("Total", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Properties -> Total
            // Data
            node1 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Properties -> Data
            // Part
            node1 = new NcXmlFilterNode ("Part", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Properties -> Part
            // Version
            node1 = new NcXmlFilterNode ("Version", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Properties -> Version
            // Data
            node0 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            // Status
            node0 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            // Version
            node0 = new NcXmlFilterNode ("Version", RedactionType.FULL, RedactionType.FULL);
            // Schema
            node0 = new NcXmlFilterNode ("Schema", RedactionType.FULL, RedactionType.FULL);
            // Part
            node0 = new NcXmlFilterNode ("Part", RedactionType.FULL, RedactionType.FULL);
            // DeleteSubFolders
            node0 = new NcXmlFilterNode ("DeleteSubFolders", RedactionType.FULL, RedactionType.FULL);
            // UserName
            node0 = new NcXmlFilterNode ("UserName", RedactionType.FULL, RedactionType.FULL);
            // Password
            node0 = new NcXmlFilterNode ("Password", RedactionType.FULL, RedactionType.FULL);
            // DstFldId
            node0 = new NcXmlFilterNode ("DstFldId", RedactionType.FULL, RedactionType.FULL);
            // ConversationId
            node0 = new NcXmlFilterNode ("ConversationId", RedactionType.FULL, RedactionType.FULL);
            // MoveAlways
            node0 = new NcXmlFilterNode ("MoveAlways", RedactionType.FULL, RedactionType.FULL);
            
            Root = node0;
        }
    }
}
