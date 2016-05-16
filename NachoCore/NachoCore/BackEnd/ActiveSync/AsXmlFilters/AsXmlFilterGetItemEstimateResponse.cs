using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterGetItemEstimateResponse : NcXmlFilter
    {
        public AsXmlFilterGetItemEstimateResponse () : base ("GetItemEstimate")
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
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // GetItemEstimate -> Status
            // Response
            node2 = new NcXmlFilterNode ("Response", RedactionType.NONE, RedactionType.NONE);
            // Status
            node3 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Response -> Status
            // Collection
            node3 = new NcXmlFilterNode ("Collection", RedactionType.NONE, RedactionType.NONE);
            // CollectionId
            node4 = new NcXmlFilterNode ("CollectionId", RedactionType.SHORT_HASH, RedactionType.NONE);
            node3.Add(node4); // Collection -> CollectionId
            // Estimate
            node4 = new NcXmlFilterNode ("Estimate", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Collection -> Estimate
            node2.Add(node3); // Response -> Collection
            node1.Add(node2); // GetItemEstimate -> Response
            node0.Add(node1); // xml -> GetItemEstimate
            
            Root = node0;
        }
    }
}
