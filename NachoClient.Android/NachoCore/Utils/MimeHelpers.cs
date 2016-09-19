//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using MimeKit;
using NachoCore;
using NachoCore.Model;
using System.Threading;
using System.Text.RegularExpressions;

namespace NachoCore.Utils
{
    public class MimeHelpers
    {
        public MimeHelpers ()
        {
        }

        protected static MimeMessage EmptyMessage ()
        {
            var emptyMessage = new MimeMessage ();
            emptyMessage.Body = new TextPart ("plain") {
                Text = ""
            };
            return emptyMessage;
        }

        /// <summary>
        /// Loads a MIME message from a file.
        /// </summary>
        /// <remarks>
        /// If the file doesn't exist, can't be read, or can't be parsed as a MIME message, then an
        /// empty MIME message is returned.
        /// </remarks>
        /// <returns>The MIME message.</returns>
        /// <param name="path">The path to the file containing the text of the MIME message.</param>
        public static MimeMessage LoadMessage (McBody body)
        {
            if (null == body) {
                return EmptyMessage ();
            }
            try {
                var path = body.GetFilePath ();
                using (var fileStream = new FileStream (path, FileMode.Open, FileAccess.Read)) {
                    return MimeMessage.Load (fileStream);
                }
            } catch {
                return EmptyMessage ();
            }
        }

        public static MimeMessage ConvertTnefToMessage (MimeKit.Tnef.TnefPart tnef)
        {
            try {
                var message = tnef.ConvertToMessage ();
                FixTnefMessage (message);
                return message;
            } catch (Exception e) {
                // We have seen ConvertToMessage() fail with ArgumentOutOfRangeException and ArgumentException.
                // It is unknown whether the problem is in Exchange server giving the app a corrupt calendar
                // event body, or in MimeKit's TNEF parser.  But either way, there is not much the app can do
                // to recover the data.
                Log.Error (Log.LOG_CALENDAR, "TnefPart.ConvertToMessage() failed with exception {0}", e.ToString ());
                return EmptyMessage ();
            }
        }

        public static MimePart SearchMessage (string cid, MimeMessage message)
        {
            NcAssert.True (null != message);
            return SearchMimeEntity (cid, message.Body);
        }

        public static MimePart SearchMimeEntity (string cid, MimeEntity entity)
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
                return part;
            } else {
                return null;
            }
        }

        static string Indent (int indent)
        {
            return indent.ToString ().PadRight (2 + (indent * 2));
        }

        static public void DumpMessage (MimeMessage message)
        {
            DumpMessage (message, 0);
        }

        static private void DumpMessage (MimeMessage message, int indent)
        {
            Log.Info (Log.LOG_EMAIL, "{0}{1} {2}", Indent (indent), message.GetType ().Name, message.Subject);
            DumpMimeEntity (message.Body, indent + 1);
        }

        static private void DumpMimeEntity (MimeEntity entity, int indent)
        {
            Log.Info (Log.LOG_EMAIL, "{0}{1} {2} {3} {4}", Indent (indent), entity.GetType ().Name, entity.ContentType, entity.ContentId, entity.ContentDisposition);
            if (entity is MessagePart) {
                DumpMessage (((MessagePart)entity).Message, indent + 1);
            }
            if (entity is Multipart) {
                foreach (var subpart in (Multipart)entity) {
                    DumpMimeEntity (subpart, indent + 1);
                }
            }
        }

        /// <summary>
        /// Return the first "subtype" entity found in the given subtree, or null if none was found.
        /// </summary>
        /// <param name="entity">The portion of the MIME message to search.</param>
        static protected TextPart FindTextPartWithSubtype (MimeEntity entity, string subtype)
        {
            if (null == entity) {
                return null;
            }
            if (entity is MimeKit.Tnef.TnefPart) {
                // Pull apart the TNEF part and see what is inside.
                var tnef = entity as MimeKit.Tnef.TnefPart;
                var mimeMessage = ConvertTnefToMessage (tnef);
                return FindTextPartWithSubtype (mimeMessage.Body, subtype);
            }
            if (entity is TextPart && entity.ContentType.IsMimeType ("text", subtype)) {
                TextPart textPart = entity as TextPart;
                if (null != textPart && null != textPart.ContentObject) {
                    return textPart;
                } else {
                    return null;
                }
            }
            if (entity is Multipart) {
                foreach (var subpart in entity as Multipart) {
                    var textPart = FindTextPartWithSubtype (subpart, subtype);
                    if (null != textPart) {
                        return textPart;
                    }
                }
            }
            return null;
        }

        public static bool FindText (McEmailMessage message, out string html, out string text)
        {
            html = null;
            text = null;

            var body = message.GetBody ();
            if (!McBody.IsNontruncatedBodyComplete (body)) {
                return false;
            }

            if (McAbstrFileDesc.BodyTypeEnum.None == body.BodyType) {
                return false;
            }

            if (McAbstrFileDesc.BodyTypeEnum.PlainText_1 == body.BodyType) {
                text = body.GetContentsString ();
                return true;
            }

            if (McAbstrFileDesc.BodyTypeEnum.HTML_2 == body.BodyType) {
                html = body.GetContentsString ();
                return true;
            }

            if (McAbstrFileDesc.BodyTypeEnum.RTF_3 == body.BodyType) {
                return false;
            }

            NcAssert.True (McAbstrFileDesc.BodyTypeEnum.MIME_4 == body.BodyType);

            var mimeMessage = LoadMessage (body);

            return FindText(mimeMessage, out html, out text);
        }

        public static bool FindText(MimeMessage mimeMessage, out string html, out string text)
        {
            html = null;
            text = null;

            if (null == mimeMessage) {
                return false;
            }

            var part = FindTextPartWithSubtype (mimeMessage.Body, "html");
            if (null != part) {
                html = part.Text;
                return true;
            }
            part = FindTextPartWithSubtype (mimeMessage.Body, "plain");
            if (null != part) {
                text = part.Text;
                return true;
            }
            return false;
        }

        static public bool FindTextWithType (MimeMessage message, out string text, out McAbstrFileDesc.BodyTypeEnum type, params McAbstrFileDesc.BodyTypeEnum[] preferredTypes)
        {
            NcAssert.True (0 < preferredTypes.Length);
            foreach (var preferredType in preferredTypes) {
                string mimeSubType;
                switch (preferredType) {
                case McAbstrFileDesc.BodyTypeEnum.PlainText_1:
                    mimeSubType = "plain";
                    break;
                case McAbstrFileDesc.BodyTypeEnum.HTML_2:
                    mimeSubType = "html";
                    break;
                case McAbstrFileDesc.BodyTypeEnum.RTF_3:
                    mimeSubType = "rtf";
                    break;
                default:
                    NcAssert.CaseError ();
                    mimeSubType = "";
                    break;
                }
                var textPart = FindTextPartWithSubtype (message.Body, mimeSubType);
                if (null != textPart) {
                    text = textPart.Text;
                    type = preferredType;
                    return true;
                }
            }
            text = null;
            type = McAbstrFileDesc.BodyTypeEnum.None;
            return false;
        }

        /// <summary>
        /// Returns the plain text part of a message body, an error message if the body is in
        /// a format other than plain text, or <code>null</code> if no body can be found.
        /// </summary>
        /// <returns>The plain text part of a message body.</returns>
        /// <param name="message">The e-mail message to search.</param>
        static public string ExtractTextPart (McEmailMessage message)
        {
            string error;
            string text = ExtractTextPartWithError (message, out error);
            if (null != error) {
                return error;
            }
            return text;
        }

        /// <summary>
        /// Returns the plain text part of a message body, or <code>null</code> if no body can be found.
        /// <code>error</code> is set to a suitable error message if something goes wrong.
        /// </summary>
        /// <returns>The plain text part of a message body.</returns>
        /// <param name="message">The e-mail message to search.</param>
        /// <param name="error">Return an error message if something goes wrong.</param>
        static public string ExtractTextPartWithError (McEmailMessage message, out string error)
        {
            error = null;

            var body = message.GetBody ();
            if (!McBody.IsNontruncatedBodyComplete (body)) {
                error = "Apollo Mail has not downloaded the body of this message yet.\n" + message.GetBodyPreviewOrEmpty ();
                return null;
            }

            if (McAbstrFileDesc.BodyTypeEnum.None == body.BodyType) {
                return "";
            }

            if (McAbstrFileDesc.BodyTypeEnum.PlainText_1 == body.BodyType) {
                return body.GetContentsString ();
            }

            if (McAbstrFileDesc.BodyTypeEnum.HTML_2 == body.BodyType) {
                error = "Apollo Mail has not converted the HTML to reply text.\n" + message.GetBodyPreviewOrEmpty ();
                return null;
            }

            if (McAbstrFileDesc.BodyTypeEnum.RTF_3 == body.BodyType) {
                error = "Apollo Mail has not converted the RTF to reply text.\n" + message.GetBodyPreviewOrEmpty ();
                return null;
            }

            NcAssert.True (McAbstrFileDesc.BodyTypeEnum.MIME_4 == body.BodyType);

            return ExtractTextPart (McBody.QueryById<McBody> (message.BodyId));
        }

        /// <summary>
        /// Returns the plain text part of a body, or <code>null</code> if no plain text can be found.
        /// </summary>
        /// <remarks>
        /// The body is assumed to be in MIME format.
        /// </remarks>
        /// <returns>The plain text part of a body.</returns>
        /// <param name="messageBody">The McBody item in MIME format to search.</param>
        static public string ExtractTextPart (McBody messageBody)
        {
            if (null == messageBody || null == messageBody.GetFilePath ()) {
                return null;
            }
            return ExtractTextPart (LoadMessage (messageBody));
        }

        /// <summary>
        /// Returns the plain text part of the MIME message, or <code>null</code> if no plain text can be found.
        /// </summary>
        /// <returns>The plain text part of the MIME message.</returns>
        /// <param name="message">The MIME message to search.</param>
        static public string ExtractTextPart (MimeMessage message)
        {
            if (null == message) {
                return null;
            }
            var textPart = FindTextPartWithSubtype (message.Body, "plain");
            if (null == textPart) {
                return null;
            }
            return textPart.Text;
        }

        /// <summary>
        /// Remove all "text" parts of the MIME message, replacing them with a single "text/plain" part with the given string.
        /// </summary>
        /// <param name="message">The MIME message to be changed.</param>
        /// <param name="text">The text value to insert into the MIME message.</param>
        static public void SetPlainText (MimeMessage message, string text)
        {
            bool replaced = SetPlainTextHelper (message.Body, null, message, text, false);
            if (!replaced) {
                // No "text/plain" parts were found, so a one needs to be created.
                TextPart newTextPart = new TextPart ("plain") {
                    Text = text
                };
                if (null == message.Body) {
                    // It's the only thing in the message.
                    message.Body = newTextPart;
                } else if (message.Body.ContentType.IsMimeType ("multipart", "mixed")) {
                    // The top-level entity is already a "multipart/mixed". Just add the "text/plain" to it.
                    ((Multipart)message.Body).Add (newTextPart);
                } else {
                    // The top-level entity is something other than "multipart/mixed". Create a new
                    // "multipart/mixed" at the top and add the "text/plain" and the old top-level entity
                    // to it.
                    var multipartBody = new Multipart ("mixed");
                    multipartBody.Add (message.Body);
                    multipartBody.Add (newTextPart);
                    message.Body = multipartBody;
                }
            }
        }

        static private bool SetPlainTextHelper (
            MimeEntity entity, Multipart parent, MimeMessage message,
            string text, bool alreadyReplaced)
        {
            if (entity.ContentType.IsMimeType ("text", "plain") && !alreadyReplaced) {
                ((TextPart)entity).Text = text;
                return true;
            }
            if (entity.ContentType.IsMimeType ("text", "*")) {
                RemoveEntity (entity, parent, message);
                return false;
            }
            if (entity is MimeKit.Tnef.TnefPart) {
                // Replace the TNEF part with an equivalent MIME entity
                var tnef = entity as MimeKit.Tnef.TnefPart;
                var tnefAsMime = ConvertTnefToMessage (tnef);
                ReplaceEntity (entity, tnefAsMime.Body, parent, message);
                return SetPlainTextHelper (tnefAsMime.Body, parent, message, text, alreadyReplaced);
            }
            if (entity is Multipart) {
                var multipart = entity as Multipart;
                // Child entities might get removed or replaced as they are being iterated over.
                // So create a copy of the list of children and iterate over the copy.
                List<MimeEntity> children = new List<MimeEntity> (multipart);
                foreach (var subpart in children) {
                    bool didReplacement = SetPlainTextHelper (subpart, multipart, message, text, alreadyReplaced);
                    alreadyReplaced = alreadyReplaced || didReplacement;
                }
                if (multipart.ContentType.IsMimeType ("multipart", "mixed") ||
                    multipart.ContentType.IsMimeType ("multipart", "alternative")) {
                    // Get rid of any multipart entities that are no longer necessary now that
                    // some entities may have been removed.
                    if (0 == multipart.Count) {
                        RemoveEntity (multipart, parent, message);
                    } else if (1 == multipart.Count) {
                        ReplaceEntity (multipart, multipart [0], parent, message);
                    }
                }
                return alreadyReplaced;
            }
            return false;
        }

        static private void RemoveEntity (MimeEntity entity, Multipart parent, MimeMessage message)
        {
            if (null == parent) {
                message.Body = null;
            } else {
                parent.Remove (entity);
            }
        }

        static private void ReplaceEntity (MimeEntity oldEntity, MimeEntity newEntity, Multipart parent, MimeMessage message)
        {
            if (null == parent) {
                message.Body = newEntity;
            } else {
                parent.Remove (oldEntity);
                parent.Add (newEntity);
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

            var msg = new McEmailMessage () {
                ClientIsSender = true,
            };
            msg.AccountId = AccountId;
            msg.To = CommaSeparatedList (mimeMessage.To);
            msg.Cc = CommaSeparatedList (mimeMessage.Cc);
            msg.Bcc = CommaSeparatedList (mimeMessage.Bcc);
            msg.From = CommaSeparatedList (mimeMessage.From);
            msg.Subject = mimeMessage.Subject;

            // For display in Outbox
            msg.DateReceived = mimeMessage.Date.DateTime;

            // Create body
            var body = McBody.InsertFile (AccountId, McAbstrFileDesc.BodyTypeEnum.MIME_4, (FileStream stream) => {
                mimeMessage.WriteTo (stream);
            });
            msg.BodyId = body.Id;

            msg.Insert ();

            return msg;
        }

        public static string MimeTypeFromNativeBodyType (int nativeBodyType)
        {
            switch (nativeBodyType) {
            case 0:
                // NativeBodyType is not known.
                return null;
            case 1:
                return "text/plain";
            case 2:
                return "text/html";
            case 3:
                return "text/rtf";
            default:
                Log.Error (Log.LOG_EMAIL, "Unexpected value for NativeBodyType: {0}", nativeBodyType);
                return null;
            }
        }

        public static void MimeDisplayList (MimeMessage message, List<MimeEntity> list, string preferredType)
        {
            MimeEntityDisplayList (message.Body, list, preferredType);
        }

        protected static void MimeEntityDisplayList (MimeEntity entity, List<MimeEntity> list, string preferredType)
        {
            if (entity is MessagePart) {
                // This entity is an attached message/rfc822 mime part.
                var messagePart = (MessagePart)entity;
                // The preferredType should only apply to the outermost message, not any nested messages.
                MimeDisplayList (messagePart.Message, list, null);
                return;
            }
            if (entity is Multipart) {
                var multipart = (Multipart)entity;
                if (multipart.ContentType.IsMimeType ("multipart", "alternative")) {
                    MimeBestAlternativeDisplayList (multipart, list, preferredType);
                    return;
                }
                if (multipart.ContentType.IsMimeType ("multipart", "related") && 0 < multipart.Count) {
                    // See https://tools.ietf.org/html/rfc2387
                    // This isn't entirely correct. The multipart/related could have a "start"
                    // parameter that points to the root entity that is not the first one.
                    // But it is hard to write code to handle that without having a real life
                    // message to test with.  All the examples that I have seen list the root
                    // entity first.
                    MimeEntityDisplayList (multipart [0], list, preferredType);
                    return;
                }
                foreach (var subpart in multipart) {
                    MimeEntityDisplayList (subpart, list, preferredType);
                }
                return;
            }

            // Everything that isn't either a MessagePart or a Multipart is a MimePart
            var part = (MimePart)entity;

            // Don't render anything that is explicitly marked as an attachment.
            if (part.IsAttachment) {
                return;
            }

            // The conversion from TNEF to MIME will sometimes create a TextPart
            // with a null ContentObject, which will result in a NullReferenceException
            // when accessing the Text property.  Discard those bogus TextParts.  Discard
            // calendar parts, which are handled differently.  Render all other TextParts.
            if (part is TextPart) {
                if (null == part.ContentObject) {
                    Log.Info (Log.LOG_EMAIL, "Discarding a {0} MIME section that has a null ContentObject.", part.ContentType);
                } else if (!part.ContentType.IsMimeType("text", "calendar")) {
                    list.Add (part);
                }
                return;
            }

            if (part is MimeKit.Tnef.TnefPart) {
                // Convert the TNEF stuff into a MIME message, and look through that.
                MimeMessage tnef = ConvertTnefToMessage (part as MimeKit.Tnef.TnefPart);
                if (null != tnef.Body) {
                    MimeDisplayList (tnef, list, preferredType);
                }
                return;
            }

            if (entity.ContentType.IsMimeType ("image", "*")) {
                list.Add (part);
                return;
            }

            NachoCore.Utils.Log.Warn (Log.LOG_EMAIL, "Unhandled MIME part: {0}\n", part.ContentType);
        }

        /// <summary>
        /// Pick the best matching entity from a set of alternatives.  If one of the entities matches the
        /// given preferred type.  Otherwise, look for HTML, RTF, or plain text, in that order.  If still
        /// no match, return the last one in the list that is not a calendar entry.
        /// </summary>
        protected static void MimeBestAlternativeDisplayList (Multipart multipart, List<MimeEntity> list, string preferredType)
        {
            MimeEntity preferred = null;
            MimeEntity html = null;
            MimeEntity rtf = null;
            MimeEntity plain = null;
            MimeEntity lastNonCalendar = null;
            foreach (var entity in multipart) {

                // When an e-mail message has HTML with embedded images, then many mailers will send out
                // a message with the following structure:
                //    multipart/alternative
                //        text/plain
                //        multipart/related
                //            text/html
                //            image/jpeg
                // When deciding which of the multipart/alternative subparts to choose, we are interested
                // in the type of the first child of the multipart/related rather than the multipart/related
                // itself, because we ultimately want to show the HTML part instead of the plain text part.
                var effectiveEntity = entity;
                while (effectiveEntity.ContentType.IsMimeType ("multipart", "related") && effectiveEntity is Multipart && 0 < ((Multipart)effectiveEntity).Count) {
                    effectiveEntity = ((Multipart)effectiveEntity) [0];
                }
                var effectiveType = effectiveEntity.ContentType;

                if (null != preferredType && effectiveType.MimeType == preferredType) {
                    preferred = entity;
                }
                if (effectiveType.IsMimeType("text", "html")) {
                    html = entity;
                }
                if (effectiveType.IsMimeType("text", "rtf")) {
                    rtf = entity;
                }
                if (effectiveType.IsMimeType("text", "plain")) {
                    plain = entity;
                }
                if (!effectiveType.IsMimeType("text", "calendar")) {
                    lastNonCalendar = entity;
                }
            }
            MimeEntity bestMatch = preferred ?? html ?? rtf ?? plain ?? lastNonCalendar;
            if (null != bestMatch) {
                MimeEntityDisplayList (bestMatch, list, preferredType);
            }
        }

        /// <summary>
        /// If the TNEF part contains text in multiple formats, MimeKit will create a multipart/mixed
        /// instead of a multipart/alternative.  Fix that.
        /// </summary>
        public static void FixTnefMessage (MimeMessage tnefMessage)
        {
            if (null == tnefMessage.Body || !tnefMessage.Body.ContentType.IsMimeType ("multipart", "mixed")) {
                return;
            }
            bool allText = true;
            int textCount = 0;
            foreach (var part in tnefMessage.Body as Multipart) {
                if (part is TextPart) {
                    ++textCount;
                } else {
                    allText = false;
                }
            }
            if (1 < textCount) {
                if (allText) {
                    // There are at least two text parts, and there are only text parts.
                    // Replace the multipart/mixed with multipart/alternative.
                    tnefMessage.Body.ContentType.MediaSubtype = "alternative";
                } else {
                    // There are at least two text parts, but there are other things as well.
                    // Create a new multipart/alternative and move the text parts to that.
                    var alternative = new Multipart ("alternative");
                    var mixed = tnefMessage.Body as Multipart;
                    foreach (var part in mixed) {
                        if (part is TextPart) {
                            alternative.Add (part);
                        }
                    }
                    foreach (var part in alternative) {
                        mixed.Remove (part);
                    }
                    mixed.Add (alternative);
                }
            }
        }

        /// <summary>
        /// Remove any TNEF parts that are within a top-level multipart/mixed part of the given message.
        /// This should only be used when the MIME message was itself converted from a TNEF part; it is
        /// not intended to be used for a full MIME message.  When a recurring meeting has exceptions,
        /// information about the exceptions are included in TNEF parts nested within the main TNEF part
        /// of the description for the meeting series.  Information about the exceptions also appears
        /// elsewhere in the Exchange metadata, so these nested TNEF sections just get in the way and
        /// should be ignored.
        /// </summary>
        public static void RemoveNestedTnefParts (MimeMessage tnefMessage)
        {
            if (null == tnefMessage.Body || !tnefMessage.Body.ContentType.IsMimeType ("multipart", "mixed")) {
                return;
            }
            var multipart = (Multipart)tnefMessage.Body;
            var tnefParts = new List<MimeEntity> ();
            foreach (var part in multipart) {
                if (part is MimeKit.Tnef.TnefPart) {
                    tnefParts.Add (part);
                }
            }
            foreach (var tnefPart in tnefParts) {
                multipart.Remove (tnefPart);
            }
        }
       
        /// <summary>
        /// Find all the attachments in the given MIME message, including those
        /// nested inside a TNEF part.
        /// </summary>
        /// <param name="message">The MIME message to be searched.</param>
        public static List<MimeEntity> AllAttachments (MimeMessage message)
        {
            List<MimeEntity> result = new List<MimeEntity> ();
            FindAttachments (message.Body, result, false, false);
            return result;
        }

        /// <summary>
        /// Find all the attachments in the given MIME message, including those nested inside a TNEF part and
        /// those that are marked as inline.
        /// </summary>
        /// <param name="message">The MIME message to be searched.</param>
        public static List<MimeEntity> AllAttachmentsIncludingInline (MimeMessage message)
        {
            List<MimeEntity> result = new List<MimeEntity> ();
            FindAttachments (message.Body, result, true, false);
            return result;
        }

        private static void FindAttachments (MimeEntity entity, List<MimeEntity> result, bool includeInline, bool insideTnef)
        {
            // If this entity originally came from TNEF, then ignore attachments named winmail.dat
            // with type application/vnd.ms-tnef.  Those are an internal artifact of how some servers
            // represent recurring meetings with exceptions, and are of no interest to the user.
            if (null != entity.ContentDisposition &&
                ((includeInline && ContentDisposition.Inline == entity.ContentDisposition.Disposition) ||
                    entity.ContentDisposition.IsAttachment) &&
                (!insideTnef || !entity.ContentType.IsMimeType ("application", "vnd.ms-tnef")))
            {
                // It's an attachment that we are interested in.
                result.Add (entity);
            } else if (!insideTnef && entity is MimeKit.Tnef.TnefPart) {
                // Pull apart the TNEF part and see what is inside.  (Unless we are already inside of
                // a TNEF part, in which case the inner TNEF part represents an exception to a recurring
                // meeting, not the meeting series that we are interested in.)
                var tnef = entity as MimeKit.Tnef.TnefPart;
                var mimeMessage = ConvertTnefToMessage (tnef);
                FindAttachments (mimeMessage.Body, result, includeInline, true);
            } else if (entity is Multipart) {
                foreach (var subpart in entity as Multipart) {
                    FindAttachments (subpart, result, includeInline, insideTnef);
                }
            }
        }

        /// <summary>
        /// Add the attachments to the MIME message.
        /// </summary>
        /// <param name="message">The MIME message to which the attachments should be added.</param>
        /// <param name="attachments">The list of attachments to be added to the message.</param>
        public static void AddAttachments (MimeMessage message, List<McAttachment> attachments)
        {
            // Convert the McAttachments into MIME attachments.
            AttachmentCollection mimeAttachments = new AttachmentCollection ();
            foreach (var attachment in attachments) {
                if (McAttachment.FilePresenceEnum.Complete == attachment.FilePresence) {
                    mimeAttachments.Add (attachment.GetFilePath ());
                }
            }
            AddAttachments (message, mimeAttachments);
        }

        /// <summary>
        /// Add the attachments to the MIME message.
        /// </summary>
        /// <param name="message">The MIME message to which the attachments should be added.</param>
        /// <param name="attachments">The list of attachments to be added to the message.</param>
        public static void AddAttachments (MimeMessage message, AttachmentCollection attachments)
        {
            Multipart attachmentsParent = null; // Where to put the attachments
            MimeEntity existingBody = message.Body;
            if (existingBody.ContentType.IsMimeType ("multipart", "mixed")) {
                // Attachments can be added directly into the existing body.
                attachmentsParent = existingBody as Multipart;
            } else {
                // Create a new multipart/mixed entity that will hold the existing body and the attachments.
                attachmentsParent = new Multipart ("mixed");
                attachmentsParent.Add (existingBody);
                message.Body = attachmentsParent;
            }

            // Ttransfer the attachment entities to their new home.
            foreach (MimeEntity mimeAttachment in attachments) {
                attachmentsParent.Add (mimeAttachment);
            }
        }

        /// <summary>
        /// Remove all of the specified MIME entities from the MIME message.
        /// Multipart sections that are no longer needed are not removed.
        /// TNEF parts are ignored and are not modified.
        /// </summary>
        public static void RemoveEntities (MimeMessage message, List<MimeEntity> entities)
        {
            if (entities.Contains (message.Body)) {
                message.Body = null;
                return;
            }
            RemoveEntities (message.Body, entities);
        }

        private static void RemoveEntities (MimeEntity parentEntity, List<MimeEntity> entities)
        {
            Multipart parent = parentEntity as Multipart;
            if (null != parent) {
                var toBeRemoved = new List<MimeEntity> ();
                foreach (var subpart in parent) {
                    if (entities.Contains (subpart)) {
                        toBeRemoved.Add (subpart);
                    }
                }
                foreach (var entity in toBeRemoved) {
                    parent.Remove (entity);
                }
                foreach (var subpart in parent) {
                    RemoveEntities (subpart, entities);
                }
            }
        }

        public static void PossiblyExtractAttachmentsFromBody (McBody body, McAbstrItem item, CancellationToken Token = default(CancellationToken))
        {
            // Now that we have a body, see if it is possible to fill in the contents of any attachments.
            if (McBody.BodyTypeEnum.MIME_4 == body.BodyType && McBody.FilePresenceEnum.Complete == body.FilePresence && !body.Truncated) {
                var bodyAttachments = MimeHelpers.AllAttachmentsIncludingInline (MimeHelpers.LoadMessage (body));
                if (0 < bodyAttachments.Count) {

                    foreach (var itemAttachment in McAttachment.QueryByItem(item)) {
                        Token.ThrowIfCancellationRequested ();
                        if (McAttachment.FilePresenceEnum.Complete == itemAttachment.FilePresence) {
                            // Attachment already downloaded.
                            continue;
                        }

                        // There isn't a field that is guaranteed to be in both places and is guaranteed to be
                        // unique.  Match on content ID or display name, but make sure the match is unique.
                        // Any attachment that isn't matched will just be downloaded later, which is just a
                        // performance issue, not a correctness issue.
                        bool duplicateContentId = false;
                        bool duplicateDisplayName = false;
                        MimeEntity contentIdMatch = null;
                        MimeEntity displayNameMatch = null;
                        foreach (var bodyAttachment in bodyAttachments) {
                            Token.ThrowIfCancellationRequested ();
                            if (null != bodyAttachment.ContentId && null != itemAttachment.ContentId &&
                                bodyAttachment.ContentId == itemAttachment.ContentId)
                            {
                                if (null == contentIdMatch) {
                                    contentIdMatch = bodyAttachment;
                                } else {
                                    duplicateContentId = true;
                                }
                            }
                            if (null != itemAttachment.DisplayName && null != bodyAttachment.ContentDisposition.FileName &&
                                itemAttachment.DisplayName == bodyAttachment.ContentDisposition.FileName)
                            {
                                if (null == displayNameMatch) {
                                    displayNameMatch = bodyAttachment;
                                } else {
                                    duplicateDisplayName = true;
                                }
                            }
                        }
                        MimeEntity match = duplicateContentId ? null : (contentIdMatch ?? (duplicateDisplayName ? null : displayNameMatch));
                        if (null != match) {
                            Token.ThrowIfCancellationRequested ();
                            if (match.ContentDisposition.Size > 0) {
                                itemAttachment.UpdateData ((stream) => {
                                    ((MimeKit.MimePart)match).ContentObject.DecodeTo (stream);
                                });
                                itemAttachment.SetFilePresence (McAttachment.FilePresenceEnum.Complete);
                                itemAttachment.Truncated = false;
                                itemAttachment.Update ();
                            }
                        }
                    }
                }
            }
        }

        public static bool isExchangeATTFilename (string filename)
        {
            var regex = new Regex (@"^ATT\d{5,}\.(txt|html?)$");
            if (regex.IsMatch (filename)) {
                return true;
            }
            return false;
        }
    }

    // This class is copied from MimeKit.AttachmentCollection.  Changes were made to reduce memory consumption.
    // The stream that is passed to ContentObject is one for the file on disk rather than a MemoryBlockStream.
    // The file streams are all closed in Dispose(), which means that NcAttachmentCollection must be properly
    // disposed to avoid leaking file descriptors.
    public class NcAttachmentCollection : IList<MimeEntity>, IDisposable
    {
        private readonly List<MimeEntity> attachments = new List<MimeEntity> ();
        private readonly List<Stream> openFiles = new List<Stream> ();

        public MimeEntity Add (string fileName)
        {
            if (null == fileName) {
                throw new ArgumentNullException ("fileName");
            }
            if (0 == fileName.Length) {
                throw new ArgumentException ("The specified file path is empty.", "fileName");
            }

            var attachment = new MimePart (ContentType.Parse (MimeTypes.GetMimeType (fileName)));
            attachment.FileName = Path.GetFileName (fileName);
            attachment.IsAttachment = true;
            attachment.ContentTransferEncoding = ContentEncoding.Base64;

            var stream = File.OpenRead (fileName);
            attachment.ContentObject = new ContentObject (stream);

            attachments.Add (attachment);
            openFiles.Add (stream);

            return attachment;
        }

        #region IList implementation

        public int Count {
            get { return attachments.Count; }
        }

        public bool IsReadOnly {
            get { return false; }
        }

        public MimeEntity this [int index] {
            get {
                CheckIndexRange (index, "index");
                return attachments [index];
            }
            set {
                CheckIndexRange (index, "index");
                CheckNullAttachment (value, "value");
                attachments [index] = value;
            }
        }

        public void Add (MimeEntity attachment)
        {
            CheckNullAttachment (attachment, "attachment");
            attachments.Add (attachment);
        }

        public void Clear ()
        {
            attachments.Clear ();
            CloseStreams ();
        }

        public bool Contains (MimeEntity attachment)
        {
            CheckNullAttachment (attachment, "attachment");
            return attachments.Contains (attachment);
        }

        public void CopyTo (MimeEntity[] array, int arrayIndex)
        {
            if (null == array) {
                throw new ArgumentNullException ("array");
            }
            if (0 > arrayIndex || arrayIndex >= array.Length) {
                throw new ArgumentOutOfRangeException ("arrayIndex");
            }
            attachments.CopyTo (array, arrayIndex);
        }

        public int IndexOf (MimeEntity attachment)
        {
            CheckNullAttachment (attachment, "attachment");
            return attachments.IndexOf (attachment);
        }

        public void Insert (int index, MimeEntity attachment)
        {
            CheckIndexRange (index, "index");
            CheckNullAttachment (attachment, "attachment");
            attachments.Insert (index, attachment);
        }

        public bool Remove (MimeEntity attachment)
        {
            CheckNullAttachment (attachment, "attachment");
            return attachments.Remove (attachment);
        }

        public void RemoveAt (int index)
        {
            CheckIndexRange (index, "index");
            attachments.RemoveAt (index);
        }

        #endregion

        #region IEnumerable implementation

        public IEnumerator<MimeEntity> GetEnumerator ()
        {
            return attachments.GetEnumerator ();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
        {
            return GetEnumerator ();
        }

        #endregion

        #region IDisposable implementation

        public void Dispose ()
        {
            CloseStreams ();
        }

        #endregion

        private void CloseStreams ()
        {
            foreach (var stream in openFiles) {
                stream.Dispose ();
            }
            openFiles.Clear ();
        }

        private void CheckNullAttachment (MimeEntity attachment, string argName)
        {
            if (null == attachment) {
                throw new ArgumentNullException (argName);
            }
        }

        private void CheckIndexRange (int index, string argName)
        {
            if (0 > index || index > Count) {
                throw new ArgumentOutOfRangeException (argName);
            }
        }
    }

    // This class was copied from class MimeKit.BodyBuilder.  The only changes are (1) to use NcAttachmentCollection
    // instead of MimeKit.AttachmentCollection, (2) make the class IDisposable, and (3) get rid of LinkedAttachments
    // because they aren't needed by the app code.
    public class NcMimeBodyBuilder : IDisposable
    {
        public NcMimeBodyBuilder ()
        {
            Attachments = new NcAttachmentCollection ();
        }

        public NcAttachmentCollection Attachments {
            get;
            private set;
        }

        public string TextBody {
            get;
            set;
        }

        public string HtmlBody {
            get;
            set;
        }

        public MimeEntity ToMessageBody ()
        {
            Multipart alternative = null;
            MimeEntity body = null;

            if (!string.IsNullOrEmpty (TextBody)) {
                var text = new TextPart ("plain");
                text.Text = TextBody;

                if (!string.IsNullOrEmpty (HtmlBody)) {
                    alternative = new Multipart ("alternative");
                    alternative.Add (text);
                    body = alternative;
                } else {
                    body = text;
                }
            }

            if (!string.IsNullOrEmpty (HtmlBody)) {
                var html = new TextPart ("html");
                html.ContentId = MimeKit.Utils.MimeUtils.GenerateMessageId ();
                html.Text = HtmlBody;

                if (null != alternative) {
                    alternative.Add (html);
                } else {
                    body = html;
                }
            }

            if (0 < Attachments.Count) {
                var mixed = new Multipart ("mixed");

                if (null != body) {
                    mixed.Add (body);
                }

                foreach (var attachment in Attachments) {
                    mixed.Add (attachment);
                }

                body = mixed;
            }

            return body ?? new TextPart ("plain") { Text = string.Empty };
        }

        #region IDisposable implementation

        public void Dispose ()
        {
            Attachments.Dispose ();
        }

        #endregion
    }
}
