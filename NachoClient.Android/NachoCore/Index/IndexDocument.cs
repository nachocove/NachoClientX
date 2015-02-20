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

    public class IndexMimeDocument : IndexDocument
    {
        protected MimeMessage Message;

        public IndexMimeDocument (string messageBodyPath, string type, string id) : base (type, id)
        {
            // MIME parse the message 
            using (var fileStream = new FileStream (messageBodyPath, FileMode.Open, FileAccess.Read)) {
                Message = MimeMessage.Load (fileStream);
            }

            // Dig thru all MIME part and index those we can:
            // 1. text/html - HTML doc
            // 2. text/plain - plain text
            // 3. multipart/mixed - Index every subpart that can be indexed.
            // 4. multipart/alternatives - Index only the preferred part.
            ProcessMimeEntity (Message.Body);
        }

        private bool ProcessMimeEntity (MimeEntity part)
        {
            bool processed = false;
            if (part is MessagePart) {
                var message = (MessagePart)part;
                processed |= ProcessMimeEntity (message.Message.Body);
                return processed;
            }
            if (part is Multipart) {
                var multipart = (Multipart)part;
                if (multipart.ContentType.Matches ("multipart", "alternative")) {
                    processed |= ProcessAlternativeMultipart (multipart);
                } else if (multipart.ContentType.Matches ("multipart", "mixed")) {
                    processed |= ProcessMixedMultipart (multipart);
                } else {
                    // Unsupported multipart
                    Log.Warn ("unsupported multipart type {0}", multipart.ContentType.Name);
                }
                return processed;
            }
            return ProcessMimePart ((MimePart)part);
        }

        private bool ProcessAlternativeMultipart (Multipart multipart)
        {
            // We start from the last part and iterate backward until we find something that works
            for (int n = multipart.Count - 1; n >= 0; n--) {
                if (ProcessMimeEntity (multipart [n])) {
                    return true;
                }
            }
            Log.Warn ("no suitable alternative part ({0} parts total)", multipart.Count);
            return false;
        }

        private bool ProcessMixedMultipart (Multipart multipart)
        {
            bool processed = false;
            foreach (var subpart in multipart) {
                processed |= ProcessMimeEntity (subpart);
            }
            return processed;
        }

        private bool ProcessMimePart (MimePart part)
        {
            // TODO - The only thing we support now is plain text. For HTML, we need a HTML tokenizer
            // But most email client will send a plain text alternative. So, we should be ok for most
            // cases.
            if ("plain" == part.ContentType.MediaSubtype) {
                TextPart body = (TextPart)part;
                if (null != body.Text) {
                    Log.Debug ("body = {0}", body.Text);
                    var bodyField = GetIndexedField ("body", body.Text);
                    Doc.Add (bodyField);
                    return true;
                }
            }
            return false;
        }

    }

    public class IndexEmailMessage : IndexMimeDocument
    {
        public IndexEmailMessage (string messageBodyPath, string id) : base (messageBodyPath, "message", id)
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

    public class IndexEvent : IndexMimeDocument
    {
        public IndexEvent (string eventBodyPath, string id) : base (eventBodyPath, "event", id)
        {
            // MIME parse the message
        }
    }
}

