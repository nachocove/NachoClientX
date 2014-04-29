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

            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // Anniversary
            node1 = new NcXmlFilterNode ("Anniversary", RedactionType.FULL, RedactionType.FULL);
            // AssistantName
            node1 = new NcXmlFilterNode ("AssistantName", RedactionType.FULL, RedactionType.FULL);
            // AssistantPhoneNumber
            node1 = new NcXmlFilterNode ("AssistantPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Birthday
            node1 = new NcXmlFilterNode ("Birthday", RedactionType.FULL, RedactionType.FULL);
            // Business2PhoneNumber
            node1 = new NcXmlFilterNode ("Business2PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCity
            node1 = new NcXmlFilterNode ("BusinessAddressCity", RedactionType.FULL, RedactionType.FULL);
            // BusinessPhoneNumber
            node1 = new NcXmlFilterNode ("BusinessPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // WebPage
            node1 = new NcXmlFilterNode ("WebPage", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCountry
            node1 = new NcXmlFilterNode ("BusinessAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // Department
            node1 = new NcXmlFilterNode ("Department", RedactionType.FULL, RedactionType.FULL);
            // Email1Address
            node1 = new NcXmlFilterNode ("Email1Address", RedactionType.FULL, RedactionType.FULL);
            // Email2Address
            node1 = new NcXmlFilterNode ("Email2Address", RedactionType.FULL, RedactionType.FULL);
            // Email3Address
            node1 = new NcXmlFilterNode ("Email3Address", RedactionType.FULL, RedactionType.FULL);
            // BusinessFaxNumber
            node1 = new NcXmlFilterNode ("BusinessFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // FileAs
            node1 = new NcXmlFilterNode ("FileAs", RedactionType.FULL, RedactionType.FULL);
            // Alias
            node1 = new NcXmlFilterNode ("Alias", RedactionType.FULL, RedactionType.FULL);
            // WeightedRank
            node1 = new NcXmlFilterNode ("WeightedRank", RedactionType.FULL, RedactionType.FULL);
            // FirstName
            node1 = new NcXmlFilterNode ("FirstName", RedactionType.FULL, RedactionType.FULL);
            // MiddleName
            node1 = new NcXmlFilterNode ("MiddleName", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCity
            node1 = new NcXmlFilterNode ("HomeAddressCity", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCountry
            node1 = new NcXmlFilterNode ("HomeAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // HomeFaxNumber
            node1 = new NcXmlFilterNode ("HomeFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // HomePhoneNumber
            node1 = new NcXmlFilterNode ("HomePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Home2PhoneNumber
            node1 = new NcXmlFilterNode ("Home2PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressPostalCode
            node1 = new NcXmlFilterNode ("HomeAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressState
            node1 = new NcXmlFilterNode ("HomeAddressState", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressStreet
            node1 = new NcXmlFilterNode ("HomeAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // MobilePhoneNumber
            node1 = new NcXmlFilterNode ("MobilePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Suffix
            node1 = new NcXmlFilterNode ("Suffix", RedactionType.FULL, RedactionType.FULL);
            // CompanyName
            node1 = new NcXmlFilterNode ("CompanyName", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCity
            node1 = new NcXmlFilterNode ("OtherAddressCity", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCountry
            node1 = new NcXmlFilterNode ("OtherAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // CarPhoneNumber
            node1 = new NcXmlFilterNode ("CarPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressPostalCode
            node1 = new NcXmlFilterNode ("OtherAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressState
            node1 = new NcXmlFilterNode ("OtherAddressState", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressStreet
            node1 = new NcXmlFilterNode ("OtherAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // PagerNumber
            node1 = new NcXmlFilterNode ("PagerNumber", RedactionType.FULL, RedactionType.FULL);
            // Title
            node1 = new NcXmlFilterNode ("Title", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressPostalCode
            node1 = new NcXmlFilterNode ("BusinessAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // LastName
            node1 = new NcXmlFilterNode ("LastName", RedactionType.FULL, RedactionType.FULL);
            // Spouse
            node1 = new NcXmlFilterNode ("Spouse", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressState
            node1 = new NcXmlFilterNode ("BusinessAddressState", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressStreet
            node1 = new NcXmlFilterNode ("BusinessAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // JobTitle
            node1 = new NcXmlFilterNode ("JobTitle", RedactionType.FULL, RedactionType.FULL);
            // YomiFirstName
            node1 = new NcXmlFilterNode ("YomiFirstName", RedactionType.FULL, RedactionType.FULL);
            // YomiLastName
            node1 = new NcXmlFilterNode ("YomiLastName", RedactionType.FULL, RedactionType.FULL);
            // YomiCompanyName
            node1 = new NcXmlFilterNode ("YomiCompanyName", RedactionType.FULL, RedactionType.FULL);
            // OfficeLocation
            node1 = new NcXmlFilterNode ("OfficeLocation", RedactionType.FULL, RedactionType.FULL);
            // RadioPhoneNumber
            node1 = new NcXmlFilterNode ("RadioPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Picture
            node1 = new NcXmlFilterNode ("Picture", RedactionType.FULL, RedactionType.FULL);
            // Categories
            node1 = new NcXmlFilterNode ("Categories", RedactionType.NONE, RedactionType.NONE);
            // Category
            node2 = new NcXmlFilterNode ("Category", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Categories -> Category
            // Children
            node1 = new NcXmlFilterNode ("Children", RedactionType.NONE, RedactionType.NONE);
            // Child
            node2 = new NcXmlFilterNode ("Child", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Children -> Child
            // Anniversary
            node1 = new NcXmlFilterNode ("Anniversary", RedactionType.FULL, RedactionType.FULL);
            // AssistantName
            node1 = new NcXmlFilterNode ("AssistantName", RedactionType.FULL, RedactionType.FULL);
            // AssistantPhoneNumber
            node1 = new NcXmlFilterNode ("AssistantPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Birthday
            node1 = new NcXmlFilterNode ("Birthday", RedactionType.FULL, RedactionType.FULL);
            // Business2PhoneNumber
            node1 = new NcXmlFilterNode ("Business2PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCity
            node1 = new NcXmlFilterNode ("BusinessAddressCity", RedactionType.FULL, RedactionType.FULL);
            // BusinessPhoneNumber
            node1 = new NcXmlFilterNode ("BusinessPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // WebPage
            node1 = new NcXmlFilterNode ("WebPage", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCountry
            node1 = new NcXmlFilterNode ("BusinessAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // Department
            node1 = new NcXmlFilterNode ("Department", RedactionType.FULL, RedactionType.FULL);
            // Email1Address
            node1 = new NcXmlFilterNode ("Email1Address", RedactionType.FULL, RedactionType.FULL);
            // Email2Address
            node1 = new NcXmlFilterNode ("Email2Address", RedactionType.FULL, RedactionType.FULL);
            // Email3Address
            node1 = new NcXmlFilterNode ("Email3Address", RedactionType.FULL, RedactionType.FULL);
            // BusinessFaxNumber
            node1 = new NcXmlFilterNode ("BusinessFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // FileAs
            node1 = new NcXmlFilterNode ("FileAs", RedactionType.FULL, RedactionType.FULL);
            // Alias
            node1 = new NcXmlFilterNode ("Alias", RedactionType.FULL, RedactionType.FULL);
            // WeightedRank
            node1 = new NcXmlFilterNode ("WeightedRank", RedactionType.FULL, RedactionType.FULL);
            // FirstName
            node1 = new NcXmlFilterNode ("FirstName", RedactionType.FULL, RedactionType.FULL);
            // MiddleName
            node1 = new NcXmlFilterNode ("MiddleName", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCity
            node1 = new NcXmlFilterNode ("HomeAddressCity", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCountry
            node1 = new NcXmlFilterNode ("HomeAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // HomeFaxNumber
            node1 = new NcXmlFilterNode ("HomeFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // HomePhoneNumber
            node1 = new NcXmlFilterNode ("HomePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Home2PhoneNumber
            node1 = new NcXmlFilterNode ("Home2PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressPostalCode
            node1 = new NcXmlFilterNode ("HomeAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressState
            node1 = new NcXmlFilterNode ("HomeAddressState", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressStreet
            node1 = new NcXmlFilterNode ("HomeAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // MobilePhoneNumber
            node1 = new NcXmlFilterNode ("MobilePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Suffix
            node1 = new NcXmlFilterNode ("Suffix", RedactionType.FULL, RedactionType.FULL);
            // CompanyName
            node1 = new NcXmlFilterNode ("CompanyName", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCity
            node1 = new NcXmlFilterNode ("OtherAddressCity", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCountry
            node1 = new NcXmlFilterNode ("OtherAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // CarPhoneNumber
            node1 = new NcXmlFilterNode ("CarPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressPostalCode
            node1 = new NcXmlFilterNode ("OtherAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressState
            node1 = new NcXmlFilterNode ("OtherAddressState", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressStreet
            node1 = new NcXmlFilterNode ("OtherAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // PagerNumber
            node1 = new NcXmlFilterNode ("PagerNumber", RedactionType.FULL, RedactionType.FULL);
            // Title
            node1 = new NcXmlFilterNode ("Title", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressPostalCode
            node1 = new NcXmlFilterNode ("BusinessAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // LastName
            node1 = new NcXmlFilterNode ("LastName", RedactionType.FULL, RedactionType.FULL);
            // Spouse
            node1 = new NcXmlFilterNode ("Spouse", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressState
            node1 = new NcXmlFilterNode ("BusinessAddressState", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressStreet
            node1 = new NcXmlFilterNode ("BusinessAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // JobTitle
            node1 = new NcXmlFilterNode ("JobTitle", RedactionType.FULL, RedactionType.FULL);
            // YomiFirstName
            node1 = new NcXmlFilterNode ("YomiFirstName", RedactionType.FULL, RedactionType.FULL);
            // YomiLastName
            node1 = new NcXmlFilterNode ("YomiLastName", RedactionType.FULL, RedactionType.FULL);
            // YomiCompanyName
            node1 = new NcXmlFilterNode ("YomiCompanyName", RedactionType.FULL, RedactionType.FULL);
            // OfficeLocation
            node1 = new NcXmlFilterNode ("OfficeLocation", RedactionType.FULL, RedactionType.FULL);
            // RadioPhoneNumber
            node1 = new NcXmlFilterNode ("RadioPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Picture
            node1 = new NcXmlFilterNode ("Picture", RedactionType.FULL, RedactionType.FULL);
            // Categories
            node1 = new NcXmlFilterNode ("Categories", RedactionType.NONE, RedactionType.NONE);
            // Category
            node2 = new NcXmlFilterNode ("Category", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Categories -> Category
            // Children
            node1 = new NcXmlFilterNode ("Children", RedactionType.NONE, RedactionType.NONE);
            // Child
            node2 = new NcXmlFilterNode ("Child", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Children -> Child
            // Anniversary
            node1 = new NcXmlFilterNode ("Anniversary", RedactionType.FULL, RedactionType.FULL);
            // Birthday
            node1 = new NcXmlFilterNode ("Birthday", RedactionType.FULL, RedactionType.FULL);
            // WebPage
            node1 = new NcXmlFilterNode ("WebPage", RedactionType.FULL, RedactionType.FULL);
            // Children
            node1 = new NcXmlFilterNode ("Children", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCountry
            node1 = new NcXmlFilterNode ("BusinessAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // Department
            node1 = new NcXmlFilterNode ("Department", RedactionType.FULL, RedactionType.FULL);
            // Email1Address
            node1 = new NcXmlFilterNode ("Email1Address", RedactionType.FULL, RedactionType.FULL);
            // Email2Address
            node1 = new NcXmlFilterNode ("Email2Address", RedactionType.FULL, RedactionType.FULL);
            // Email3Address
            node1 = new NcXmlFilterNode ("Email3Address", RedactionType.FULL, RedactionType.FULL);
            // BusinessFaxNumber
            node1 = new NcXmlFilterNode ("BusinessFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // FileAs
            node1 = new NcXmlFilterNode ("FileAs", RedactionType.FULL, RedactionType.FULL);
            // FirstName
            node1 = new NcXmlFilterNode ("FirstName", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCity
            node1 = new NcXmlFilterNode ("HomeAddressCity", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCountry
            node1 = new NcXmlFilterNode ("HomeAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // HomeFaxNumber
            node1 = new NcXmlFilterNode ("HomeFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // HomePhoneNumber
            node1 = new NcXmlFilterNode ("HomePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Home2PhoneNumber
            node1 = new NcXmlFilterNode ("Home2PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressPostalCode
            node1 = new NcXmlFilterNode ("HomeAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressState
            node1 = new NcXmlFilterNode ("HomeAddressState", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressStreet
            node1 = new NcXmlFilterNode ("HomeAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCity
            node1 = new NcXmlFilterNode ("BusinessAddressCity", RedactionType.FULL, RedactionType.FULL);
            // MiddleName
            node1 = new NcXmlFilterNode ("MiddleName", RedactionType.FULL, RedactionType.FULL);
            // MobilePhoneNumber
            node1 = new NcXmlFilterNode ("MobilePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Suffix
            node1 = new NcXmlFilterNode ("Suffix", RedactionType.FULL, RedactionType.FULL);
            // CompanyName
            node1 = new NcXmlFilterNode ("CompanyName", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCity
            node1 = new NcXmlFilterNode ("OtherAddressCity", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCountry
            node1 = new NcXmlFilterNode ("OtherAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // CarPhoneNumber
            node1 = new NcXmlFilterNode ("CarPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressPostalCode
            node1 = new NcXmlFilterNode ("OtherAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressState
            node1 = new NcXmlFilterNode ("OtherAddressState", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressStreet
            node1 = new NcXmlFilterNode ("OtherAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // PagerNumber
            node1 = new NcXmlFilterNode ("PagerNumber", RedactionType.FULL, RedactionType.FULL);
            // Title
            node1 = new NcXmlFilterNode ("Title", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressPostalCode
            node1 = new NcXmlFilterNode ("BusinessAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // AssistantName
            node1 = new NcXmlFilterNode ("AssistantName", RedactionType.FULL, RedactionType.FULL);
            // AssistantPhoneNumber
            node1 = new NcXmlFilterNode ("AssistantPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // AssistnamePhoneNumber
            node1 = new NcXmlFilterNode ("AssistnamePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // LastName
            node1 = new NcXmlFilterNode ("LastName", RedactionType.FULL, RedactionType.FULL);
            // Spouse
            node1 = new NcXmlFilterNode ("Spouse", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressState
            node1 = new NcXmlFilterNode ("BusinessAddressState", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressStreet
            node1 = new NcXmlFilterNode ("BusinessAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // BusinessPhoneNumber
            node1 = new NcXmlFilterNode ("BusinessPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Business2PhoneNumber
            node1 = new NcXmlFilterNode ("Business2PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // JobTitle
            node1 = new NcXmlFilterNode ("JobTitle", RedactionType.FULL, RedactionType.FULL);
            // YomiFirstName
            node1 = new NcXmlFilterNode ("YomiFirstName", RedactionType.FULL, RedactionType.FULL);
            // YomiLastName
            node1 = new NcXmlFilterNode ("YomiLastName", RedactionType.FULL, RedactionType.FULL);
            // YomiCompanyName
            node1 = new NcXmlFilterNode ("YomiCompanyName", RedactionType.FULL, RedactionType.FULL);
            // OfficeLocation
            node1 = new NcXmlFilterNode ("OfficeLocation", RedactionType.FULL, RedactionType.FULL);
            // RadioPhoneNumber
            node1 = new NcXmlFilterNode ("RadioPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Picture
            node1 = new NcXmlFilterNode ("Picture", RedactionType.FULL, RedactionType.FULL);
            // Categories
            node1 = new NcXmlFilterNode ("Categories", RedactionType.FULL, RedactionType.FULL);
            // Anniversary
            node1 = new NcXmlFilterNode ("Anniversary", RedactionType.FULL, RedactionType.FULL);
            // Birthday
            node1 = new NcXmlFilterNode ("Birthday", RedactionType.FULL, RedactionType.FULL);
            // Webpage
            node1 = new NcXmlFilterNode ("Webpage", RedactionType.FULL, RedactionType.FULL);
            // Children
            node1 = new NcXmlFilterNode ("Children", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCountry
            node1 = new NcXmlFilterNode ("BusinessAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // Department
            node1 = new NcXmlFilterNode ("Department", RedactionType.FULL, RedactionType.FULL);
            // Email1Address
            node1 = new NcXmlFilterNode ("Email1Address", RedactionType.FULL, RedactionType.FULL);
            // Email2Address
            node1 = new NcXmlFilterNode ("Email2Address", RedactionType.FULL, RedactionType.FULL);
            // Email3Address
            node1 = new NcXmlFilterNode ("Email3Address", RedactionType.FULL, RedactionType.FULL);
            // BusinessFaxNumber
            node1 = new NcXmlFilterNode ("BusinessFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // FileAs
            node1 = new NcXmlFilterNode ("FileAs", RedactionType.FULL, RedactionType.FULL);
            // FirstName
            node1 = new NcXmlFilterNode ("FirstName", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCity
            node1 = new NcXmlFilterNode ("HomeAddressCity", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCountry
            node1 = new NcXmlFilterNode ("HomeAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // HomeFaxNumber
            node1 = new NcXmlFilterNode ("HomeFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // HomeTelephoneNumber
            node1 = new NcXmlFilterNode ("HomeTelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Home2TelephoneNumber
            node1 = new NcXmlFilterNode ("Home2TelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressPostalCode
            node1 = new NcXmlFilterNode ("HomeAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressState
            node1 = new NcXmlFilterNode ("HomeAddressState", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressStreet
            node1 = new NcXmlFilterNode ("HomeAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCity
            node1 = new NcXmlFilterNode ("BusinessAddressCity", RedactionType.FULL, RedactionType.FULL);
            // MiddleName
            node1 = new NcXmlFilterNode ("MiddleName", RedactionType.FULL, RedactionType.FULL);
            // MobileTelephoneNumber
            node1 = new NcXmlFilterNode ("MobileTelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Suffix
            node1 = new NcXmlFilterNode ("Suffix", RedactionType.FULL, RedactionType.FULL);
            // CompanyName
            node1 = new NcXmlFilterNode ("CompanyName", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCity
            node1 = new NcXmlFilterNode ("OtherAddressCity", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCountry
            node1 = new NcXmlFilterNode ("OtherAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // CarTelephoneNumber
            node1 = new NcXmlFilterNode ("CarTelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressPostalCode
            node1 = new NcXmlFilterNode ("OtherAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressState
            node1 = new NcXmlFilterNode ("OtherAddressState", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressStreet
            node1 = new NcXmlFilterNode ("OtherAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // PagerNumber
            node1 = new NcXmlFilterNode ("PagerNumber", RedactionType.FULL, RedactionType.FULL);
            // Title
            node1 = new NcXmlFilterNode ("Title", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressPostalCode
            node1 = new NcXmlFilterNode ("BusinessAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // AssistantName
            node1 = new NcXmlFilterNode ("AssistantName", RedactionType.FULL, RedactionType.FULL);
            // AssistantTelephoneNumber
            node1 = new NcXmlFilterNode ("AssistantTelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // LastName
            node1 = new NcXmlFilterNode ("LastName", RedactionType.FULL, RedactionType.FULL);
            // Spouse
            node1 = new NcXmlFilterNode ("Spouse", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressState
            node1 = new NcXmlFilterNode ("BusinessAddressState", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressStreet
            node1 = new NcXmlFilterNode ("BusinessAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // BusinessTelephoneNumber
            node1 = new NcXmlFilterNode ("BusinessTelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Business2TelephoneNumber
            node1 = new NcXmlFilterNode ("Business2TelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // JobTitle
            node1 = new NcXmlFilterNode ("JobTitle", RedactionType.FULL, RedactionType.FULL);
            // YomiFirstName
            node1 = new NcXmlFilterNode ("YomiFirstName", RedactionType.FULL, RedactionType.FULL);
            // YomiLastName
            node1 = new NcXmlFilterNode ("YomiLastName", RedactionType.FULL, RedactionType.FULL);
            // YomiCompanyName
            node1 = new NcXmlFilterNode ("YomiCompanyName", RedactionType.FULL, RedactionType.FULL);
            // OfficeLocation
            node1 = new NcXmlFilterNode ("OfficeLocation", RedactionType.FULL, RedactionType.FULL);
            // RadioTelephoneNumber
            node1 = new NcXmlFilterNode ("RadioTelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Categories
            node1 = new NcXmlFilterNode ("Categories", RedactionType.FULL, RedactionType.FULL);
            // Picture
            node1 = new NcXmlFilterNode ("Picture", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1);
            
            Root = node0;
        }
    }
}
