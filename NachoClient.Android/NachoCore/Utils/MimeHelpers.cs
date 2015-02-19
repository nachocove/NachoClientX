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
                var mimeMessage = tnef.ConvertToMessage ();
                return FindTextPartWithSubtype (mimeMessage.Body, subtype);
            }
            if (entity is TextPart && entity.ContentType.Matches ("text", subtype)) {
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
                error = "Nacho Mail has not downloaded the body of this message yet.\n" + message.GetBodyPreviewOrEmpty ();
                return null;
            }

            if (McAbstrFileDesc.BodyTypeEnum.None == body.BodyType) {
                return "";
            }

            if (McAbstrFileDesc.BodyTypeEnum.PlainText_1 == body.BodyType) {
                return body.GetContentsString ();
            }

            if (McAbstrFileDesc.BodyTypeEnum.HTML_2 == body.BodyType) {
                error = "Nacho Mail has not converted the HTML to reply text.\n" + message.GetBodyPreviewOrEmpty ();
                return null;
            }

            if (McAbstrFileDesc.BodyTypeEnum.RTF_3 == body.BodyType) {
                error = "Nacho Mail has not converted the RTF to reply text.\n" + message.GetBodyPreviewOrEmpty ();
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
                } else if (message.Body.ContentType.Matches ("multipart", "mixed")) {
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
            if (entity.ContentType.Matches ("text", "plain") && !alreadyReplaced) {
                ((TextPart)entity).Text = text;
                return true;
            }
            if (entity.ContentType.Matches ("text", "*")) {
                RemoveEntity (entity, parent, message);
                return false;
            }
            if (entity is MimeKit.Tnef.TnefPart) {
                // Replace the TNEF part with an equivalent MIME entity
                var tnef = entity as MimeKit.Tnef.TnefPart;
                var tnefAsMime = tnef.ConvertToMessage ();
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
                if (multipart.ContentType.Matches ("multipart", "mixed") ||
                    multipart.ContentType.Matches ("multipart", "alternative")) {
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

            var msg = new McEmailMessage ();
            msg.AccountId = AccountId;
            msg.To = CommaSeparatedList (mimeMessage.To);
            msg.Cc = CommaSeparatedList (mimeMessage.Cc);
            msg.From = CommaSeparatedList (mimeMessage.From);
            msg.Subject = mimeMessage.Subject;

            // Create body
            var body = McBody.InsertFile (AccountId, McAbstrFileDesc.BodyTypeEnum.MIME_4, (FileStream stream) => {
                mimeMessage.WriteTo (stream);
            });
            msg.BodyId = body.Id;

            msg.Insert ();

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
                if (multipart.ContentType.Matches ("multipart", "related") && 0 < multipart.Count) {
                    // See https://tools.ietf.org/html/rfc2387
                    // This isn't entirely correct. The multipart/related could have a "start"
                    // parameter that points to the root entity that is not the first one.
                    // But it is hard to write code to handle that without having a real life
                    // message to test with.
                    MimeEntityDisplayList (multipart [0], ref list);
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

            // The conversion from TNEF to MIME will sometimes create a TextPart
            // with a null ContentObject, which will result in a NullReferenceException
            // when accessing the Text property.  Render all non-calendar TextParts,
            // except for those bogus ones.
            if (part is TextPart && null != part.ContentObject && !part.ContentType.Matches ("text", "calendar")) {
                list.Add (part);
                return;
            }

            if (part is MimeKit.Tnef.TnefPart) {
                // Convert the TNEF stuff into a MIME message, and look through that.
                try {
                    MimeMessage tnef = (part as MimeKit.Tnef.TnefPart).ConvertToMessage ();
                    if (null != tnef.Body) {
                        FixTnefMessage (tnef);
                        MimeDisplayList (tnef, ref list);
                    }
                } catch (Exception e) {
                    // Parsing the TNEF has failed with an ArgumentOutOfRangeException before.  If the
                    // TNEF can't be parsed, log an error but otherwise ignore the problem.  There is
                    // nothing we can do to extract the data.
                    Log.Error (Log.LOG_CALENDAR, "Parsing of TNEF section failed: {0}", e.ToString ());
                }
                return;
            }

            if (entity.ContentType.Matches ("image", "*")) {
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
        /// Pick the best alternative to be displayed, which is always supposed to be
        /// the last one in the list.  But we want to ignore calendar entries (which are
        /// handled a different way).
        /// </summary>
        protected static void MimeBestAlternativeDisplayList (Multipart multipart, ref List<MimeEntity> list)
        {
            var last = multipart.Last ();
            if (last.ContentType.Matches ("text", "calendar")) {
                if (1 < multipart.Count) {
                    var nextToLast = multipart [multipart.Count - 2];
                    MimeEntityDisplayList (nextToLast, ref list);
                }
            } else {
                MimeEntityDisplayList (last, ref list);
            }
        }

        private static void FixTnefMessage (MimeMessage tnefMessage)
        {
            // If the TNEF part contains text in multiple formats, MimeKit will create a multipart/mixed
            // instead of a multipart/alternative.  Fix that.
            if (null == tnefMessage.Body || !tnefMessage.Body.ContentType.Matches ("multipart", "mixed")) {
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

        public static MimePart EntityWithContentId (MimeMessage message, string contentId)
        {
            return EntityWithContentId (message.Body, contentId);
        }

        public static MimePart EntityWithContentId (MimeEntity root, string contentId)
        {
            if (null == root) {
                return null;
            }
            if (root is MimePart && root.ContentId == contentId) {
                return (MimePart)root;
            }
            if (root is Multipart) {
                foreach (var subentity in (Multipart)root) {
                    var match = EntityWithContentId (subentity, contentId);
                    if (null != match) {
                        return match;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Find all the attachments in the given MIME message, including those
        /// nested inside a TNEF part.
        /// </summary>
        /// <param name="message">The MIME message to be searched.</param>
        public static List<MimeEntity> AllAttachments (MimeMessage message)
        {
            List<MimeEntity> result = new List<MimeEntity> ();
            FindAttachments (message.Body, result);
            return result;
        }

        private static void FindAttachments (MimeEntity entity, List<MimeEntity> result)
        {
            if (null != entity.ContentDisposition && entity.ContentDisposition.IsAttachment) {
                result.Add (entity);
            } else if (entity is MimeKit.Tnef.TnefPart) {
                // Pull apart the TNEF part and see what is inside.
                var tnef = entity as MimeKit.Tnef.TnefPart;
                var mimeMessage = tnef.ConvertToMessage ();
                FindAttachments (mimeMessage.Body, result);
            } else if (entity is Multipart) {
                foreach (var subpart in entity as Multipart) {
                    FindAttachments (subpart, result);
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
            if (existingBody.ContentType.Matches ("multipart", "mixed")) {
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
    }
}



