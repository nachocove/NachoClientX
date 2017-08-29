using SQLite;
using System;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NachoCore.Utils;
using NachoCore.ActiveSync;
using NachoCore.Brain;
using NachoCore.Index;

namespace NachoCore.Model
{
    public class NcContactIndex
    {
        public int Id { set; get; }

        public string FirstLetter { set; get; }

        public McContact GetContact ()
        {
            return McContact.QueryById<McContact> (Id);
        }
    }

    public class NcContactStringIndex
    {
        public int Id { set; get; }

        public int ContactId { set; get; }

        public string Value { set; get; }

        public McContact GetContact ()
        {
            return McContact.QueryById<McContact> (Id);
        }
    }

    public class NcContactPortraitIndex
    {
        public int PortraitId { set; get; }

        public int EmailAddress { set; get; }
    }

    public class NcContactPortraitEmailIndex
    {
        public int PortraitId { set; get; }
        public int EmailAddressId { set; get; }
        public int ColorIndex { set; get; }
        public string EmailAddress { set; get; }
    }

    public class McContactComparer : IEqualityComparer<McContact>
    {
        public bool Equals (McContact a, McContact b)
        {
            return a.Id == b.Id;
        }

        public int GetHashCode (McContact c)
        {
            return c.Id;
        }
    }

    public class McContactNameComparer : IComparer<McContact>
    {
        protected string GetFirstName (McContact c)
        {
            if (!String.IsNullOrEmpty (c.FirstName)) {
                return c.FirstName;
            }
            if (0 < c.EmailAddresses.Count) {
                return c.EmailAddresses [0].Value;
            }
            return null;
        }

        public int Compare (McContact a, McContact b)
        {
            int result = String.Compare (GetFirstName (a), GetFirstName (b), ignoreCase: true);
            if (0 != result) {
                return result;
            }
            result = String.Compare (a.MiddleName, b.MiddleName, ignoreCase: true);
            if (0 != result) {
                return result;
            }
            return String.Compare (a.LastName, b.LastName, ignoreCase: true);
        }
    }

    public class McContactRicCache : Dictionary<int, bool>
    {
        public bool Get (int contactId, out bool isRic)
        {
            if (TryGetValue (contactId, out isRic)) {
                return true;
            }
            return false;
        }
    }

    public partial class McContact : McAbstrItem
    {
        /// <summary>
        /// Contacts schema
        /// Contacts are associated with folders, real or pseudo.
        /// </summary>
        /// 

        public enum McContactAncillaryDataEnum
        {
            READ_NONE = 0,
            READ_DATES = 1,
            READ_ADDRESSES = 2,
            READ_EMAILADDRESSES = 4,
            READ_RELATIONSHIPS = 8,
            READ_PHONENUMBERS = 16,
            READ_IMADDRESSES = 32,
            READ_CATEGORIES = 64,
            READ_ALL = 127,
        }

        public enum McContactOpEnum
        {
            Insert,
            Update,
            Delete,
        }

        /// ActiveSync, Device, Salesforce, etc.
        public McAbstrItem.ItemSource Source { get; set; }

        /// Set only for Device contacts
        public DateTime DeviceCreation { get; set; }

        /// Set only for Device contacts
        public DateTime DeviceLastUpdate { get; set; }

        /// The collection of important dates associated with the contact
        private List<McContactDateAttribute> DbDates;
        /// The collection addresses associated with the contact
        private List<McContactAddressAttribute> DbAddresses;

        public bool PhoneNumbersEclipsed { get; set; }

        /// The collection of phone numbers associated with the contact
        private List<McContactStringAttribute> DbPhoneNumbers;

        public bool EmailAddressesEclipsed { get; set; }

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

        public bool IsVip { get; set; }

        [Indexed]
        public string CachedSortFirstName { get; set; }

        [Indexed]
        public string CachedSortLastName { get; set; }

        public string CachedGroupFirstName { get; set; }

        public string CachedGroupLastName { get; set; }

        // 0 means unindexed. If IndexedVersion == ContactIndexDocument.Version - 1, only McContact fields
        // are indexed. If IndexedVersion == ContactIndexDocument.Version, both fields and body (note) are
        // indexed.
        public int IndexVersion { get; set; }

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
            HasReadAncillaryData = McContactAncillaryDataEnum.READ_NONE;
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
                ReadAncillaryData (McContactAncillaryDataEnum.READ_DATES);
                return DbDates;
            }
            set {
                ReadAncillaryData (McContactAncillaryDataEnum.READ_DATES);
                DbDates = value;
            }
        }

        [Ignore]
        public List<McContactAddressAttribute> Addresses {
            get {
                ReadAncillaryData (McContactAncillaryDataEnum.READ_ADDRESSES);
                return DbAddresses;
            }
            set {
                ReadAncillaryData (McContactAncillaryDataEnum.READ_ADDRESSES);
                DbAddresses = value;
            }
        }

        [Ignore]
        public List<McContactStringAttribute> PhoneNumbers {
            get {
                ReadAncillaryData (McContactAncillaryDataEnum.READ_PHONENUMBERS);
                return DbPhoneNumbers;
            }
            set {
                ReadAncillaryData (McContactAncillaryDataEnum.READ_PHONENUMBERS);
                DbPhoneNumbers = value;
            }
        }

        [Ignore]
        public List<McContactEmailAddressAttribute> EmailAddresses {
            get {
                ReadAncillaryData (McContactAncillaryDataEnum.READ_EMAILADDRESSES);
                return DbEmailAddresses;
            }
            set {
                ReadAncillaryData (McContactAncillaryDataEnum.READ_EMAILADDRESSES);
                DbEmailAddresses = value;
            }
        }

        [Ignore]
        public List<McContactStringAttribute> IMAddresses {
            get {
                ReadAncillaryData (McContactAncillaryDataEnum.READ_IMADDRESSES);
                return DbIMAddresses;
            }
            set {
                ReadAncillaryData (McContactAncillaryDataEnum.READ_IMADDRESSES);
                DbIMAddresses = value;
            }
        }

        [Ignore]
        public List<McContactStringAttribute> Relationships {
            get {
                ReadAncillaryData (McContactAncillaryDataEnum.READ_RELATIONSHIPS);
                return DbRelationships;
            }
            set {
                ReadAncillaryData (McContactAncillaryDataEnum.READ_RELATIONSHIPS);
                DbRelationships = value;
            }
        }

        [Ignore]
        public List<McContactStringAttribute> Categories {
            get {
                ReadAncillaryData (McContactAncillaryDataEnum.READ_CATEGORIES);
                return DbCategories;
            }
            set {
                ReadAncillaryData (McContactAncillaryDataEnum.READ_CATEGORIES);
                DbCategories = value;
            }
        }

        [Ignore]
        public List<ContactOtherAttribute> Others {
            get {
                var attributeNames = ContactsHelper.GetTakenMiscNames (this);
                var others = new List<ContactOtherAttribute> ();
                foreach (var attr in attributeNames) {
                    others.Add (new ContactOtherAttribute (this, attr));
                }
                return others;
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

        public void RemoveDateAttribute (string name)
        {
            foreach (var l in Dates) {
                if (l.Name.Equals (name)) {
                    Dates.Remove (l);
                    return;
                }
            }
        }

        public void RemoveAddressAttribute (string name)
        {
            foreach (var l in Addresses) {
                if (l.Name.Equals (name)) {
                    Addresses.Remove (l);
                    return;
                }
            }
        }

        public void RemoveEmailAddressAttribute (string name)
        {
            foreach (var l in EmailAddresses) {
                if (l.Name.Equals (name)) {
                    EmailAddresses.Remove (l);
                    return;
                }
            }
        }

        public void RemoveStringAttribute (List<McContactStringAttribute> list, McContactStringType type, string name)
        {
            foreach (var l in list) {
                if (l.Type.Equals (type) && l.Name.Equals (name)) {
                    list.Remove (l);
                    return;
                }
            }
        }

        public void RemovePhoneNumberAttribute (string name)
        {
            RemoveStringAttribute (PhoneNumbers, McContactStringType.PhoneNumber, name);
        }

        public void RemoveIMAddressAttribute (string name)
        {
            RemoveStringAttribute (IMAddresses, McContactStringType.IMAddress, name);
        }

        public void RemoveRelationshipAttribute (string name)
        {
            RemoveStringAttribute (Relationships, McContactStringType.Relationship, name);
        }

        public void RemoveRelationshipAttributes (string name, string value)
        {
            foreach (var r in Relationships) {
                if (r.Name.Equals (name) && r.Value.Equals (value)) {
                    Relationships.Remove (r);
                    return;
                }
            }
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
            if (!McEmailAddress.Get (AccountId, value, out emailAddress)) {
                return null;
            }
            f.EmailAddress = emailAddress.Id;
            if (0 == this.CircleColor) {
                this.CircleColor = emailAddress.ColorIndex;
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
            ReadAncillaryData (McContactAncillaryDataEnum.READ_PHONENUMBERS);
            return AddStringAttribute (ref DbPhoneNumbers, accountId, McContactStringType.PhoneNumber, name, label, value);
        }

        public McContactStringAttribute AddDefaultPhoneNumberAttribute (int accountId, string name, string label, string value)
        {
            ReadAncillaryData (McContactAncillaryDataEnum.READ_PHONENUMBERS);
            return AddDefaultStringAttribute (ref DbPhoneNumbers, accountId, McContactStringType.PhoneNumber, name, label, value);
        }

        public McContactStringAttribute AddOrUpdatePhoneNumberAttribute (int accountId, string name, string label, string value)
        {
            ReadAncillaryData (McContactAncillaryDataEnum.READ_PHONENUMBERS);
            return AddOrUpdateStringAttribute (ref DbPhoneNumbers, accountId, McContactStringType.PhoneNumber, name, label, value);
        }

        public McContactStringAttribute AddOrUpdatePhoneNumberAttribute (int accountId, string name, string label, string value, bool isDefault)
        {
            ReadAncillaryData (McContactAncillaryDataEnum.READ_PHONENUMBERS);
            return AddOrUpdateStringAttribute (ref DbPhoneNumbers, accountId, McContactStringType.PhoneNumber, name, label, value, isDefault);
        }

        public McContactStringAttribute AddIMAddressAttribute (int accountId, string name, string label, string value)
        {
            ReadAncillaryData (McContactAncillaryDataEnum.READ_IMADDRESSES);
            return AddOrUpdateStringAttribute (ref DbIMAddresses, accountId, McContactStringType.IMAddress, name, label, value);
        }

        public McContactStringAttribute AddRelationshipAttribute (int accountId, string name, string label, string value)
        {
            ReadAncillaryData (McContactAncillaryDataEnum.READ_RELATIONSHIPS);
            return AddOrUpdateStringAttribute (ref DbRelationships, accountId, McContactStringType.Relationship, name, label, value);
        }

        public McContactStringAttribute AddChildAttribute (int accountId, string name, string label, string value)
        {
            ReadAncillaryData (McContactAncillaryDataEnum.READ_RELATIONSHIPS);
            return AddStringAttribute (ref DbRelationships, accountId, McContactStringType.Relationship, name, label, value);
        }

        public McContactStringAttribute AddCategoryAttribute (int accountId, string name)
        {
            ReadAncillaryData (McContactAncillaryDataEnum.READ_CATEGORIES);
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
            ReadAncillaryData (McContactAncillaryDataEnum.READ_EMAILADDRESSES);
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

        public string GetDefaultOrSinglePhoneNumber ()
        {
            if (0 == PhoneNumbers.Count) {
                return null;
            }
            if (1 == PhoneNumbers.Count) {
                return PhoneNumbers [0].Value;
            }
            foreach (var p in PhoneNumbers) {
                if (p.IsDefault) {
                    return p.Value;
                }
            }
            return null;
        }

        public string GetDefaultOrSingleEmailAddress ()
        {
            if (0 == EmailAddresses.Count) {
                return null;
            }
            if (1 == EmailAddresses.Count) {
                return EmailAddresses [0].Value;
            }
            foreach (var e in EmailAddresses) {
                if (e.IsDefault) {
                    return e.Value;
                }
            }
            return null;
        }

        /// <summary>
        ///        Db.CreateTable<McContact> ();
        ///        Db.CreateTable<McContactDateAttribute> ();
        ///        Db.CreateTable<McContactStringAttribute> ();
        ///        Db.CreateTable<McContactAddressAttribute> ();
        ///        Db.CreateTable<McContactEmailAddressAttribute> ();
        /// </summary>
        /// 

        protected McContactAncillaryDataEnum HasReadAncillaryData;

        // For unit test only
        public McContactAncillaryDataEnum TestHasReadAncillaryData {
            get {
                return HasReadAncillaryData;
            }
        }

        private NcResult ReadAncillaryData (McContactAncillaryDataEnum flags)
        {
            if (0 == Id) {
                return NcResult.OK ();
            }
            if (HasReadAncillaryData.HasFlag (flags)) {
                return NcResult.OK ();
            }
            var missingFlags = flags & (~HasReadAncillaryData);
            HasReadAncillaryData |= flags;
            return ForceReadAncillaryData (missingFlags);
        }

        public NcResult ForceReadAncillaryData (McContactAncillaryDataEnum flags)
        {
            NcAssert.True (0 < Id);
            if (flags.HasFlag (McContactAncillaryDataEnum.READ_DATES)) {
                DbDates = McContactDateAttribute.QueryByContactId<McContactDateAttribute> (Id);
            }
            if (flags.HasFlag (McContactAncillaryDataEnum.READ_ADDRESSES)) {
                DbAddresses = McContactAddressAttribute.QueryByContactId<McContactAddressAttribute> (Id);
            }
            if (flags.HasFlag (McContactAncillaryDataEnum.READ_EMAILADDRESSES)) {
                DbEmailAddresses = McContactEmailAddressAttribute.QueryByContactId<McContactEmailAddressAttribute> (Id);
            }
            if (flags.HasFlag (McContactAncillaryDataEnum.READ_RELATIONSHIPS)) {
                DbRelationships = McContactStringAttribute.QueryByContactIdAndType (Id, McContactStringType.Relationship).ToList ();
            }
            if (flags.HasFlag (McContactAncillaryDataEnum.READ_PHONENUMBERS)) {
                DbPhoneNumbers = McContactStringAttribute.QueryByContactIdAndType (Id, McContactStringType.PhoneNumber).ToList ();
            }
            if (flags.HasFlag (McContactAncillaryDataEnum.READ_IMADDRESSES)) {
                DbIMAddresses = McContactStringAttribute.QueryByContactIdAndType (Id, McContactStringType.IMAddress).ToList ();
            }
            if (flags.HasFlag (McContactAncillaryDataEnum.READ_CATEGORIES)) {
                DbCategories = McContactStringAttribute.QueryByContactIdAndType (Id, McContactStringType.Category).ToList ();
            }

            // FIXME: Error handling
            return NcResult.OK ();
        }

        public NcResult InsertAncillaryData ()
        {
            NcAssert.True (0 < Id);

            // FIXME: Fix this hammer?
            // FIXME: For update, Id may not be zero. Insert() asserts that Id is zero, so zero it.
            // FIXME: After hammer is fixed, use DeleteAncillaryData to clean up associated McPortrait.
            DeleteAncillaryData (deleteAll: false);

            if (HasReadAncillaryData.HasFlag (McContactAncillaryDataEnum.READ_DATES)) {
                foreach (var o in Dates) {
                    o.Id = 0;
                    o.ContactId = Id;
                    o.Insert ();
                }
            }
            if (HasReadAncillaryData.HasFlag (McContactAncillaryDataEnum.READ_ADDRESSES)) {
                foreach (var o in Addresses) {
                    o.Id = 0;
                    o.ContactId = Id;
                    o.Insert ();
                }
            }
            if (HasReadAncillaryData.HasFlag (McContactAncillaryDataEnum.READ_RELATIONSHIPS)) {
                foreach (var o in Relationships) {
                    o.Id = 0;
                    o.ContactId = Id;
                    o.Insert ();
                }
            }
            if (HasReadAncillaryData.HasFlag (McContactAncillaryDataEnum.READ_EMAILADDRESSES)) {
                foreach (var o in EmailAddresses) {
                    o.Id = 0;
                    o.ContactId = Id;
                    o.Insert ();
                }
            }
            if (HasReadAncillaryData.HasFlag (McContactAncillaryDataEnum.READ_PHONENUMBERS)) {
                foreach (var o in PhoneNumbers) {
                    o.Id = 0;
                    o.ContactId = Id;
                    o.Insert ();
                }
            }
            if (HasReadAncillaryData.HasFlag (McContactAncillaryDataEnum.READ_IMADDRESSES)) {
                foreach (var o in IMAddresses) {
                    o.Id = 0;
                    o.ContactId = Id;
                    o.Insert ();
                }
            }
            if (HasReadAncillaryData.HasFlag (McContactAncillaryDataEnum.READ_CATEGORIES)) {
                foreach (var o in Categories) {
                    o.Id = 0;
                    o.ContactId = Id;
                    o.Insert ();
                }
            }

            // FIXME: Error handling
            return NcResult.OK ();
        }

        public void UpdateCachedSortNames ()
        {
            // cached names for sorting are always lowercase so the sql index can be used to return results
            // as efficiently as possible already sorted in a case-insensitive manner
            CachedSortFirstName = (GetDisplayName () ?? "").Trim ().ToLower ();
            CachedSortLastName = (GetDisplayName (lastNameFirst: true) ?? "").Trim ().ToLower ();
            if (CachedSortFirstName.Length == 0) {
                var email = (GetEmailAddress () ?? "").Trim ().ToLower ();
                CachedSortFirstName = email;
                CachedSortLastName = email;
            }

            // group names are always uppercase so they don't need to be converted in the UI, which is
            // the only place they're used
            CachedGroupFirstName = GroupForName (CachedSortFirstName);
            CachedGroupLastName = GroupForName (CachedSortLastName);
        }

        private static string GroupForName (string name)
        {
            if (String.IsNullOrWhiteSpace (name) || !IsAsciiLetter (name [0])) {
                return "#";
            }
            return name.Substring (0, 1).ToUpper ();
        }

        private static bool IsAsciiLetter (char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        public override int Insert ()
        {
            using (var capture = CaptureWithStart ("Insert")) {
                if (0 == CircleColor) {
                    CircleColor = NachoPlatform.PlatformUserColorIndex.PickRandomColorForUser ();
                }
                EvaluateSelfEclipsing ();
                UpdateCachedSortNames ();

                // Indexing gleaned contacts is a waste of time.  Mark them as already indexed.
                if (this.IsGleaned ()) {
                    IndexVersion = ContactIndexDocument.Version;
                }

                int retval = 0;
                NcModel.Instance.RunInTransaction (() => {
                    retval = base.Insert ();
                    HasReadAncillaryData = McContactAncillaryDataEnum.READ_ALL;
                    InsertAncillaryData ();
                    EvaluateOthersEclipsing (EmailAddresses, PhoneNumbers, McContactOpEnum.Insert);
                });
                return retval;
            }
        }

        public override int Update ()
        {
            using (var capture = CaptureWithStart ("Update")) {
                EvaluateSelfEclipsing ();
                UpdateCachedSortNames ();
                int retval = 0;
                NcModel.Instance.RunInTransaction (() => {
                    retval = base.Update ();
                    if (McContactAncillaryDataEnum.READ_NONE != HasReadAncillaryData) {
                        InsertAncillaryData ();
                    }
                    EvaluateOthersEclipsing (EmailAddresses, PhoneNumbers, McContactOpEnum.Update);

                    if (!this.IsGleaned () && ContactIndexDocument.Version == this.IndexVersion) {
                        // A non-gleaned contact that has already been indexed. Re-index the contact.
                        NcBrain.ReindexContact (this);
                    }
                });
                return retval;
            }
        }

        public void UpdateEmailAddressesEclipsing ()
        {
            NcModel.Instance.BusyProtect (() => {
                return NcModel.Instance.Db.Execute ("UPDATE McContact SET EmailAddressesEclipsed = ? WHERE Id = ?",
                    EmailAddressesEclipsed, Id);
            });
        }

        public void UpdatePhoneNumbersEclipsing ()
        {
            NcModel.Instance.BusyProtect (() => {
                return NcModel.Instance.Db.Execute ("UPDATE McContact SET PhoneNumbersEclipsed = ? WHERE Id = ?",
                    PhoneNumbersEclipsed, Id);
            });
        }

        // We need a specialized version of Update() because the normal Update()
        // assumes there is a change in content and un-index the message. This would lead
        // to a perpetual loop of indexing and un-indexing.
        public void UpdateIndexVersion ()
        {
            NcModel.Instance.BusyProtect (() => {
                return NcModel.Instance.Db.Execute ("UPDATE McContact SET IndexVersion = ? WHERE Id = ?",
                    IndexVersion, Id);
            });
        }

        public override int Delete ()
        {
            using (var capture = CaptureWithStart ("Delete")) {
                // Force an auxilary read
                var addressList = EmailAddresses;
                var phoneList = PhoneNumbers;
                int retval = 0;
                NcModel.Instance.RunInTransaction (() => {
                    NcBrain.UnindexContact (this);
                    retval = base.Delete ();
                    EvaluateOthersEclipsing (addressList, phoneList, McContactOpEnum.Delete);
                });
                return retval;
            }
        }

        private void EvaluateEmailAddressEclipsing (List<McContactEmailAddressAttribute> addressList, McContactOpEnum op)
        {
            foreach (var address in addressList) {
                var contactList =
                    QueryByEmailAddress (AccountId, address.Value)
                        .Where (x => (x.Id != Id) && (HasSameName (x) || x.IsAnonymous ()));
                var count = contactList.Count ();
                if (6 < count) {
                    Log.Warn (Log.LOG_DB, "EvaluateEmailAddressEclipsing: {0} contacts", count);
                }
                foreach (var contact in contactList) {
                    count++;
                    if (contact.EmailAddressesEclipsed && (McContactOpEnum.Insert == op)) {
                        continue; // insertion can never cause an eclipsed contact to become uneclipsed
                    }
                    if (!contact.EmailAddressesEclipsed && (McContactOpEnum.Delete == op)) {
                        continue; // deletion can never cuase an uneclipsed contact to become eclipsed
                    }
                    var newEclipsed = contact.ShouldEmailAddressesBeEclipsed ();
                    if (newEclipsed != contact.EmailAddressesEclipsed) {
                        contact.EmailAddressesEclipsed = newEclipsed;
                        contact.UpdateEmailAddressesEclipsing ();
                    }
                }
            }
        }

        private void EvaluatePhoneNumberEclipsing (List<McContactStringAttribute> phoneList, McContactOpEnum op)
        {
            foreach (var phone in phoneList) {
                if (McContactStringType.PhoneNumber != phone.Type) {
                    continue;
                }
                var contactList =
                    QueryByPhoneNumber (AccountId, phone.Value)
                        .Where (x => (x.Id != Id) && (HasSameName (x) || x.IsAnonymous ()));
                var count = contactList.Count ();
                if (6 < count) {
                    Log.Warn (Log.LOG_DB, "EvaluatePhoneNumberEclipsing: {0} contacts", count);
                }
                foreach (var contact in contactList) {
                    if (contact.PhoneNumbersEclipsed && (McContactOpEnum.Insert == op)) {
                        continue; // insertion can never cause an eclipsed contact to become uneclipsed
                    }
                    if (!contact.PhoneNumbersEclipsed && (McContactOpEnum.Delete == op)) {
                        continue; // deletion can never cuase an uneclipsed contact to become eclipsed
                    }
                    var newEclipsed = contact.ShouldPhoneNumbersBeEclipsed ();
                    if (newEclipsed != contact.PhoneNumbersEclipsed) {
                        contact.PhoneNumbersEclipsed = newEclipsed;
                        contact.UpdatePhoneNumbersEclipsing ();
                    }
                }
            }
        }

        private void EvaluateOthersEclipsing (List<McContactEmailAddressAttribute> addressList,
                                              List<McContactStringAttribute> phoneList,
                                              McContactOpEnum op)
        {
            SetupRicCache ();
            EvaluateEmailAddressEclipsing (addressList, op);
            EvaluatePhoneNumberEclipsing (phoneList, op);
            ResetRicCache ();
        }

        private void EvaluateSelfEclipsing ()
        {
            EmailAddressesEclipsed = ShouldEmailAddressesBeEclipsed ();
            PhoneNumbersEclipsed = ShouldPhoneNumbersBeEclipsed ();
        }

        public override void DeleteAncillary ()
        {
            DeleteAncillaryData (deleteAll: true);
        }

        private void DeleteStringAttribute (McContactStringType stringType)
        {
            NcAssert.True (NcModel.Instance.IsInTransaction ());
            NcModel.Instance.Db.Execute (
                "DELETE FROM McContactStringAttribute WHERE ContactId = ? AND Type = ?", Id, (int)stringType);
        }

        private NcResult DeleteAncillaryData (bool deleteAll)
        {
            NcAssert.True (NcModel.Instance.IsInTransaction ());
            if (deleteAll) {
                NcModel.Instance.Db.Execute ("DELETE FROM McContactStringAttribute WHERE ContactId = ?", Id);
            } else {
                if (HasReadAncillaryData.HasFlag (McContactAncillaryDataEnum.READ_RELATIONSHIPS)) {
                    DeleteStringAttribute (McContactStringType.Relationship);
                }
                if (HasReadAncillaryData.HasFlag (McContactAncillaryDataEnum.READ_PHONENUMBERS)) {
                    DeleteStringAttribute (McContactStringType.PhoneNumber);
                }
                if (HasReadAncillaryData.HasFlag (McContactAncillaryDataEnum.READ_IMADDRESSES)) {
                    DeleteStringAttribute (McContactStringType.IMAddress);
                }
                if (HasReadAncillaryData.HasFlag (McContactAncillaryDataEnum.READ_CATEGORIES)) {
                    DeleteStringAttribute (McContactStringType.Category);
                }
            }
            if (deleteAll || HasReadAncillaryData.HasFlag (McContactAncillaryDataEnum.READ_DATES)) {
                NcModel.Instance.Db.Execute ("DELETE FROM McContactDateAttribute WHERE ContactId = ?", Id);
            }
            if (deleteAll || HasReadAncillaryData.HasFlag (McContactAncillaryDataEnum.READ_ADDRESSES)) {
                NcModel.Instance.Db.Execute ("DELETE FROM McContactAddressAttribute WHERE ContactId = ?", Id);
            }
            if (deleteAll || HasReadAncillaryData.HasFlag (McContactAncillaryDataEnum.READ_EMAILADDRESSES)) {
                NcModel.Instance.Db.Execute ("DELETE FROM McContactEmailAddressAttribute WHERE ContactId = ?", Id);
            }
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
                                              " s.Value = ? AND " +
                                              " likelihood (c.AccountId = ?, 1.0) AND " +
                                              " likelihood (c.IsAwaitingDelete = 0, 1.0) ",
                                              emailAddress, accountId);
            return contactList;
        }

        public static List<McContact> QueryByEmailAddress (string emailAddress)
        {
            List<McContact> contactList = NcModel.Instance.Db.Query<McContact> (
                "SELECT c.* FROM McContact AS c " +
                " JOIN McContactEmailAddressAttribute AS s ON c.Id = s.ContactId " +
                " WHERE " +
                " s.Value = ? AND " +
                " likelihood (c.IsAwaitingDelete = 0, 1.0) ",
                emailAddress);
            return contactList;
        }

        public static McContact QueryBestMatchByEmailAddress (int preferredAccount, string emailAddress, int addressId)
        {
            if (0 != addressId) {
                var address = McEmailAddress.QueryById<McEmailAddress> (addressId);
                if (null != address) {
                    var contact = BestMatch (QueryByEmailAddress (address.CanonicalEmailAddress), preferredAccount);
                    if (null != contact) {
                        return contact;
                    }
                }
            }
            return QueryBestMatchByEmailAddress (preferredAccount, emailAddress);
        }

        public static McContact QueryBestMatchByEmailAddress (int preferredAccount, string emailAddress)
        {
            var result = BestMatch (QueryByEmailAddress (emailAddress), preferredAccount);
            if (null == result) {
                MimeKit.MailboxAddress parsedAddress;
                if (MimeKit.MailboxAddress.TryParse (emailAddress, out parsedAddress)) {
                    if (emailAddress != parsedAddress.Address) {
                        result = BestMatch (QueryByEmailAddress (parsedAddress.Address), preferredAccount);
                    }
                }
            }
            return result;
        }

        private static McContact BestMatch (List<McContact> matches, int preferredAccount)
        {
            McContact best = null;
            foreach (var match in matches) {
                if (null == best ||
                    (best.IsGleaned () && !match.IsGleaned ()) ||
                    (best.IsGleaned () == match.IsGleaned () && best.AccountId != preferredAccount && match.AccountId == preferredAccount)) {
                    best = match;
                }
            }
            return best;
        }

        public static List<NcContactPortraitIndex> QueryForPortraits (List<int> emailAddressIndexList)
        {
            var set = String.Format ("( {0} )", String.Join (",", emailAddressIndexList.ToArray<int> ()));
            var cmd = String.Format (
                          "Select s.EmailAddress, c.PortraitId From McContact AS c" +
                          " JOIN McContactEmailAddressAttribute AS s ON c.Id = s.ContactId " +
                          " WHERE " +
                          " s.EmailAddress IN {0} AND " +
                          " likelihood (c.IsAwaitingDelete = 0, 1.0) AND " +
                          " likelihood (c.PortraitId <> 0, 0.1)", set);
            return NcModel.Instance.Db.Query<NcContactPortraitIndex> (cmd);
        }

        public static List<NcContactPortraitEmailIndex> QueryForMessagePortraitEmails (int messageId)
        {
            var cmd = "SELECT " +
                      "e.id as EmailAddressId, " +
                      "e.CanonicalEmailAddress as EmailAddress, " +
                      "e.ColorIndex as ColorIndex, " +
                      "(SELECT MAX(PortraitId) FROM McContact c JOIN McContactEmailAddressAttribute ea ON c.Id = ea.ContactId WHERE ea.EmailAddress = e.Id AND likelihood (c.IsAwaitingDelete = 0, 1.0) AND likelihood (c.PortraitId <> 0, 0.1)) as PortraitId " +
                      "FROM McMapEmailAddressEntry a " +
                      "JOIN McEmailAddress e ON a.EmailAddressId = e.Id " +
                      "WHERE a.ObjectId = ?";
            return NcModel.Instance.Db.Query<NcContactPortraitEmailIndex> (cmd, messageId);
        }

        public static List<McContact> QueryGleanedContactsByEmailAddress (int accountId, string emailAddress)
        {
            // TODO - When we use Source = Internal for something other than gleaned, we need to fix this
            //        query to use McMapFolderFolderEntry to look for only internal contacts in the 
            //        gleaned folder
            List<McContact> contactList = NcModel.Instance.Db.Query<McContact> (
                                              "SELECT c.* FROM McContact AS c " +
                                              " JOIN McContactEmailAddressAttribute AS s ON c.Id = s.ContactId " +
                                              "WHERE " +
                                              " s.Value = ? AND " +
                                              " c.Source = ? AND " +
                                              " likelihood (c.AccountId = ?, 1.0) AND " +
                                              " likelihood (c.IsAwaitingDelete = 0, 1.0) ",
                                              emailAddress, (int)McAbstrItem.ItemSource.Internal, accountId);
            return contactList;
        }

        public static List<McContact> QueryByPhoneNumber (int accountId, string phoneNumber)
        {
            return NcModel.Instance.Db.Query<McContact> (
                "SELECT c.* FROM McContact AS c " +
                " JOIN McContactStringAttribute AS s ON c.Id = s.ContactId " +
                " WHERE " +
                " s.Value = ? AND " +
                " s.Type = ? AND " +
                " likelihood (c.AccountId = ?, 1.0) AND " +
                " likelihood (c.IsAwaitingDelete = 0, 1.0) ",
                phoneNumber, (int)McContactStringType.PhoneNumber, accountId);
        }

        public static List<McContact> QueryLikeEmailAddress (int accountId, string emailAddress)
        {
            var emailWildcard = "%" + emailAddress + "%";
            List<McContact> contactList = NcModel.Instance.Db.Query<McContact> (
                                              "SELECT c.* FROM McContact AS c " +
                                              " JOIN McContactEmailAddressAttribute AS s ON c.Id = s.ContactId " +
                                              " WHERE " +
                                              " s.Value LIKE ? AND " +
                                              " likelihood (c.AccountId = ?, 1.0) AND " +
                                              " likelihood (c.IsAwaitingDelete = 0, 1.0) ",
                                              emailWildcard, accountId);
            return contactList;
        }

        public static List<McContact> QueryByEmailAddressInFolder (int accountId, int folderId, string emailAddress)
        {
            List<McContact> contactList = NcModel.Instance.Db.Query<McContact> (
                                              "SELECT c.* FROM McContact AS c " +
                                              " JOIN McContactEmailAddressAttribute AS s ON c.Id = s.ContactId " +
                                              " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                                              " WHERE " +
                                              " likelihood (c.AccountId = m.AccountId, 1.0) AND " +
                                              " likelihood (c.AccountId = ?, 1.0) AND " +
                                              " likelihood (c.IsAwaitingDelete = 0, 1.0) AND " +
                                              " s.Value = ? AND " +
                                              " likelihood (m.ClassCode = ?, 0.2) AND " +
                                              " likelihood (m.FolderId = ?, 0.05) ",
                                              accountId, emailAddress, (int)McAbstrFolderEntry.ClassCodeEnum.Contact, folderId);
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
                                              " likelihood (c.AccountId = m.AccountId, 1.0) AND " +
                                              " likelihood (c.AccountId = f.AccountId, 1.0) AND " +
                                              " likelihood (c.AccountId = ?, 1.0) AND " +
                                              " likelihood (c.IsAwaitingDelete = 0, 1.0) AND " +
                                              " s.Value = ? AND " +
                                              " likelihood (m.ClassCode = ?, 0.2) AND " +
                                              " f.IsClientOwned = false ",
                                              accountId, emailAddress, (int)McAbstrFolderEntry.ClassCodeEnum.Contact);
            return contactList;
        }

        public static List<NcContactIndex> QueryContactItems (int accountId, int folderId)
        {
            return NcModel.Instance.Db.Query<NcContactIndex> (
                "SELECT c.Id as Id, substr(c.FirstName, 0, 1) as FirstLetter FROM McContact AS c " +
                " JOIN McMapFolderFolderEntry AS m ON c.Id = m.FolderEntryId " +
                " WHERE " +
                " likelihood (c.AccountId = ?, 1.0) AND " +
                " likelihood (c.IsAwaitingDelete = 0, 1.0) AND " +
                " likelihood (m.AccountId = ?, 1.0) AND " +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (m.FolderId = ?, 0.05) " +
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
                " likelihood (c.AccountId = ?, 1.0) AND " +
                " likelihood (m.AccountId = ?, 1.0) AND " +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (m.FolderId = ?, 0.05) " +
                " ORDER BY c.WeightedRank DESC",
                accountId, accountId, (int)McAbstrFolderEntry.ClassCodeEnum.Contact, ricFolder.Id);
        }

        public static List<McContact> QueryByIds (List<string> ids)
        {
            if (0 == ids.Count) {
                return new List<McContact> ();
            }
            var query = String.Format ("SELECT c.* FROM McContact AS c WHERE c.Id IN ({0})", String.Join (",", ids));
            return NcModel.Instance.Db.Query<McContact> (query);
        }

        public static List<McContact> QueryByIds (List<long> ids)
        {
            if (0 == ids.Count) {
                return new List<McContact> ();
            }
            var query = string.Format ("SELECT * FROM McContact WHERE Id IN ({0})", string.Join (",", ids));
            return NcModel.Instance.Db.Query<McContact> (query);
        }

        /// <summary>
        /// Search all contacts, returning any contact whose first name, last name, or company name
        /// starts with the given string.
        /// </summary>
        public static List<McContact> SearchAllContactsByName (string searchFor)
        {
            if (searchFor.Contains ("%") || searchFor.Contains ("_")) {
                string prefixPattern = searchFor.Replace ("^", "^^").Replace ("%", "^%").Replace ("_", "^_") + "%";
                return NcModel.Instance.Db.Query<McContact> (
                    "SELECT * FROM McContact WHERE FirstName LIKE ? ESCAPE '^' OR LastName LIKE ? ESCAPE '^' OR CompanyName LIKE ? ESCAPE '^' LIMIT 100",
                    prefixPattern, prefixPattern, prefixPattern);
            } else {
                string prefixPattern = searchFor + "%";
                return NcModel.Instance.Db.Query<McContact> (
                    "SELECT * FROM McContact WHERE FirstName LIKE ? OR LastName LIKE ? OR CompanyName LIKE ? LIMIT 100",
                    prefixPattern, prefixPattern, prefixPattern);
            }
        }

        /// <summary>
        /// Search the e-mail addresses associated with all contacts, returning any e-mail address
        /// where the address as a whole or the domain name starts with the given string.
        /// </summary>
        public static List<McContactEmailAddressAttribute> SearchAllContactEmail (string searchFor)
        {
            if (searchFor.Contains ("%") || searchFor.Contains ("_")) {
                string prefixPattern = searchFor.Replace ("^", "^^").Replace ("%", "^%").Replace ("_", "^_") + "%";
                string domainPattern = "%@" + prefixPattern;
                return NcModel.Instance.Db.Query<McContactEmailAddressAttribute> (
                    "SELECT * FROM McContactEmailAddressAttribute WHERE Value LIKE ? ESCAPE '^' OR Value LIKE ? ESCAPE '^' LIMIT 100",
                    prefixPattern, domainPattern);
            } else {
                string prefixPattern = searchFor + "%";
                string domainPattern = "%@" + prefixPattern;
                return NcModel.Instance.Db.Query<McContactEmailAddressAttribute> (
                    "SELECT * FROM McContactEmailAddressAttribute WHERE Value LIKE ? OR Value LIKE ? LIMIT 100",
                    prefixPattern, domainPattern);
            }
        }

        public static int CountByAccountId (int accountId)
        {
            return NcModel.Instance.Db.ExecuteScalar<int> ("SELECT COUNT(*) FROM McContact WHERE AccountId = ?", accountId);
        }

        public static List<NcContactIndex> AllContactsSortedByName (int accountId, bool withEclipsing = false, bool usingLastName = false)
        {
            if (accountId == 0) {
                return AllContactsSortedByName (withEclipsing: withEclipsing);
            }
            var format = "SELECT Id, {0} as FirstLetter FROM McContact WHERE accountId = ? AND likelihood (IsAwaitingDelete = 0, 1.0)";
            if (withEclipsing) {
                format += " AND (EmailAddressesEclipsed = 0 OR PhoneNumbersEclipsed = 0)";
            }
            format += " ORDER BY {1}";
            string sql;
            if (usingLastName) {
                sql = String.Format (format, "CachedGroupLastName", "CachedSortLastName");
            } else {
                sql = String.Format (format, "CachedGroupFirstName", "CachedSortFirstName");
            }
            return NcModel.Instance.Db.Query<NcContactIndex> (sql, accountId);
        }

        public static List<NcContactIndex> AllContactsSortedByName (bool withEclipsing = false, bool usingLastName = false)
        {
            var format = "SELECT Id, {0} as FirstLetter FROM McContact WHERE likelihood (IsAwaitingDelete = 0, 1.0)";
            if (withEclipsing) {
                format += " AND (EmailAddressesEclipsed = 0 OR PhoneNumbersEclipsed = 0)";
            }
            format += " ORDER BY {1}";
            string sql;
            if (usingLastName) {
                sql = String.Format (format, "CachedGroupLastName", "CachedSortLastName");
            } else {
                sql = String.Format (format, "CachedGroupFirstName", "CachedSortFirstName");
            }
            return NcModel.Instance.Db.Query<NcContactIndex> (sql);
        }

        public static List<NcContactIndex> AllEmailContactsSortedByName (bool usingLastName = false)
        {
            var format = "SELECT Id, {0} as FirstLetter FROM McContact c WHERE likelihood (c.IsAwaitingDelete = 0, 1.0) AND EXISTS (SELECT Id FROM McContactEmailAddressAttribute a WHERE a.ContactId = c.Id AND trim(ifnull(a.Value, '')) != '') ORDER BY {1}";
            string sql;
            if (usingLastName) {
                sql = String.Format (format, "CachedGroupLastName", "CachedSortLastName");
            } else {
                sql = String.Format (format, "CachedGroupFirstName", "CachedSortFirstName");
            }
            return NcModel.Instance.Db.Query<NcContactIndex> (sql);
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
                " likelihood (c.AccountId = ?, 1.0) AND " +
                " likelihood (m.AccountId = ?, 1.0) AND " +
                " likelihood (m.ClassCode = ?, 0.2) AND " +
                " likelihood (m.FolderId = ?, 0.05) " +
                " ORDER BY c.WeightedRank DESC LIMIT ?",
                accountId, accountId, (int)McAbstrFolderEntry.ClassCodeEnum.Contact, ricFolder.Id, limit);
        }

        public static List<McContact> QueryNeedIndexing (int maxContact)
        {
            return NcModel.Instance.Db.Query<McContact> (
                "SELECT c.* FROM McContact as c " +
                " LEFT JOIN McBody as b ON b.Id == c.BodyId " +
                " WHERE likelihood (c.IndexVersion < ?, 0.5) OR " +
                " (likelihood (c.BodyId > 0, 0.2) AND " +
                "  likelihood (b.FilePresence = ?, 0.5) AND " +
                "  likelihood (c.IndexVersion < ?, 0.5)) " +
                " LIMIT ?",
                ContactIndexDocument.Version - 1, McAbstrFileDesc.FilePresenceEnum.Complete,
                ContactIndexDocument.Version, maxContact
            );
        }

        public static List<object> QueryNeedIndexingObjects (int maxContacts)
        {
            return new List<object> (QueryNeedIndexing (maxContacts));
        }

        public string GetDisplayName (bool lastNameFirst = false)
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
                if (lastNameFirst) {
                    value.Insert (0, LastName);
                } else {
                    value.Add (LastName);
                }
            }
            if (!String.IsNullOrEmpty (Suffix)) {
                value.Add (Suffix);
            }
            var name = String.Join (" ", value);
            if (String.IsNullOrEmpty (name)) {
                name = CompanyName;
            }
            return name;
        }

        public string GetInformalDisplayName ()
        {
            if (!String.IsNullOrEmpty (FirstName)) {
                return FirstName;
            }
            if (!String.IsNullOrEmpty (LastName)) {
                return LastName;
            }
            if (!String.IsNullOrEmpty (MiddleName)) {
                return MiddleName;
            }
            if (!String.IsNullOrEmpty (DisplayName)) {
                return DisplayName;
            }
            return CompanyName;
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
                    NachoCore.Brain.NcBrain.UpdateAddressScore (emailAddress.AccountId, emailAddress.Id, true);
                }
            }

            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Status = NcResult.Info (NcResult.SubKindEnum.Info_ContactChanged),
                Account = McAccount.QueryById<McAccount> (this.AccountId),
            });
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

        public bool IsDevice ()
        {
            return (ItemSource.Device == Source);
        }

        public bool IsSynced ()
        {
            if (ItemSource.ActiveSync != Source) {
                return false;
            }
            return (!IsGal () && !IsRic ());
        }

        public bool IsGleaned ()
        {
            return ((ItemSource.Internal == Source) && (1 == EmailAddresses.Count));
        }

        public bool IsGal ()
        {
            return ((ItemSource.ActiveSync == Source) && (!String.IsNullOrEmpty (GalCacheToken)));
        }

        // IsRic() requires a db query and is called quite a bit during contact eclipsing evaluation.
        // In orer to speed this up, we create a "Is RIC" cache. You must use this inside a transaction
        // (RunInTransaction) in order to guarantee thread safety.
        private McContactRicCache ricCache = null;

        public bool IsRic ()
        {
            if (null != ricCache) {
                bool isRic;
                if (ricCache.Get (Id, out isRic)) {
                    return isRic;
                }
            }

            int ricFolderId = McFolder.GetRicFolderId (AccountId);
            if (-1 == ricFolderId) {
                if (null != ricCache) {
                    ricCache.Add (Id, false);
                }
                return false;
            }
            var map =
                McMapFolderFolderEntry.QueryByFolderIdFolderEntryIdClassCode (
                    AccountId,
                    ricFolderId,
                    Id,
                    ClassCodeEnum.Contact);
            bool result = (null != map);
            if (null != ricCache) {
                ricCache.Add (Id, result);
            }
            return result;
        }

        private int GetContactTypeIndex ()
        {
            if (IsDevice ()) {
                return 5;
            } else if (IsSynced ()) {
                return 4;
            } else if (IsGal ()) {
                return 3;
            } else if (IsRic ()) {
                return 2;
            } else if (IsGleaned ()) {
                return 1;
            }
            Log.Error (Log.LOG_CONTACTS,
                "unknown contact type: source={0}, accountId={1}, galCacheToken={2}, # email addresses={3}",
                Source, AccountId, GalCacheToken, EmailAddresses.Count);
            // Make contacts of unknown type the highest priority so they are never eclipsed by mistake
            return 6;
        }

        private static bool ShouldSuperceded (McContact a, McContact b)
        {
            return (a.GetContactTypeIndex () > b.GetContactTypeIndex ());
        }

        private bool ShouldBeSupercededBy (McContact c)
        {
            return ShouldSuperceded (c, this);
        }

        private delegate bool CheckAttributeEclipsingFunc (McContact c);

        private bool ShouldAttributeBeEclipsed (List<McContact> contactList, CheckAttributeEclipsingFunc checkFunc)
        {
            foreach (var contact in contactList.Distinct (new McContactComparer ())) {
                if (checkFunc (contact)) {
                    return true;
                }
            }
            return false;
        }

        public bool ShouldEmailAddressesBeEclipsed ()
        {
            if (!IsGleaned () && !IsGal () && !IsRic ()) {
                return false;
            }
            if (0 == EmailAddresses.Count) {
                return true;
            }

            List<McContact> contactList = new List<McContact> ();
            foreach (var address in EmailAddresses) {
                var contacts = McContact.QueryByEmailAddress (AccountId, address.Value).Where (x => x.Id != Id);
                contactList.AddRange (contacts);
            }

            var isAnonymous = IsAnonymous () && (IsRic () || IsGleaned ());
            return ShouldAttributeBeEclipsed (contactList, (c) => {
                if (isAnonymous && !c.IsAnonymous ()) {
                    return true;
                }
                return HasSameName (c) && ShouldBeSupercededBy (c) && McContactEmailAddressAttribute.IsSuperSet (c.EmailAddresses, EmailAddresses);
            });
        }

        public bool ShouldPhoneNumbersBeEclipsed ()
        {
            if (!IsGleaned () && !IsGal () && !IsRic ()) {
                return false;
            }
            if (0 == PhoneNumbers.Count) {
                return true;
            }

            List<McContact> contactList = new List<McContact> ();
            foreach (var address in PhoneNumbers) {
                var contacts = McContact.QueryByPhoneNumber (AccountId, address.Value).Where (x => x.Id != Id);
                contactList.AddRange (contacts);
            }

            return ShouldAttributeBeEclipsed (contactList, (c) => {
                return HasSameName (c) && ShouldBeSupercededBy (c) && McContactStringAttribute.IsSuperSet (c.PhoneNumbers, PhoneNumbers);
            });
        }

        public bool HasSameName (McContact other)
        {
            return (
                (FirstName == other.FirstName) &&
                (MiddleName == other.MiddleName) &&
                (LastName == other.LastName) &&
                (Suffix == other.Suffix));
        }

        public bool IsAnonymous ()
        {
            return (
                String.IsNullOrEmpty (FirstName) &&
                String.IsNullOrEmpty (MiddleName) &&
                String.IsNullOrEmpty (LastName)
            );
        }

        private void SetupRicCache ()
        {
            NcAssert.True (NcModel.Instance.Db.IsInTransaction);
            ricCache = new McContactRicCache ();
        }

        private void ResetRicCache ()
        {
            NcAssert.True (NcModel.Instance.Db.IsInTransaction);
            ricCache = null;
        }

        public void SetIndexVersion ()
        {
            if (0 == BodyId) {
                IndexVersion = ContactIndexDocument.Version - 1;
            } else {
                var body = GetBody ();
                if ((null != body) && body.IsComplete ()) {
                    IndexVersion = ContactIndexDocument.Version;
                } else {
                    IndexVersion = ContactIndexDocument.Version - 1;
                }
            }
        }

        protected static bool CompareLists<T> (List<T> list1, List<T> list2, Func<T, T, bool> comparer)
        {
            if (list1.Count != list2.Count) {
                return false;
            }
            for (int n = 0; n < list1.Count; n++) {
                if (!comparer (list1 [n], list2 [n])) {
                    return false;
                }
            }
            return true;
        }

        protected static bool CompareStringAttributeLists (List<McContactStringAttribute> list1, List<McContactStringAttribute> list2)
        {
            return CompareLists<McContactStringAttribute> (list1, list2,
                (attr1, attr2) => (attr1.Type == attr2.Type) && (attr1.Value == attr2.Value) && (attr1.Label == attr2.Label));
        }

        public static bool CompareOnEditableFields (McContact a, McContact b)
        {
            if ((null == a) || (null == b)) {
                return false;
            }

            string valueA, valueB;
            string [] properties = new string [] {
                "FirstName",
                "MiddleName",
                "LastName",
                "Suffix",
                "CompanyName",
                "Alias",
                "FileAs",
                "JobTitle",
                "OfficeLocation",
                "Title",
                "WebPage",
                "AccountName",
                "CustomerId",
                "GovernmentId",
                "MMS",
                "NickName",
                "YomiCompanyName",
                "YomiFirstName",
                "YomiLastName",
            };

            foreach (var prop in properties) {
                valueA = (string)a.GetType ().GetProperty (prop).GetValue (a, null);
                valueB = (string)b.GetType ().GetProperty (prop).GetValue (b, null);
                if (valueA != valueB) {
                    return false;
                }
            }

            if (!CompareLists<McContactEmailAddressAttribute> (a.EmailAddresses, b.EmailAddresses,
                    (attr1, attr2) => (attr1.Value == attr2.Value))) {
                return false;
            }
            if (!CompareStringAttributeLists (a.PhoneNumbers, b.PhoneNumbers)) {
                return false;
            }
            if (!CompareLists<McContactDateAttribute> (a.Dates, b.Dates,
                    (attr1, attr2) => ((attr1.Label == attr2.Label) && (attr1.Value == attr2.Value)))) {
                return false;
            }
            if (!CompareLists<McContactAddressAttribute> (a.Addresses, b.Addresses,
                    (addr1, addr2) => (
                        (addr1.City == addr2.City) && (addr1.Country == addr2.Country) &&
                        (addr1.PostalCode == addr2.PostalCode) && (addr1.State == addr2.State) &&
                        (addr1.Street == addr2.Street)
                    ))) {
                return false;
            }
            if (!CompareStringAttributeLists (a.IMAddresses, b.IMAddresses)) {
                return false;
            }
            if (!CompareStringAttributeLists (a.Relationships, b.Relationships)) {
                return false;
            }

            return true;
        }
    }
}
