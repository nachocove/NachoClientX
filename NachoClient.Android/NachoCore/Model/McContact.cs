using SQLite;
using System;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    public partial class McContact : McItem
    {
        /// <summary>
        /// Contacts schema
        /// Contacts are associated with folders, real or pseudo.
        /// </summary>
        /// 


        /// ActiveSync or Device
        public McItem.ItemSource Source { get; set; }

        /// The collection of important dates associated with the contact
        public List<McContactDateAttribute> Dates;
        /// The collection addresses associated with the contact
        public List<McContactAddressAttribute> Addresses;
        /// The collection of phone numbers associated with the contact
        public List<McContactStringAttribute> PhoneNumbers;
        /// The collection of email addresses associated with the contact
        public List<McContactStringAttribute> EmailAddresses;
        /// The collection of instant messaging addresses associated with the contact
        public List<McContactStringAttribute> IMAddresses;
        /// The collection of relationsihps assigned to the contact.
        public List<McContactStringAttribute> Relationships;
        /// The collection of user labels assigned to the contact
        public List<McContactStringAttribute> Categories;
        // Valid only for GAL-cache entries.
        public string GalCacheToken { get; set; }

        /// Reference count.
        [Indexed]
        public uint RefCount { get; set; }

        /// First name of the contact
        [Indexed]
        public string FirstName { get; set; }

        /// Middle name of the contact
        public string MiddleName { get; set; }

        /// Contact's last name
        [Indexed]
        public string LastName { get; set; }

        /// Suffix for the contact's name
        public string Suffix { get; set; }

        /// User's alias
        public string Alias { get; set; }

        /// Contact's business title
        public string Title { get; set; }

        /// Contact's job title
        public string JobTitle { get; set; }

        /// Department name for the contact
        public string Department { get; set; }

        /// Company name for the contact
        [Indexed]
        public string CompanyName { get; set; }

        /// Account name and/or number of the contact
        public string AccountName { get; set; }

        /// Customer identifier (ID) for the contact
        public string CustomerId { get; set; }

        /// Government-assigned identifier (ID) for the contact
        public string GovernmentId { get; set; }

        /// Office location for the contact
        public string OfficeLocation { get; set; }

        /// Specifies how a contact is filed in the Contacts folder
        public string FileAs { get; set; }

        /// Picture of the contact (base64 encoded)
        public string Picture { get; set; }

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

        /// Multimedia Messaging Service (MMS) address for the contact
        public string MMS { get; set; }

        /// Nickname for the contact
        public string NickName { get; set; }

        /// Index of Body container
        public int BodyId { get; set; }

        /// How the body stored on the server.
        public int NativeBodyType { get; set; }

        // "Hotness" of the contact. Currently, updated by the emails.
        public int Score { get; set; }

        public static ClassCodeEnum GetClassCode ()
        {
            return McFolderEntry.ClassCodeEnum.Contact;
        }
    }

    /// Addresses associated with the contact
    public class McContactAddress : McObject
    {
        [Indexed]
        public Int64 ContactId { get; set; }

        /// iOS and Android allow custom fields
        public bool UserDefined { get; set; }
    }

    /// Used to create lists of name/value pairs
    public class McContactAttribute : McObject
    {
        [Indexed]
        public Int64 ContactId { get; set; }

        /// Values are created & displayed in a certain order
        public int Order { get; set; }

        /// Field name
        public string Name { get; set; }

        /// User-defined label if one exists
        public string Label { get; set; }
    }

    /// <summary>
    /// Date attributes such as birthdays and anniversaries
    /// </summary>
    public class McContactDateAttribute : McContactAttribute
    {
        public DateTime Value { get; set; }
    }

    /// <summary>
    /// Data types stored in the string table.
    /// </summary>
    public enum McContactStringType
    {
        Relationship,
        EmailAddress,
        PhoneNumber,
        IMAddress,
        Category,
        Address,
        Date,
    }

    /// <summary>
    /// String attributes, such as email, phone numbers, im addresses
    /// </summary>
    public class McContactStringAttribute : McContactAttribute
    {
        [Indexed]
        public McContactStringType Type { get; set; }

        [Indexed]
        public string Value { get; set; }
    }

    /// <summary>
    /// Address attributes, like business or home address
    /// </summary>
    public class McContactAddressAttribute : McContactAttribute
    {
        /// Street address of the contact's alternate address
        public string Street { get; set; }

        /// City for the contact's alternate address
        public string City { get; set; }

        /// State of the contact's alternate address
        public string State { get; set; }

        /// Country/region of the contact's alternate address
        public string Country { get; set; }

        /// Postal code of the contact's alternate address
        public string PostalCode { get; set; }
    }

    public partial class McContact
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NachoCore.Model.McContact"/> class.
        /// By convention, the lists are always initialized because C# is stupid about nulls.
        /// </summary>
        public McContact ()
        {
            HasReadAncillaryData = false;
            Dates = new List<McContactDateAttribute> ();
            Addresses = new List<McContactAddressAttribute> ();
            PhoneNumbers = new List<McContactStringAttribute> ();
            EmailAddresses = new List<McContactStringAttribute> ();
            IMAddresses = new List<McContactStringAttribute> ();
            Relationships = new List<McContactStringAttribute> ();
            Categories = new List<McContactStringAttribute> ();
        }

        public McContact (McItem.ItemSource source) : this ()
        {
            Source = source;
        }

        private string displayName;

        [Ignore]
        /// <summary>
        /// Gets the display name.
        /// </summary>
        /// <value>The display name is calculated unless set non-null.</value>
        public string DisplayName {
            get {
                if (null != displayName) {
                    return displayName;
                }
                if ((null != FirstName) && (null != LastName)) {
                    return FirstName + " " + LastName;
                } else if ((null != FirstName) || (null != LastName)) {
                    return (FirstName ?? "") + (LastName ?? "");
                } else {
                    return DisplayEmailAddress;
                }
            }
            protected set {
                displayName = value;
            }
        }

        [Ignore]
        /// <summary>
        /// Gets the display email address.
        /// </summary>
        /// <value>The display email address.</value>
        public string DisplayEmailAddress {
            get {
                if (null == EmailAddresses) {
                    return "";
                }
                if (0 == EmailAddresses.Count ()) {
                    return "";
                }
                return EmailAddresses.First ().Value;
            }
        }

        public static bool IsNull (DateTime t)
        {
            return (DateTime.MinValue == t);
        }

        public static bool IsNull (int i)
        {
            return (int.MinValue == 1);
        }

        /// <summary>
        /// Gets a date from the contact record.
        /// </summary>
        public DateTime GetDateAttribute (string name)
        {
            foreach (var l in Dates) {
                if (l.Name.Equals (name)) {
                    return l.Value;
                }
            }
            return DateTime.MinValue;       
        }

        /// <summary>
        /// Gets an address from the contact record.
        /// </summary>
        public McContactAddressAttribute GetAddressAttribute (string name)
        {
            foreach (var l in Addresses) {
                if (l.Name.Equals (name)) {
                    return l;
                }
            }
            return null;       
        }

        public string GetStringAttribute (List<McContactStringAttribute> list, McContactStringType type, string name)
        {
            foreach (var l in list) {
                if (l.Type.Equals (type) && l.Name.Equals (name)) {
                    return l.Value;
                }
            }
            return null;
        }

        public string GetPhoneNumberAttribute (string name)
        {
            return GetStringAttribute (PhoneNumbers, McContactStringType.PhoneNumber, name);
        }

        public string GetEmailAddressAttribute (string name)
        {
            return GetStringAttribute (EmailAddresses, McContactStringType.EmailAddress, name);
        }

        public string GetIMAddressAttribute (string name)
        {
            return GetStringAttribute (IMAddresses, McContactStringType.IMAddress, name);
        }

        public string GetRelationshipAttribute (string name)
        {
            return GetStringAttribute (Relationships, McContactStringType.Relationship, name);
        }

        public List<string> GetRelationshipAttributes (string name)
        {
            var l = new List<string> ();
            foreach (var r in Relationships) {
                if (r.Name.Equals (name)) {
                    l.Add (r.Value);
                }
            }
            return l;
        }

        public List<string> GetCategoryAttributes ()
        {
            var l = new List<string> ();
            foreach (var c in Categories) {
                l.Add (c.Name);
            }
            return l;
        }

        public void AddDateAttribute (string name, string label, DateTime value)
        {
            var f = new McContactDateAttribute ();
            f.Name = name;
            f.Label = label;
            f.Value = value;
            f.ContactId = this.Id;
            Dates.Add (f);
        }

        public void AddAddressAttribute (string name, string label, McContactAddressAttribute value)
        {
            var f = new McContactAddressAttribute ();
            f.Name = name;
            f.Label = label;
            f.Street = value.Street;
            f.City = value.City;
            f.State = value.State;
            f.Country = value.Country;
            f.PostalCode = value.PostalCode;
            f.ContactId = this.Id;
            Addresses.Add (f);
        }

        protected void AddStringAttribute (ref List<McContactStringAttribute> list, McContactStringType type, string name, string label, string value)
        {
            var f = new McContactStringAttribute ();
            f.Name = name;
            f.Type = type;
            f.Label = label;
            f.Value = value;
            f.ContactId = this.Id;
            list.Add (f);
        }

        public void AddPhoneNumberAttribute (string name, string label, string value)
        {
            AddStringAttribute (ref PhoneNumbers, McContactStringType.PhoneNumber, name, label, value);
        }

        public void AddOrUpdatePhoneNumberAttribute (string name, string label, string value)
        {
            AddOrUpdateStringAttribute (ref PhoneNumbers, McContactStringType.PhoneNumber, name, label, value);
        }

        public void AddEmailAddressAttribute (string name, string label, string value)
        {
            AddStringAttribute (ref EmailAddresses, McContactStringType.EmailAddress, name, label, value);
        }

        public void AddOrUpdateEmailAddressAttribute (string name, string label, string value)
        {
            AddOrUpdateStringAttribute (ref EmailAddresses, McContactStringType.EmailAddress, name, label, value);
        }

        public void AddIMAddressAttribute (string name, string label, string value)
        {
            AddStringAttribute (ref IMAddresses, McContactStringType.IMAddress, name, label, value);
        }

        public void AddRelationshipAttribute (string name, string label, string value)
        {
            AddStringAttribute (ref Relationships, McContactStringType.Relationship, name, label, value);
        }

        public void AddCategoryAttribute (string name)
        {
            AddStringAttribute (ref Categories, McContactStringType.Category, name, null, null);
        }

        protected void AddOrUpdateStringAttribute (ref List<McContactStringAttribute> list, McContactStringType type, string name, string label, string value)
        {
            var existing = list.SingleOrDefault (attr => attr.Type.Equals (type) && attr.Name.Equals (name));
            if (null != existing) {
                existing.Label = label;
                existing.Value = value;
            } else {
                var newbie = new McContactStringAttribute ();
                newbie.Name = name;
                newbie.Type = type;
                newbie.Label = label;
                newbie.Value = value;
                newbie.ContactId = this.Id;
                list.Add (newbie);
            }
        }
    }

    partial class McContact
    {
        /// <summary>
        ///        Db.CreateTable<McContact> ();
        ///        Db.CreateTable<McContactDateAttribute> ();
        ///        Db.CreateTable<McContactStringAttribute> ();
        ///        Db.CreateTable<McContactAddressAttribute> ();
        /// </summary>
        /// 
      
        protected bool HasReadAncillaryData;

        /// <summary>
        /// Read the specified db and pk.
        /// </summary>
        /// <param name="db">Db.</param>
        /// <param name="pk">Pk.</param>
        public static NcResult Read (SQLiteConnection db, Int64 pk)
        {
            var c = db.Table<McContact> ().Where (x => x.Id == pk).SingleOrDefault ();
            NachoCore.NachoAssert.True (null != c);
            c.ReadAncillaryData (db);
            // TODO: Error handling
            return NcResult.OK (c);
        }

        public NcResult ReadAncillaryData (SQLiteConnection db)
        {
            if (!HasReadAncillaryData) {
                HasReadAncillaryData = true;
                return ForceReadAncillaryData (db);
            }
            return NcResult.OK ();
        }

        public NcResult ForceReadAncillaryData (SQLiteConnection db)
        {
            Dates = db.Table<McContactDateAttribute> ().Where (x => x.ContactId == Id).ToList ();
            Addresses = db.Table<McContactAddressAttribute> ().Where (x => x.ContactId == Id).ToList ();
            Relationships = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.Relationship).ToList ();
            EmailAddresses = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.EmailAddress).ToList ();
            PhoneNumbers = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.PhoneNumber).ToList ();
            IMAddresses = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.IMAddress).ToList ();
            Categories = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.Category).ToList ();

            // FIXME: Error handling
            return NcResult.OK ();
        }

        public NcResult InsertAncillaryData (SQLiteConnection db)
        {
            NachoCore.NachoAssert.True (0 < Id);

            // TODO: Fix this hammer?
            DeleteAncillaryData (db);

            foreach (var o in Dates) {
                o.ContactId = Id;
                db.Insert (o);
            }
            foreach (var o in Addresses) {
                o.ContactId = Id;
                db.Insert (o);
            }
            foreach (var o in Relationships) {
                o.ContactId = Id;
                db.Insert (o);
            }
            foreach (var o in EmailAddresses) {
                o.ContactId = Id;
                db.Insert (o);
            }
            foreach (var o in PhoneNumbers) {
                o.ContactId = Id;
                db.Insert (o);
            }
            foreach (var o in IMAddresses) {
                o.ContactId = Id;
                db.Insert (o);
            }
            foreach (var o in Categories) {
                o.ContactId = Id;
                db.Insert (o);
            }
    
            // FIXME: Error handling
            return NcResult.OK ();
        }

        public override int Insert ()
        {
            // FIXME db transaction.
            int retval = base.Insert ();
            InsertAncillaryData (BackEnd.Instance.Db);
            return retval;
        }

        public override int Update ()
        {
            int retval = base.Update ();
            InsertAncillaryData (BackEnd.Instance.Db);
            return retval;
        }

        public override int Delete ()
        {
            int retval = base.Delete ();
            DeleteAncillaryData (BackEnd.Instance.Db);
            return retval;
        }

        private NcResult DeleteAncillaryData (SQLiteConnection db)
        {
            var dates = db.Table<McContactDateAttribute> ().Where (x => x.ContactId == Id).ToList ();
            foreach (var d in dates) {
                db.Delete (d);
            }
            var strings = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id).ToList ();
            foreach (var s in strings) {
                db.Delete (s);
            }
            var addresses = db.Table<McContactAddressAttribute> ().Where (x => x.ContactId == Id).ToList ();
            foreach (var a in addresses) {
                db.Delete (a);
            }
            // TODO: Add error processing
            return NcResult.OK ();
        }

        public void RefreshFromGalXml (XElement xmlProperties)
        {
            var props = xmlProperties.Elements ();
            foreach (var prop in props) {
                switch (prop.Name.LocalName) {
                case Xml.Gal.Alias:
                    Alias = prop.Value;
                    break;

                case Xml.Gal.Company:
                    CompanyName = prop.Value;
                    break;

                case Xml.Gal.Data:
                    // FIXME.
                    Log.Warn (Log.LOG_AS, "Xml.Gal.Data not yet implemented.");
                    break;

                case Xml.Gal.DisplayName:
                    DisplayName = prop.Value;
                    break;

                case Xml.Gal.EmailAddress:
                    AddOrUpdateEmailAddressAttribute (Xml.Contacts.Email1Address, null, prop.Value);
                    break;

                case Xml.Gal.FirstName:
                    FirstName = prop.Value;
                    break;

                case Xml.Gal.HomePhone:
                    AddOrUpdatePhoneNumberAttribute (Xml.Contacts.HomePhoneNumber, null, prop.Value);
                    break;

                case Xml.Gal.LastName:
                    LastName = prop.Value;
                    break;

                case Xml.Gal.MobilePhone:
                    AddOrUpdatePhoneNumberAttribute (Xml.Contacts.MobilePhoneNumber, null, prop.Value);
                    break;
                
                case Xml.Gal.Office:
                    OfficeLocation = prop.Value;
                    break;

                case Xml.Gal.Phone:
                    AddOrUpdatePhoneNumberAttribute (Xml.Contacts.BusinessPhoneNumber, null, prop.Value);
                    break;

                case Xml.Gal.Picture:
                    // FIXME.
                    Log.Warn (Log.LOG_AS, "Xml.Gal.Picture not yet implemented.");
                    break;

                default:
                    Log.Error (Log.LOG_AS, "Unknown GAL property {0}.", prop.Name.LocalName);
                    break;
                }
            }
        }

        private void ApplyDateTime (XElement applyTo, XNamespace ns, string name)
        {
            var dateVal = GetDateAttribute (name);
            if (DateTime.MinValue != dateVal) {
                applyTo.Add (new XElement (ns + name, dateVal.ToString (AsHelpers.DateTimeFmt1)));
            }
        }

        private void ApplyPhoneString (XElement applyTo, XNamespace ns, string name)
        {
            var stringVal = GetPhoneNumberAttribute (name);
            if (null != stringVal) {
                applyTo.Add (new XElement (ns + name, stringVal));
            }
        }

        private void ApplyEmailAddressString (XElement applyTo, XNamespace ns, string name)
        {
            var stringVal = GetEmailAddressAttribute (name);
            if (null != stringVal) {
                applyTo.Add (new XElement (ns + name, stringVal));
            }
        }

        private void ApplyIMAddressString (XElement applyTo, XNamespace ns, string name)
        {
            var stringVal = GetIMAddressAttribute (name);
            if (null != stringVal) {
                applyTo.Add (new XElement (ns + name, stringVal));
            }
        }

        private void ApplyRelationshipString (XElement applyTo, XNamespace ns, string name)
        {
            var stringVal = GetRelationshipAttribute (name);
            if (null != stringVal) {
                applyTo.Add (new XElement (ns + name, stringVal));
            }
        }

        private void ApplyAddress (XElement applyTo, XNamespace ns, string nameCity)
        {
            var baseName = nameCity.Replace ("AddressCity", string.Empty);
            var address = GetAddressAttribute (baseName);
            if (null != address) {
                if (null != address.City) {
                    applyTo.Add (new XElement (ns + (baseName + "AddressCity"), address.City));
                }
                if (null != address.Country) {
                    applyTo.Add (new XElement (ns + (baseName + "AddressCountry"), address.Country));
                }
                if (null != address.PostalCode) {
                    applyTo.Add (new XElement (ns + (baseName + "AddressPostalCode"), address.PostalCode));
                }
                if (null != address.State) {
                    applyTo.Add (new XElement (ns + (baseName + "AddressState"), address.State));
                }
                if (null != address.Street) {
                    applyTo.Add (new XElement (ns + (baseName + "AddressStreet"), address.Street));
                }
            }
        }

        public XElement ToXmlApplicationData ()
        {
            XNamespace AirSyncNs = Xml.AirSync.Ns;
            XNamespace ContactsNs = Xml.Contacts.Ns;
            XNamespace Contacts2Ns = Xml.Contacts2.Ns;
            XNamespace AirSyncBaseNs = Xml.AirSyncBase.Ns;

            var xmlAppData = new XElement (AirSyncNs + Xml.AirSync.ApplicationData);

            if (0 != BodyId) {
                var body = McBody.QueryById<McBody> (BodyId);
                NachoAssert.True (null != body);
                xmlAppData.Add (new XElement (AirSyncBaseNs + Xml.AirSyncBase.Body,
                    new XElement (AirSyncBaseNs + Xml.AirSyncBase.Type, (uint)Xml.AirSync.TypeCode.PlainText_1),
                    new XElement (AirSyncBaseNs + Xml.AirSyncBase.Data, body.Body)));
            }

            if (0 != NativeBodyType) {
                xmlAppData.Add (new XElement (AirSyncBaseNs + Xml.AirSyncBase.NativeBodyType, NativeBodyType));
            }
            if (null != Alias) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.Alias, Alias));
            }
            if (null != CompanyName) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.CompanyName, CompanyName));
            }
            if (null != Department) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.Department, Department));
            }
            if (null != FileAs) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.FileAs, FileAs));
            }
            if (null != FirstName) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.FirstName, FirstName));
            }
            if (null != JobTitle) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.JobTitle, JobTitle));
            }
            if (null != LastName) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.LastName, LastName));
            }
            if (null != MiddleName) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.MiddleName, MiddleName));
            }
            if (null != Picture) {
                // FIXME - we may not need to send this.
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.Picture, Picture));
            }
            if (null != Suffix) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.Suffix, Suffix));
            }
            if (null != Title) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.Title, Title));
            }
            if (null != WebPage) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.WebPage, WebPage));
            }
            if (null != YomiCompanyName) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.YomiCompanyName, YomiCompanyName));
            }
            if (null != YomiFirstName) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.YomiFirstName, YomiFirstName));
            }
            if (null != YomiLastName) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.YomiLastName, YomiLastName));
            }
            if (null != AccountName) {
                xmlAppData.Add (new XElement (Contacts2Ns + Xml.Contacts2.AccountName, AccountName));
            }
            if (null != CustomerId) {
                xmlAppData.Add (new XElement (Contacts2Ns + Xml.Contacts2.CustomerId, CustomerId));
            }
            if (null != GovernmentId) {
                xmlAppData.Add (new XElement (Contacts2Ns + Xml.Contacts2.GovernmentId, GovernmentId));
            }
            if (null != MMS) {
                xmlAppData.Add (new XElement (Contacts2Ns + Xml.Contacts2.MMS, MMS));
            }
            if (null != NickName) {
                xmlAppData.Add (new XElement (Contacts2Ns + Xml.Contacts2.NickName, NickName));
            }
            if (null != OfficeLocation) {
                xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.OfficeLocation, OfficeLocation));
            }
            // We don't write to WeightedRank.

            ApplyDateTime (xmlAppData, ContactsNs, Xml.Contacts.Anniversary);
            ApplyDateTime (xmlAppData, ContactsNs, Xml.Contacts.Birthday);
            ApplyPhoneString (xmlAppData, ContactsNs, Xml.Contacts.AssistantPhoneNumber);
            ApplyPhoneString (xmlAppData, ContactsNs, Xml.Contacts.CarPhoneNumber);
            ApplyPhoneString (xmlAppData, ContactsNs, Xml.Contacts.MobilePhoneNumber);
            ApplyPhoneString (xmlAppData, ContactsNs, Xml.Contacts.PagerNumber);
            ApplyPhoneString (xmlAppData, ContactsNs, Xml.Contacts.RadioPhoneNumber);
            ApplyPhoneString (xmlAppData, Contacts2Ns, Xml.Contacts2.CompanyMainPhone);
            ApplyPhoneString (xmlAppData, ContactsNs, Xml.Contacts.BusinessFaxNumber);
            ApplyPhoneString (xmlAppData, ContactsNs, Xml.Contacts.Business2PhoneNumber);
            ApplyPhoneString (xmlAppData, ContactsNs, Xml.Contacts.HomeFaxNumber);
            ApplyPhoneString (xmlAppData, ContactsNs, Xml.Contacts.HomePhoneNumber);
            ApplyPhoneString (xmlAppData, ContactsNs, Xml.Contacts.Home2PhoneNumber);

            ApplyEmailAddressString (xmlAppData, ContactsNs, Xml.Contacts.Email1Address);
            ApplyEmailAddressString (xmlAppData, ContactsNs, Xml.Contacts.Email2Address);
            ApplyEmailAddressString (xmlAppData, ContactsNs, Xml.Contacts.Email3Address);

            ApplyIMAddressString (xmlAppData, Contacts2Ns, Xml.Contacts2.IMAddress);
            ApplyIMAddressString (xmlAppData, Contacts2Ns, Xml.Contacts2.IMAddress2);
            ApplyIMAddressString (xmlAppData, Contacts2Ns, Xml.Contacts2.IMAddress3);

            ApplyRelationshipString (xmlAppData, ContactsNs, Xml.Contacts.Spouse);
            ApplyRelationshipString (xmlAppData, ContactsNs, Xml.Contacts.AssistantName);
            ApplyRelationshipString (xmlAppData, Contacts2Ns, Xml.Contacts2.ManagerName);

            // FIXME - No child support yet ;-).

            var cats = GetCategoryAttributes ();
            if (0 < cats.Count) {
                var xmlCategories = new XElement (ContactsNs + Xml.Contacts.Categories);
                foreach (var cat in cats) {
                    xmlCategories.Add (new XElement (ContactsNs + Xml.Contacts.Category, cat));
                }
                xmlAppData.Add (xmlCategories);
            }

            ApplyAddress (xmlAppData, ContactsNs, Xml.Contacts.BusinessAddressCity);
            ApplyAddress (xmlAppData, ContactsNs, Xml.Contacts.HomeAddressCity);
            ApplyAddress (xmlAppData, ContactsNs, Xml.Contacts.OtherAddressCity);

            return xmlAppData;
        }

        public static McContact CreateFromGalXml (int accountId, XElement xmlProperties)
        {
            var contact = new McContact (ItemSource.ActiveSync);
            contact.AccountId = accountId;
            contact.RefreshFromGalXml (xmlProperties);
            return contact;
        }

        private List<McContactStringAttribute> QueryAncillaryString (McContactStringType type)
        {
            return BackEnd.Instance.Db.Query<McContactStringAttribute> ("SELECT * FROM " + 
                "McContactStringAttribute WHERE ContactId = ? AND Type = ?", 
                Id, type).ToList();
        }

        private void QueryAncillaryData ()
        {
            EmailAddresses = QueryAncillaryString (McContactStringType.EmailAddress);
            PhoneNumbers = QueryAncillaryString (McContactStringType.PhoneNumber);
            IMAddresses = QueryAncillaryString (McContactStringType.IMAddress);
            Relationships = QueryAncillaryString (McContactStringType.Relationship);
            Categories = QueryAncillaryString (McContactStringType.Category);
            Dates = BackEnd.Instance.Db.Query<McContactDateAttribute> ("SELECT * FROM " + 
                "McContactDateAttribute WHERE ContactId = ?", Id).ToList();
            Addresses = BackEnd.Instance.Db.Query<McContactAddressAttribute> ("SELECT * FROM " + 
                "McContactAddressAttribute WHERE ContactId = ?", Id).ToList();
        }

        private static void QueryAncillaryDataList (List<McContact> contactList)
        {
            for (int n = 0; n < contactList.Count; n++) {
                contactList [n].QueryAncillaryData ();
            }
        }

        public static List<McContact> QueryByEmailAddress (int accountId, string emailAddress)
        {
            List<McContact> contactList = BackEnd.Instance.Db.Query<McContact> ("SELECT c.* FROM McContact AS c JOIN McContactStringAttribute AS s ON c.Id = s.ContactId WHERE " +
            " c.AccountId = ? AND " +
            " s.Type = ? AND " +
            " s.Value = ? ",
                accountId, McContactStringType.EmailAddress, emailAddress).ToList();
            QueryAncillaryDataList (contactList);
            return contactList;
        }

        public static List<McContact> QueryByEmailAddressInFolder (int accountId, int folderId, string emailAddress)
        {
            List<McContact> contactList = BackEnd.Instance.Db.Query<McContact> ("SELECT c.* FROM McContact AS c " +
            " JOIN McContactStringAttribute AS s ON c.Id = s.ContactId " +
            " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
            " WHERE " +
            " c.AccountId = m.AccountId AND " +
            " c.AccountId = ? AND " +
            " s.Type = ? AND " +
            " s.Value = ? AND " +
            " m.FolderId = ? ",
                accountId, McContactStringType.EmailAddress, emailAddress, folderId).ToList ();
            QueryAncillaryDataList (contactList);
            return contactList;
        }

        public static List<McContact> QueryByEmailAddressInSyncedFolder (int accountId, string emailAddress)
        {
            List<McContact> contactList = BackEnd.Instance.Db.Query<McContact> ("SELECT c.* FROM McContact AS c " +
            " JOIN McContactStringAttribute AS s ON c.Id = s.ContactId " +
            " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
            " JOIN McFolder AS f ON f.Id = m.FolderId " +
            " WHERE " +
            " c.AccountId = m.AccountId AND " +
            " c.AccountId = f.AccountId AND " +
            " c.AccountId = ? AND " +
            " s.Type = ? AND " +
            " s.Value = ? AND " +
            " f.IsClientOwned = false ",
                accountId, McContactStringType.EmailAddress, emailAddress).ToList ();
            QueryAncillaryDataList (contactList);
            return contactList;
        }
       
        public void UpdateScore (string reason, int score)
        {
            Log.Info ("SCORE: {0} {1} {2}", DisplayName, score, reason);
            Score += score;
            Update ();
        }
    }
}
