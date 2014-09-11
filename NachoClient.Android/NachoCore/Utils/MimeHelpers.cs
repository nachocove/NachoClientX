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
            NcAssert.True (null != message);
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
            Log.Info (Log.LOG_EMAIL, "{0}MimeMessage: {1}", Indent (indent), message);
            DumpMimeEntity (message.Body, indent + 1);
        }

        static void DumpMimeEntity (MimeEntity entity, int indent)
        {
            if (entity is MessagePart) {
                var messagePart = (MessagePart)entity;
                Log.Info (Log.LOG_EMAIL, "{0}MimeEntity: {1} {2}", Indent (indent), messagePart, messagePart.ContentType);
                DumpMessage (messagePart.Message, indent + 1);
                return;
            }
            if (entity is Multipart) {
                var multipart = (Multipart)entity;
                Log.Info (Log.LOG_EMAIL, "{0}Multipart: {1} {2}", Indent (indent), multipart, multipart.ContentType);
                foreach (var subpart in multipart) {
                    Log.Info (Log.LOG_EMAIL, "{0}Subpart: {1} {2}", Indent (indent), subpart, subpart.ContentType);
                    DumpMimeEntity (subpart, indent + 1);
                }
                return;
            }
            Log.Info (Log.LOG_EMAIL, "{0}MimeEntity: {1} {2}", Indent (indent), entity, entity.ContentType);
        }

        static protected TextPart FindTextPart (MimeEntity entity)
        {
            if (null == entity) {
                return null;
            }
            if (entity is Multipart) {
                var multipart = (Multipart)entity;
                foreach (var subpart in multipart) {
                    var s = FindTextPart (subpart);
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
                        return text;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Return a text summary of the message.
        /// </summary>
        /// <returns>The summary.</returns>
        /// <param name="message">Message.</param>
        static public string ExtractSummary (McEmailMessage message)
        {
            var path = message.GetBodyPath ();
            if (null == path) {
                return null;
            }
            if (McBody.MIME == message.GetBodyType()) {
                if (McAbstrItem.BodyStateEnum.Whole_0 != message.BodyState) {
                    return null;
                }
                using (var fileStream = new FileStream (path, FileMode.Open, FileAccess.Read)) {
                    var mimeParser = new MimeParser (fileStream, true);
                    var mimeMessage = mimeParser.ParseMessage ();
                    return ExtractSummary (mimeMessage);
                }
            }
            if (McBody.PlainText == message.GetBodyType ()) {
                var body = message.GetBody ();
                if (null == body) {
                    return null;
                }
                var raw = body.Substring (0, Math.Min (body.Length, 1000));
                var cooked = System.Text.RegularExpressions.Regex.Replace(raw, @"\s+", " ");
                return cooked;
            }
            return "No summary available.";
        }

        static public string ExtractSummary (MimeMessage mimeMessage)
        {
            var textPart = FindTextPart (mimeMessage.Body);
            if (null == textPart) {
                return null;
            }
            if (null == textPart.Text) {
                return null;
            }
            var raw = textPart.Text.Substring (0, Math.Min (textPart.Text.Length, 1000));
            var cooked = System.Text.RegularExpressions.Regex.Replace(raw, @"\s+", " ");
            return cooked;
        }

        static public string ExtractTextPart (McEmailMessage message)
        {
            string error;
            string text = ExtractTextPartWithError (message, out error);
            if (null != error) {
                return error;
            }
            return text;
        }

        static public string ExtractTextPartWithError (McEmailMessage message, out string error)
        {
            error = null;
            if (McAbstrItem.BodyStateEnum.Whole_0 != message.BodyState) {
                error = "Nacho Mail has not downloaded the body of this message yet.\n" + message.GetBodyPreviewOrEmpty();
                return null;
            }

            if (McBody.PlainText == message.BodyType) {
                return message.GetBody ();
            }

            if (McBody.HTML == message.BodyType) {
                error = "Nacho Mail has not converted the HTML to reply text.\n" + message.GetBodyPreviewOrEmpty ();
                return null;
            }

            if (McBody.RTF == message.BodyType) {
                error = "Nacho Mail has not converted the RTF to reply text.\n" + message.GetBodyPreviewOrEmpty ();
                return null;
            }

            NcAssert.True (McBody.MIME == message.BodyType);

            var path = message.GetBodyPath ();
            if (null == path) {
                return null;
            }
            using (var fileStream = new FileStream (path, FileMode.Open, FileAccess.Read)) {
                var mimeParser = new MimeParser (fileStream, true);
                var mimeMessage = mimeParser.ParseMessage ();
                var textPart = FindTextPart (mimeMessage.Body);
                if (null == textPart) {
                    return null;
                }
                if (null == textPart.Text) {
                    return null;
                }
                return textPart.Text;
            }
        }

        static protected string CommaSeparatedList (InternetAddressList addresses)
        {
            var list = new List<string> ();

            foreach (var a in addresses) {
                list.Add (a.ToString ());
            }
            return String.Join (",", list);
        }

        /// <summary>
        /// Convert MimeMessage to McEmailMessage and add it to the database.
        /// </summary>
        static public McEmailMessage AddToDb (int AccountId, MimeMessage mimeMessage)
        {
            // Don't let 0 into the db
            NcAssert.True (AccountId > 0);

            var msg = new McEmailMessage ();
            msg.AccountId = AccountId;
            msg.To = CommaSeparatedList (mimeMessage.To);
            msg.Cc = CommaSeparatedList (mimeMessage.Cc);
            msg.From = CommaSeparatedList (mimeMessage.From);
            msg.Subject = mimeMessage.Subject;

            // Create body
            var body = McBody.SaveStart ();
            using (var fileStream = body.SaveFileStream ()) {
                mimeMessage.WriteTo (fileStream);
            }
            body.SaveDone ();
            msg.BodyId = body.Id;

            NcModel.Instance.Db.Insert (msg);

            return msg;
        }

        public static void MimeDisplayList (MimeMessage message, ref List<MimeEntity> list)
        {
            if (null == list) {
                list = new List<MimeEntity> ();
            }
            MimeEntityDisplayList (message.Body, ref list);
        }

        protected static void MimeEntityDisplayList (MimeEntity entity, ref List<MimeEntity> list)
        {
            if (entity is MessagePart) {
                // This entity is an attached message/rfc822 mime part.
                var messagePart = (MessagePart)entity;
                // If you'd like to render this inline instead of treating
                // it as an attachment, you would just continue to recurse:
                MimeDisplayList (messagePart.Message, ref list);
                return;
            }
            if (entity is Multipart) {
                var multipart = (Multipart)entity;
                if (multipart.ContentType.Matches ("multipart", "alternative")) {
                    MimeBestAlternativeDisplayList (multipart, ref list);
                    return;
                }
                foreach (var subpart in multipart) {
                    MimeEntityDisplayList (subpart, ref list);
                }
                return;
            }

            // Everything that isn't either a MessagePart or a Multipart is a MimePart
            var part = (MimePart)entity;

            // Don't render anything that is explicitly marked as an attachment.
            if (part.IsAttachment) {
                return;
            }

            if (part is TextPart) {
                list.Add (part);
                return;
            }

            if (entity.ContentType.Matches ("image", "*")) {
                list.Add (part);
                return;
            }

            if (part.ContentType.Matches ("application", "ms-tnef")) {
                list.Add (part);
                return;
            }

            if (entity.ContentType.Matches ("application", "ics")) {
                NachoCore.Utils.Log.Error (Log.LOG_EMAIL, "Unhandled ics: {0}\n", part.ContentType);
                return;
            }
            if (entity.ContentType.Matches ("application", "octet-stream")) {
                NachoCore.Utils.Log.Error (Log.LOG_EMAIL, "Unhandled octet-stream: {0}\n", part.ContentType);
                return;
            }

            NachoCore.Utils.Log.Error (Log.LOG_EMAIL, "Unhandled Render: {0}\n", part.ContentType);
        }

        /// <summary>
        /// Renders the best alternative.
        /// http://en.wikipedia.org/wiki/MIME#Alternative
        /// </summary>
        protected static void MimeBestAlternativeDisplayList (Multipart multipart, ref List<MimeEntity> list)
        {
            var e = multipart.Last ();
            MimeEntityDisplayList (e, ref list);

        }
    }
}



