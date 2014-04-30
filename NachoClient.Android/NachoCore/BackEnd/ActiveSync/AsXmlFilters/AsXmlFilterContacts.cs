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

            // Anniversary
            node0 = new NcXmlFilterNode ("Anniversary", RedactionType.FULL, RedactionType.FULL);
            // AssistantName
            node0 = new NcXmlFilterNode ("AssistantName", RedactionType.FULL, RedactionType.FULL);
            // AssistantPhoneNumber
            node0 = new NcXmlFilterNode ("AssistantPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Birthday
            node0 = new NcXmlFilterNode ("Birthday", RedactionType.FULL, RedactionType.FULL);
            // Business2PhoneNumber
            node0 = new NcXmlFilterNode ("Business2PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCity
            node0 = new NcXmlFilterNode ("BusinessAddressCity", RedactionType.FULL, RedactionType.FULL);
            // BusinessPhoneNumber
            node0 = new NcXmlFilterNode ("BusinessPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // WebPage
            node0 = new NcXmlFilterNode ("WebPage", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCountry
            node0 = new NcXmlFilterNode ("BusinessAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // Department
            node0 = new NcXmlFilterNode ("Department", RedactionType.FULL, RedactionType.FULL);
            // Email1Address
            node0 = new NcXmlFilterNode ("Email1Address", RedactionType.FULL, RedactionType.FULL);
            // Email2Address
            node0 = new NcXmlFilterNode ("Email2Address", RedactionType.FULL, RedactionType.FULL);
            // Email3Address
            node0 = new NcXmlFilterNode ("Email3Address", RedactionType.FULL, RedactionType.FULL);
            // BusinessFaxNumber
            node0 = new NcXmlFilterNode ("BusinessFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // FileAs
            node0 = new NcXmlFilterNode ("FileAs", RedactionType.FULL, RedactionType.FULL);
            // Alias
            node0 = new NcXmlFilterNode ("Alias", RedactionType.FULL, RedactionType.FULL);
            // WeightedRank
            node0 = new NcXmlFilterNode ("WeightedRank", RedactionType.FULL, RedactionType.FULL);
            // FirstName
            node0 = new NcXmlFilterNode ("FirstName", RedactionType.FULL, RedactionType.FULL);
            // MiddleName
            node0 = new NcXmlFilterNode ("MiddleName", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCity
            node0 = new NcXmlFilterNode ("HomeAddressCity", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCountry
            node0 = new NcXmlFilterNode ("HomeAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // HomeFaxNumber
            node0 = new NcXmlFilterNode ("HomeFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // HomePhoneNumber
            node0 = new NcXmlFilterNode ("HomePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Home2PhoneNumber
            node0 = new NcXmlFilterNode ("Home2PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressPostalCode
            node0 = new NcXmlFilterNode ("HomeAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressState
            node0 = new NcXmlFilterNode ("HomeAddressState", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressStreet
            node0 = new NcXmlFilterNode ("HomeAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // MobilePhoneNumber
            node0 = new NcXmlFilterNode ("MobilePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Suffix
            node0 = new NcXmlFilterNode ("Suffix", RedactionType.FULL, RedactionType.FULL);
            // CompanyName
            node0 = new NcXmlFilterNode ("CompanyName", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCity
            node0 = new NcXmlFilterNode ("OtherAddressCity", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCountry
            node0 = new NcXmlFilterNode ("OtherAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // CarPhoneNumber
            node0 = new NcXmlFilterNode ("CarPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressPostalCode
            node0 = new NcXmlFilterNode ("OtherAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressState
            node0 = new NcXmlFilterNode ("OtherAddressState", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressStreet
            node0 = new NcXmlFilterNode ("OtherAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // PagerNumber
            node0 = new NcXmlFilterNode ("PagerNumber", RedactionType.FULL, RedactionType.FULL);
            // Title
            node0 = new NcXmlFilterNode ("Title", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressPostalCode
            node0 = new NcXmlFilterNode ("BusinessAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // LastName
            node0 = new NcXmlFilterNode ("LastName", RedactionType.FULL, RedactionType.FULL);
            // Spouse
            node0 = new NcXmlFilterNode ("Spouse", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressState
            node0 = new NcXmlFilterNode ("BusinessAddressState", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressStreet
            node0 = new NcXmlFilterNode ("BusinessAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // JobTitle
            node0 = new NcXmlFilterNode ("JobTitle", RedactionType.FULL, RedactionType.FULL);
            // YomiFirstName
            node0 = new NcXmlFilterNode ("YomiFirstName", RedactionType.FULL, RedactionType.FULL);
            // YomiLastName
            node0 = new NcXmlFilterNode ("YomiLastName", RedactionType.FULL, RedactionType.FULL);
            // YomiCompanyName
            node0 = new NcXmlFilterNode ("YomiCompanyName", RedactionType.FULL, RedactionType.FULL);
            // OfficeLocation
            node0 = new NcXmlFilterNode ("OfficeLocation", RedactionType.FULL, RedactionType.FULL);
            // RadioPhoneNumber
            node0 = new NcXmlFilterNode ("RadioPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Picture
            node0 = new NcXmlFilterNode ("Picture", RedactionType.FULL, RedactionType.FULL);
            // Categories
            node0 = new NcXmlFilterNode ("Categories", RedactionType.NONE, RedactionType.NONE);
            // Category
            node1 = new NcXmlFilterNode ("Category", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Categories -> Category
            // Children
            node0 = new NcXmlFilterNode ("Children", RedactionType.NONE, RedactionType.NONE);
            // Child
            node1 = new NcXmlFilterNode ("Child", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Children -> Child
            // Anniversary
            node0 = new NcXmlFilterNode ("Anniversary", RedactionType.FULL, RedactionType.FULL);
            // AssistantName
            node0 = new NcXmlFilterNode ("AssistantName", RedactionType.FULL, RedactionType.FULL);
            // AssistantPhoneNumber
            node0 = new NcXmlFilterNode ("AssistantPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Birthday
            node0 = new NcXmlFilterNode ("Birthday", RedactionType.FULL, RedactionType.FULL);
            // Business2PhoneNumber
            node0 = new NcXmlFilterNode ("Business2PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCity
            node0 = new NcXmlFilterNode ("BusinessAddressCity", RedactionType.FULL, RedactionType.FULL);
            // BusinessPhoneNumber
            node0 = new NcXmlFilterNode ("BusinessPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // WebPage
            node0 = new NcXmlFilterNode ("WebPage", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCountry
            node0 = new NcXmlFilterNode ("BusinessAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // Department
            node0 = new NcXmlFilterNode ("Department", RedactionType.FULL, RedactionType.FULL);
            // Email1Address
            node0 = new NcXmlFilterNode ("Email1Address", RedactionType.FULL, RedactionType.FULL);
            // Email2Address
            node0 = new NcXmlFilterNode ("Email2Address", RedactionType.FULL, RedactionType.FULL);
            // Email3Address
            node0 = new NcXmlFilterNode ("Email3Address", RedactionType.FULL, RedactionType.FULL);
            // BusinessFaxNumber
            node0 = new NcXmlFilterNode ("BusinessFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // FileAs
            node0 = new NcXmlFilterNode ("FileAs", RedactionType.FULL, RedactionType.FULL);
            // Alias
            node0 = new NcXmlFilterNode ("Alias", RedactionType.FULL, RedactionType.FULL);
            // WeightedRank
            node0 = new NcXmlFilterNode ("WeightedRank", RedactionType.FULL, RedactionType.FULL);
            // FirstName
            node0 = new NcXmlFilterNode ("FirstName", RedactionType.FULL, RedactionType.FULL);
            // MiddleName
            node0 = new NcXmlFilterNode ("MiddleName", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCity
            node0 = new NcXmlFilterNode ("HomeAddressCity", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCountry
            node0 = new NcXmlFilterNode ("HomeAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // HomeFaxNumber
            node0 = new NcXmlFilterNode ("HomeFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // HomePhoneNumber
            node0 = new NcXmlFilterNode ("HomePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Home2PhoneNumber
            node0 = new NcXmlFilterNode ("Home2PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressPostalCode
            node0 = new NcXmlFilterNode ("HomeAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressState
            node0 = new NcXmlFilterNode ("HomeAddressState", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressStreet
            node0 = new NcXmlFilterNode ("HomeAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // MobilePhoneNumber
            node0 = new NcXmlFilterNode ("MobilePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Suffix
            node0 = new NcXmlFilterNode ("Suffix", RedactionType.FULL, RedactionType.FULL);
            // CompanyName
            node0 = new NcXmlFilterNode ("CompanyName", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCity
            node0 = new NcXmlFilterNode ("OtherAddressCity", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCountry
            node0 = new NcXmlFilterNode ("OtherAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // CarPhoneNumber
            node0 = new NcXmlFilterNode ("CarPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressPostalCode
            node0 = new NcXmlFilterNode ("OtherAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressState
            node0 = new NcXmlFilterNode ("OtherAddressState", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressStreet
            node0 = new NcXmlFilterNode ("OtherAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // PagerNumber
            node0 = new NcXmlFilterNode ("PagerNumber", RedactionType.FULL, RedactionType.FULL);
            // Title
            node0 = new NcXmlFilterNode ("Title", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressPostalCode
            node0 = new NcXmlFilterNode ("BusinessAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // LastName
            node0 = new NcXmlFilterNode ("LastName", RedactionType.FULL, RedactionType.FULL);
            // Spouse
            node0 = new NcXmlFilterNode ("Spouse", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressState
            node0 = new NcXmlFilterNode ("BusinessAddressState", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressStreet
            node0 = new NcXmlFilterNode ("BusinessAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // JobTitle
            node0 = new NcXmlFilterNode ("JobTitle", RedactionType.FULL, RedactionType.FULL);
            // YomiFirstName
            node0 = new NcXmlFilterNode ("YomiFirstName", RedactionType.FULL, RedactionType.FULL);
            // YomiLastName
            node0 = new NcXmlFilterNode ("YomiLastName", RedactionType.FULL, RedactionType.FULL);
            // YomiCompanyName
            node0 = new NcXmlFilterNode ("YomiCompanyName", RedactionType.FULL, RedactionType.FULL);
            // OfficeLocation
            node0 = new NcXmlFilterNode ("OfficeLocation", RedactionType.FULL, RedactionType.FULL);
            // RadioPhoneNumber
            node0 = new NcXmlFilterNode ("RadioPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Picture
            node0 = new NcXmlFilterNode ("Picture", RedactionType.FULL, RedactionType.FULL);
            // Categories
            node0 = new NcXmlFilterNode ("Categories", RedactionType.NONE, RedactionType.NONE);
            // Category
            node1 = new NcXmlFilterNode ("Category", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Categories -> Category
            // Children
            node0 = new NcXmlFilterNode ("Children", RedactionType.NONE, RedactionType.NONE);
            // Child
            node1 = new NcXmlFilterNode ("Child", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Children -> Child
            // Anniversary
            node0 = new NcXmlFilterNode ("Anniversary", RedactionType.FULL, RedactionType.FULL);
            // Birthday
            node0 = new NcXmlFilterNode ("Birthday", RedactionType.FULL, RedactionType.FULL);
            // WebPage
            node0 = new NcXmlFilterNode ("WebPage", RedactionType.FULL, RedactionType.FULL);
            // Children
            node0 = new NcXmlFilterNode ("Children", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCountry
            node0 = new NcXmlFilterNode ("BusinessAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // Department
            node0 = new NcXmlFilterNode ("Department", RedactionType.FULL, RedactionType.FULL);
            // Email1Address
            node0 = new NcXmlFilterNode ("Email1Address", RedactionType.FULL, RedactionType.FULL);
            // Email2Address
            node0 = new NcXmlFilterNode ("Email2Address", RedactionType.FULL, RedactionType.FULL);
            // Email3Address
            node0 = new NcXmlFilterNode ("Email3Address", RedactionType.FULL, RedactionType.FULL);
            // BusinessFaxNumber
            node0 = new NcXmlFilterNode ("BusinessFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // FileAs
            node0 = new NcXmlFilterNode ("FileAs", RedactionType.FULL, RedactionType.FULL);
            // FirstName
            node0 = new NcXmlFilterNode ("FirstName", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCity
            node0 = new NcXmlFilterNode ("HomeAddressCity", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCountry
            node0 = new NcXmlFilterNode ("HomeAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // HomeFaxNumber
            node0 = new NcXmlFilterNode ("HomeFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // HomePhoneNumber
            node0 = new NcXmlFilterNode ("HomePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Home2PhoneNumber
            node0 = new NcXmlFilterNode ("Home2PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressPostalCode
            node0 = new NcXmlFilterNode ("HomeAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressState
            node0 = new NcXmlFilterNode ("HomeAddressState", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressStreet
            node0 = new NcXmlFilterNode ("HomeAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCity
            node0 = new NcXmlFilterNode ("BusinessAddressCity", RedactionType.FULL, RedactionType.FULL);
            // MiddleName
            node0 = new NcXmlFilterNode ("MiddleName", RedactionType.FULL, RedactionType.FULL);
            // MobilePhoneNumber
            node0 = new NcXmlFilterNode ("MobilePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Suffix
            node0 = new NcXmlFilterNode ("Suffix", RedactionType.FULL, RedactionType.FULL);
            // CompanyName
            node0 = new NcXmlFilterNode ("CompanyName", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCity
            node0 = new NcXmlFilterNode ("OtherAddressCity", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCountry
            node0 = new NcXmlFilterNode ("OtherAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // CarPhoneNumber
            node0 = new NcXmlFilterNode ("CarPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressPostalCode
            node0 = new NcXmlFilterNode ("OtherAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressState
            node0 = new NcXmlFilterNode ("OtherAddressState", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressStreet
            node0 = new NcXmlFilterNode ("OtherAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // PagerNumber
            node0 = new NcXmlFilterNode ("PagerNumber", RedactionType.FULL, RedactionType.FULL);
            // Title
            node0 = new NcXmlFilterNode ("Title", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressPostalCode
            node0 = new NcXmlFilterNode ("BusinessAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // AssistantName
            node0 = new NcXmlFilterNode ("AssistantName", RedactionType.FULL, RedactionType.FULL);
            // AssistantPhoneNumber
            node0 = new NcXmlFilterNode ("AssistantPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // AssistnamePhoneNumber
            node0 = new NcXmlFilterNode ("AssistnamePhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // LastName
            node0 = new NcXmlFilterNode ("LastName", RedactionType.FULL, RedactionType.FULL);
            // Spouse
            node0 = new NcXmlFilterNode ("Spouse", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressState
            node0 = new NcXmlFilterNode ("BusinessAddressState", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressStreet
            node0 = new NcXmlFilterNode ("BusinessAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // BusinessPhoneNumber
            node0 = new NcXmlFilterNode ("BusinessPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Business2PhoneNumber
            node0 = new NcXmlFilterNode ("Business2PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // JobTitle
            node0 = new NcXmlFilterNode ("JobTitle", RedactionType.FULL, RedactionType.FULL);
            // YomiFirstName
            node0 = new NcXmlFilterNode ("YomiFirstName", RedactionType.FULL, RedactionType.FULL);
            // YomiLastName
            node0 = new NcXmlFilterNode ("YomiLastName", RedactionType.FULL, RedactionType.FULL);
            // YomiCompanyName
            node0 = new NcXmlFilterNode ("YomiCompanyName", RedactionType.FULL, RedactionType.FULL);
            // OfficeLocation
            node0 = new NcXmlFilterNode ("OfficeLocation", RedactionType.FULL, RedactionType.FULL);
            // RadioPhoneNumber
            node0 = new NcXmlFilterNode ("RadioPhoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Picture
            node0 = new NcXmlFilterNode ("Picture", RedactionType.FULL, RedactionType.FULL);
            // Categories
            node0 = new NcXmlFilterNode ("Categories", RedactionType.FULL, RedactionType.FULL);
            // Anniversary
            node0 = new NcXmlFilterNode ("Anniversary", RedactionType.FULL, RedactionType.FULL);
            // Birthday
            node0 = new NcXmlFilterNode ("Birthday", RedactionType.FULL, RedactionType.FULL);
            // Webpage
            node0 = new NcXmlFilterNode ("Webpage", RedactionType.FULL, RedactionType.FULL);
            // Children
            node0 = new NcXmlFilterNode ("Children", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCountry
            node0 = new NcXmlFilterNode ("BusinessAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // Department
            node0 = new NcXmlFilterNode ("Department", RedactionType.FULL, RedactionType.FULL);
            // Email1Address
            node0 = new NcXmlFilterNode ("Email1Address", RedactionType.FULL, RedactionType.FULL);
            // Email2Address
            node0 = new NcXmlFilterNode ("Email2Address", RedactionType.FULL, RedactionType.FULL);
            // Email3Address
            node0 = new NcXmlFilterNode ("Email3Address", RedactionType.FULL, RedactionType.FULL);
            // BusinessFaxNumber
            node0 = new NcXmlFilterNode ("BusinessFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // FileAs
            node0 = new NcXmlFilterNode ("FileAs", RedactionType.FULL, RedactionType.FULL);
            // FirstName
            node0 = new NcXmlFilterNode ("FirstName", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCity
            node0 = new NcXmlFilterNode ("HomeAddressCity", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressCountry
            node0 = new NcXmlFilterNode ("HomeAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // HomeFaxNumber
            node0 = new NcXmlFilterNode ("HomeFaxNumber", RedactionType.FULL, RedactionType.FULL);
            // HomeTelephoneNumber
            node0 = new NcXmlFilterNode ("HomeTelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Home2TelephoneNumber
            node0 = new NcXmlFilterNode ("Home2TelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressPostalCode
            node0 = new NcXmlFilterNode ("HomeAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressState
            node0 = new NcXmlFilterNode ("HomeAddressState", RedactionType.FULL, RedactionType.FULL);
            // HomeAddressStreet
            node0 = new NcXmlFilterNode ("HomeAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressCity
            node0 = new NcXmlFilterNode ("BusinessAddressCity", RedactionType.FULL, RedactionType.FULL);
            // MiddleName
            node0 = new NcXmlFilterNode ("MiddleName", RedactionType.FULL, RedactionType.FULL);
            // MobileTelephoneNumber
            node0 = new NcXmlFilterNode ("MobileTelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Suffix
            node0 = new NcXmlFilterNode ("Suffix", RedactionType.FULL, RedactionType.FULL);
            // CompanyName
            node0 = new NcXmlFilterNode ("CompanyName", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCity
            node0 = new NcXmlFilterNode ("OtherAddressCity", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressCountry
            node0 = new NcXmlFilterNode ("OtherAddressCountry", RedactionType.FULL, RedactionType.FULL);
            // CarTelephoneNumber
            node0 = new NcXmlFilterNode ("CarTelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressPostalCode
            node0 = new NcXmlFilterNode ("OtherAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressState
            node0 = new NcXmlFilterNode ("OtherAddressState", RedactionType.FULL, RedactionType.FULL);
            // OtherAddressStreet
            node0 = new NcXmlFilterNode ("OtherAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // PagerNumber
            node0 = new NcXmlFilterNode ("PagerNumber", RedactionType.FULL, RedactionType.FULL);
            // Title
            node0 = new NcXmlFilterNode ("Title", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressPostalCode
            node0 = new NcXmlFilterNode ("BusinessAddressPostalCode", RedactionType.FULL, RedactionType.FULL);
            // AssistantName
            node0 = new NcXmlFilterNode ("AssistantName", RedactionType.FULL, RedactionType.FULL);
            // AssistantTelephoneNumber
            node0 = new NcXmlFilterNode ("AssistantTelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // LastName
            node0 = new NcXmlFilterNode ("LastName", RedactionType.FULL, RedactionType.FULL);
            // Spouse
            node0 = new NcXmlFilterNode ("Spouse", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressState
            node0 = new NcXmlFilterNode ("BusinessAddressState", RedactionType.FULL, RedactionType.FULL);
            // BusinessAddressStreet
            node0 = new NcXmlFilterNode ("BusinessAddressStreet", RedactionType.FULL, RedactionType.FULL);
            // BusinessTelephoneNumber
            node0 = new NcXmlFilterNode ("BusinessTelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Business2TelephoneNumber
            node0 = new NcXmlFilterNode ("Business2TelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // JobTitle
            node0 = new NcXmlFilterNode ("JobTitle", RedactionType.FULL, RedactionType.FULL);
            // YomiFirstName
            node0 = new NcXmlFilterNode ("YomiFirstName", RedactionType.FULL, RedactionType.FULL);
            // YomiLastName
            node0 = new NcXmlFilterNode ("YomiLastName", RedactionType.FULL, RedactionType.FULL);
            // YomiCompanyName
            node0 = new NcXmlFilterNode ("YomiCompanyName", RedactionType.FULL, RedactionType.FULL);
            // OfficeLocation
            node0 = new NcXmlFilterNode ("OfficeLocation", RedactionType.FULL, RedactionType.FULL);
            // RadioTelephoneNumber
            node0 = new NcXmlFilterNode ("RadioTelephoneNumber", RedactionType.FULL, RedactionType.FULL);
            // Categories
            node0 = new NcXmlFilterNode ("Categories", RedactionType.FULL, RedactionType.FULL);
            // Picture
            node0 = new NcXmlFilterNode ("Picture", RedactionType.FULL, RedactionType.FULL);
            
            Root = node0;
        }
    }
}
