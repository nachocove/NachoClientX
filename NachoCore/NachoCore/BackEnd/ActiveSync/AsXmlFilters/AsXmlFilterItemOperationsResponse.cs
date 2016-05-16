using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterItemOperationsResponse : NcXmlFilter
    {
        public AsXmlFilterItemOperationsResponse () : base ("ItemOperations")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;
            NcXmlFilterNode node4 = null;
            NcXmlFilterNode node5 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // Store
            node1 = new NcXmlFilterNode ("Store", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Store
            // Range
            node1 = new NcXmlFilterNode ("Range", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Range
            // Total
            node1 = new NcXmlFilterNode ("Total", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Total
            // Properties
            node1 = new NcXmlFilterNode ("Properties", RedactionType.NONE, RedactionType.NONE);
            // Range
            node2 = new NcXmlFilterNode ("Range", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Properties -> Range
            // Total
            node2 = new NcXmlFilterNode ("Total", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Properties -> Total
            // Data
            node2 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Properties -> Data
            // Part
            node2 = new NcXmlFilterNode ("Part", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Properties -> Part
            // Version
            node2 = new NcXmlFilterNode ("Version", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Properties -> Version
            node0.Add(node1); // xml -> Properties
            // Data
            node1 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Data
            // Status
            node1 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Status
            // Version
            node1 = new NcXmlFilterNode ("Version", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Version
            // Schema
            node1 = new NcXmlFilterNode ("Schema", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Schema
            // Part
            node1 = new NcXmlFilterNode ("Part", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Part
            // DeleteSubFolders
            node1 = new NcXmlFilterNode ("DeleteSubFolders", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> DeleteSubFolders
            // UserName
            node1 = new NcXmlFilterNode ("UserName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> UserName
            // Password
            node1 = new NcXmlFilterNode ("Password", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Password
            // DstFldId
            node1 = new NcXmlFilterNode ("DstFldId", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> DstFldId
            // ConversationId
            node1 = new NcXmlFilterNode ("ConversationId", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> ConversationId
            // MoveAlways
            node1 = new NcXmlFilterNode ("MoveAlways", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> MoveAlways
            // ItemOperations
            node1 = new NcXmlFilterNode ("ItemOperations", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // ItemOperations -> Status
            // Response
            node2 = new NcXmlFilterNode ("Response", RedactionType.NONE, RedactionType.NONE);
            // Move
            node3 = new NcXmlFilterNode ("Move", RedactionType.NONE, RedactionType.NONE);
            // Status
            node4 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Move -> Status
            // ConversationId
            node4 = new NcXmlFilterNode ("ConversationId", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Move -> ConversationId
            node2.Add(node3); // Response -> Move
            // EmptyFolderContents
            node3 = new NcXmlFilterNode ("EmptyFolderContents", RedactionType.NONE, RedactionType.NONE);
            // Status
            node4 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // EmptyFolderContents -> Status
            node2.Add(node3); // Response -> EmptyFolderContents
            // Fetch
            node3 = new NcXmlFilterNode ("Fetch", RedactionType.NONE, RedactionType.NONE);
            // Status
            node4 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Fetch -> Status
            // Properties
            node4 = new NcXmlFilterNode ("Properties", RedactionType.NONE, RedactionType.NONE);
            // Range
            node5 = new NcXmlFilterNode ("Range", RedactionType.NONE, RedactionType.NONE);
            node4.Add(node5); // Properties -> Range
            // Total
            node5 = new NcXmlFilterNode ("Total", RedactionType.NONE, RedactionType.NONE);
            node4.Add(node5); // Properties -> Total
            // Data
            node5 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Properties -> Data
            // Part
            node5 = new NcXmlFilterNode ("Part", RedactionType.NONE, RedactionType.NONE);
            node4.Add(node5); // Properties -> Part
            // Version
            node5 = new NcXmlFilterNode ("Version", RedactionType.NONE, RedactionType.NONE);
            node4.Add(node5); // Properties -> Version
            node3.Add(node4); // Fetch -> Properties
            node2.Add(node3); // Response -> Fetch
            node1.Add(node2); // ItemOperations -> Response
            node0.Add(node1); // xml -> ItemOperations
            
            Root = node0;
        }
    }
}
