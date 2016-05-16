using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterSearchRequest : NcXmlFilter
    {
        public AsXmlFilterSearchRequest () : base ("Search")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;
            NcXmlFilterNode node4 = null;
            NcXmlFilterNode node5 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // And
            node1 = new NcXmlFilterNode ("And", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> And
            // Or
            node1 = new NcXmlFilterNode ("Or", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Or
            // FreeText
            node1 = new NcXmlFilterNode ("FreeText", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> FreeText
            // ConversationId
            node1 = new NcXmlFilterNode ("ConversationId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ConversationId
            // EqualTo
            node1 = new NcXmlFilterNode ("EqualTo", RedactionType.NONE, RedactionType.NONE);
            // Value
            node2 = new NcXmlFilterNode ("Value", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EqualTo -> Value
            node0.Add(node1); // xml -> EqualTo
            // GreaterThan
            node1 = new NcXmlFilterNode ("GreaterThan", RedactionType.NONE, RedactionType.NONE);
            // Value
            node2 = new NcXmlFilterNode ("Value", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // GreaterThan -> Value
            node0.Add(node1); // xml -> GreaterThan
            // LessThan
            node1 = new NcXmlFilterNode ("LessThan", RedactionType.NONE, RedactionType.NONE);
            // Value
            node2 = new NcXmlFilterNode ("Value", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // LessThan -> Value
            node0.Add(node1); // xml -> LessThan
            // Name
            node1 = new NcXmlFilterNode ("Name", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Name
            // Query
            node1 = new NcXmlFilterNode ("Query", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Query
            // Options
            node1 = new NcXmlFilterNode ("Options", RedactionType.NONE, RedactionType.NONE);
            // Range
            node2 = new NcXmlFilterNode ("Range", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Options -> Range
            // UserName
            node2 = new NcXmlFilterNode ("UserName", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Options -> UserName
            // Password
            node2 = new NcXmlFilterNode ("Password", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Options -> Password
            // DeepTraversal
            node2 = new NcXmlFilterNode ("DeepTraversal", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Options -> DeepTraversal
            // RebuildResults
            node2 = new NcXmlFilterNode ("RebuildResults", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Options -> RebuildResults
            // Picture
            node2 = new NcXmlFilterNode ("Picture", RedactionType.NONE, RedactionType.NONE);
            // MaxSize
            node3 = new NcXmlFilterNode ("MaxSize", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Picture -> MaxSize
            // MaxPictures
            node3 = new NcXmlFilterNode ("MaxPictures", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Picture -> MaxPictures
            node1.Add(node2); // Options -> Picture
            node0.Add(node1); // xml -> Options
            // Status
            node1 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Status
            // Total
            node1 = new NcXmlFilterNode ("Total", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Total
            // Value
            node1 = new NcXmlFilterNode ("Value", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Value
            // LongId
            node1 = new NcXmlFilterNode ("LongId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> LongId
            // Range
            node1 = new NcXmlFilterNode ("Range", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Range
            // Search
            node1 = new NcXmlFilterNode ("Search", RedactionType.NONE, RedactionType.NONE);
            // Store
            node2 = new NcXmlFilterNode ("Store", RedactionType.NONE, RedactionType.NONE);
            // Name
            node3 = new NcXmlFilterNode ("Name", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Store -> Name
            // Query
            node3 = new NcXmlFilterNode ("Query", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Store -> Query
            // Options
            node3 = new NcXmlFilterNode ("Options", RedactionType.NONE, RedactionType.NONE);
            // Range
            node4 = new NcXmlFilterNode ("Range", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Options -> Range
            // UserName
            node4 = new NcXmlFilterNode ("UserName", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Options -> UserName
            // Password
            node4 = new NcXmlFilterNode ("Password", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Options -> Password
            // DeepTraversal
            node4 = new NcXmlFilterNode ("DeepTraversal", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Options -> DeepTraversal
            // RebuildResults
            node4 = new NcXmlFilterNode ("RebuildResults", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Options -> RebuildResults
            // Picture
            node4 = new NcXmlFilterNode ("Picture", RedactionType.NONE, RedactionType.NONE);
            // MaxSize
            node5 = new NcXmlFilterNode ("MaxSize", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Picture -> MaxSize
            // MaxPictures
            node5 = new NcXmlFilterNode ("MaxPictures", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Picture -> MaxPictures
            node3.Add(node4); // Options -> Picture
            node2.Add(node3); // Store -> Options
            node1.Add(node2); // Search -> Store
            node0.Add(node1); // xml -> Search
            
            Root = node0;
        }
    }
}
