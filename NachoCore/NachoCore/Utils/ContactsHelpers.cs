
using System;
using System.Collections;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.ActiveSync;
using System.Linq;

namespace NachoCore.Utils
{
    public static class ContactsHelper
    {
 
        //The order of these names also determines their priority when sorting.
        public static readonly List<string> PhoneNames = new List<string> () {
            Xml.Contacts.MobilePhoneNumber,
            Xml.Contacts.BusinessPhoneNumber,
            Xml.Contacts.HomePhoneNumber,
            Xml.Contacts.AssistantPhoneNumber,
            Xml.Contacts2.CompanyMainPhone,
            Xml.Contacts.Business2PhoneNumber,
            Xml.Contacts.Home2PhoneNumber,
            Xml.Contacts.CarPhoneNumber,
            Xml.Contacts.PagerNumber,
            Xml.Contacts.RadioPhoneNumber,
            Xml.Contacts.BusinessFaxNumber,
            Xml.Contacts.HomeFaxNumber,
        };

        public static readonly List<string> EmailNames = new List<string> () {
            Xml.Contacts.Email1Address,
            Xml.Contacts.Email2Address,
            Xml.Contacts.Email3Address
        };

        public static readonly List<string> DateNames = new List<string> () {
            Xml.Contacts.Birthday,
            Xml.Contacts.Anniversary
        };

        public static readonly List<string> IMAddressNames = new List<string> () {
            Xml.Contacts2.IMAddress,
            Xml.Contacts2.IMAddress2,
            Xml.Contacts2.IMAddress3
        };

        public static readonly List<string> AddressNames = new List<string> () {
            "Home",
            "Business",
            "Other"
        };

        public static readonly List<string> MiscNames = new List<string> () {
            Xml.Contacts.Alias,
            Xml.Contacts.Department,
            Xml.Contacts.FileAs,
            Xml.Contacts.JobTitle,
            Xml.Contacts.OfficeLocation,
            Xml.Contacts.Title,
            Xml.Contacts.WebPage,
            Xml.Contacts2.AccountName,
            Xml.Contacts2.CustomerId,
            Xml.Contacts2.GovernmentId,
            Xml.Contacts2.MMS,
            Xml.Contacts2.NickName,
            Xml.Contacts.YomiCompanyName,
            Xml.Contacts.YomiFirstName,
            Xml.Contacts.YomiLastName,
        };

        public static readonly List<string> RelationshipNames = new List<string> () {
            Xml.Contacts.Spouse,
            Xml.Contacts.AssistantName,
            Xml.Contacts2.ManagerName,
            Xml.Contacts.Child,
        };

        public static readonly Dictionary<string, string> ExchangeLabelDictionary = new Dictionary<string, string> () {
            { Xml.Contacts.Email1Address, "Email" },
            { Xml.Contacts.Email2Address, "Email Two" },
            { Xml.Contacts.Email3Address, "Email Three" },
            { Xml.Contacts.AssistantPhoneNumber, "Assistant" },
            { Xml.Contacts.BusinessPhoneNumber, "Work" },
            { Xml.Contacts.Business2PhoneNumber, "Work Two" },
            { Xml.Contacts.BusinessFaxNumber, "Business Fax" },
            { Xml.Contacts.CarPhoneNumber, "Car" },
            { Xml.Contacts.Home2PhoneNumber, "Home Two" },
            { Xml.Contacts.HomePhoneNumber, "Home" },
            { Xml.Contacts.HomeFaxNumber, "Home Fax" },
            { Xml.Contacts.MobilePhoneNumber, "Mobile" },
            { Xml.Contacts.PagerNumber, "Pager" },
            { Xml.Contacts.RadioPhoneNumber, "Radio" },
            { Xml.Contacts2.CompanyMainPhone, "Company Main" },
            { Xml.Contacts.Anniversary, "Anniversary" },
            { Xml.Contacts.Birthday, "Birthday" },
            { "Home", "Home" },
            { "Business", "Business" },
            { "Other", "Other" },
            { Xml.Contacts2.IMAddress, "Primary IM" },
            { Xml.Contacts2.IMAddress2, "Second IM" },
            { Xml.Contacts2.IMAddress3, "Third IM" },
            { Xml.Contacts.Child, "Child" },
            { Xml.Contacts.Children, "Children" },
            { Xml.Contacts.Spouse, "Spouse" },
            { Xml.Contacts.AssistantName, "Assistant" },
            { Xml.Contacts2.ManagerName, "Manager" },
            { Xml.Contacts.Alias, "Alias" },
            { Xml.Contacts.Department, "Department" },
            { Xml.Contacts.FileAs, "File As" },
            { Xml.Contacts.JobTitle, "Job Title" },
            { Xml.Contacts.OfficeLocation, "Office Location" },
            { Xml.Contacts.Title, "Title" },
            { Xml.Contacts.WebPage, "Web Page" },
            { Xml.Contacts2.AccountName, "Account Name" },
            { Xml.Contacts2.CustomerId, "Customer ID" },
            { Xml.Contacts2.GovernmentId, "Government ID" },
            { Xml.Contacts2.MMS, "MMS" },
            { Xml.Contacts2.NickName, "Nickname" },
            { Xml.Contacts.YomiCompanyName, "Yomi Company Name" },
            { Xml.Contacts.YomiFirstName, "Yomi First Name" },
            { Xml.Contacts.YomiLastName, "Yomi Last Name" },
        };

        private static string GetFirstLetterOrDigit (string src)
        {
            string initial = "";
            if (!String.IsNullOrEmpty (src)) {
                foreach (char c in src) {
                    if (Char.IsLetterOrDigit (c)) {
                        initial += Char.ToUpperInvariant (c);
                        break;
                    }
                }
            }
            return initial;
        }

        public static string GetInitials (McContact contact)
        {
            string initials = "";
            // First try the user's name
            initials += GetFirstLetterOrDigit (contact.FirstName);
            if (!String.IsNullOrEmpty (contact.LastName)) {
                if (Char.IsLetter (contact.LastName [0])) {
                    initials += Char.ToUpperInvariant (contact.LastName [0]);
                } else if (!String.IsNullOrEmpty (contact.MiddleName)) {
                    initials += GetFirstLetterOrDigit (contact.MiddleName);
                }
            }
            // Or, failing that, email address
            if (String.IsNullOrEmpty (initials)) {
                initials = GetFirstLetterOrDigit (contact.GetPrimaryCanonicalEmailAddress ());
            }
            // Or, finally, anything we've got
            if (String.IsNullOrEmpty (initials)) {
                initials = GetFirstLetterOrDigit (contact.GetDisplayName ());
            }
            return initials;
        }

        public static void CopyContact (McContact orig, ref McContact copy)
        {
            copy.ServerId = orig.ServerId;

            copy.CircleColor = orig.CircleColor;
            copy.PortraitId = orig.PortraitId;
            copy.NativeBodyType = orig.NativeBodyType;

            copy.Alias = orig.Alias;
            copy.CompanyName = orig.CompanyName;
            copy.Department = orig.Department;
            copy.FileAs = orig.FileAs;
            copy.FirstName = orig.FirstName;
            copy.JobTitle = orig.JobTitle;
            copy.LastName = orig.LastName;
            copy.MiddleName = orig.MiddleName;
            copy.Suffix = orig.Suffix;
            copy.Title = orig.Title;
            copy.WebPage = orig.WebPage;
            copy.WeightedRank = orig.WeightedRank;
            copy.YomiCompanyName = orig.YomiCompanyName;
            copy.YomiFirstName = orig.YomiFirstName;
            copy.YomiLastName = orig.YomiLastName;
            copy.AccountName = orig.AccountName;
            copy.CustomerId = orig.CustomerId;
            copy.GovernmentId = orig.GovernmentId;
            copy.MMS = orig.MMS;
            copy.NickName = orig.NickName;
            copy.OfficeLocation = orig.OfficeLocation;

            string originalBodyString = "";

            McBody originalContactBody = McBody.QueryById<McBody> (orig.BodyId);
            if (null != originalContactBody) {
                originalBodyString = originalContactBody.GetContentsString ();
            }

            McBody copyContactBody = McBody.QueryById<McBody> (copy.BodyId);
            if (null != copyContactBody) {
                copyContactBody.UpdateData (originalBodyString);
            } else {
                copy.BodyId = McBody.InsertFile (copy.AccountId
                    , McAbstrFileDesc.BodyTypeEnum.PlainText_1,
                    originalBodyString).Id;
            }


            foreach (var p in orig.PhoneNumbers) {
                if (p.IsDefault) {
                    copy.AddDefaultPhoneNumberAttribute (p.AccountId,
                        p.Name,
                        p.Label,
                        p.Value);
                } else {
                    copy.AddOrUpdatePhoneNumberAttribute (p.AccountId,
                        p.Name,
                        p.Label,
                        p.Value);
                }
            }

            foreach (var e in orig.EmailAddresses) {
                if (e.IsDefault) {
                    copy.AddDefaultEmailAddressAttribute (e.AccountId,
                        e.Name,
                        e.Label,
                        e.Value);
                } else {
                    copy.AddOrUpdateEmailAddressAttribute (e.AccountId,
                        e.Name,
                        e.Label,
                        e.Value);
                }
            }

            foreach (var d in orig.Dates) {
                copy.AddDateAttribute (d.AccountId,
                    d.Name,
                    d.Label,
                    d.Value);
            }

            foreach (var im in orig.IMAddresses) {
                copy.AddIMAddressAttribute (orig.AccountId,
                    im.Name,
                    im.Label,
                    im.Value);
            }

            foreach (var r in orig.Relationships) {
                if (r.Name != Xml.Contacts.Child) {
                    copy.AddRelationshipAttribute (r.AccountId,
                        r.Name,
                        r.Label,
                        r.Value);
                } else {
                    copy.AddChildAttribute (r.AccountId,
                        r.Name,
                        r.Label,
                        r.Value);
                }
            }

            foreach (var a in orig.Addresses) {
                copy.AddAddressAttribute (a.AccountId,
                    a.Name,
                    a.Label,
                    a);
            }

            foreach (var cat in orig.Categories) {
                copy.AddCategoryAttribute (cat.AccountId, cat.Name);
            }
        }

        public static void SaveContact (McContact contact, string notes)
        {
            NcModel.Instance.RunInTransaction (() => {
                McBody body = McBody.QueryById<McBody> (contact.BodyId);
                if (body != null) {
                    body.UpdateData (notes);
                } else {
                    contact.BodyId = McBody.InsertFile (contact.AccountId, McAbstrFileDesc.BodyTypeEnum.PlainText_1, notes).Id;
                }
                foreach (var email in contact.EmailAddresses) {
                    email.EmailAddress = McEmailAddress.Get (email.AccountId, email.Value);
                }
                if (contact.Id == 0) {
                    contact.Insert ();
                    var folder = McFolder.GetDefaultContactFolder (contact.AccountId);
                    folder.Link (contact);
                    NachoCore.BackEnd.Instance.CreateContactCmd (contact.AccountId, contact.Id, folder.Id);
                } else {
                    contact.Update ();
                    NachoCore.BackEnd.Instance.UpdateContactCmd (contact.AccountId, contact.Id);
                }
            });
        }

        public static string ExchangeNameToLabel (string name, string defaultLabel = null)
        {
            if (name == null) {
                return defaultLabel;
            }
            string value;
            if (ExchangeLabelDictionary.TryGetValue (name, out value)) {
                return value;
            }
            Log.Error (Log.LOG_CONTACTS, "Exchange label dictionary missing key {0}", name);
            return name;
        }

        public static List<string> GetAvailablePhoneNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            foreach (var p in contact.PhoneNumbers) {
                takenNames.Add (p.Name);
            }

            return PhoneNames.Except (takenNames).ToList ();
        }

        public static List<string> GetAvailableEmailNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            foreach (var p in contact.EmailAddresses) {
                takenNames.Add (p.Name);
            }

            return EmailNames.Except (takenNames).ToList ();
        }

        public static List<string> GetAvailableDateNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            foreach (var d in contact.Dates) {
                takenNames.Add (d.Name);
            }

            return DateNames.Except (takenNames).ToList ();
        }

        public static List<string> GetAvailableAddressNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            foreach (var a in contact.Addresses) {
                takenNames.Add (a.Name);
            }

            return AddressNames.Except (takenNames).ToList ();
        }

        public static List<string> GetAvailableIMAddressNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            foreach (var a in contact.IMAddresses) {
                takenNames.Add (a.Name);
            }
            return IMAddressNames.Except (takenNames).ToList ();
        }

        public static List<string> GetAvailableRelationshipNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            List<string> availableNames = new List<string> (RelationshipNames);
            foreach (var a in contact.Relationships) {
                takenNames.Add (a.Name);
            }

            foreach (var t in takenNames) {
                if (t != Xml.Contacts.Child) {
                    availableNames.Remove (t);
                }
            }
            return availableNames;
        }


        public static List<string> GetTakenMiscNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();

            if (!string.IsNullOrEmpty (contact.Alias)) {
                takenNames.Add (Xml.Contacts.Alias);
            }

            if (!string.IsNullOrEmpty (contact.Department)) {
                takenNames.Add (Xml.Contacts.Department);
            }

            if (!string.IsNullOrEmpty (contact.FileAs)) {
                takenNames.Add (Xml.Contacts.FileAs);
            }

            if (!string.IsNullOrEmpty (contact.JobTitle)) {
                takenNames.Add (Xml.Contacts.JobTitle);
            }

            if (!string.IsNullOrEmpty (contact.OfficeLocation)) {
                takenNames.Add (Xml.Contacts.OfficeLocation);
            }

            if (!string.IsNullOrEmpty (contact.Title)) {
                takenNames.Add (Xml.Contacts.Title);
            }

            if (!string.IsNullOrEmpty (contact.WebPage)) {
                takenNames.Add (Xml.Contacts.WebPage);
            }

            if (!string.IsNullOrEmpty (contact.AccountName)) {
                takenNames.Add (Xml.Contacts2.AccountName);
            }

            if (!string.IsNullOrEmpty (contact.CustomerId)) {
                takenNames.Add (Xml.Contacts2.CustomerId);
            }

            if (!string.IsNullOrEmpty (contact.GovernmentId)) {
                takenNames.Add (Xml.Contacts2.GovernmentId);
            }

            if (!string.IsNullOrEmpty (contact.MMS)) {
                takenNames.Add (Xml.Contacts2.MMS);
            }

            if (!string.IsNullOrEmpty (contact.NickName)) {
                takenNames.Add (Xml.Contacts2.NickName);
            }

            return takenNames;
        }

        public static string MiscContactAttributeNameToValue (string name, McContact contact)
        {
            switch (name) {
            case Xml.Contacts.Alias:
                return contact.Alias;
            case Xml.Contacts.Department:
                return contact.Department;
            case Xml.Contacts.FileAs:
                return contact.FileAs;
            case Xml.Contacts.JobTitle:
                return contact.JobTitle;
            case Xml.Contacts.OfficeLocation:
                return  contact.OfficeLocation;
            case Xml.Contacts.Title:
                return contact.Title;
            case Xml.Contacts.WebPage:
                return contact.WebPage;
            case Xml.Contacts2.AccountName:
                return contact.AccountName;
            case Xml.Contacts2.CustomerId:
                return contact.CustomerId;
            case Xml.Contacts2.GovernmentId:
                return contact.GovernmentId;
            case Xml.Contacts2.MMS:
                return contact.MMS;
            case Xml.Contacts2.NickName:
                return contact.NickName;
            }
            return "";
        }

        public static void AssignMiscContactAttribute (McContact contact, string name, string value)
        {
            switch (name) {
            case Xml.Contacts.Alias:
                contact.Alias = value;
                break;
            case Xml.Contacts.Department:
                contact.Department = value;
                break;
            case Xml.Contacts.FileAs:
                contact.FileAs = value;
                break;
            case Xml.Contacts.JobTitle:
                contact.JobTitle = value;
                break;
            case Xml.Contacts.OfficeLocation:
                contact.OfficeLocation = value;
                break;
            case Xml.Contacts.Title:
                contact.Title = value;
                break;
            case Xml.Contacts.WebPage:
                contact.WebPage = value;
                break;
            case Xml.Contacts2.AccountName:
                contact.AccountName = value;
                break;
            case Xml.Contacts2.CustomerId:
                contact.CustomerId = value;
                break;
            case Xml.Contacts2.GovernmentId:
                contact.GovernmentId = value;
                break;
            case Xml.Contacts2.MMS:
                contact.MMS = value;
                break;
            case Xml.Contacts2.NickName:
                contact.NickName = value;
                break;
            default:
                Log.Warn (Log.LOG_UI, "Setting unknown contact misc. field {0}", name);
                break;
            }
        }

        public static List<string> GetAvailableMiscNames (List<string> takenNames)
        {
            return MiscNames.Except (takenNames).ToList ();
        }

        public static List<string> GetAvailableMiscNames(McContact contact)
        {
            var takenNames = GetTakenMiscNames (contact);
            return GetAvailableMiscNames (takenNames);
        }

        public class PhoneAttributeComparer: IComparer<McContactStringAttribute>
        {
            public int Compare (McContactStringAttribute x, McContactStringAttribute y)
            {
                int xPriority = ContactsHelper.PhoneNames.IndexOf (x.Name);
                int yPriority = ContactsHelper.PhoneNames.IndexOf (y.Name);

                return xPriority.CompareTo (yPriority);
            }
        }

        public static string NameToLetters (string name)
        {
            if (null == name) {
                return "";
            }
            var Initials = "";
            string[] names = name.Split (new char [] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (1 == names.Length) {
                Initials = (names [0].Substring (0, 1)).ToCapitalized ();
            }
            if (2 == names.Length) {
                if (0 < name.IndexOf (',')) {
                    // Last name, First name
                    Initials = (names [1].Substring (0, 1)).ToCapitalized () + (names [0].Substring (0, 1)).ToCapitalized ();
                } else {
                    // First name, Last name
                    Initials = (names [0].Substring (0, 1)).ToCapitalized () + (names [1].Substring (0, 1)).ToCapitalized ();
                }
            }
            if (2 < names.Length) {
                if (0 < name.IndexOf (',')) {
                    // Last name, First name
                    Initials = (names [1].Substring (0, 1)).ToCapitalized () + (names [0].Substring (0, 1)).ToCapitalized ();
                } else if (-1 == name.IndexOf (',')) {
                    if ((names [1].Substring (0, 1)).ToLower () != (names [1].Substring (0, 1))) {
                        Initials = (names [0].Substring (0, 1)).ToCapitalized () + (names [1].Substring (0, 1)).ToCapitalized ();
                    } else {
                        Initials = (names [0].Substring (0, 1)).ToCapitalized ();
                    }
                }
            }

            return Initials;
        }

        public static void SaveNoteText (McContact contact, string noteText)
        {
            NcAssert.True (null != contact);
            McBody contactBody = McBody.QueryById<McBody> (contact.BodyId);
            if (null != contactBody) {
                contactBody.UpdateData (noteText);
            } else {
                contact.BodyId = McBody.InsertFile (contact.AccountId, McAbstrFileDesc.BodyTypeEnum.PlainText_1, noteText).Id;
            }
            contact.Update ();
            NachoCore.BackEnd.Instance.UpdateContactCmd (contact.AccountId, contact.Id);
        }

        public static string GetNoteText (McContact contact)
        {
            NcAssert.True (null != contact);
            McBody contactBody = McBody.QueryById<McBody> (contact.BodyId);
            if (null != contactBody) {
                return contactBody.GetContentsString ();
            } else {
                return "";
            }
        }
    }

    public class ContactOtherAttribute : McAbstrContactAttribute
    {
        McContact Contact;

        public ContactOtherAttribute (McContact contact, string name) : base()
        {
            Contact = contact;
            Name = name;
            Label = ContactsHelper.ExchangeNameToLabel (Name);
        }
        
        public string Value {
            get {
                return ContactsHelper.MiscContactAttributeNameToValue (Name, Contact);
            }
            set {
                ContactsHelper.AssignMiscContactAttribute (Contact, Name, value);
            }
        }
        
        public override void ChangeName (string name)
        {
            // Since Value really comes from a Contact property, we need to move the value
            // from the old property to the new property when changing our name
            var val = Value;
            Value = null;
            base.ChangeName (name);
            Value = val;
        }
    }

    #region Platform Independent UI Models

    public class ContactGroup
    {
        public string Name;
        public List<NcContactIndex> Contacts;
        private ContactCache Cache;

        public ContactGroup (string name, ContactCache cache)
        {
            Name = name;
            Contacts = new List<NcContactIndex> ();
            Cache = cache;
        }

        public static List<ContactGroup> CreateGroups (List<NcContactIndex> contacts, ContactCache cache)
        {
            var previousFirstLetter = "";
            var firstLetter = "";
            var contactGroups = new List<ContactGroup> ();
            ContactGroup group = null;
            foreach (var contact in contacts) {
                firstLetter = contact.FirstLetter;
                if (String.IsNullOrEmpty (firstLetter)) {
                    // If we get here, then something is wrong in the DB because a contact should always have
                    // a first letter
                    firstLetter = "#";
                }
                if (firstLetter != previousFirstLetter) {
                    group = new ContactGroup (firstLetter, cache);
                    contactGroups.Add (group);
                    previousFirstLetter = firstLetter;
                }
                group.Contacts.Add (contact);
            }
            // If we have two or more groups, make sure the # group, if present, is at the end.
            // It will be sorted out of the DB first.
            if (contactGroups.Count > 1 && contactGroups [0].Name == "#") {
                var numberGroup = contactGroups [0];
                contactGroups.RemoveAt (0);
                contactGroups.Add (numberGroup);
            }
            return contactGroups;
        }

        public McContact GetCachedContact (int index)
        {
            var contactIndex = Contacts [index];
            return Cache.GetContact (contactIndex.Id);
        }
    }

    public class ContactCache
    {

        Dictionary<int, McContact> ContactsById;
        Queue<int> ContactIds;
        int MaxContactCount = 100;

        public ContactCache ()
        {
            ContactsById = new Dictionary<int, McContact> ();
            ContactIds = new Queue<int> ();
        }

        public McContact GetContact (int contactId)
        {
            McContact contact;
            if (!ContactsById.TryGetValue (contactId, out contact)) {
                contact = McContact.QueryById<McContact> (contactId);
                ContactsById.Add (contactId, contact);
                ContactIds.Enqueue (contactId);
                if (ContactIds.Count > MaxContactCount) {
                    var contactIdToRemove = ContactIds.Dequeue ();
                    ContactsById.Remove (contactIdToRemove);
                }
            }
            return contact;
        }
    }

    public class ContactField
    {
        public string Name { get; private set; }
        public string DisplayValue { get; private set; }
        public string Email { get; private set; }
        public string Phone { get; private set; }

        public ContactField (string name, string displayValue, string email = null, string phone = null)
        {
            Name = name;
            DisplayValue = displayValue;
            Email = email;
            Phone = phone;
        }

        public static List<ContactField> FieldsFromContact (McContact contact)
        {
            var fields = new List<ContactField> ();

            // Emails fields come first, and the default is always at the top
            var emails = contact.EmailAddresses;
            emails.Sort ((x, y) => {
                if (x.IsDefault && !y.IsDefault) {
                    return -1;
                }
                if (!x.IsDefault && y.IsDefault) {
                    return 1;
                }
                return 0;
            });
            foreach (var email in emails) {
                var mcemail = McEmailAddress.QueryById<McEmailAddress> (email.EmailAddress);
                fields.Add (new ContactField (email.GetDisplayLabel (fallback: "Email"), mcemail.CanonicalEmailAddress, email: mcemail.CanonicalEmailAddress));
            }

            // Phone numbers come next, and the default is always at the top
            var phones = contact.PhoneNumbers;
            phones.Sort ((x, y) => {
                if (x.IsDefault && !y.IsDefault) {
                    return -1;
                }
                if (!x.IsDefault && y.IsDefault) {
                    return 1;
                }
                return 0;
            });
            foreach (var phone in phones) {
                fields.Add (new ContactField (phone.GetDisplayLabel (fallback: "Phone"), phone.Value, phone: "tel:" + phone.Value));
            }

            // Addresses come next
            var addresses = contact.Addresses;
            foreach (var address in addresses) {
                fields.Add (new ContactField (address.GetDisplayLabel (fallback: "Address"), address.FormattedAddress));
            }

            // IMs come next
            var ims = contact.IMAddresses;
            foreach (var im in ims) {
                fields.Add (new ContactField (im.GetDisplayLabel (fallback: "IM"), im.Value));
            }

            // Dates next
            var dates = contact.Dates;
            foreach (var date in dates) {
                if (date.Value != DateTime.MinValue) {
                    fields.Add (new ContactField (date.GetDisplayLabel (), Pretty.BirthdayOrAnniversary (date.Value)));
                }
            }

            // Relationships next
            var relationships = contact.Relationships;
            foreach (var relationship in relationships) {
                fields.Add (new ContactField (relationship.GetDisplayLabel (), relationship.Value));
            }

            // Any other fields next
            var others = contact.Others;
            foreach (var other in others) {
                fields.Add (new ContactField (other.GetDisplayLabel (), other.Value));
            }

            // Finally notes
            if (contact.BodyId != 0) {
                var body = McBody.QueryById<McBody> (contact.BodyId);
                if (body != null) {
                    var notes = body.GetContentsString ();
                    if (!String.IsNullOrWhiteSpace (notes)) {
                        fields.Add (new ContactField ("Notes", notes));
                    }
                }
            }

            return fields;
        }

    }

    #endregion
}





