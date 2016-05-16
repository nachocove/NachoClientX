using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterContacts : NcXmlFilter
    {
        public AsXmlFilterContacts () : base ("Contacts")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // Anniversary
            node1 = new NcXmlFilterNode ("Anniversary", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Anniversary
            // AssistantName
            node1 = new NcXmlFilterNode ("AssistantName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> AssistantName
            // AssistantPhoneNumber
            node1 = new NcXmlFilterNode ("AssistantPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> AssistantPhoneNumber
            // Birthday
            node1 = new NcXmlFilterNode ("Birthday", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Birthday
            // Business2PhoneNumber
            node1 = new NcXmlFilterNode ("Business2PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Business2PhoneNumber
            // BusinessAddressCity
            node1 = new NcXmlFilterNode ("BusinessAddressCity", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> BusinessAddressCity
            // BusinessPhoneNumber
            node1 = new NcXmlFilterNode ("BusinessPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> BusinessPhoneNumber
            // WebPage
            node1 = new NcXmlFilterNode ("WebPage", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> WebPage
            // BusinessAddressCountry
            node1 = new NcXmlFilterNode ("BusinessAddressCountry", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> BusinessAddressCountry
            // Department
            node1 = new NcXmlFilterNode ("Department", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Department
            // Email1Address
            node1 = new NcXmlFilterNode ("Email1Address", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Email1Address
            // Email2Address
            node1 = new NcXmlFilterNode ("Email2Address", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Email2Address
            // Email3Address
            node1 = new NcXmlFilterNode ("Email3Address", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Email3Address
            // BusinessFaxNumber
            node1 = new NcXmlFilterNode ("BusinessFaxNumber", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> BusinessFaxNumber
            // FileAs
            node1 = new NcXmlFilterNode ("FileAs", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> FileAs
            // Alias
            node1 = new NcXmlFilterNode ("Alias", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Alias
            // WeightedRank
            node1 = new NcXmlFilterNode ("WeightedRank", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> WeightedRank
            // FirstName
            node1 = new NcXmlFilterNode ("FirstName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> FirstName
            // MiddleName
            node1 = new NcXmlFilterNode ("MiddleName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> MiddleName
            // HomeAddressCity
            node1 = new NcXmlFilterNode ("HomeAddressCity", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> HomeAddressCity
            // HomeAddressCountry
            node1 = new NcXmlFilterNode ("HomeAddressCountry", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> HomeAddressCountry
            // HomeFaxNumber
            node1 = new NcXmlFilterNode ("HomeFaxNumber", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> HomeFaxNumber
            // HomePhoneNumber
            node1 = new NcXmlFilterNode ("HomePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> HomePhoneNumber
            // Home2PhoneNumber
            node1 = new NcXmlFilterNode ("Home2PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Home2PhoneNumber
            // HomeAddressPostalCode
            node1 = new NcXmlFilterNode ("HomeAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> HomeAddressPostalCode
            // HomeAddressState
            node1 = new NcXmlFilterNode ("HomeAddressState", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> HomeAddressState
            // HomeAddressStreet
            node1 = new NcXmlFilterNode ("HomeAddressStreet", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> HomeAddressStreet
            // MobilePhoneNumber
            node1 = new NcXmlFilterNode ("MobilePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> MobilePhoneNumber
            // Suffix
            node1 = new NcXmlFilterNode ("Suffix", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Suffix
            // CompanyName
            node1 = new NcXmlFilterNode ("CompanyName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> CompanyName
            // OtherAddressCity
            node1 = new NcXmlFilterNode ("OtherAddressCity", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> OtherAddressCity
            // OtherAddressCountry
            node1 = new NcXmlFilterNode ("OtherAddressCountry", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> OtherAddressCountry
            // CarPhoneNumber
            node1 = new NcXmlFilterNode ("CarPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> CarPhoneNumber
            // OtherAddressPostalCode
            node1 = new NcXmlFilterNode ("OtherAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> OtherAddressPostalCode
            // OtherAddressState
            node1 = new NcXmlFilterNode ("OtherAddressState", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> OtherAddressState
            // OtherAddressStreet
            node1 = new NcXmlFilterNode ("OtherAddressStreet", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> OtherAddressStreet
            // PagerNumber
            node1 = new NcXmlFilterNode ("PagerNumber", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> PagerNumber
            // Title
            node1 = new NcXmlFilterNode ("Title", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Title
            // BusinessAddressPostalCode
            node1 = new NcXmlFilterNode ("BusinessAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> BusinessAddressPostalCode
            // LastName
            node1 = new NcXmlFilterNode ("LastName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> LastName
            // Spouse
            node1 = new NcXmlFilterNode ("Spouse", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Spouse
            // BusinessAddressState
            node1 = new NcXmlFilterNode ("BusinessAddressState", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> BusinessAddressState
            // BusinessAddressStreet
            node1 = new NcXmlFilterNode ("BusinessAddressStreet", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> BusinessAddressStreet
            // JobTitle
            node1 = new NcXmlFilterNode ("JobTitle", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> JobTitle
            // YomiFirstName
            node1 = new NcXmlFilterNode ("YomiFirstName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> YomiFirstName
            // YomiLastName
            node1 = new NcXmlFilterNode ("YomiLastName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> YomiLastName
            // YomiCompanyName
            node1 = new NcXmlFilterNode ("YomiCompanyName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> YomiCompanyName
            // OfficeLocation
            node1 = new NcXmlFilterNode ("OfficeLocation", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> OfficeLocation
            // RadioPhoneNumber
            node1 = new NcXmlFilterNode ("RadioPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> RadioPhoneNumber
            // Picture
            node1 = new NcXmlFilterNode ("Picture", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Picture
            // Categories
            node1 = new NcXmlFilterNode ("Categories", RedactionType.NONE, RedactionType.NONE);
            // Category
            node2 = new NcXmlFilterNode ("Category", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Categories -> Category
            node0.Add(node1); // xml -> Categories
            // Children
            node1 = new NcXmlFilterNode ("Children", RedactionType.NONE, RedactionType.NONE);
            // Child
            node2 = new NcXmlFilterNode ("Child", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Children -> Child
            node0.Add(node1); // xml -> Children
            
            Root = node0;
        }
    }
}
