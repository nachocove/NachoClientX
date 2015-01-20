using SQLite;
using System;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using NachoCore.Utils;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    public class NcContactIndex
    {
        public int Id { set; get; }

        public string FirstLetter { set; get; }

        public McContact GetContact ()
        {
            return NcModel.Instance.Db.Get<McContact> (Id);
        }
    }

    public class NcContactStringIndex
    {
        public int Id { set; get; }

        public int ContactId { set; get; }

        public string Value { set; get; }

        public McContact GetContact ()
        {
            return NcModel.Instance.Db.Get<McContact> (ContactId);
        }
    }

    public partial class McContact : McAbstrItem
    {
        /// <summary>
        /// Contacts schema
        /// Contacts are associated with folders, real or pseudo.
        /// </summary>
        /// 

        /// ActiveSync or Device
        public McAbstrItem.ItemSource Source { get; set; }

        /// Set only for Device contacts
        public string DeviceUniqueId { get; set; }

        /// Set only for Device contacts
        public DateTime DeviceCreation { get; set; }

        /// Set only for Device contacts
        public DateTime DeviceLastUpdate { get; set; }

        /// The collection of important dates associated with the contact
        private List<McContactDateAttribute> DbDates;
        /// The collection addresses associated with the contact
        private List<McContactAddressAttribute> DbAddresses;
        /// The collection of phone numbers associated with the contact
        private List<McContactStringAttribute> DbPhoneNumbers;
        /// The collection of email addresses associated with the contact
        private List<McContactEmailAddressAttribute> DbEmailAddresses;
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

        /// Contact's display name, from the GAL
        public string DisplayName { get; set; }

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

        // Specifies the original format type of the item
        public int NativeBodyType { get; set; }

        // Color of contact's profile circle if they have not set their photo or a photo cannot be found
        public int CircleColor { get; set; }

        public int PortraitId { get; set; }

        [Indexed]
        public bool IsVip { get; set; }

        public override ClassCodeEnum GetClassCode ()
        {
            return McAbstrFolderEntry.ClassCodeEnum.Contact;
        }

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
            DbEmailAddresses = new List<McContactEmailAddressAttribute> ();
            DbIMAddresses = new List<McContactStringAttribute> ();
            DbRelationships = new List<McContactStringAttribute> ();
            DbCategories = new List<McContactStringAttribute> ();
        }

        public McContact (McAbstrItem.ItemSource source) : this ()
        {
            Source = source;
        }

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
        public List<McContactEmailAddressAttribute> EmailAddresses {
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

        public static bool IsNull (DateTime t)
        {
            return (DateTime.MinValue == t);
        }

        public static bool IsNull (int i)
        {
            return (int.MinValue == i);
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

        public string GetEmailAddressAttribute (string name)
        {
            foreach (var l in EmailAddresses) {
                if (l.Name.Equals (name)) {
                    return l.Value;
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

        public McContactDateAttribute AddDateAttribute (int accountId, string name, string label, DateTime value)
        {
            var f = new McContactDateAttribute ();
            f.AccountId = accountId;
            f.Name = name;
            f.Label = label;
            f.Value = value;
            f.ContactId = this.Id;
            Dates.Add (f);
            return f;
        }

        public McContactAddressAttribute AddAddressAttribute (int accountId, string name, string label, McContactAddressAttribute value)
        {
            var f = new McContactAddressAttribute ();
            f.AccountId = accountId;
            f.Name = name;
            f.Label = label;
            f.Street = value.Street;
            f.City = value.City;
            f.State = value.State;
            f.Country = value.Country;
            f.PostalCode = value.PostalCode;
            f.ContactId = this.Id;
            Addresses.Add (f);
            return f;
        }

        public McContactEmailAddressAttribute AddDefaultEmailAddressAttribute (int accountId, string name, string label, string value)
        {
            var f = new McContactEmailAddressAttribute ();
            f.AccountId = accountId;
            f.Name = name;
            f.Label = label;
            f.Value = value;
            f.IsDefault = true;
            f.ContactId = this.Id;
            McEmailAddress emailAddress;
            if (McEmailAddress.Get (AccountId, value, out emailAddress)) {
                f.EmailAddress = emailAddress.Id;
            }
            EmailAddresses.Add (f);
            return f;
        }

        public McContactEmailAddressAttribute AddEmailAddressAttribute (int accountId, string name, string label, string value)
        {
            var f = new McContactEmailAddressAttribute ();
            f.AccountId = accountId;
            f.Name = name;
            f.Label = label;
            f.Value = value;
            f.ContactId = this.Id;
            McEmailAddress emailAddress;
            if (McEmailAddress.Get (AccountId, value, out emailAddress)) {
                f.EmailAddress = emailAddress.Id;
            }
            EmailAddresses.Add (f);
            return f;
        }

        protected McContactStringAttribute AddStringAttribute (ref List<McContactStringAttribute> list, int accountId, McContactStringType type, string name, string label, string value)
        {
            var f = new McContactStringAttribute ();
            f.AccountId = accountId;
            f.Name = name;
            f.Type = type;
            f.Label = label;
            f.Value = value;
            f.ContactId = this.Id;
            list.Add (f);
            return f;
        }

        protected McContactStringAttribute AddDefaultStringAttribute (ref List<McContactStringAttribute> list, int accountId, McContactStringType type, string name, string label, string value)
        {
            var f = new McContactStringAttribute ();
            f.AccountId = accountId;
            f.Name = name;
            f.Type = type;
            f.Label = label;
            f.Value = value;
            f.IsDefault = true;
            f.ContactId = this.Id;
            list.Add (f);
            return f;
        }

        public McContactStringAttribute AddPhoneNumberAttribute (int accountId, string name, string label, string value)
        {
            ReadAncillaryData ();
            return AddStringAttribute (ref DbPhoneNumbers, accountId, McContactStringType.PhoneNumber, name, label, value);
        }

        public McContactStringAttribute AddDefaultPhoneNumberAttribute (int accountId, string name, string label, string value)
        {
            ReadAncillaryData ();
            return AddDefaultStringAttribute (ref DbPhoneNumbers, accountId, McContactStringType.PhoneNumber, name, label, value);
        }

        public McContactStringAttribute AddOrUpdatePhoneNumberAttribute (int accountId, string name, string label, string value)
        {
            ReadAncillaryData ();
            return AddOrUpdateStringAttribute (ref DbPhoneNumbers, accountId, McContactStringType.PhoneNumber, name, label, value);
        }

        public McContactStringAttribute AddOrUpdatePhoneNumberAttribute (int accountId, string name, string label, string value, bool isDefault)
        {
            ReadAncillaryData ();
            return AddOrUpdateStringAttribute (ref DbPhoneNumbers, accountId, McContactStringType.PhoneNumber, name, label, value, isDefault);
        }

        public McContactStringAttribute AddIMAddressAttribute (int accountId, string name, string label, string value)
        {
            ReadAncillaryData ();
            return AddOrUpdateStringAttribute (ref DbIMAddresses, accountId, McContactStringType.IMAddress, name, label, value);
        }

        public McContactStringAttribute AddRelationshipAttribute (int accountId, string name, string label, string value)
        {
            ReadAncillaryData ();
            return AddOrUpdateStringAttribute (ref DbRelationships, accountId, McContactStringType.Relationship, name, label, value);
        }

        public McContactStringAttribute AddChildAttribute (int accountId, string name, string label, string value)
        {
            ReadAncillaryData ();
            return AddStringAttribute (ref DbRelationships, accountId, McContactStringType.Relationship, name, label, value);
        }

        public McContactStringAttribute AddCategoryAttribute (int accountId, string name)
        {
            ReadAncillaryData ();
            return AddOrUpdateStringAttribute (ref DbCategories, accountId, McContactStringType.Category, name, null, null);
        }

        protected McContactStringAttribute AddOrUpdateStringAttribute (ref List<McContactStringAttribute> list, int accountId, McContactStringType type, string name, string label, string value)
        {
            var existing = list.Where (attr => attr.Type.Equals (type) && attr.Name.Equals (name)).SingleOrDefault ();
            if (null != existing) {
                existing.Label = label;
                existing.Value = value;
                return existing;
            } else {
                var newbie = new McContactStringAttribute ();
                newbie.AccountId = accountId;
                newbie.Name = name;
                newbie.Type = type;
                newbie.Label = label;
                newbie.Value = value;
                newbie.ContactId = this.Id;
                list.Add (newbie);
                return newbie;
            }
        }

        protected McContactStringAttribute AddOrUpdateStringAttribute (ref List<McContactStringAttribute> list, int accountId, McContactStringType type, string name, string label, string value, bool isDefault)
        {
            var existing = list.Where (attr => attr.Type.Equals (type) && attr.Name.Equals (name)).SingleOrDefault ();
            if (null != existing) {
                existing.Label = label;
                existing.Value = value;
                existing.IsDefault = isDefault;
                return existing;
            } else {
                var newbie = new McContactStringAttribute ();
                newbie.AccountId = accountId;
                newbie.Name = name;
                newbie.Type = type;
                newbie.Label = label;
                newbie.Value = value;
                newbie.ContactId = this.Id;
                list.Add (newbie);
                return newbie;
            }
        }

        public McContactEmailAddressAttribute AddOrUpdateEmailAddressAttribute (int accountId, string name, string label, string value)
        {
            ReadAncillaryData ();
            var f = EmailAddresses.Where (attr => attr.Name.Equals (name)).SingleOrDefault ();
            if (null != f) {
                f.Label = label;
                f.Value = value;
            } else {
                f = new McContactEmailAddressAttribute ();
                f.AccountId = accountId;
                f.Name = name;
                f.Label = label;
                f.Value = value;
                f.ContactId = this.Id;
                EmailAddresses.Add (f);
            }
            McEmailAddress emailAddress;
            if (McEmailAddress.Get (AccountId, value, out emailAddress)) {
                f.EmailAddress = emailAddress.Id;
            }
            return f;
        }

        /// <summary>
        ///        Db.CreateTable<McContact> ();
        ///        Db.CreateTable<McContactDateAttribute> ();
        ///        Db.CreateTable<McContactStringAttribute> ();
        ///        Db.CreateTable<McContactAddressAttribute> ();
        ///        Db.CreateTable<McContactEmailAddressAttribute> ();
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
            NcAssert.True (0 < Id);
            DbDates = db.Table<McContactDateAttribute> ().Where (x => x.ContactId == Id).ToList ();
            DbAddresses = db.Table<McContactAddressAttribute> ().Where (x => x.ContactId == Id).ToList ();
            DbEmailAddresses = db.Table<McContactEmailAddressAttribute> ().Where (x => x.ContactId == Id).ToList ();
            DbRelationships = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.Relationship).ToList ();
            DbPhoneNumbers = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.PhoneNumber).ToList ();
            DbIMAddresses = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.IMAddress).ToList ();
            DbCategories = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.Category).ToList ();

            // FIXME: Error handling
            return NcResult.OK ();
        }

        public NcResult InsertAncillaryData (SQLiteConnection db)
        {
            NcAssert.True (0 < Id);

            // Don't read what will be deleted
            HasReadAncillaryData = true;

            // FIXME: Fix this hammer?
            // FIXME: For update, Id may not be zero. Insert() asserts that Id is zero, so zero it.
            // FIXME: After hammer is fixed, use DeleteAncillaryData to clean up associated McPortrait.
            DeleteAncillaryData (db);

            foreach (var o in Dates) {
                o.Id = 0;
                o.ContactId = Id;
                o.Insert ();
            }
            foreach (var o in Addresses) {
                o.Id = 0;
                o.ContactId = Id;
                o.Insert ();
            }
            foreach (var o in Relationships) {
                o.Id = 0;
                o.ContactId = Id;
                o.Insert ();
            }
            foreach (var o in EmailAddresses) {
                o.Id = 0;
                o.ContactId = Id;
                o.Insert ();
            }
            foreach (var o in PhoneNumbers) {
                o.Id = 0;
                o.ContactId = Id;
                o.Insert ();
            }
            foreach (var o in IMAddresses) {
                o.Id = 0;
                o.ContactId = Id;
                o.Insert ();
            }
            foreach (var o in Categories) {
                o.Id = 0;
                o.ContactId = Id;
                o.Insert ();
            }
    
            // FIXME: Error handling
            return NcResult.OK ();
        }

        public override int Insert ()
        {
            // FIXME db transaction.
            CircleColor = NachoPlatform.PlatformUserColorIndex.PickRandomColorForUser ();
            int retval = base.Insert ();
            InsertAncillaryData (NcModel.Instance.Db);
            return retval;
        }

        public override int Update ()
        {
            int retval = base.Update ();
            if (HasReadAncillaryData) {
                InsertAncillaryData (NcModel.Instance.Db);
            }
            return retval;
        }

        public override void DeleteAncillary ()
        {
            NcAssert.True (NcModel.Instance.IsInTransaction ());
            DeleteAncillaryData (NcModel.Instance.Db);
        }

        private NcResult DeleteAncillaryData (SQLiteConnection db)
        {
            db.Query<McContactDateAttribute> ("DELETE FROM McContactDateAttribute WHERE ContactId=?", Id);
            db.Query<McContactStringAttribute> ("DELETE FROM McContactStringAttribute WHERE ContactId=?", Id);
            db.Query<McContactAddressAttribute> ("DELETE FROM McContactAddressAttribute WHERE ContactId=?", Id);
            db.Query<McContactEmailAddressAttribute> ("DELETE FROM McContactEmailAddressAttribute WHERE ContactId=?", Id);
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
                    Log.Warn (Log.LOG_AS, "Xml.Gal.Data seen not under Xml.Gal.Picture.");
                    break;

                case Xml.Gal.DisplayName:
                    DisplayName = prop.Value;
                    break;

                case Xml.Gal.EmailAddress:
                    AddOrUpdateEmailAddressAttribute (AccountId, Xml.Contacts.Email1Address, null, prop.Value);
                    break;

                case Xml.Gal.FirstName:
                    FirstName = prop.Value;
                    break;

                case Xml.Gal.HomePhone:
                    AddOrUpdatePhoneNumberAttribute (AccountId, Xml.Contacts.HomePhoneNumber, null, prop.Value);
                    break;

                case Xml.Gal.LastName:
                    LastName = prop.Value;
                    break;

                case Xml.Gal.MobilePhone:
                    AddOrUpdatePhoneNumberAttribute (AccountId, Xml.Contacts.MobilePhoneNumber, null, prop.Value);
                    break;
                
                case Xml.Gal.Office:
                    OfficeLocation = prop.Value;
                    break;

                case Xml.Gal.Phone:
                    AddOrUpdatePhoneNumberAttribute (AccountId, Xml.Contacts.BusinessPhoneNumber, null, prop.Value);
                    break;

                case Xml.Gal.Picture:
                    var xmlStatus = prop.ElementAnyNs (Xml.AirSync.Status);
                    if (null != xmlStatus && (int)Xml.Search.SearchStatusCode.Success_1 != xmlStatus.Value.ToInt ()) {
                        // We can expect non-error, non-success codes for missing pic, too many pics, etc.
                        Log.Info (Log.LOG_AS, "Status for Xml.Gal.Picture {0}", xmlStatus.Value);
                    }
                    var xmlData = prop.ElementAnyNs (Xml.Gal.Data);
                    if (null != xmlData) {
                        var data = Convert.FromBase64String (xmlData.Value);
                        var portrait = McPortrait.InsertFile (AccountId, data);
                        PortraitId = portrait.Id;
                    }
                    break;
 
                case Xml.Gal.Title:
                    Title = prop.Value;
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
                    new XElement (AirSyncBaseNs + Xml.AirSyncBase.Data, body.GetContentsString ())));
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
            if (0 != PortraitId) {
                var data = McPortrait.GetContentsByteArray (PortraitId);
                var portraitB64 = Convert.ToBase64String (data);
                // MS-ASCNTC 2.2.2.56 Picture.
                if (48 * 1024 > portraitB64.Length) {
                    xmlAppData.Add (new XElement (ContactsNs + Xml.Contacts.Picture, portraitB64));
                }
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
                                              " JOIN McContactEmailAddressAttribute AS s ON c.Id = s.ContactId " +
                                              " WHERE " +
                                              "s.Value = ? AND " +
                                              " c.AccountId = ? AND " +
                                              " c.IsAwaitingDelete = 0 ",
                                              emailAddress, accountId).ToList ();
            return contactList;
        }

        public static List<McContact> QueryLikeEmailAddress (int accountId, string emailAddress)
        {
            var emailWildcard = "%" + emailAddress + "%";
            List<McContact> contactList = NcModel.Instance.Db.Query<McContact> (
                                              "SELECT c.* FROM McContact AS c " +
                                              " JOIN McContactEmailAddressAttribute AS s ON c.Id = s.ContactId " +
                                              " WHERE " +
                                              "s.Value LIKE ? AND " +
                                              " c.AccountId = ? AND " +
                                              " c.IsAwaitingDelete = 0 ",
                                              emailWildcard, accountId).ToList ();
            return contactList;
        }

        public static List<McContact> QueryByEmailAddressInFolder (int accountId, int folderId, string emailAddress)
        {
            List<McContact> contactList = NcModel.Instance.Db.Query<McContact> (
                                              "SELECT c.* FROM McContact AS c " +
                                              " JOIN McContactEmailAddressAttribute AS s ON c.Id = s.ContactId " +
                                              " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                                              " WHERE " +
                                              " c.AccountId = m.AccountId AND " +
                                              " c.AccountId = ? AND " +
                                              " c.IsAwaitingDelete = 0 AND " +
                                              " s.Value = ? AND " +
                                              " m.ClassCode = ? AND " +
                                              " m.FolderId = ? ",
                                              accountId, emailAddress, (int)McAbstrFolderEntry.ClassCodeEnum.Contact, folderId).ToList ();
            return contactList;
        }

        public static List<McContact> QueryByEmailAddressInSyncedFolder (int accountId, string emailAddress)
        {
            List<McContact> contactList = NcModel.Instance.Db.Query<McContact> (
                                              "SELECT c.* FROM McContact AS c " +
                                              " JOIN McContactEmailAddressAttribute AS s ON c.Id = s.ContactId " +
                                              " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                                              " JOIN McFolder AS f ON f.Id = m.FolderId " +
                                              " WHERE " +
                                              " c.AccountId = m.AccountId AND " +
                                              " c.AccountId = f.AccountId AND " +
                                              " c.AccountId = ? AND " +
                                              " c.IsAwaitingDelete = 0 AND " +
                                              " s.Value = ? AND " +
                                              " m.ClassCode = ? AND " +
                                              " f.IsClientOwned = false ",
                                              accountId, emailAddress, (int)McAbstrFolderEntry.ClassCodeEnum.Contact).ToList ();
            return contactList;
        }

        public static List<NcContactIndex> QueryAllContactItems ()
        {
            return NcModel.Instance.Db.Query<NcContactIndex> (
                "SELECT c.Id as Id, substr(c.FirstName, 0, 1) as FirstLetter FROM McContact AS c " +
                " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                " WHERE " +
                " c.AccountId = ? AND " +
                " c.IsAwaitingDelete = 0 AND " +
                " m.ClassCode = ?  " +
                " m.AccountId = ? AND " +
                " ORDER BY c.FirstName",
                McAbstrFolderEntry.ClassCodeEnum.Contact);
        }

        public static List<NcContactIndex> QueryAllContactItems (int accountId)
        {
            return NcModel.Instance.Db.Query<NcContactIndex> (
                "SELECT c.Id as Id, substr(c.FirstName, 0, 1) as FirstLetter FROM McContact AS c " +
                " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                " WHERE " +
                " c.IsAwaitingDelete = 0 AND " +
                " m.ClassCode = ?  " +
                " ORDER BY c.FirstName",
                (int)McAbstrFolderEntry.ClassCodeEnum.Contact);
        }

        public static List<NcContactIndex> QueryContactItems (int accountId, int folderId)
        {
            return NcModel.Instance.Db.Query<NcContactIndex> (
                "SELECT c.Id as Id, substr(c.FirstName, 0, 1) as FirstLetter FROM McContact AS c " +
                " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                " WHERE " +
                " c.AccountId = ? AND " +
                " c.IsAwaitingDelete = 0 AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? " +
                " ORDER BY c.FirstName",
                accountId, accountId, (int)McAbstrFolderEntry.ClassCodeEnum.Contact, folderId);
        }

        // FIXME: NOT USED But using Score
        //        public static List<NcContactIndex> QueryAllHotContactItems ()
        //        {
        //            return NcModel.Instance.Db.Query<NcContactIndex> (
        //                "SELECT c.Id as Id FROM McContact AS c " +
        //                " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
        //                " WHERE " +
        //                " c.IsAwaitingDelete = 0 AND " +
        //                " m.ClassCode = ? AND " +
        //                " c.Score > ? " +
        //                " ORDER BY c.Score DESC, c.FirstName",
        //                McAbstrFolderEntry.ClassCodeEnum.Contact, McEmailAddress.minHotScore);
        //        }

        // FIXME: NOT USED But using Score
        //        public static List<NcContactIndex> QueryAllHotContactItems (int accountId)
        //        {
        //            return NcModel.Instance.Db.Query<NcContactIndex> (
        //                "SELECT c.Id as Id FROM McContact AS c " +
        //                " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
        //                " WHERE " +
        //                " c.AccountId = ? AND " +
        //                " c.IsAwaitingDelete = 0 AND " +
        //                " m.AccountId = ? AND " +
        //                " m.ClassCode = ? AND " +
        //                " c.Score > ? " +
        //                " ORDER BY c.Score DESC, c.FirstName",
        //                accountId, accountId, McAbstrFolderEntry.ClassCodeEnum.Contact, McEmailAddress.minHotScore);
        //        }

        public static List<McContact> QueryAllRicContacts (int accountId)
        {
            // Get the RIC folder
            McFolder ricFolder = McFolder.GetRicContactFolder (accountId);
            if (null == ricFolder) {
                return null;
            }

            // Order by descending weighted rank so that the first entry has the max rank.
            return NcModel.Instance.Db.Query<McContact> (
                "SELECT c.* FROM McContact AS c " +
                " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                " WHERE " +
                " c.AccountId = ? AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? " +
                " ORDER BY c.WeightedRank DESC",
                accountId, accountId, (int)McAbstrFolderEntry.ClassCodeEnum.Contact, ricFolder.Id);
        }

        public static List<McContactEmailAddressAttribute> SearchAllContactItems (string searchFor)
        {
            // TODO: Put this in the brain
            if (String.IsNullOrEmpty (searchFor)) {
                return new List<McContactEmailAddressAttribute> ();
            }
            var target = searchFor.Split (new char[] { ' ' });
            var firstName = target.First () + "%";
            var lastName = target.Last () + "%";
            return NcModel.Instance.Db.Query<McContactEmailAddressAttribute> (
                "Select s.*, coalesce(c.FirstName,c.LastName,ltrim(s.Value,'\"')) AS SORT_ORDER " +
                "FROM McContactEmailAddressAttribute AS s " +
                "JOIN McEmailAddress AS a ON s.EmailAddress = a.Id " +
                "JOIN McContact AS c ON s.ContactId = c.Id " +
                "JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                "WHERE " +
                "m.ClassCode=? AND " +
                "c.IsAwaitingDelete = 0 AND " +
                "( " +
                "  c.FirstName LIKE ? OR c.LastName LIKE ?  OR s.Value LIKE ? OR s.Value LIKE ? " +
                ") " +
                "ORDER BY a.Score DESC, SORT_ORDER COLLATE NOCASE ASC  LIMIT 100", 
                (int)McAbstrFolderEntry.ClassCodeEnum.Contact, firstName, lastName, firstName, lastName);
        }

        static string GetContactSearchString (int accountId = 0)
        {
            var fmt = "SELECT DISTINCT Id, coalesce(nullif(upper(substr(SORT_ORDER, 1, 1)), ''), '#') as FirstLetter  FROM   " +
                      "(  " +
                      "    SELECT c.Id, trim(trim(coalesce(c.FirstName,'') || ' ' || coalesce(c.LastName, '')) || ' ' || coalesce(ltrim(s.Value,'\"'), '')) AS SORT_ORDER  " +
                      "    FROM McContact AS c  " +
                      "    LEFT OUTER JOIN McContactEmailAddressAttribute AS s ON c.Id = s.ContactId  " +
                      "    JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId  " +
                      "    WHERE " +
                      "    {0}" +
                      "    m.ClassCode = ? AND  " +
                      "    c.IsAwaitingDelete = 0  " +
                      ")  " +
                      "ORDER BY SORT_ORDER COLLATE NOCASE ASC ";

            if (0 == accountId) {
                return String.Format (fmt, "");
            } else {
                var selectAccount = String.Format ("    c.AccountId = {0} AND ", accountId);
                return String.Format (fmt, selectAccount);
            }
        }

        public static List<NcContactIndex> AllContactsSortedByName (int accountId)
        {
            return NcModel.Instance.Db.Query<NcContactIndex> (GetContactSearchString (accountId), (int)McAbstrFolderEntry.ClassCodeEnum.Contact);
        }

        public static List<NcContactIndex> AllContactsSortedByName ()
        {
            return NcModel.Instance.Db.Query<NcContactIndex> (GetContactSearchString (0), (int)McAbstrFolderEntry.ClassCodeEnum.Contact);
        }

        public static List<NcContactIndex> RicContactsSortedByRank (int accountId, int limit)
        {
            // Get the RIC folder
            McFolder ricFolder = McFolder.GetRicContactFolder (accountId);
            if (null == ricFolder) {
                return null;
            }

            // Order by descending weighted rank so that the first entry has the max rank.
            return NcModel.Instance.Db.Query<NcContactIndex> (
                "SELECT c.Id as Id, \" \" FROM McContact AS c " +
                " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                " WHERE " +
                " c.AccountId = ? AND " +
                " m.AccountId = ? AND " +
                " m.ClassCode = ? AND " +
                " m.FolderId = ? " +
                " ORDER BY c.WeightedRank DESC LIMIT ?",
                accountId, accountId, (int)McAbstrFolderEntry.ClassCodeEnum.Contact, ricFolder.Id, limit);
        }

        public static McContact QueryByDeviceUniqueId (string deviceUniqueId)
        {
            var account = McAccount.GetDeviceAccount ();
            return NcModel.Instance.Db.Table<McContact> ().Where (x => 
                x.DeviceUniqueId == deviceUniqueId &&
            x.AccountId == account.Id
            ).SingleOrDefault ();
        }

        public string GetDisplayName ()
        {
            if (!String.IsNullOrEmpty (DisplayName)) {
                return DisplayName;
            }
            List<string> value = new List<string> ();
            if (!String.IsNullOrEmpty (FirstName)) {
                value.Add (FirstName);
            }
            if (!String.IsNullOrEmpty (MiddleName)) {
                value.Add (MiddleName);
            }           
            if (!String.IsNullOrEmpty (LastName)) {
                value.Add (LastName);
            }
            return String.Join (" ", value);
        }

        public string GetEmailAddress ()
        {
            if (null == EmailAddresses) {
                return "";
            }
            if (0 == EmailAddresses.Count ()) {
                return "";
            }
            return EmailAddresses.First ().Value;
        }

        public string GetPrimaryCanonicalEmailAddress ()
        {
            if (null == EmailAddresses) {
                return "";
            }
            if (0 == EmailAddresses.Count ()) {
                return "";
            }

            //First, grab the default (if available)
            foreach (var e in EmailAddresses) {
                if (e.IsDefault) {
                    var emailAddress = McEmailAddress.QueryById<McEmailAddress> (e.EmailAddress);
                    if (null != emailAddress) {
                        return emailAddress.CanonicalEmailAddress;
                    }
                    break;
                }
            }

            //Second, grab the first email address w/non-empty value
            foreach (var e in EmailAddresses) {
                var emailAddress = McEmailAddress.QueryById<McEmailAddress> (e.EmailAddress);
                if (null != emailAddress) {
                    return emailAddress.CanonicalEmailAddress;
                }
            }

            //No email addresses
            return "";
        }

        public string GetPrimaryPhoneNumber ()
        {
            if (null == PhoneNumbers) {
                return "";
            }
            if (0 == PhoneNumbers.Count ()) {
                return "";
            }

            foreach (var p in PhoneNumbers) {
                if (p.IsDefault) {
                    return p.Value;
                }
            }
            return PhoneNumbers.First ().Value;
        }

        public string GetDisplayNameOrEmailAddress ()
        {
            var displayName = GetDisplayName ();
            if (String.IsNullOrEmpty (displayName)) {
                return GetPrimaryCanonicalEmailAddress ();
            } else {
                return displayName;
            }
        }

        public void SetVIP (bool IsVip)
        {
            this.IsVip = IsVip;
            this.Update ();

            foreach (var emailAddressAttribute in this.EmailAddresses) {
                var emailAddress = McEmailAddress.QueryById<McEmailAddress> (emailAddressAttribute.EmailAddress);
                if (null != emailAddress) {
                    emailAddress.IsVip = this.IsVip;
                    emailAddress.Update ();
                    NachoCore.Brain.NcBrain.UpdateAddressScore (emailAddress.Id, true);
                }
            }
        }

        public bool CanUserEdit ()
        {
            if (McAbstrItem.ItemSource.ActiveSync != Source) {
                return false;
            }
            var ric = McFolder.GetRicContactFolder (AccountId);
            var maps = McMapFolderFolderEntry.QueryByFolderEntryIdClassCode (AccountId, Id, GetClassCode ());
            foreach (var map in maps) {
                if ((null != ric) && (map.FolderId == ric.Id)) {
                    Log.Info (Log.LOG_CONTACTS, "cannot edit contact from ric");
                    return false;
                }
                var folder = McFolder.QueryById<McFolder> (map.FolderId);
                if (folder.IsClientOwned) {
                    Log.Info (Log.LOG_CONTACTS, "cannot edit contact from client-owned folder {0}", folder.DisplayName);
                    return false;
                }
            }
            return true;
        }
    }
}
