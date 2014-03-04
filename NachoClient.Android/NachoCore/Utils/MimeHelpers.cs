//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using MimeKit;
using NachoCore;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class MimeHelpers
    {
        public MimeHelpers ()
        {
        }

        public static MimeEntity SearchMessage (string cid, MimeMessage message)
        {
            NachoAssert.True (null != message);
            return SearchMimeEntity (cid, message.Body);
        }

        public static MimeEntity SearchMimeEntity (string cid, MimeEntity entity)
        {
            if (null == entity) {
                return null;
            }
            if (entity is MessagePart) {
                var messagePart = (MessagePart)entity;
                return SearchMessage (cid, messagePart.Message);
            }
            if (entity is Multipart) {
                var multipart = (Multipart)entity;
                foreach (var subpart in multipart) {
                    var e = SearchMimeEntity (cid, subpart);
                    if (null != e) {
                        return e;
                    }
                }
                return null;
            }
            var part = (MimePart)entity;
            if ((null != part.ContentId) && part.ContentId.Contains (cid)) {
                return entity;
            } else {
                return null;
            }
        }

        static string Indent (int indent)
        {
            return indent.ToString ().PadRight (2 + (indent * 2));
        }

        static public void DumpMessage (MimeMessage message, int indent)
        {
            Log.Info ("{0}MimeMessage: {1}", Indent (indent), message);
            DumpMimeEntity (message.Body, indent + 1);
        }

        static void DumpMimeEntity (MimeEntity entity, int indent)
        {
            if (entity is MessagePart) {
                var messagePart = (MessagePart)entity;
                Log.Info ("{0}MimeEntity: {1} {2}", Indent (indent), messagePart, messagePart.ContentType);
                DumpMessage (messagePart.Message, indent + 1);
                return;
            }
            if (entity is Multipart) {
                var multipart = (Multipart)entity;
                Log.Info ("{0}Multipart: {1} {2}", Indent (indent), multipart, multipart.ContentType);
                foreach (var subpart in multipart) {
                    Log.Info ("{0}Subpart: {1} {2}", Indent (indent), subpart, subpart.ContentType);
                    DumpMimeEntity (subpart, indent + 1);
                }
                return;
            }
            Log.Info ("{0}MimeEntity: {1} {2}", Indent (indent), entity, entity.ContentType);
        }

        static public string FetchSomeText (string body)
        {
            if (null == body) {
                return null;
            }
            var bodySource = new MemoryStream (Encoding.UTF8.GetBytes (body));
            var bodyParser = new MimeParser (bodySource, MimeFormat.Default);
            var message = bodyParser.ParseMessage ();
            return FetchSomeText (message.Body);
        }

        static public string FetchSomeText (MimeEntity entity)
        {
            if (null == entity) {
                return null;
            }
            if (entity is Multipart) {
                var multipart = (Multipart)entity;
                foreach (var subpart in multipart) {
                    var s = FetchSomeText (subpart);
                    if (null != s) {
                        return s;
                    }
                }
                return null;
            }

            if (entity is MimePart) {
                var part = (MimePart)entity;
                if (part is TextPart) {
                    var text = (TextPart)part;
                    if (text.ContentType.Matches ("text", "plain")) {
                        return text.Text;
                    }
                }
            }
            return null;
        }

        static public string CreateSummary(string body)
        {
            var text = FetchSomeText (body);
            if (null == text) {
                return " ";
            } else {
                return text.Substring (0, Math.Min (text.Length, 180));
            }
        }

        static public string CreateSummary (MimeMessage message)
        {
            NachoAssert.True (null != message);
            var text = FetchSomeText (message.Body);
            if (null == text) {
                return " ";
            } else {
                return text.Substring (0, Math.Min(text.Length, 180));
            }
        }

        static protected string CommaSeparatedList (InternetAddressList addresses)
        {
            var list = new List<string> ();

            foreach (var a in addresses) {
                list.Add (a.Name);
            }
            return String.Join (",", list);
        }

        /// <summary>
        /// Convert MimeMessage to McEmailMessage and send it.
        /// </summary>
        static public void SendEmail(int AccountId, MimeMessage mimeMessage)
        {
            // Don't let 0 into the db
            NachoAssert.True (AccountId > 0);

            var msg = new McEmailMessage ();
            msg.To = CommaSeparatedList (mimeMessage.To);
            msg.Cc = CommaSeparatedList (mimeMessage.Cc);
            msg.From = CommaSeparatedList(mimeMessage.From);
            msg.Subject = mimeMessage.Subject;

            var body = new McBody ();
            var bodyStream = new System.IO.MemoryStream();
            mimeMessage.WriteTo (bodyStream);
            bodyStream.Seek (0, SeekOrigin.Begin);
            var textStream = new StreamReader(bodyStream);

            // TODO: KLUDGE ZONE
            StringBuilder builder = new StringBuilder(textStream.ReadToEnd ());
            builder.Replace ("method=\"REQUEST\"", "method=REQUEST");
            body.Body = builder.ToString ();
//            body.Body = textStream.ReadToEnd ();
            // TODO: KLUDGE END

            BackEnd.Instance.Db.Insert (body);
            msg.BodyId = body.Id;

            msg.AccountId = AccountId;
            BackEnd.Instance.Db.Insert (msg);

            BackEnd.Instance.SendEmailCmd (msg.AccountId, msg.Id);
        }
    }
}



