
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

        public List<string> PhoneNames = new List<string> () {
            Xml.Contacts.MobilePhoneNumber,
            Xml.Contacts.BusinessPhoneNumber,
            Xml.Contacts.Business2PhoneNumber,
            Xml.Contacts.HomePhoneNumber,
            Xml.Contacts.Home2PhoneNumber,
            Xml.Contacts.AssistantPhoneNumber,
            Xml.Contacts.CarPhoneNumber,
            Xml.Contacts.PagerNumber,
            Xml.Contacts.RadioPhoneNumber,
            Xml.Contacts.BusinessFaxNumber,
            Xml.Contacts.HomeFaxNumber
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

        public List<string> MiscNames = new List<string>(){
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
            Xml.Contacts2.NickName
        };

        public List<string> RelationshipNames = new List<string> () {
            Xml.Contacts.Spouse,
            Xml.Contacts.AssistantName,
            Xml.Contacts2.ManagerName,
            Xml.Contacts.Child,
        };

        public Dictionary<string, string> EmailLabelDictionary = new Dictionary<string, string> () {
            {Xml.Contacts.Email1Address, "Email"},
            {Xml.Contacts.Email2Address, "Email Two"},
            {Xml.Contacts.Email3Address, "Email Three"}
        };

        public Dictionary<string, string> ExchangeLabelDictionary = new Dictionary<string, string> () {
            {Xml.Contacts.Email1Address, "Email"},
            {Xml.Contacts.Email2Address, "Email Two"},
            {Xml.Contacts.Email3Address, "Email Three"},
            {Xml.Contacts.AssistantPhoneNumber, "Assistant"},
            {Xml.Contacts.BusinessPhoneNumber, "Work"},
            {Xml.Contacts.Business2PhoneNumber, "Work Two"},
            {Xml.Contacts.BusinessFaxNumber, "Business Fax"},
            {Xml.Contacts.CarPhoneNumber, "Car"},
            {Xml.Contacts.Home2PhoneNumber, "Home"},
            {Xml.Contacts.HomePhoneNumber, "Home Two"},
            {Xml.Contacts.HomeFaxNumber, "Home Fax"},
            {Xml.Contacts.MobilePhoneNumber, "Mobile"},
            {Xml.Contacts.PagerNumber, "Pager"},
            {Xml.Contacts.RadioPhoneNumber, "Radio"},
            {Xml.Contacts.Anniversary, "Anniversary"},
            {Xml.Contacts.Birthday, "Birthday"},
            {"Home", "Home"},
            {"Business", "Business"},
            {"Other", "Other"},
            {Xml.Contacts2.IMAddress, "Primary IM"},
            {Xml.Contacts2.IMAddress2, "Secondary IM"},
            {Xml.Contacts2.IMAddress3, "Tertiary IM"},
            {Xml.Contacts.Child, "Child"},
            {Xml.Contacts.Spouse, "Spouse"},
            {Xml.Contacts.AssistantName, "Assistant"},
            {Xml.Contacts2.ManagerName, "Manager"},
            {Xml.Contacts.Alias, "Alias"},
            {Xml.Contacts.Department, "Department"},
            {Xml.Contacts.FileAs, "File As"},
            {Xml.Contacts.JobTitle, "Job Title"},
            {Xml.Contacts.OfficeLocation, "Office Location"},
            {Xml.Contacts.Title, "Title"},
            {Xml.Contacts.WebPage, "Web Page"},
            {Xml.Contacts2.AccountName, "Account Name"},
            {Xml.Contacts2.CustomerId, "Customer ID"},
            {Xml.Contacts2.GovernmentId, "Government ID"},
            {Xml.Contacts2.MMS, "MMS"},
            {Xml.Contacts2.NickName, "Nickname"}
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
                if (0 != contact.EmailAddresses.Count) {
                    var emailAddressAttribute = contact.EmailAddresses [0];
                    var emailAddress = McEmailAddress.QueryById<McEmailAddress> (emailAddressAttribute.EmailAddress);
                    foreach (char c in emailAddress.CanonicalEmailAddress) {
                        if (Char.IsLetterOrDigit (c)) {
                            initials += Char.ToUpper (c);
                            break;
                        }
                    }
                }
            }
            return initials;
        }

        public static void CopyContact (McContact c, ref McContact n)
        {
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

            foreach (var p in n.PhoneNumbers) {
                p.Delete ();
            }
            n.PhoneNumbers.Clear ();

            foreach (var p in c.PhoneNumbers) {
                if (p.IsDefault) {
                    n.AddDefaultPhoneNumberAttribute (c.AccountId,
                        p.Name,
                        p.Label,
                        p.Value);
                } else {
                    n.AddOrUpdatePhoneNumberAttribute (c.AccountId,
                        p.Name,
                        p.Label,
                        p.Value);
                }
            }

            foreach (var p in n.EmailAddresses) {
                p.Delete ();
            }
            n.EmailAddresses.Clear ();

            foreach (var e in c.EmailAddresses) {
                if (e.IsDefault) {
                    n.AddDefaultEmailAddressAttribute (c.AccountId,
                        e.Name,
                        e.Label,
                        e.Value);
                } else {
                    n.AddOrUpdateEmailAddressAttribute (c.AccountId,
                        e.Name,
                        e.Label,
                        e.Value);
                }
            }

            foreach (var p in n.Dates) {
                p.Delete ();
            }
            n.Dates.Clear ();

            foreach (var d in c.Dates) {
                n.AddDateAttribute (c.AccountId,
                    d.Name,
                    d.Label,
                    d.Value);
            }

            foreach (var p in n.IMAddresses) {
                p.Delete ();
            }
            n.IMAddresses.Clear ();

            foreach (var im in c.IMAddresses) {
                n.AddIMAddressAttribute (c.AccountId,
                    im.Name,
                    im.Label,
                    im.Value);
            }

            foreach (var p in n.Relationships) {
                p.Delete ();
            }
            n.Relationships.Clear ();

            foreach (var r in c.Relationships) {
                n.AddRelationshipAttribute (c.AccountId,
                    r.Name,
                    r.Label,
                    r.Value);
            }

            foreach (var p in n.Addresses) {
                p.Delete ();
            }
            n.Addresses.Clear ();

            foreach (var a in c.Addresses) {
                n.AddAddressAttribute (c.AccountId,
                    a.Name,
                    a.Label,
                    a);
            }
        }

        public string ExchangeNameToLabel (string name)
        {
            return ExchangeLabelDictionary [name];
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

            return EmailNames.Except (takenNames).ToList();
        }

        public List<string> GetTakenMiscNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();

            if(!string.IsNullOrEmpty(contact.Alias)){
                takenNames.Add (Xml.Contacts.Alias);
            }

            if(!string.IsNullOrEmpty(contact.Department)){
                takenNames.Add (Xml.Contacts.Department);
            }

            if(!string.IsNullOrEmpty(contact.FileAs)){
                takenNames.Add (Xml.Contacts.FileAs);
            }

            if(!string.IsNullOrEmpty(contact.JobTitle)){
                takenNames.Add (Xml.Contacts.JobTitle);
            }

            if(!string.IsNullOrEmpty(contact.OfficeLocation)){
                takenNames.Add (Xml.Contacts.OfficeLocation);
            }

            if(!string.IsNullOrEmpty(contact.Title)){
                takenNames.Add (Xml.Contacts.Title);
            }

            if(!string.IsNullOrEmpty(contact.WebPage)){
                takenNames.Add (Xml.Contacts.WebPage);
            }

            if(!string.IsNullOrEmpty(contact.AccountName)){
                takenNames.Add (Xml.Contacts2.AccountName);
            }

            if(!string.IsNullOrEmpty(contact.CustomerId)){
                takenNames.Add (Xml.Contacts2.CustomerId);
            }

            if(!string.IsNullOrEmpty(contact.GovernmentId)){
                takenNames.Add (Xml.Contacts2.GovernmentId);
            }

            if(!string.IsNullOrEmpty(contact.MMS)){
                takenNames.Add (Xml.Contacts2.MMS);
            }

            if(!string.IsNullOrEmpty(contact.NickName)){
                takenNames.Add (Xml.Contacts2.NickName);
            }

            return takenNames;
        }

        public List<string> GetAvailableMiscNames (List<string> takenNames)
        {
            //List<string> takenNames = new List<string> (GetTakenMiscNames(contact));
            List<string> availableNames = new List<string>(MiscNames);

            foreach (var t in takenNames) {
                availableNames.Remove (t);
            }

            return availableNames;
        }

        public List<string> GetAvailableDateNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            List<string> availableNames = new List<string>(DateNames);
            foreach (var d in contact.Dates) {
                takenNames.Add (d.Name);
            }

            foreach (var t in takenNames) {
                availableNames.Remove (t);
            }

            return availableNames;
        }

        public List<string> GetAvailableAddressNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            List<string> availableNames = new List<string>(AddressNames);
            foreach (var a in contact.Addresses) {
                takenNames.Add (a.Name);
            }

            foreach (var t in takenNames) {
                availableNames.Remove (t);
            }

            return availableNames;
        }

        public List<string> GetAvailableIMAddressNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            List<string> availableNames = new List<string>(IMAddressNames);
            foreach (var a in contact.IMAddresses) {
                takenNames.Add (a.Name);
            }

            foreach (var t in takenNames) {
                availableNames.Remove (t);
            }

            return availableNames;
        }

        public List<string> GetAvailableRelationshipNames (McContact contact)
        {
            List<string> takenNames = new List<string> ();
            List<string> availableNames = new List<string>(RelationshipNames);
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


    }
}





