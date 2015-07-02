using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterGAL : NcXmlFilter
    {
        public AsXmlFilterGAL () : base ("GAL")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // DisplayName
            node1 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> DisplayName
            // Phone
            node1 = new NcXmlFilterNode ("Phone", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Phone
            // Office
            node1 = new NcXmlFilterNode ("Office", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Office
            // Title
            node1 = new NcXmlFilterNode ("Title", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Title
            // Company
            node1 = new NcXmlFilterNode ("Company", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Company
            // Alias
            node1 = new NcXmlFilterNode ("Alias", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Alias
            // FirstName
            node1 = new NcXmlFilterNode ("FirstName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> FirstName
            // LastName
            node1 = new NcXmlFilterNode ("LastName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> LastName
            // HomePhone
            node1 = new NcXmlFilterNode ("HomePhone", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> HomePhone
            // MobilePhone
            node1 = new NcXmlFilterNode ("MobilePhone", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> MobilePhone
            // EmailAddress
            node1 = new NcXmlFilterNode ("EmailAddress", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> EmailAddress
            // Picture
            node1 = new NcXmlFilterNode ("Picture", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Picture -> Status
            // Data
            node2 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Picture -> Data
            node0.Add(node1); // xml -> Picture
            
            Root = node0;
        }
    }
}
