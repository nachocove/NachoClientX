using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterItemOperationsRequest : NcXmlFilter
    {
        public AsXmlFilterItemOperationsRequest () : base ("ItemOperations")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;
            NcXmlFilterNode node4 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // Store
            node1 = new NcXmlFilterNode ("Store", RedactionType.NONE, RedactionType.NONE);
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
            // EmptyFolderContents
            node2 = new NcXmlFilterNode ("EmptyFolderContents", RedactionType.NONE, RedactionType.NONE);
            // Options
            node3 = new NcXmlFilterNode ("Options", RedactionType.NONE, RedactionType.NONE);
            // DeleteSubFolders
            node4 = new NcXmlFilterNode ("DeleteSubFolders", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Options -> DeleteSubFolders
            node2.Add(node3); // EmptyFolderContents -> Options
            node1.Add(node2); // ItemOperations -> EmptyFolderContents
            // Fetch
            node2 = new NcXmlFilterNode ("Fetch", RedactionType.NONE, RedactionType.NONE);
            // Store
            node3 = new NcXmlFilterNode ("Store", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Fetch -> Store
            // Options
            node3 = new NcXmlFilterNode ("Options", RedactionType.NONE, RedactionType.NONE);
            // Schema
            node4 = new NcXmlFilterNode ("Schema", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Options -> Schema
            // Range
            node4 = new NcXmlFilterNode ("Range", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Options -> Range
            // UserName
            node4 = new NcXmlFilterNode ("UserName", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Options -> UserName
            // Password
            node4 = new NcXmlFilterNode ("Password", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Options -> Password
            node2.Add(node3); // Fetch -> Options
            node1.Add(node2); // ItemOperations -> Fetch
            // Move
            node2 = new NcXmlFilterNode ("Move", RedactionType.NONE, RedactionType.NONE);
            // ConversationId
            node3 = new NcXmlFilterNode ("ConversationId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Move -> ConversationId
            // DstFldId
            node3 = new NcXmlFilterNode ("DstFldId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Move -> DstFldId
            // Options
            node3 = new NcXmlFilterNode ("Options", RedactionType.NONE, RedactionType.NONE);
            // MoveAlways
            node4 = new NcXmlFilterNode ("MoveAlways", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Options -> MoveAlways
            node2.Add(node3); // Move -> Options
            node1.Add(node2); // ItemOperations -> Move
            node0.Add(node1); // xml -> ItemOperations
            
            Root = node0;
        }
    }
}
