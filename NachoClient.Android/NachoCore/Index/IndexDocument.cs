//  Copyright (C) 2014-2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using MimeKit;
using Lucene.Net.Documents;

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

        public NcIndexDocument (string type, string id, string body)
        {
            BytesIndexed = 0;
            Doc = new Document ();
            AddExactMatchOnlyField ("type", type);
            AddExactMatchOnlyField ("id", id);
            AddIndexedField ("body", body);
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
            foreach (var address in addressList) {
                var addressString = address.ToString ();
                AddIndexedField (field, addressString);
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

    public class MimeIndexDocument : NcIndexDocument
    {
        protected MimeMessage Message;

        public MimeIndexDocument (string type, string id, string content, MimeMessage message) :
            base (type, id, content)
        {
            Message = message;
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
        public DateTime ReceivedDate;
    }

    public class EmailMessageIndexDocument : MimeIndexDocument
    {
        public const int Version = 2;

        public EmailMessageIndexDocument (string id, EmailMessageIndexParameters parameters, MimeMessage message) :
            base ("message", id, parameters.Content, message)
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

        public ContactIndexDocument (string id, ContactIndexParameters contact)
            : base ("contact", id, null)
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

