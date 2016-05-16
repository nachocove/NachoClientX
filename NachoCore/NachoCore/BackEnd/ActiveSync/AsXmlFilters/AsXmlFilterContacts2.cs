using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterContacts2 : NcXmlFilter
    {
        public AsXmlFilterContacts2 () : base ("Contacts2")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // CustomerId
            node1 = new NcXmlFilterNode ("CustomerId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> CustomerId
            // GovernmentId
            node1 = new NcXmlFilterNode ("GovernmentId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> GovernmentId
            // IMAddress
            node1 = new NcXmlFilterNode ("IMAddress", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> IMAddress
            // IMAddress2
            node1 = new NcXmlFilterNode ("IMAddress2", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> IMAddress2
            // IMAddress3
            node1 = new NcXmlFilterNode ("IMAddress3", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> IMAddress3
            // ManagerName
            node1 = new NcXmlFilterNode ("ManagerName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ManagerName
            // CompanyMainPhone
            node1 = new NcXmlFilterNode ("CompanyMainPhone", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> CompanyMainPhone
            // AccountName
            node1 = new NcXmlFilterNode ("AccountName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> AccountName
            // NickName
            node1 = new NcXmlFilterNode ("NickName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> NickName
            // MMS
            node1 = new NcXmlFilterNode ("MMS", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> MMS
            
            Root = node0;
        }
    }
}
