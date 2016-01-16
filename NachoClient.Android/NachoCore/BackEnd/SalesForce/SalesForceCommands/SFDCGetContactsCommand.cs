﻿//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
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
            var request = NewRequest (HttpMethod.Get, string.Format ("{0}{1}", protoControl.ObjectUrls ["Contact"], ContactId), jsonContentType);
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
            var query = "SELECT Id FROM Contact WHERE IsDeleted=false";

            if (Resync && BEContext.ProtocolState.SFDCLastContactsSynced > DateTime.MinValue) {
                query += string.Format (" AND LastModifiedDate > {0}", BEContext.ProtocolState.SFDCLastContactsSynced.ToAsUtcString ());
            }
            query = Regex.Replace (query, " ", "+");
            var request = NewRequest (HttpMethod.Get, string.Format ("{0}?q={1}", protoControl.ResourcePaths ["query"], query), jsonContentType);
            GetRequest (request);
        }

        public class ContactRecord
        {
            public SFDCGetContactsCommand.RecordAttributes attributes { get; set; }
            public string Id { get; set; }

            public override string ToString ()
            {
                return string.Format ("[attributes={0}, Id={1}]", attributes, Id);
            }
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
                SFDCGetContactsDataCommand cmd = contactRecords.Any () ? new SFDCGetContactsDataCommand (BEContext, contactRecords.Select (x => x.Id).ToList ()) : null;
                return Event.Create ((uint)SmEvt.E.Success, "SFDCCONTSUMSUCC", cmd);
            } catch (JsonSerializationException) {
                return ProcessErrorResponse (jsonResponse);
            } catch (JsonReaderException) {
                return ProcessErrorResponse (jsonResponse);
            }
        }
    }

    public class SFDCGetContactsDataCommand : SFDCCommand
    {
        List<string> IdList;

        public SFDCGetContactsDataCommand (IBEContext beContext, List<string> idList) : base (beContext)
        {
            IdList = idList;
        }

        public override void Execute (NcStateMachine sm)
        {
            if (IdList.Any ()) {
                base.Execute (sm);
            } else {
                BEContext.ProtocolState.UpdateWithOCApply<McProtocolState> (((record) => {
                    var target = (McProtocolState)record;
                    target.SFDCLastContactsSynced = DateTime.UtcNow;
                    return true;
                }));
                sm.PostEvent (Event.Create ((uint)SalesForceProtoControl.SfdcEvt.E.SyncDone, "SFDCCONTSUMDONE"));
            }
        }

        protected override void MakeAndSendRequest ()
        {
            var protoControl = BEContext.ProtoControl as SalesForceProtoControl;
            NcAssert.NotNull (protoControl);

            string id = IdList.First ();
            IdList.Remove (id);
            Log.Info (Log.LOG_SFDC, "Fetching data for Contact Id {0}", id);
            var request = NewRequest (HttpMethod.Get, string.Format ("{0}/{1}", protoControl.ObjectUrls ["Contact"], id), jsonContentType);
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

                Execute (OwnerSm);

            } catch (JsonSerializationException) {
                return ProcessErrorResponse (jsonResponse);
            } catch (JsonReaderException) {
                return ProcessErrorResponse (jsonResponse);
            }
            return Event.Create ((uint)SmEvt.E.Success, "SFDCCONTACSUCC1");
        }
    }
}

