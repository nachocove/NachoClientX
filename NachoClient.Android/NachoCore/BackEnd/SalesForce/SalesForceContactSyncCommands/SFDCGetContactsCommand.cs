//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using System.Text;
using System.Threading;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using NachoCore.Model;
using NachoCore.ActiveSync;

namespace NachoCore
{
    public class SFDCGetContactsCommand : SFDCCommand
    {
        string ContactId;

        public SFDCGetContactsCommand (IBEContext beContext, string contactId) : base (beContext)
        {
            NcAssert.True (!string.IsNullOrEmpty (contactId));
            ContactId = contactId;
        }

        protected override void MakeAndSendRequest ()
        {
            var protoControl = BEContext.ProtoControl as SalesForceProtoControl;
            NcAssert.NotNull (protoControl);
            var request = NewRequest (HttpMethod.Get, string.Format ("{0}{1}", protoControl.SFDCSetup.ObjectUrls ["Contact"], ContactId), jsonContentType);
            GetRequest (request);
        }

        protected override Event ProcessSuccessResponse (NcHttpResponse response, CancellationToken token)
        {
            byte[] contentBytes = response.GetContent ();
            string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
            if (string.IsNullOrEmpty (jsonResponse)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCCONTFAIL");
            }
            return Event.Create ((uint)SmEvt.E.Success, "SFDCCONTACSUCC");
        }

        public class RecordAttributes
        {
            public string type { get; set; }

            public string url { get; set; }

            public override string ToString ()
            {
                return string.Format ("[type={0}, url={1}]", type, url);
            }
        }
    }

    public class SFDCGetContactIdsCommand : SFDCCommand
    {
        bool Resync;

        public SFDCGetContactIdsCommand (IBEContext beContext, bool resync = false) : base (beContext)
        {
            Resync = resync;
        }

        protected override void MakeAndSendRequest ()
        {
            var protoControl = BEContext.ProtoControl as SalesForceProtoControl;
            NcAssert.NotNull (protoControl);
            //BEContext.ProtocolState.SFDCLastContactsSynced
            var request = NewRequest (HttpMethod.Get, string.Format ("{0}?q={1}", protoControl.SFDCSetup.ResourcePaths ["query"], ContactRecord.GetQuery (Resync, DateTime.MinValue)), jsonContentType);
            GetRequest (request);
        }

        public class ContactRecord
        {
            public SFDCGetContactsCommand.RecordAttributes attributes { get; set; }

            public string Id { get; set; }

            public DateTime LastModifiedDate { get; set; }

            public override string ToString ()
            {
                return string.Format ("[attributes={0}, Id={1}, LastModifiedDate={2}]", attributes, Id, LastModifiedDate);
            }

            public static string GetQuery (bool Resync, DateTime LastSynced)
            {
                var query = "SELECT Id,LastModifiedDate FROM Contact WHERE IsDeleted=false";

                if (Resync && LastSynced > DateTime.MinValue) {
                    query += string.Format (" AND LastModifiedDate > {0}", LastSynced.ToAsUtcString ());
                }
                return Regex.Replace (query, " ", "+");
            }
        }

        public class SFDCMcContactRecord
        {
            public int Id { get; set; }
            public DateTime LastModified { get; set; }
            public string ServerId { get; set; }
        }

        public static List<SFDCMcContactRecord> GetContactsByServerIds (List<string> ids)
        {
            if (0 == ids.Count) {
                return new List<SFDCMcContactRecord> ();
            }
            var query = string.Format ("SELECT Id,ServerId,LastModified FROM McContact WHERE Source=? AND ServerId IN ('{0}')", string.Join ("','", ids));
            return NcModel.Instance.Db.Query<SFDCMcContactRecord> (query, McAbstrItem.ItemSource.SalesForce);
        }

        protected override Event ProcessSuccessResponse (NcHttpResponse response, CancellationToken token)
        {
            byte[] contentBytes = response.GetContent ();
            string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
            if (string.IsNullOrEmpty (jsonResponse)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCCONTSUMFAIL1");
            }
            try {
                var responseData = Newtonsoft.Json.Linq.JObject.Parse (jsonResponse);
                var jsonRecords = responseData.SelectToken ("records");
                var contactRecords = jsonRecords.ToObject<List<ContactRecord>> ();
                var localContacts = GetContactsByServerIds(contactRecords.Select (x => x.Id).ToList ());
                var needContacts = new List<string> ();
                foreach (var contact in contactRecords) {
                    var local = localContacts.FirstOrDefault (x => x.ServerId == contact.Id);
                    if (null == local || local.LastModified < contact.LastModifiedDate) {
                        needContacts.Add (contact.Id);
                    }
                }
                return Event.Create ((uint)SmEvt.E.Success, "SFDCCONTSUMSUCC", needContacts);
            } catch (JsonSerializationException) {
                return ProcessErrorResponse (jsonResponse);
            } catch (JsonReaderException) {
                return ProcessErrorResponse (jsonResponse);
            }
        }
    }

    public class SFDCGetContactsDataCommand : SFDCCommand
    {
        string ContactId;

        public SFDCGetContactsDataCommand (IBEContext beContext, string contactId) : base (beContext)
        {
            ContactId = contactId;
        }

        protected override void MakeAndSendRequest ()
        {
            var protoControl = BEContext.ProtoControl as SalesForceProtoControl;
            NcAssert.NotNull (protoControl);
            NcAssert.NotNull (protoControl.SFDCSetup);

            Log.Info (Log.LOG_SFDC, "Fetching data for Contact Id {0}", ContactId);
            var request = NewRequest (HttpMethod.Get, string.Format ("{0}/{1}", protoControl.SFDCSetup.ObjectUrls ["Contact"], ContactId), jsonContentType);
            GetRequest (request);
        }

        public class AddressInfo
        {
            public string city { get; set; }

            public string country { get; set; }

            public string countryCode { get; set; }

            public string geocodeAccuracy { get; set; }

            public string MailingCountry { get; set; }

            public string latitude { get; set; }

            public string longitude { get; set; }

            public string postalCode { get; set; }

            public string state { get; set; }

            public string stateCode { get; set; }

            public string street { get; set; }
        }

        public class ContactRecord
        {
            public SFDCGetContactsCommand.RecordAttributes attributes { get; set; }

            public string Id { get; set; }

            public string LastModifiedDate { get; set; }

            public string Salutation { get; set; }

            public string Title { get; set; }

            public string Department { get; set; }

            public string Name { get; set; }

            public string FirstName { get; set; }

            public string MiddleName { get; set; }

            public string LastName { get; set; }

            public string Suffix { get; set; }

            public string Email { get; set; }

            public string Phone { get; set; }

            public string Fax { get; set; }

            public string HomePhone { get; set; }

            public string MobilePhone { get; set; }

            public string OtherPhone { get; set; }

            public string PhotoUrl { get; set; }

            public string MailingStreet { get; set; }

            public string MailingCity { get; set; }

            public string MailingState { get; set; }

            public string MailingPostalCode { get; set; }

            public string MailingCountry { get; set; }

            public string MailingLatitude { get; set; }

            public string MailingLongitude { get; set; }

            public AddressInfo MailingAddress { get; set; }

            public string MailingGeocodeAccuracy { get; set; }

            public string OtherStreet { get; set; }

            public string OtherCity { get; set; }

            public string OtherState { get; set; }

            public string OtherPostalCode { get; set; }

            public string OtherCountry { get; set; }

            public string OtherLatitude { get; set; }

            public string OtherLongitude { get; set; }

            public string OtherGeocodeAccuracy { get; set; }

            public AddressInfo OtherAddress { get; set; }
        }

        protected override Event ProcessSuccessResponse (NcHttpResponse response, CancellationToken token)
        {
            byte[] contentBytes = response.GetContent ();
            string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
            if (string.IsNullOrEmpty (jsonResponse)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCCONTFAIL1");
            }
            try {
                var contactInfo = JsonConvert.DeserializeObject<ContactRecord> (jsonResponse);
                var contact = McContact.QueryByServerId<McContact> (AccountId, contactInfo.Id);
                if (contact == null) {
                    contact = new McContact () {
                        AccountId = AccountId,
                        ServerId = contactInfo.Id,
                        Source = McAbstrItem.ItemSource.SalesForce,
                    };
                    contact.Insert ();
                }
                NcAssert.True (contact.Source == McAbstrItem.ItemSource.SalesForce);
                contact = contact.UpdateWithOCApply<McContact> (((record) => {
                    var target = (McContact)record;
                    target.DisplayName = contactInfo.Name;
                    target.LastModified = DateTime.Parse (contactInfo.LastModifiedDate);
                    target.FirstName = contactInfo.FirstName;
                    target.LastName = contactInfo.LastName;
                    target.MiddleName = contactInfo.MiddleName;
                    target.JobTitle = contactInfo.Title;
                    target.Suffix = contactInfo.Suffix;
                    target.Department = contactInfo.Department;
                    target.AddEmailAddressAttribute (AccountId, "EmailAddress", "Email", contactInfo.Email);
                    target.AddPhoneNumberAttribute (AccountId, Xml.Contacts.HomePhoneNumber, "Home", contactInfo.HomePhone);
                    target.AddPhoneNumberAttribute (AccountId, Xml.Contacts.BusinessFaxNumber, "work fax", contactInfo.Fax);
                    target.AddPhoneNumberAttribute (AccountId, Xml.Contacts.MobilePhoneNumber, "mobile", contactInfo.MobilePhone);
                    target.AddPhoneNumberAttribute (AccountId, Xml.Contacts.BusinessPhoneNumber, "work", contactInfo.OtherPhone);

                    var attr = new McContactAddressAttribute ();
                    attr.State = contactInfo.MailingState;
                    attr.Street = contactInfo.MailingStreet;
                    attr.City = contactInfo.MailingCity;
                    attr.State = contactInfo.MailingState;
                    attr.PostalCode = contactInfo.MailingPostalCode;
                    attr.Country = contactInfo.MailingCountry;
                    target.AddAddressAttribute (AccountId, "Home", "home", attr);

                    attr = new McContactAddressAttribute ();
                    attr.State = contactInfo.OtherState;
                    attr.Street = contactInfo.OtherStreet;
                    attr.City = contactInfo.OtherCity;
                    attr.State = contactInfo.MailingState;
                    attr.PostalCode = contactInfo.OtherPostalCode;
                    attr.Country = contactInfo.OtherCountry;
                    target.AddAddressAttribute (AccountId, "Work", "work", attr);
                    return true;
                }));

                contact.SetVIP (true);
            } catch (JsonSerializationException) {
                return ProcessErrorResponse (jsonResponse);
            } catch (JsonReaderException) {
                return ProcessErrorResponse (jsonResponse);
            }
            return Event.Create ((uint)SmEvt.E.Success, "SFDCCONTACSUCC1");
        }
    }
}

