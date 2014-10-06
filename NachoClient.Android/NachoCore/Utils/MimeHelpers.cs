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

        /// <summary>
        /// Loads a MIME message from a file.
        /// </summary>
        /// <remarks>
        /// If the file doesn't exist, can't be read, or can't be parsed as a MIME message, then an
        /// empty MIME message is returned.
        /// </remarks>
        /// <returns>The MIME message.</returns>
        /// <param name="path">The path to the file containing the text of the MIME message.</param>
        public static MimeMessage LoadMessage (string path)
        {
            try {
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                    return MimeMessage.Load (fileStream);
                }
            } catch {
                var emptyMessage = new MimeMessage ();
                emptyMessage.Body = new TextPart ("plain") {
                    Text = ""
                };
                return emptyMessage;
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

        /// <summary>
        /// Return the first "text/plain" entity found in the given subtree, or null if none was found.
        /// </summary>
        /// <param name="entity">The portion of the MIME message to search.</param>
        static protected TextPart FindTextPart (MimeEntity entity)
        {
            if (null == entity) {
                return null;
            }
            if (entity is MimeKit.Tnef.TnefPart) {
                // Pull apart the TNEF part and see what is inside.
                var tnef = entity as MimeKit.Tnef.TnefPart;
                var mimeMessage = tnef.ConvertToMessage ();
                return FindTextPart (mimeMessage.Body);
            }
            if (entity is TextPart && entity.ContentType.Matches ("text", "plain")) {
                TextPart textPart = entity as TextPart;
                if (null != textPart && null != textPart.ContentObject) {
                    return textPart;
                } else {
                    return null;
                }
            }
            if (entity is Multipart) {
                foreach (var subpart in entity as Multipart) {
                    var textPart = FindTextPart (subpart);
                    if (null != textPart) {
                        return textPart;
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
            return ExtractTextPart (LoadMessage (messageBody.GetFilePath ()));
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
            var textPart = FindTextPart (message.Body);
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
            var body = McBody.InsertFile (AccountId, (FileStream stream) => {
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

            if (part is MimeKit.Tnef.TnefPart) {
                // Convert the TNEF stuff into a MIME message, and look through that.
                MimeMessage tnef = (part as MimeKit.Tnef.TnefPart).ConvertToMessage ();
                if (null != tnef.Body) {
                    FixTnefMessage (tnef);
                    MimeDisplayList (tnef, ref list);
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
        /// the last one in the list.
        /// </summary>
        /// <description>
        /// If the best alternative is a calendar entry, then also select the next best
        /// item, since we want to display that one as well.
        /// </description>
        protected static void MimeBestAlternativeDisplayList (Multipart multipart, ref List<MimeEntity> list)
        {
            var last = multipart.Last ();
            MimeEntityDisplayList (last, ref list);
            if (1 < multipart.Count && last.ContentType.Matches ("text", "calendar")) {
                var nextToLast = multipart [multipart.Count - 2];
                MimeEntityDisplayList (nextToLast, ref list);
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



