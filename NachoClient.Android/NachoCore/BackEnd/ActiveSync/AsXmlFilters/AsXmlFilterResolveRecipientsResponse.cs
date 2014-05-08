using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterResolveRecipientsResponse : NcXmlFilter
    {
        public AsXmlFilterResolveRecipientsResponse () : base ("ResolveRecipients")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;
            NcXmlFilterNode node4 = null;
            NcXmlFilterNode node5 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // ResolveRecipients
            node1 = new NcXmlFilterNode ("ResolveRecipients", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // ResolveRecipients -> Status
            // Response
            node2 = new NcXmlFilterNode ("Response", RedactionType.NONE, RedactionType.NONE);
            // To
            node3 = new NcXmlFilterNode ("To", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Response -> To
            // Status
            node3 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Response -> Status
            // RecipientCount
            node3 = new NcXmlFilterNode ("RecipientCount", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Response -> RecipientCount
            // Recipient
            node3 = new NcXmlFilterNode ("Recipient", RedactionType.NONE, RedactionType.NONE);
            // Type
            node4 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Recipient -> Type
            // DisplayName
            node4 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Recipient -> DisplayName
            // EmailAddress
            node4 = new NcXmlFilterNode ("EmailAddress", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Recipient -> EmailAddress
            // Availability
            node4 = new NcXmlFilterNode ("Availability", RedactionType.NONE, RedactionType.NONE);
            // Status
            node5 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Availability -> Status
            // MergedFreeBusy
            node5 = new NcXmlFilterNode ("MergedFreeBusy", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Availability -> MergedFreeBusy
            node3.Add(node4); // Recipient -> Availability
            // Certificates
            node4 = new NcXmlFilterNode ("Certificates", RedactionType.NONE, RedactionType.NONE);
            // Status
            node5 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Certificates -> Status
            // CertificateCount
            node5 = new NcXmlFilterNode ("CertificateCount", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Certificates -> CertificateCount
            // RecipientCount
            node5 = new NcXmlFilterNode ("RecipientCount", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Certificates -> RecipientCount
            // Certificate
            node5 = new NcXmlFilterNode ("Certificate", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Certificates -> Certificate
            // MiniCertificate
            node5 = new NcXmlFilterNode ("MiniCertificate", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Certificates -> MiniCertificate
            node3.Add(node4); // Recipient -> Certificates
            // Picture
            node4 = new NcXmlFilterNode ("Picture", RedactionType.NONE, RedactionType.NONE);
            // Status
            node5 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Picture -> Status
            // Data
            node5 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Picture -> Data
            node3.Add(node4); // Recipient -> Picture
            node2.Add(node3); // Response -> Recipient
            node1.Add(node2); // ResolveRecipients -> Response
            node0.Add(node1); // xml -> ResolveRecipients
            
            Root = node0;
        }
    }
}
