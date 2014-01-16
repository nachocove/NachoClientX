using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public partial class McContact : McItem
    {
        /// <summary>
        /// Contacts schema
        /// Contacts are associated with folders, real or pseudo.
        /// </summary>
        /// 

        /// Who defined this contact?
        public enum McContactSource
        {
            ActiveSync,
            Device,
            User,
            Internal,
        }

        /// ActiveSync or Device
        public McContactSource Source { get; set; }

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
            Dates = new List<McContactDateAttribute> ();
            Addresses = new List<McContactAddressAttribute> ();
            PhoneNumbers = new List<McContactStringAttribute> ();
            EmailAddresses = new List<McContactStringAttribute> ();
            IMAddresses = new List<McContactStringAttribute> ();
            Relationships = new List<McContactStringAttribute> ();
            Categories = new List<McContactStringAttribute> ();
        }

        public McContact (McContactSource source) : this ()
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

        public void AddEmailAddressAttribute (string name, string label, string value)
        {
            AddStringAttribute (ref EmailAddresses, McContactStringType.EmailAddress, name, label, value);
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
    }

    partial class McContact
    {
        /// <summary>
        ///        Db.CreateTable<McContact> ();
        ///        Db.CreateTable<McContactDateAttribute> ();
        ///        Db.CreateTable<McContactStringAttribute> ();
        ///        Db.CreateTable<McContactAddressAttribute> ();
        /// </summary>

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
            Dates = db.Table<McContactDateAttribute> ().Where (x => x.ContactId == Id).ToList ();
            Addresses = db.Table<McContactAddressAttribute> ().Where (x => x.ContactId == Id).ToList ();
            Relationships = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.Relationship).ToList ();
            EmailAddresses = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.EmailAddress).ToList ();
            PhoneNumbers = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.PhoneNumber).ToList ();
            IMAddresses = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.IMAddress).ToList ();
            Categories = db.Table<McContactStringAttribute> ().Where (x => x.ContactId == Id && x.Type == McContactStringType.Category).ToList ();

            // TODO: Error handling
            return NcResult.OK ();
        }

        public NcResult Insert (SQLiteConnection db)
        {
            db.Insert (this);
            InsertAncillaryData (db);
            // TODO: Error handling
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
    
            // TODO: Error handling
            return NcResult.OK ();
        }

        public NcResult Delete (SQLiteConnection db)
        {
            db.Delete (this);
            DeleteAncillaryData (db);
            // TODO: Add error processing
            return NcResult.OK ();
        }

        protected NcResult DeleteAncillaryData (SQLiteConnection db)
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

        public static List<McContact> QueryByEmailAddress (int accountId, string emailAddress)
        {
            return BackEnd.Instance.Db.Query<McContact> ("SELECT c.* FROM McContact AS c JOIN McContactStringAttribute AS s ON c.Id = s.ContactId WHERE " +
            " c.AccountId = ? AND " +
            " s.Type = ? AND " +
            " s.Value = ? ",
                accountId, McContactStringType.EmailAddress, emailAddress);
        }
    }
}
