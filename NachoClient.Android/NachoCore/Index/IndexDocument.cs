//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using MimeKit;
using Lucene.Net.Documents;

namespace NachoCore.Index
{
    public class IndexDocument
    {
        // Document is sealed; so we use the container pattern
        public Document Doc { protected set; get; }

        public long BytesIndexed { protected set; get; }

        public IndexDocument (string type, string id)
        {
            BytesIndexed = 0;
            Doc = new Document ();
            Doc.Add (GetExactMatchOnlyField ("type", type));
            Doc.Add (GetExactMatchOnlyField ("id", id));
            BytesIndexed += type.Length + id.Length;
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
                Doc.Add (GetIndexedField (field, addressString));
                BytesIndexed += addressString.Length;
            }
        }
    }

    public class IndexEmailMessage : IndexDocument
    {
        public IndexEmailMessage (string messageBodyPath, string id) : base ("message", id)
        {
            // MIME parse the message 
            MimeMessage message;
            using (var fileStream = new FileStream (messageBodyPath, FileMode.Open, FileAccess.Read)) {
                message = MimeMessage.Load (fileStream);
            }

            Doc.Add (GetIndexedField ("subject", message.Subject));
            BytesIndexed += message.Subject.Length;

            var dateString = DateTools.DateToString (message.Date.DateTime, DateTools.Resolution.SECOND);
            var dateField = GetExactMatchOnlyField ("received_date", dateString);
            Doc.Add (dateField);

            // Index the addresses
            AddAddressList ("from", message.From);
            AddAddressList ("to", message.To);
            AddAddressList ("cc", message.Cc);
            AddAddressList ("bcc", message.Bcc);

            // Index the body
            foreach (var part in message.BodyParts) {
                var textPart = part as TextPart;
                if (null == textPart) {
                    continue; // can only handle plain text part
                }
                string body = textPart.Text;
                Doc.Add (GetIndexedField ("body", body));
                BytesIndexed += body.Length;
            }
        }
    }

    public class IndexEvent : IndexDocument
    {
        public IndexEvent (string eventBodyPath, string id) : base ("event", id)
        {
        }
    }
}

