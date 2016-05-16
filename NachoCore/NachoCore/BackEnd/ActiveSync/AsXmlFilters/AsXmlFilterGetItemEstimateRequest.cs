using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterGetItemEstimateRequest : NcXmlFilter
    {
        public AsXmlFilterGetItemEstimateRequest () : base ("GetItemEstimate")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;
            NcXmlFilterNode node4 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // GetItemEstimate
            node1 = new NcXmlFilterNode ("GetItemEstimate", RedactionType.NONE, RedactionType.NONE);
            // Collections
            node2 = new NcXmlFilterNode ("Collections", RedactionType.NONE, RedactionType.NONE);
            // Collection
            node3 = new NcXmlFilterNode ("Collection", RedactionType.NONE, RedactionType.NONE);
            // CollectionId
            node4 = new NcXmlFilterNode ("CollectionId", RedactionType.SHORT_HASH, RedactionType.NONE);
            node3.Add(node4); // Collection -> CollectionId
            node2.Add(node3); // Collections -> Collection
            node1.Add(node2); // GetItemEstimate -> Collections
            node0.Add(node1); // xml -> GetItemEstimate
            
            Root = node0;
        }
    }
}
