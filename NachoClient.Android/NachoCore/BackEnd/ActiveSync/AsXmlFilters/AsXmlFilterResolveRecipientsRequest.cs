using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterResolveRecipientsRequest : NcXmlFilter
    {
        public AsXmlFilterResolveRecipientsRequest () : base ("ResolveRecipients")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;
            NcXmlFilterNode node4 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // ResolveRecipients
            node1 = new NcXmlFilterNode ("ResolveRecipients", RedactionType.NONE, RedactionType.NONE);
            // To
            node2 = new NcXmlFilterNode ("To", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // ResolveRecipients -> To
            // Options
            node2 = new NcXmlFilterNode ("Options", RedactionType.NONE, RedactionType.NONE);
            // CertificateRetrieval
            node3 = new NcXmlFilterNode ("CertificateRetrieval", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Options -> CertificateRetrieval
            // MaxCertificates
            node3 = new NcXmlFilterNode ("MaxCertificates", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Options -> MaxCertificates
            // MaxAmbiguousRecipients
            node3 = new NcXmlFilterNode ("MaxAmbiguousRecipients", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Options -> MaxAmbiguousRecipients
            // Availability
            node3 = new NcXmlFilterNode ("Availability", RedactionType.NONE, RedactionType.NONE);
            // StartTime
            node4 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Availability -> StartTime
            // EndTime
            node4 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Availability -> EndTime
            node2.Add(node3); // Options -> Availability
            // Picture
            node3 = new NcXmlFilterNode ("Picture", RedactionType.NONE, RedactionType.NONE);
            // MaxSize
            node4 = new NcXmlFilterNode ("MaxSize", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Picture -> MaxSize
            // MaxPictures
            node4 = new NcXmlFilterNode ("MaxPictures", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Picture -> MaxPictures
            node2.Add(node3); // Options -> Picture
            node1.Add(node2); // ResolveRecipients -> Options
            node0.Add(node1); // xml -> ResolveRecipients
            
            Root = node0;
        }
    }
}
