using SQLite;
using System;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class NcContact : NcItem
    {
        public string ClassName = "NcContact";

        /// <summary>
        /// Contacts schema
        /// </summary>

        /// User's alias
        public string Alias { get; set; }

        /// Wedding anniversary
        public DateTime Anniversary { get; set; }

        /// Name of the contacts assistant
        public string AssistantName { get; set; }

        /// Phone number of the contact's assistant
        public string AssistantPhoneNumber { get; set; }

        /// Birth date of the contact
        public DateTime Birthday { get; set; }

        /// Notes for the contact
        // TODO: Add body

        /// Business city of the contact
        public string BusinessAddressCity { get; set; }

        /// Business country/region of the contact
        public string BusinessAddressCountry { get; set; }

        /// Business postal code for the contact
        public string BusinessAddressPostalCode { get; set; }

        /// Business state for the contact
        public string BusinessAddressState { get; set; }

        /// Business stree address for the contact
        public string BusinessAddressStreet { get; set; }

        /// Business fax number for the contact
        public string BusinessFaxNumber { get; set; }

        /// Business phone number for the contact
        public string BusinessPhoneNumber { get; set; }

        /// Secondary business telephone number for the contact
        public string Business2PhoneNumber { get; set; }

        /// Car telephone number for the contact
        public string CarPhoneNumber { get; set; }

        /// A collection of user labels assigned to the contact
        public List<NcContactCategory> categories;

        /// A collection of the contact's children (pickle)
        public string Children { get; set; }

        /// Company name for the contact
        public string CompanyName { get; set; }

        /// Department name for the contact
        public string Department { get; set; }

        /// First e-mail address for the contact.k
        public string Email1Address { get; set; }

        /// Second e-mail address for the contact
        public string Email2Address { get; set; }

        /// Third e-mail address for the contact
        public string Email3Address { get; set; }

        /// Specifies how a contact is filed in the Contacts folder
        public string FileAs { get; set; }

        /// First name of the contact
        public string FirstName { get; set; }

        /// Home city for the contact
        public string HomeAddressCity { get; set; }

        /// Home country/region for the contact
        public string HomeAddressCountry { get; set; }

        /// Home postal code for the contact
        public string HomeAddressPostalCode { get; set; }

        /// Home state for the contact
        public string HomeAddressState { get; set; }

        /// Home street address for the contact
        public string HomeAddressStreet { get; set; }

        /// Home fax number for the contact
        public string HomeFaxNumber { get; set; }

        /// Home phone number for the contact
        public string HomePhoneNumber { get; set; }

        /// Alternative home phone number for the contact
        public string Home2PhoneNumber { get; set; }

        /// Contact's job title
        public string JobTitle { get; set; }

        /// Contact's last name
        public string LastName { get; set; }

        /// Middle name of the contact
        public string MiddleName { get; set; }

        /// Th emobile phone number for the contact
        public string MobilePhoneNumber { get; set; }

        /// Office location for the contact
        public string OfficeLocation { get; set; }

        /// City for the contact's alternate address
        public string OtherAddressCity { get; set; }

        /// Country/region of the contact's alternate address
        public string OtherAddressCountry { get; set; }

        /// Postal code of the contact's alternate address
        public string OtherAddressPostalCode { get; set; }

        /// State of the contact's alternate address
        public string OtherAddressState { get; set; }

        /// Street address of the contact's alternate address
        public string OtherAddressStreet { get; set; }

        /// Pager number for the contact
        public string PagerNumber { get; set; }

        /// Picture of the contact (base64 encoded)
        public string Picture { get; set; }

        /// Radio phone number for the contact
        public string RadioPhoneNumber { get; set; }

        /// Name of the contact's spouse/partner
        public string Spouse { get; set; }

        /// Suffix for the contact's name
        public string Suffix { get; set; }

        /// Contact's business title
        public string Title { get; set; }

        /// Web site or personal Web page for the contact
        public string WebPage { get; set; }

        /// Rank of this contact entry in the recipient information cache
        public int WeightedRank { get; set; }

        /// Japanese phonetic rendering of the company name for the contact
        public string YomiCompanyName { get; set; }

        /// Japanese phonetic rendering of the first name of the contact
        public string YomiFirstName { get; set; }

        /// Japanese phonetic rendering of the last name of the contact
        public string YomiLastName { get; set; }

        /// <summary>
        /// Contacts2 schema
        /// </summary>
 
        /// Account name and/or number of the contact
        public string AccountName { get; set; }

        /// Main telephone number for the contact's company
        public string CompanyMainPhone { get; set; }

        /// Customer identifier (ID) for the contact
        public string CustomerId { get; set; }

        /// Government-assigned identifier (ID) for the contact
        public string GovernmentId { get; set; }

        /// Instant messaging address for the contact
        public string IMAddress { get; set; }

        /// Alternative instant messaging address for the contact
        public string IMAddress2 { get; set; }

        /// Tertiary instant messaging address for the contact
        public string IMAddress3 { get; set; }

        /// Distinguished name of the contact's manager
        public string ManagerName { get; set; }

        /// Multimedia Messaging Service (MMS) address for the contact
        public string MMS { get; set; }

        /// Nickname for the contact
        public string NickName { get; set; }
    }

    public class NcContactCategory : NcObject
    {
        public string Category { get; set; }

    }
}
