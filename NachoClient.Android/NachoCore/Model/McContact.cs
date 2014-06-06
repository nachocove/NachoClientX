using SQLite;
using System;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    public class McContactIndex
    {
        public int Id { set; get; }

        public McContact GetContact ()
        {
            return NcModel.Instance.Db.Get<McContact> (Id);
        }
    }

    public class McContactStringIndex
    {
        public int Id { set; get; }
        public int ContactId { set; get; }
        public string Value { set; get; }

        public McContact GetContact ()
        {
            return NcModel.Instance.Db.Get<McContact> (ContactId);
        }
    }

    public partial class McContact : McItem
    {
        /// <summary>
        /// Contacts schema
        /// Contacts are associated with folders, real or pseudo.
        /// </summary>
        /// 

        public const int minHotScore = 1;
        public const int minVipScore = 1000000;

        /// ActiveSync or Device
        public McItem.ItemSource Source { get; set; }

        /// The collection of important dates associated with the contact
        private List<McContactDateAttribute> DbDates;
        /// The collection addresses associated with the contact
        private List<McContactAddressAttribute> DbAddresses;
        /// The collection of phone numbers associated with the contact
        private List<McContactStringAttribute> DbPhoneNumbers;
        /// The collection of email addresses associated with the contact
        private List<McContactStringAttribute> DbEmailAddresses;
        /// The collection of instant messaging addresses associated with the contact
        private List<McContactStringAttribute> DbIMAddresses;
        /// The collection of relationsihps assigned to the contact.
        private List<McContactStringAttribute> DbRelationships;
        /// The collection of user labels assigned to the contact
        private List<McContactStringAttribute> DbCategories;
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
        // "Hotness" of the contact. Currently, updated by the emails.
        public int Score { get; set; }

        // Color of contact's profile circle if they have not set their photo or a photo cannot be found
        public int CircleColor { get; set; }

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

        public McContact GetContact()
        {
            return NcModel.Instance.Db.Get<McContact> (ContactId);
        }
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
            DbDates = new List<McContactDateAttribute> ();
            DbAddresses = new List<McContactAddressAttribute> ();
            DbPhoneNumbers = new List<McContactStringAttribute> ();
            DbEmailAddresses = new List<McContactStringAttribute> ();
            DbIMAddresses = new List<McContactStringAttribute> ();
            DbRelationships = new List<McContactStringAttribute> ();
            DbCategories = new List<McContactStringAttribute> ();
        }

        public McContact (McItem.ItemSource source) : this ()
        {
            Source = source;
        }

        private string displayName;

        [Ignore]
        public List<McContactDateAttribute> Dates {
            get {
                ReadAncillaryData ();
                return DbDates;
            }
            set {
                ReadAncillaryData ();
                DbDates = value;
            }
        }

        [Ignore]
        public List<McContactAddressAttribute> Addresses {
            get {
                ReadAncillaryData ();
                return DbAddresses;
            }
            set {
                ReadAncillaryData ();
                DbAddresses = value;
            }
        }

        [Ignore]
        public List<McContactStringAttribute> PhoneNumbers {
            get {
                ReadAncillaryData ();
                return DbPhoneNumbers;
            }
            set {
                ReadAncillaryData ();
                DbPhoneNumbers = value;
            }
        }

        [Ignore]
        public List<McContactStringAttribute> EmailAddresses {
            get {
                ReadAncillaryData ();
                return DbEmailAddresses;
            }
            set {
                ReadAncillaryData ();
                DbEmailAddresses = value;
            }
        }

        [Ignore]
        public List<McContactStringAttribute> IMAddresses {
            get {
                ReadAncillaryData ();
                return DbIMAddresses;
            }
            set {
                ReadAncillaryData ();
                DbIMAddresses = value;
            }
        }

        [Ignore]
        public List<McContactStringAttribute> Relationships {
            get {
                ReadAncillaryData ();
                return DbRelationships;
            }
            set {
                ReadAncillaryData ();
                DbRelationships = value;
            }
        }

        [Ignore]
        public List<McContactStringAttribute> Categories {
            get {
                ReadAncillaryData ();
                return DbCategories;
            }
            set {
                ReadAncillaryData ();
                DbCategories = value;
            }
        }

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
            ReadAncillaryData ();
            AddStringAttribute (ref DbPhoneNumbers, McContactStringType.PhoneNumber, name, label, value);
        }

        public void AddOrUpdatePhoneNumberAttribute (string name, string label, string value)
        {
            ReadAncillaryData ();
            AddOrUpdateStringAttribute (ref DbPhoneNumbers, McContactStringType.PhoneNumber, name, label, value);
        }

        public void AddEmailAddressAttribute (string name, string label, string value)
        {
            ReadAncillaryData ();
            AddStringAttribute (ref DbEmailAddresses, McContactStringType.EmailAddress, name, label, value);
        }

        public void AddOrUpdateEmailAddressAttribute (string name, string label, string value)
        {
            ReadAncillaryData ();
            AddOrUpdateStringAttribute (ref DbEmailAddresses, McContactStringType.EmailAddress, name, label, value);
        }

        public void AddIMAddressAttribute (string name, string label, string value)
        {
            ReadAncillaryData ();
            AddStringAttribute (ref DbIMAddresses, McContactStringType.IMAddress, name, label, value);
        }

        public void AddRelationshipAttribute (string name, string label, string value)
        {
            ReadAncillaryData ();
            AddStringAttribute (ref DbRelationships, McContactStringType.Relationship, name, label, value);
        }

        public void AddCategoryAttribute (string name)
        {
            ReadAncillaryData ();
            AddStringAttribute (ref DbCategories, McContactStringType.Category, name, null, null);
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

        private NcResult ReadAncillaryData ()
        {
            if (0 == Id) {
                return NcResult.OK ();
            }
            if (HasReadAncillaryData) {
                return NcResult.OK ();
            }
            HasReadAncillaryData = true;
            return ForceReadAncillaryData ();
        }
           
        public NcResult ForceReadAncillaryData ()
        {
            var db = NcModel.Instance.Db;
            NachoCore.NcAssert.True (0 < Id);
            DbDates = db.Table<McContactDateAttribute> ().Where (x => x.ContactId == Id).ToList ();
            DbAddresses = db.Table<McContactAddressAttribute> ().Where (x => x.ContactId == Id).ToList ();
            DbRelationships = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.Relationship).ToList ();
            DbEmailAddresses = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.EmailAddress).ToList ();
            DbPhoneNumbers = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.PhoneNumber).ToList ();
            DbIMAddresses = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.IMAddress).ToList ();
            DbCategories = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.Category).ToList ();

            // FIXME: Error handling
            return NcResult.OK ();
        }

        public NcResult InsertAncillaryData (SQLiteConnection db)
        {
            NachoCore.NcAssert.True (0 < Id);

            // Don't read what will be deleted
            HasReadAncillaryData = true;

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
            InsertAncillaryData (NcModel.Instance.Db);
            return retval;
        }

        public override int Update ()
        {
            int retval = base.Update ();
            InsertAncillaryData (NcModel.Instance.Db);
            return retval;
        }

        public override int Delete ()
        {
            int retval = base.Delete ();
            DeleteAncillaryData (NcModel.Instance.Db);
            return retval;
        }

        private NcResult DeleteAncillaryData (SQLiteConnection db)
        {
            db.Query<McContactDateAttribute> ("DELETE FROM McContactDateAttribute WHERE ContactId=?", Id);
            db.Query<McContactStringAttribute> ("DELETE FROM McContactStringAttribute WHERE ContactId=?", Id);
            db.Query<McContactAddressAttribute> ("DELETE FROM McContactAddressAttribute WHERE ContactId=?", Id);
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
                NcAssert.True (null != body);
                xmlAppData.Add (new XElement (AirSyncBaseNs + Xml.AirSyncBase.Body,
                    new XElement (AirSyncBaseNs + Xml.AirSyncBase.Type, (uint)Xml.AirSync.TypeCode.PlainText_1),
                    new XElement (AirSyncBaseNs + Xml.AirSyncBase.Data, body.Body)));
            }

            if (0 != BodyType) {
                xmlAppData.Add (new XElement (AirSyncBaseNs + Xml.AirSyncBase.NativeBodyType, BodyType));
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
            return NcModel.Instance.Db.Query<McContactStringAttribute> (
                "SELECT * FROM McContactStringAttribute " +
                " WHERE " +
                " ContactId = ? AND Type = ?", 
                Id, type).ToList ();
        }

        public static List<McContact> QueryByEmailAddress (int accountId, string emailAddress)
        {
            List<McContact> contactList = NcModel.Instance.Db.Query<McContact> (
                                              "SELECT c.* FROM McContact AS c " +
                                              " JOIN McContactStringAttribute AS s ON c.Id = s.ContactId " +
                                              " WHERE " +
                                              " c.AccountId = ? AND " +
                                              " s.Type = ? AND " +
                                              " s.Value = ? ",
                                              accountId, McContactStringType.EmailAddress, emailAddress).ToList ();
            return contactList;
        }

        public static List<McContact> QueryByEmailAddressInFolder (int accountId, int folderId, string emailAddress)
        {
            List<McContact> contactList = NcModel.Instance.Db.Query<McContact> (
                                              "SELECT c.* FROM McContact AS c " +
                                              " JOIN McContactStringAttribute AS s ON c.Id = s.ContactId " +
                                              " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                                              " WHERE " +
                                              " c.AccountId = m.AccountId AND " +
                                              " c.AccountId = ? AND " +
                                              " s.Type = ? AND " +
                                              " s.Value = ? AND " +
                                              " m.ClassCode = ? AND " +
                                              " m.FolderId = ? ",
                                              accountId, McContactStringType.EmailAddress, emailAddress, McFolderEntry.ClassCodeEnum.Contact, folderId).ToList ();
            return contactList;
        }

        public static List<McContact> QueryByEmailAddressInSyncedFolder (int accountId, string emailAddress)
        {
            List<McContact> contactList = NcModel.Instance.Db.Query<McContact> (
                                              "SELECT c.* FROM McContact AS c " +
                                              " JOIN McContactStringAttribute AS s ON c.Id = s.ContactId " +
                                              " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                                              " JOIN McFolder AS f ON f.Id = m.FolderId " +
                                              " WHERE " +
                                              " c.AccountId = m.AccountId AND " +
                                              " c.AccountId = f.AccountId AND " +
                                              " c.AccountId = ? AND " +
                                              " s.Type = ? AND " +
                                              " s.Value = ? AND " +
                                              " m.ClassCode = ? AND " +
                                              " f.IsClientOwned = false ",
                                              accountId, McContactStringType.EmailAddress, emailAddress, McFolderEntry.ClassCodeEnum.Contact).ToList ();
            return contactList;
        }

        public static List<McContactIndex> QueryAllContactItems (int accountId)
        {
            return NcModel.Instance.Db.Query<McContactIndex> (
                "SELECT c.Id as Id FROM McContact AS c " +
                " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                " WHERE " +
                " c.AccountId = ? AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ?  " +
                " ORDER BY c.FirstName",
                accountId, accountId, McFolderEntry.ClassCodeEnum.Contact);
        }

        public static List<McContactIndex> QueryContactItems (int accountId, int folderId)
        {
            return NcModel.Instance.Db.Query<McContactIndex> (
                "SELECT c.Id as Id FROM McContact AS c " +
                " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                " WHERE " +
                " c.AccountId = ? AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? " +
                " ORDER BY c.FirstName",
                accountId, accountId, McFolderEntry.ClassCodeEnum.Contact, folderId);
        }

        public static List<McContactIndex> QueryAllHotContactItems (int accountId)
        {
            return NcModel.Instance.Db.Query<McContactIndex> (
                "SELECT c.Id as Id FROM McContact AS c " +
                " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                " WHERE " +
                " c.AccountId = ? AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " c.Score > ? " +
                " ORDER BY c.Score DESC, c.FirstName",
                accountId, accountId, McFolderEntry.ClassCodeEnum.Contact, minHotScore);
        }

        public static List<McContactStringAttribute> SearchAllContactItems (int accountId, string searchFor)
        {
            // TODO: Put this in the brain
            if (String.IsNullOrEmpty (searchFor)) {
                return new List<McContactStringAttribute> ();
            }
            var target = searchFor.Split (new char[] { ' ' });
            var firstName = target.First () + "%";
            var lastName = target.Last () + "%";
            var email1 = firstName;
            var email2 = "'" + firstName;
            if (1 == target.Count ()) {
                return NcModel.Instance.Db.Query<McContactStringAttribute> (
                    "Select s.* FROM MCContactStringAttribute AS s  " +
                    " JOIN McContact AS c ON s.ContactId = c.Id   " +
                    " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId  " +
                    " WHERE " +
                    " m.ClassCode=? AND  " +
                    " s.Type=? AND  " +
                    " c.AccountId = ? AND " +
                    " c.AccountId=m.AccountId AND " +
                    " ( " +
                    "     ( c.FirstName LIKE ? OR c.LastName LIKE ? ) OR " +
                    "     s.Value LIKE ? OR s.Value LIKE ? " +
                    " ) " +
                    " ORDER BY c.Score DESC, c.FirstName LIMIT 100", 
                    McFolderEntry.ClassCodeEnum.Contact, McContactStringType.EmailAddress, accountId, firstName, lastName, email1, email2);
            } else {
                return NcModel.Instance.Db.Query<McContactStringAttribute> (
                    "Select s.* FROM MCContactStringAttribute AS s  " +
                    " JOIN McContact AS c ON s.ContactId = c.Id   " +
                    " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId  " +
                    " WHERE " +
                    " m.ClassCode=? AND  " +
                    " s.Type=? AND  " +
                    " c.AccountId = ? AND " +
                    " c.AccountId=m.AccountId AND " +
                    " ( c.FirstName LIKE ? AND c.LastName LIKE ? ) " +
                    " ORDER BY c.Score DESC, c.FirstName LIMIT 100", 
                    McFolderEntry.ClassCodeEnum.Contact, McContactStringType.EmailAddress, accountId, firstName, lastName, firstName);
            }
        }

        public static void UpdateUserCircleColor(int circleColor, string userAddress)
        {
            NcModel.Instance.Db.Query<McContact> (
                "UPDATE McContact SET CircleColor = ? WHERE Id IN" +
                " (SELECT ContactId FROM McContactStringAttribute WHERE Value = ?)",
                circleColor, userAddress);
        }

        public void UpdateScore (string reason, int score)
        {
            Log.Info (Log.LOG_BRAIN, "SCORE: {0} {1} {2}", DisplayName, score, reason);
            Score += score;
            Update ();
        }

        /// TODO: VIPness should be in its own member
        public bool isHot ()
        {
            if (isVip ()) {
                return ((Score - minVipScore) >= minHotScore);
            } else {
                return (Score >= minHotScore);
            }
        }

        /// TODO: VIPness should be in its own member
        public bool isVip ()
        {
            return (Score >= minVipScore);
        }
    }
}
