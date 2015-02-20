//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using MimeKit;
using Lucene.Net.Documents;

namespace NachoCore.Index
{
    /// <summary>
    /// In order to keep this class totally portable across Nacho Mail client and test harness,
    /// we cannot use NachoCore.Utils.Log. Instead, we define some vanila interface and let each
    /// host to glue their own logging API to these API.
    /// </summary>
    public class Log
    {
        public delegate void ErrorFunc (string fmt, params object[] args);

        public delegate void WarnFunc (string fmt, params object[] args);

        public delegate void InfoFunc (string fmt, params object[] args);

        public delegate void DebugFunc (string fmt, params object[] args);

        public static ErrorFunc PlatformError;

        public static WarnFunc PlatformWarn;

        public static InfoFunc PlatformInfo;

        public static DebugFunc PlatformDebug;

        public static void Error (string fmt, params object[] args)
        {
            if (null == PlatformError) {
                return;
            }
            PlatformError (fmt, args);
        }

        public static void Warn (string fmt, params object[] args)
        {
            if (null == PlatformWarn) {
                return;
            }
            PlatformWarn (fmt, args);
        }

        public static void Info (string fmt, params object[] args)
        {
            if (null == PlatformInfo) {
                return;
            }
            PlatformInfo (fmt, args);
        }

        public static void Debug (string fmt, params object[] args)
        {
            if (null == PlatformDebug) {
                return;
            }
            PlatformDebug (fmt, args);
        }
    }

    public class IndexDocument
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

        public IndexDocument (string type, string id, string body)
        {
            BytesIndexed = 0;
            Doc = new Document ();
            Doc.Add (GetExactMatchOnlyField ("type", type));
            Doc.Add (GetExactMatchOnlyField ("id", id));
            Doc.Add (GetIndexedField ("body", body));
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

    public class IndexMimeDocument : IndexDocument
    {
        protected MimeMessage Message;

        public IndexMimeDocument (string type, string id, string content, MimeMessage message) :
            base (type, id, content)
        {
            Message = message;
        }
    }

    public class IndexEmailMessage : IndexMimeDocument
    {
        public IndexEmailMessage (string id, string content, MimeMessage message) :
            base ("message", id, content, message)
        {
            Doc.Add (GetIndexedField ("subject", Message.Subject));
            BytesIndexed += Message.Subject.Length;

            var dateString = DateTools.DateToString (Message.Date.DateTime, DateTools.Resolution.SECOND);
            var dateField = GetExactMatchOnlyField ("received_date", dateString);
            Doc.Add (dateField);

            // Index the addresses
            AddAddressList ("from", Message.From);
            AddAddressList ("to", Message.To);
            AddAddressList ("cc", Message.Cc);
            AddAddressList ("bcc", Message.Bcc);
        }
    }
}

