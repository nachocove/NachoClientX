
using System;
using System.Collections;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.ActiveSync;
using System.Linq;

namespace NachoCore.Utils
{
    public class ContactsHelper
    {
        public ContactsHelper ()
        {
        }

        //The order of these names also determines their priority when sorting.
        public List<string> PhoneNames = new List<string> () {
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

        public List<string> EmailNames = new List<string> () {
            Xml.Contacts.Email1Address,
            Xml.Contacts.Email2Address,
            Xml.Contacts.Email3Address
        };

        public List<string> DateNames = new List<string> () {
            Xml.Contacts.Birthday,
            Xml.Contacts.Anniversary
        };

        public List<string> IMAddressNames = new List<string> () {
            Xml.Contacts2.IMAddress,
            Xml.Contacts2.IMAddress2,
            Xml.Contacts2.IMAddress3
        };

        public List<string> AddressNames = new List<string> () {
            "Home",
            "Business",
            "Other"
        };

        public List<string> MiscNames = new List<string> () {
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

        public List<string> RelationshipNames = new List<string> () {
            Xml.Contacts.Spouse,
            Xml.Contacts.AssistantName,
            Xml.Contacts2.ManagerName,
            Xml.Contacts.Child,
        };

        public Dictionary<string, string> ExchangeLabelDictionary = new Dictionary<string, string> () {
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

        public static string GetInitials (McContact contact)
        {
            string initials = "";
            if (!String.IsNullOrEmpty (contact.FirstName)) {
                initials += Char.ToUpper (contact.FirstName [0]);
            }
            if (!String.IsNullOrEmpty (contact.LastName)) {
                initials += Char.ToUpper (contact.LastName [0]);
            }
            // Or, failing that, the first char
            if (String.IsNullOrEmpty (initials)) {
                if (!string.IsNullOrEmpty (contact.GetPrimaryCanonicalEmailAddress ())) {
                    foreach (char c in contact.GetPrimaryCanonicalEmailAddress()) {
                        if (Char.IsLetterOrDigit (c)) {
                            initials += Char.ToUpper (c);
                            break;
                        }
                    }
                }
            }
            if (String.IsNullOrEmpty (initials)) {
                var displayName = contact.GetDisplayName ();
                if (!String.IsNullOrEmpty (displayName)) {
                    initials += Char.ToUpper (displayName [0]);
                }
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

        public string ExchangeNameToLabel (string name)
        {
            string value;
            if (ExchangeLabelDictionary.TryGetValue (name, out value)) {
                return value;
            }
            Log.Error (Log.LOG_CONTACTS, "Exchange label dictionary missing key {0}", name);
            return name;
        }

        public List<string> GetAvailablePhoneNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            foreach (var p in contact.PhoneNumbers) {
                takenNames.Add (p.Name);
            }

            return PhoneNames.Except (takenNames).ToList ();
        }

        public List<string> GetAvailableEmailNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            foreach (var p in contact.EmailAddresses) {
                takenNames.Add (p.Name);
            }

            return EmailNames.Except (takenNames).ToList ();
        }

        public List<string> GetAvailableDateNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            foreach (var d in contact.Dates) {
                takenNames.Add (d.Name);
            }

            return DateNames.Except (takenNames).ToList ();
        }

        public List<string> GetAvailableAddressNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            foreach (var a in contact.Addresses) {
                takenNames.Add (a.Name);
            }

            return AddressNames.Except (takenNames).ToList ();
        }

        public List<string> GetAvailableIMAddressNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            foreach (var a in contact.IMAddresses) {
                takenNames.Add (a.Name);
            }
            return IMAddressNames.Except (takenNames).ToList ();
        }

        public List<string> GetAvailableRelationshipNames (McContact contact)
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


        public List<string> GetTakenMiscNames (McContact contact)
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

        public string MiscContactAttributeNameToValue (string name, McContact contact)
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

        public List<string> GetAvailableMiscNames (List<string> takenNames)
        {
            return MiscNames.Except (takenNames).ToList ();
        }
    }
}





