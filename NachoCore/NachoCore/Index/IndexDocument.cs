//  Copyright (C) 2014-2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using MimeKit;
using Lucene.Net.Documents;
using NachoCore.Utils;

namespace NachoCore.Index
{
    public class NcIndexDocument
    {
        protected Document _Doc { set; get; }

        public Document Doc {
            protected set {
                if (null == _Doc) {
                    _Doc = new Document ();
                }
            }
            get {
                return _Doc;
            }
        }

        public long BytesIndexed { protected set; get; }

        public NcIndexDocument (string type, string id, EmailMessageIndexParameters parameters)
        {
            BytesIndexed = 0;
            Doc = new Document ();
            AddExactMatchOnlyField ("type", type);
            AddExactMatchOnlyField ("id", id);
            if (null != parameters) {
                AddIndexedField ("body", parameters.Content);
            }
        }

        protected Field GetExactMatchOnlyField (string field, string value)
        {
            return new Field (field, value, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS, Field.TermVector.NO);
        }

        protected Field GetIndexedField (string field, string value)
        {
            return new Field (field, value, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.NO);
        }

        protected void AddAddressList (string field, InternetAddressList addressList)
        {
            var domain_field = field + "_domain";
            foreach (var address in addressList) {
                var addressString = address.ToString ();
                AddIndexedField (field, addressString);
                var mbAddr = address as MailboxAddress;
                if (null != mbAddr) {
                    var idx = mbAddr.Address.IndexOf ("@");
                    var domain = mbAddr.Address.Substring (idx + 1);
                    AddIndexedField (domain_field, domain);
                }
            }
        }

        protected void AddExactMatchOnlyField (string field, string value)
        {
            if (String.IsNullOrEmpty (value)) {
                return;
            }
            Doc.Add (GetExactMatchOnlyField (field, value));
            BytesIndexed += value.Length;
        }

        protected void AddIndexedField (string field, string value)
        {
            if (String.IsNullOrEmpty (value)) {
                return;
            }
            Doc.Add (GetIndexedField (field, value));
            BytesIndexed += value.Length;
        }
    }

    public class EmailMessageIndexParameters
    {
        public InternetAddressList From;
        public InternetAddressList To;
        public InternetAddressList Cc;
        public InternetAddressList Bcc;
        public string Subject;
        public string Content;
        public string Preview;
        public DateTime ReceivedDate;
    }

    public class EmailMessageIndexDocument : NcIndexDocument
    {
        public const int Version = 3;
        public const string DocumentType = "message";

        public EmailMessageIndexDocument (string id, EmailMessageIndexParameters parameters) : base (DocumentType, id, parameters)
        {
            AddIndexedField ("subject", parameters.Subject);

            var dateString = DateTools.DateToString (parameters.ReceivedDate, DateTools.Resolution.SECOND);
            var dateField = GetExactMatchOnlyField ("received_date", dateString);
            Doc.Add (dateField);
            BytesIndexed += dateString.Length;

            // Index the addresses
            AddAddressList ("from", parameters.From);
            AddAddressList ("to", parameters.To);
            AddAddressList ("cc", parameters.Cc);
            AddAddressList ("bcc", parameters.Bcc);

            // Index the preview
            AddIndexedField ("preview", parameters.Preview);
        }
    }

    /// <summary>
    /// This class is used for McContact to convert to a model agnostic format so we don't 
    /// need to include NachoCove.Model here.
    /// </summary>
    public class ContactIndexParameters
    {
        public string FirstName;
        public string MiddleName;
        public string LastName;
        public List<string> EmailAddresses;
        public List<string> EmailDomains;
        public List<string> PhoneNumbers;
        public List<string> Addresses;
        public string Note;
        public string CompanyName;

        public ContactIndexParameters ()
        {
            EmailAddresses = new List<string> ();
            EmailDomains = new List<string> ();
            PhoneNumbers = new List<string> ();
            Addresses = new List<string> ();
        }
    }

    public class ContactIndexDocument : NcIndexDocument
    {
        public const string DocumentType = "contact";
        // We support versioned indexing in case we want to add some field to the index
        // later. Initially, there are two versions. V1 is for all non-body field. V2
        // is for body field. The reason for this is that we want to index contact's
        // name, emails and etc ASAP and body is not downloaded until the user selects
        // the contact. I believe most contacts do not have a body. So, we index
        // everything else (in v1). When the body is downloaded, we re-index.
        //
        // If in the future, we need to add a new indexed field. That will become v2
        // and v3 is when body (really notes) is indexed. A migration will be needed
        // to set all v1 and v2 to v1. They will be re-indexed to v2 or v3.
        public const int Version = 3;

        public ContactIndexDocument (string id, ContactIndexParameters contact) : base (DocumentType, id, null)
        {
            AddIndexedField ("first_name", contact.FirstName);
            AddIndexedField ("middle_name", contact.MiddleName);
            AddIndexedField ("last_name", contact.LastName);
            AddIndexedField ("company_name", contact.CompanyName);
            foreach (var emailAddress in contact.EmailAddresses) {
                AddIndexedField ("email_address", emailAddress);
            }
            foreach (var emailDomain in contact.EmailDomains) {
                AddIndexedField ("email_domain", emailDomain);
            }
            foreach (var phoneNumber in contact.PhoneNumbers) {
                AddIndexedField ("phone_number", phoneNumber);
            }
            foreach (var address in contact.Addresses) {
                AddIndexedField ("address", address);
            }
            AddIndexedField ("note", contact.Note);
        }
    }
}

