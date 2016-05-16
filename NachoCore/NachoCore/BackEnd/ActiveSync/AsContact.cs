//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public partial class AsContact
    {
        /// <summary>
        /// Parse ActiveSync contacts and convert to/from McContacts.
        /// </summary>

        /// Server id
        public string ServerId { get; set; }

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

        /// Business city of the contact
        public string BusinessAddressCity { get; set; }

        /// Business country/region of the contact
        public string BusinessAddressCountry { get; set; }

        /// Business postal code for the contact
        public string BusinessAddressPostalCode { get; set; }

        /// Business state for the contact
        public string BusinessAddressState { get; set; }

        /// Business street address for the contact
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
        public List<string> Categories;
        /// A collection of the contact's children
        public List<string> Children;

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

        /// Index of Body container
        public int BodyId { get; set; }

        /// How the body stored on the server.
        /// Beware: Not documented in MS-ASCNTC.
        public int NativeBodyType { get; set; }

        protected void AddCategoriesFromXml (XNamespace ns, XElement categories)
        {
            NcAssert.True (null != categories);
            NcAssert.True (categories.Name.LocalName.Equals (Xml.Contacts.Categories));
            NcAssert.True (null != Categories);

            foreach (var category in categories.Elements()) {
                NcAssert.True (category.Name.LocalName.Equals (Xml.Contacts.Category));
                Categories.Add (category.Value);
            }
        }

        protected void AddChildrenFromXML (XNamespace ns, XElement children)
        {
            NcAssert.True (null != children);
            NcAssert.True (children.Name.LocalName.Equals (Xml.Contacts.Children));
            NcAssert.True (null != Children);

            foreach (var child in children.Elements()) {
                NcAssert.True (child.Name.LocalName.Equals (Xml.Contacts.Child));
                Children.Add (child.Value);
            }
        }

        public static NcResult FromXML (int accountId, XNamespace ns, XElement command)
        {
            var h = new AsHelpers ();

            // <ServerId>..</ServerId>
            var serverId = command.Element (ns + Xml.AirSync.ServerId);
            NcAssert.True (null != serverId);

            var c = new AsContact ();
            c.Categories = new List<string> ();
            c.Children = new List<string> ();

            c.ServerId = serverId.Value;

            // <ApplicationData>...</ApplicationData>
            var applicationData = command.Element (ns + Xml.AirSync.ApplicationData);
            NcAssert.True (null != applicationData);

            Log.Debug (Log.LOG_XML, "AsContact FromXML\n{0}", applicationData);
            foreach (var child in applicationData.Elements()) {
                switch (child.Name.LocalName) {
                case Xml.AirSyncBase.Body:
                    McAbstrItem tmp = new McAbstrItem () {
                        AccountId = accountId,
                    };
                    tmp.ApplyAsXmlBody (child);
                    c.BodyId = tmp.BodyId;
                    break;
                case Xml.AirSyncBase.NativeBodyType:
                    c.NativeBodyType = child.Value.ToInt ();
                    break;
                case Xml.Contacts.Anniversary:
                case Xml.Contacts.Birthday:
                    h.TrySetDateTimeFromXml (c, child.Name.LocalName, child.Value);
                    break;
                case Xml.Contacts.Categories:
                    c.AddCategoriesFromXml (ns, child);
                    break;
                case Xml.Contacts.Children:
                    c.AddChildrenFromXML (ns, child);
                    break;
                case Xml.Contacts.WeightedRank:
                    h.TrySetIntFromXml (c, child.Name.LocalName, child.Value);
                    break;
                case Xml.Contacts.Alias:
                case Xml.Contacts.AssistantName:
                case Xml.Contacts.AssistantPhoneNumber:
                case Xml.Contacts.Business2PhoneNumber:
                case Xml.Contacts.BusinessAddressCity:
                case Xml.Contacts.BusinessAddressCountry:
                case Xml.Contacts.BusinessAddressPostalCode:
                case Xml.Contacts.BusinessAddressState:
                case Xml.Contacts.BusinessAddressStreet:
                case Xml.Contacts.BusinessFaxNumber:
                case Xml.Contacts.BusinessPhoneNumber:
                case Xml.Contacts.CarPhoneNumber:
                case Xml.Contacts.Category:
                case Xml.Contacts.CompanyName:
                case Xml.Contacts.Department:
                case Xml.Contacts.Email1Address:
                case Xml.Contacts.Email2Address:
                case Xml.Contacts.Email3Address:
                case Xml.Contacts.FileAs:
                case Xml.Contacts.FirstName:
                case Xml.Contacts.Home2PhoneNumber:
                case Xml.Contacts.HomeAddressCity:
                case Xml.Contacts.HomeAddressCountry:
                case Xml.Contacts.HomeAddressPostalCode:
                case Xml.Contacts.HomeAddressState:
                case Xml.Contacts.HomeAddressStreet:
                case Xml.Contacts.HomeFaxNumber:
                case Xml.Contacts.HomePhoneNumber:
                case Xml.Contacts.JobTitle:
                case Xml.Contacts.LastName:
                case Xml.Contacts.MiddleName:
                case Xml.Contacts.MobilePhoneNumber:
                case Xml.Contacts.OfficeLocation:
                case Xml.Contacts.OtherAddressCity:
                case Xml.Contacts.OtherAddressCountry:
                case Xml.Contacts.OtherAddressPostalCode:
                case Xml.Contacts.OtherAddressState:
                case Xml.Contacts.OtherAddressStreet:
                case Xml.Contacts.PagerNumber:
                case Xml.Contacts.Picture:
                case Xml.Contacts.RadioPhoneNumber:
                case Xml.Contacts.Spouse:
                case Xml.Contacts.Suffix:
                case Xml.Contacts.Title:
                case Xml.Contacts.WebPage:
                case Xml.Contacts.YomiCompanyName:
                case Xml.Contacts.YomiFirstName:
                case Xml.Contacts.YomiLastName:
                case Xml.Contacts2.AccountName:
                case Xml.Contacts2.CompanyMainPhone:
                case Xml.Contacts2.CustomerId:
                case Xml.Contacts2.GovernmentId:
                case Xml.Contacts2.IMAddress2:
                case Xml.Contacts2.IMAddress3:
                case Xml.Contacts2.IMAddress:
                case Xml.Contacts2.MMS:
                case Xml.Contacts2.ManagerName:
                case Xml.Contacts2.NickName:
                    h.TrySetStringFromXml (c, child.Name.LocalName, child.Value);
                    break;
                default:
                    Log.Warn (Log.LOG_AS, "ParseContact UNHANDLED: " + child.Name.LocalName + " value=" + child.Value);
                    break;
                }
            }
            return NcResult.OK (c);
        }

        /// <summary>
        /// Create an AsContact from an McContact
        /// </summary>
        /// <returns>An NcResult with an embedded AsContact object</returns>
        /// <param name="c">The McContact to convert.</param>
        public static NcResult FromMcContact (McContact c)
        {
            var n = new AsContact ();

            n.ServerId = c.ServerId;

            n.BodyId = c.BodyId;
            n.NativeBodyType = c.NativeBodyType;

            n.Alias = c.Alias;
            n.CompanyName = c.CompanyName;
            n.Department = c.Department;
            n.FileAs = c.FileAs;
            n.FirstName = c.FirstName;
            n.JobTitle = c.JobTitle;
            n.LastName = c.LastName;
            n.MiddleName = c.MiddleName;
            if (0 != c.PortraitId) {
                var data = McPortrait.GetContentsByteArray (c.PortraitId);
                n.Picture = Convert.ToBase64String (data);
            }
            n.Suffix = c.Suffix;
            n.Title = c.Title;
            n.WebPage = c.WebPage;
            n.WeightedRank = c.WeightedRank;
            n.YomiCompanyName = c.YomiCompanyName;
            n.YomiFirstName = c.YomiFirstName;
            n.YomiLastName = c.YomiLastName;
            n.AccountName = c.AccountName;
            n.CustomerId = c.CustomerId;
            n.GovernmentId = c.GovernmentId;
            n.MMS = c.MMS;
            n.NickName = c.NickName;
            n.OfficeLocation = c.OfficeLocation;

            n.Anniversary = c.GetDateAttribute ("Anniversary");
            n.Birthday = c.GetDateAttribute ("Birthday");

            n.AssistantPhoneNumber = c.GetPhoneNumberAttribute ("AssistantPhoneNumber");
            n.CarPhoneNumber = c.GetPhoneNumberAttribute ("CarPhoneNumber");
            n.MobilePhoneNumber = c.GetPhoneNumberAttribute ("MobilePhoneNumber");
            n.PagerNumber = c.GetPhoneNumberAttribute ("PagerNumber");
            n.RadioPhoneNumber = c.GetPhoneNumberAttribute ("RadioPhoneNumber");
            n.CompanyMainPhone = c.GetPhoneNumberAttribute ("CompanyMainPhone");
            n.BusinessFaxNumber = c.GetPhoneNumberAttribute ("BusinessFaxNumber");
            n.BusinessPhoneNumber = c.GetPhoneNumberAttribute ("BusinessPhoneNumber");
            n.Business2PhoneNumber = c.GetPhoneNumberAttribute ("Business2PhoneNumber");
            n.HomeFaxNumber = c.GetPhoneNumberAttribute ("HomeFaxNumber");
            n.HomePhoneNumber = c.GetPhoneNumberAttribute ("HomePhoneNumber");
            n.Home2PhoneNumber = c.GetPhoneNumberAttribute ("Home2PhoneNumber");

            n.Email1Address = c.GetEmailAddressAttribute ("Email1Address");
            n.Email2Address = c.GetEmailAddressAttribute ("Email2Address");
            n.Email3Address = c.GetEmailAddressAttribute ("Email3Address");

            n.IMAddress = c.GetIMAddressAttribute ("IMAddress");
            n.IMAddress2 = c.GetIMAddressAttribute ("IMAddress2");
            n.IMAddress3 = c.GetIMAddressAttribute ("IMAddress3");

            n.Spouse = c.GetRelationshipAttribute ("Spouse");
            n.AssistantName = c.GetRelationshipAttribute ("AssistantName");
            n.ManagerName = c.GetRelationshipAttribute ("ManagerName");

            n.Children = c.GetRelationshipAttributes ("Child");
            n.Categories = c.GetCategoryAttributes ();

            var business = c.GetAddressAttribute ("Business");
            if (null != business) {
                n.BusinessAddressCity = business.City;
                n.BusinessAddressCountry = business.Country;
                n.BusinessAddressPostalCode = business.PostalCode;
                n.BusinessAddressState = business.State;
                n.BusinessAddressStreet = business.Street;
            }

            var home = c.GetAddressAttribute ("Home");
            if (null != home) {
                n.HomeAddressCity = home.City;
                n.HomeAddressCountry = home.Country;
                n.HomeAddressPostalCode = home.PostalCode;
                n.HomeAddressState = home.State;
                n.HomeAddressStreet = home.Street;
            }

            var other = c.GetAddressAttribute ("Other");
            if (null != other) {
                n.OtherAddressCity = other.City;
                n.OtherAddressCountry = other.Country;
                n.OtherAddressPostalCode = other.PostalCode;
                n.OtherAddressState = other.State;
                n.OtherAddressStreet = other.Street;
            }

            return NcResult.OK (n);
        }

        /// <summary>
        /// Convert an AsContact to an McContact
        /// </summary>
        /// <returns>An NcResult with an embeded AsContact object.</returns>
        public NcResult ToMcContact (int AccountId)
        {
            var c = new McContact ();

            c.Source = McAbstrItem.ItemSource.ActiveSync;
            c.ServerId = ServerId;
            c.AccountId = AccountId;

            c.BodyId = BodyId;
            c.NativeBodyType = NativeBodyType;

            c.Alias = Alias;
            c.CompanyName = CompanyName;
            c.Department = Department;
            c.FileAs = FileAs;
            c.FirstName = FirstName;
            c.JobTitle = JobTitle;
            c.LastName = LastName;
            c.MiddleName = MiddleName;
            c.OfficeLocation = OfficeLocation;
            if (null != Picture) {
                var portrait = McPortrait.InsertFile (AccountId, Convert.FromBase64String (Picture));
                c.PortraitId = portrait.Id;
            }
            c.Suffix = Suffix;
            c.Title = Title;
            c.WebPage = WebPage;
            c.WeightedRank = WeightedRank;
            c.YomiCompanyName = YomiCompanyName;
            c.YomiFirstName = YomiFirstName;
            c.YomiLastName = YomiLastName;
            c.AccountName = AccountName;
            c.CustomerId = CustomerId;
            c.GovernmentId = GovernmentId;
            c.MMS = MMS;
            c.NickName = NickName;

            if (DateTime.MinValue != Anniversary) {
                c.AddDateAttribute (AccountId, "Anniversary", "Anniversary", Anniversary);
            }
            if (DateTime.MinValue != Birthday) {
                c.AddDateAttribute (AccountId, "Birthday", "Birthday", Birthday);
            }
            if (null != Email1Address) {
                c.AddEmailAddressAttribute (AccountId, "Email1Address", "Email", Email1Address);
            }
            if (null != Email2Address) {
                c.AddEmailAddressAttribute (AccountId, "Email2Address", "Email Two", Email2Address);
            }
            if (null != Email3Address) {
                c.AddEmailAddressAttribute (AccountId, "Email3Address", "Email Three", Email3Address);
            }
            if (null != AssistantPhoneNumber) {
                c.AddPhoneNumberAttribute (AccountId, "AssistantPhoneNumber", "Assistant", AssistantPhoneNumber);
            }
            if (null != BusinessFaxNumber) {
                c.AddPhoneNumberAttribute (AccountId, "BusinessFaxNumber", "Business Fax", BusinessFaxNumber);
            }
            if (null != BusinessPhoneNumber) {
                c.AddPhoneNumberAttribute (AccountId, "BusinessPhoneNumber", "Work", BusinessPhoneNumber);
            }
            if (null != Business2PhoneNumber) {
                c.AddPhoneNumberAttribute (AccountId, "Business2PhoneNumber", "Work Two", Business2PhoneNumber);
            }
            if (null != CarPhoneNumber) {
                c.AddPhoneNumberAttribute (AccountId, "CarPhoneNumber", "Car", CarPhoneNumber);
            }
            if (null != HomeFaxNumber) {
                c.AddPhoneNumberAttribute (AccountId, "HomeFaxNumber", "Home Fax", HomeFaxNumber);
            }
            if (null != HomePhoneNumber) {
                c.AddPhoneNumberAttribute (AccountId, "HomePhoneNumber", "Home", HomePhoneNumber);
            }
            if (null != Home2PhoneNumber) {
                c.AddPhoneNumberAttribute (AccountId, "Home2PhoneNumber", "Home Two", Home2PhoneNumber);
            }
            if (null != MobilePhoneNumber) {
                c.AddPhoneNumberAttribute (AccountId, "MobilePhoneNumber", "Mobile", MobilePhoneNumber);
            }
            if (null != PagerNumber) {
                c.AddPhoneNumberAttribute (AccountId, "PagerNumber", "Pager", PagerNumber);
            }
            if (null != RadioPhoneNumber) {
                c.AddPhoneNumberAttribute (AccountId, "RadioPhoneNumber", "Radio", RadioPhoneNumber);
            }
            if (null != CompanyMainPhone) {
                c.AddPhoneNumberAttribute (AccountId, "CompanyMainPhone", "Company Main", CompanyMainPhone);
            }
            if (null != IMAddress) {
                c.AddIMAddressAttribute (AccountId, "IMAddress", null, IMAddress);
            }
            if (null != IMAddress2) {
                c.AddIMAddressAttribute (AccountId, "IMAddress2", null, IMAddress2);
            }
            if (null != IMAddress3) {
                c.AddIMAddressAttribute (AccountId, "IMAddress3", null, IMAddress3);
            }

            McContactAddressAttribute home = null;

            if (null != HomeAddressCity) {
                home = home ?? new McContactAddressAttribute ();
                home.City = HomeAddressCity;
            }
            if (null != HomeAddressCountry) {
                home = home ?? new McContactAddressAttribute ();
                home.Country = HomeAddressCountry;
            }
            if (null != HomeAddressPostalCode) {
                home = home ?? new McContactAddressAttribute ();
                home.PostalCode = HomeAddressPostalCode;
            }
            if (null != HomeAddressState) {
                home = home ?? new McContactAddressAttribute ();
                home.State = HomeAddressState;
            }
            if (null != HomeAddressStreet) {
                home = home ?? new McContactAddressAttribute ();
                home.Street = HomeAddressStreet;
            }

            if (null != home) {
                c.AddAddressAttribute (AccountId, "Home", null, home);
            }

            McContactAddressAttribute business = null;

            if (null != BusinessAddressCity) {
                business = business ?? new McContactAddressAttribute ();
                business.City = BusinessAddressCity;
            }
            if (null != BusinessAddressCountry) {
                business = business ?? new McContactAddressAttribute ();
                business.Country = BusinessAddressCountry;
            }
            if (null != BusinessAddressPostalCode) {
                business = business ?? new McContactAddressAttribute ();
                business.PostalCode = BusinessAddressPostalCode;
            }
            if (null != BusinessAddressState) {
                business = business ?? new McContactAddressAttribute ();
                business.State = BusinessAddressState;
            }
            if (null != BusinessAddressStreet) {
                business = business ?? new McContactAddressAttribute ();
                business.Street = BusinessAddressStreet;
            }

            if (null != business) {
                c.AddAddressAttribute (AccountId, "Business", null, business);
            }

            McContactAddressAttribute other = null;

            if (null != OtherAddressCity) {
                other = other ?? new McContactAddressAttribute ();
                other.City = OtherAddressCity;
            }
            if (null != OtherAddressCountry) {
                other = other ?? new McContactAddressAttribute ();
                other.Country = OtherAddressCountry;
            }
            if (null != OtherAddressPostalCode) {
                other = other ?? new McContactAddressAttribute ();
                other.PostalCode = OtherAddressPostalCode;
            }
            if (null != OtherAddressState) {
                other = other ?? new McContactAddressAttribute ();
                other.State = OtherAddressState;
            }
            if (null != OtherAddressStreet) {
                other = other ?? new McContactAddressAttribute ();
                other.Street = OtherAddressStreet;
            }

            if (null != other) {
                c.AddAddressAttribute (AccountId, "Other", null, other);
            }

            foreach (var s in Categories) {
                c.AddCategoryAttribute (AccountId, s);
            }

            if (null != AssistantName) {
                c.AddRelationshipAttribute (AccountId, "AssistantName", null, AssistantName);
            }
            if (null != ManagerName) {
                c.AddRelationshipAttribute (AccountId, "ManagerName", null, ManagerName);
            }
            if (null != Spouse) {
                c.AddRelationshipAttribute (AccountId, "Spouse", null, Spouse);
            }
            foreach (var s in Children) {
                c.AddChildAttribute (AccountId, "Child", null, s);
            }

            return NcResult.OK (c);
        }
    }
}