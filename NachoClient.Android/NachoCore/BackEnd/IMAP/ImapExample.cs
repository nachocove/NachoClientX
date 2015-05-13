//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using NachoCore.Utils;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit;
using MimeKit;
using NachoClient.Build;
using NachoCore.Model;
using NachoCore.Utils;
using System.Collections.Generic;
using NachoCore.ActiveSync;

namespace NachoCore.Imap
{
    public class Imap
    {
        private int AccountId { get; set; }

        private string m_hostname { get; set; }

        private int m_port { get; set; }

        private bool m_useSsl { get; set; }

        private string m_username { get; set; }

        private string m_password { get; set; }

        private ImapClient m_imapClient;

        private ImapClient client {
            get {
                if (null == m_imapClient) {
                    Log.Error (Log.LOG_IMAP, "no imapClient set in getter");
                    GetAuthenticatedClient ();
                }
                return m_imapClient;
            }
        }

        public Imap (string hostname, int port, bool useSsl, string username, string password)
        {
            m_hostname = hostname;
            m_port = port;
            m_useSsl = useSsl;
            m_username = username;
            m_password = password;
            AccountId = 1;
            GetAuthenticatedClient ();
            DoImap ();
        }

        private void DoImap (bool readSpecial = false)
        {
            var folder = CreateFolderInPersonalNamespace ("Nacho");
            folder.Delete ();
            // The Inbox folder is always available on all IMAP servers...
            syncFolder (fromImapFolder (AccountId, client.Inbox));

            if (readSpecial) {
                foreach (SpecialFolder special in Enum.GetValues(typeof(SpecialFolder)).Cast<SpecialFolder>()) {
                    folder = client.GetFolder (special);
                    if (folder == null) {
                        Log.Error (Log.LOG_IMAP, "Could not get folder {0}", special);
                    } else {
                        syncFolder (fromImapFolder(AccountId, folder));
                    }
                }
            }
            client.Disconnect (true);
        }

        public class FolderNotFoundException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="FolderNotFoundException"/> class.
            /// </summary>
            /// <remarks>
            /// Creates a new <see cref="FolderNotFoundException"/>.
            /// </remarks>
            /// <param name="message">The error message.</param>
            /// <param name="innerException">An inner exception.</param>
            public FolderNotFoundException (string message) : base (message)
            {
            }
        }

        private void syncFolder (McFolder mcFolder)
        {
            IMailFolder folder = null;
            if (mcFolder.IsDistinguished) {
                switch (mcFolder.Type) {
                case Xml.FolderHierarchy.TypeCode.DefaultInbox_2:
                    folder = client.Inbox;
                    break;

                case Xml.FolderHierarchy.TypeCode.DefaultDrafts_3:
                    folder = client.GetFolder (SpecialFolder.Drafts);
                    break;
                }
            } else {
                // TODO Is DisplayName the correct mapping to the IMAP path?
                folder = client.GetFolder (mcFolder.DisplayName);
            }
            if (null == folder) {
                throw new FolderNotFoundException (string.Format ("Could not find folder {0}", mcFolder.DisplayName));
            }

            folder.Open (FolderAccess.ReadOnly);
            Log.Info (Log.LOG_IMAP, "Total {0} messages: {1}", folder.Name, folder.Count);
            Log.Info (Log.LOG_IMAP, "Recent {0} messages: {1}", folder.Name, folder.Recent);
            Log.Info (Log.LOG_IMAP, "UidValidity {0}: {1}", folder.Name, folder.UidValidity);
            if (folder.UidValidity != mcFolder.AsFolderSyncEpoch) {
                // TODO folder has been recreated. Dump everything and resync.
            }

            var mcMessages = McEmailMessage.QueryByFolderId<McEmailMessage> (mcFolder.AccountId, mcFolder.Id);
            if (mcMessages.Count != folder.Count) {
                // TODO something changed, so re-sync
                var query = SearchQuery.DoesNotHaveFlags (MessageFlags.Deleted);
                var uids = folder.Search (query);
                foreach (var summary in folderSummary(folder, uids)) {
                    if (!summary.UniqueId.HasValue) {
                        Log.Error (Log.LOG_IMAP, "Message does not have a UID!");
                        continue;
                    }
                    if (summary.UniqueId.Value.Validity != folder.UidValidity) {
                        Log.Error (Log.LOG_IMAP, "Message does not belong to this folder!");
                        continue;
                    }
                    var eMsg = ServerSaysAddOrChangeEmail (summary as MessageSummary, folder, mcFolder.AccountId);
                    Log.Info (Log.LOG_IMAP, "IMAP: uid {0} Flags {1} Size {2}", 
                        eMsg.ServerId,
                        summary.Flags,
                        summary.MessageSize);
                }
            }
            folder.Close ();
        }

        public class NcProtocolLogger : IProtocolLogger
        {
            public void LogConnect (Uri uri)
            {
                if (uri == null)
                    throw new ArgumentNullException ("uri");

                Log.Info (Log.LOG_IMAP, "Connected to {0}", uri);
            }

            private void logBuffer (string prefix, byte[] buffer, int offset, int count)
            {
                char[] delimiterChars = { '\n' };
                var lines = Encoding.UTF8.GetString (buffer.Skip (offset).Take (count).ToArray ()).Split (delimiterChars);

                Array.ForEach (lines, (line) => {
                    if (line.Length > 0) {
                        Log.Info (Log.LOG_IMAP, "{0}{1}", prefix, line);
                    }
                });
            }

            public void LogClient (byte[] buffer, int offset, int count)
            {
                logBuffer ("IMAP C: ", buffer, offset, count);
            }

            public void LogServer (byte[] buffer, int offset, int count)
            {
                logBuffer ("IMAP S: ", buffer, offset, count);
            }

            public void Dispose ()
            {
            }
        }

        private string dumpImapImplementation (ImapImplementation imapId)
        {
            return string.Join (", ", imapId.Properties);
        }

        public class AuthenticationException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="AuthenticationException"/> class.
            /// </summary>
            /// <remarks>
            /// Creates a new <see cref="AuthenticationException"/>.
            /// </remarks>
            /// <param name="message">The error message.</param>
            /// <param name="innerException">An inner exception.</param>
            public AuthenticationException (string message, Exception innerException) : base (message, innerException)
            {
            }
        }

        private void GetAuthenticatedClient ()
        {
            NcProtocolLogger logger = new NcProtocolLogger ();
            m_imapClient = new ImapClient (logger);
            m_imapClient.ClientCertificates = new X509CertificateCollection ();
            /*
                    2015-05-07 17:02:00.703 NachoClientiOS[70577:13497471] Info:1:: Connected to imap://imap.gmail.com:143/?starttls=when-available
                    2015-05-07 17:02:00.809 NachoClientiOS[70577:13497471] Info:1:: IMAP S: * OK Gimap ready for requests from 69.145.38.236 pw12mb32849803pab
                    2015-05-07 17:02:00.826 NachoClientiOS[70577:13497471] Info:1:: IMAP C: A00000000 CAPABILITY
                    2015-05-07 17:02:00.855 NachoClientiOS[70577:13497471] Info:1:: IMAP S: * CAPABILITY IMAP4rev1 UNSELECT IDLE NAMESPACE QUOTA ID XLIST CHILDREN X-GM-EXT-1 XYZZY SASL-IR AUTH=XOAUTH2 AUTH=PLAIN AUTH=PLAIN-CLIENTTOKEN
                    2015-05-07 17:02:00.861 NachoClientiOS[70577:13497471] Info:1:: IMAP S: A00000000 OK Thats all she wrote! pw12mb32849803pab
            */
            m_imapClient.Connect (m_hostname, m_port, m_useSsl);

            // Note: since we don't have an OAuth2 token, disable
            // the XOAUTH2 authentication mechanism.
            m_imapClient.AuthenticationMechanisms.Remove ("XOAUTH");
            m_imapClient.AuthenticationMechanisms.Remove ("XOAUTH2");
            try {
                /* Does all of this for gmail:
                    2015-05-07 16:58:42.670 NachoClientiOS[70517:13478094] Info:1:: IMAP C: A00000001 AUTHENTICATE PLAIN .....
                    2015-05-07 16:58:43.049 NachoClientiOS[70517:13478094] Info:1:: IMAP S: * CAPABILITY IMAP4rev1 UNSELECT IDLE NAMESPACE QUOTA ID XLIST CHILDREN X-GM-EXT-1 UIDPLUS XXXXXXXXXXXXXXXX ENABLE MOVE XXXXXXXXX ESEARCH UTF8=ACCEPT
                    2015-05-07 16:58:43.050 NachoClientiOS[70517:13478094] Info:1:: IMAP S: A00000001 OK jan.vilhuber@gmail.com authenticated (Success)
                    2015-05-07 16:58:43.052 NachoClientiOS[70517:13478094] Info:1:: IMAP C: A00000002 NAMESPACE
                    2015-05-07 16:58:43.227 NachoClientiOS[70517:13478094] Info:1:: IMAP S: * NAMESPACE (("" "/")) NIL NIL
                    2015-05-07 16:58:43.237 NachoClientiOS[70517:13478094] Info:1:: IMAP S: A00000002 OK Success
                    2015-05-07 16:58:43.238 NachoClientiOS[70517:13478094] Info:1:: IMAP C: A00000003 LIST "" "INBOX"
                    2015-05-07 16:58:43.418 NachoClientiOS[70517:13478094] Info:1:: IMAP S: * LIST (\HasNoChildren) "/" "INBOX"
                    2015-05-07 16:58:43.420 NachoClientiOS[70517:13478094] Info:1:: IMAP S: A00000003 OK Success
                    2015-05-07 16:58:43.421 NachoClientiOS[70517:13478094] Info:1:: IMAP C: A00000004 XLIST "" "*"
                    2015-05-07 16:58:43.604 NachoClientiOS[70517:13478094] Info:1:: IMAP S: * XLIST (\Inbox \HasNoChildren) "/" "Inbox"
                    2015-05-07 16:58:43.605 NachoClientiOS[70517:13478094] Info:1:: IMAP S: * XLIST (\HasNoChildren) "/" "Notes"
                    2015-05-07 16:58:43.606 NachoClientiOS[70517:13478094] Info:1:: IMAP S: * XLIST (\Noselect \HasChildren) "/" "[Gmail]"
                    2015-05-07 16:58:43.606 NachoClientiOS[70517:13478094] Info:1:: IMAP S: * XLIST (\AllMail \HasNoChildren) "/" "[Gmail]/All Mail"
                    2015-05-07 16:58:43.607 NachoClientiOS[70517:13478094] Info:1:: IMAP S: * XLIST (\HasNoChildren \Drafts) "/" "[Gmail]/Drafts"
                    2015-05-07 16:58:43.607 NachoClientiOS[70517:13478094] Info:1:: IMAP S: * XLIST (\Important \HasNoChildren) "/" "[Gmail]/Important"
                    2015-05-07 16:58:43.608 NachoClientiOS[70517:13478094] Info:1:: IMAP S: * XLIST (\HasNoChildren \Sent) "/" "[Gmail]/Sent Mail"
                    2015-05-07 16:58:43.612 NachoClientiOS[70517:13478094] Info:1:: IMAP S: * XLIST (\Spam \HasNoChildren) "/" "[Gmail]/Spam"
                    2015-05-07 16:58:43.613 NachoClientiOS[70517:13478094] Info:1:: IMAP S: * XLIST (\Starred \HasNoChildren) "/" "[Gmail]/Starred"
                    2015-05-07 16:58:43.614 NachoClientiOS[70517:13478094] Info:1:: IMAP S: * XLIST (\HasNoChildren \Trash) "/" "[Gmail]/Trash"
                    2015-05-07 16:58:43.614 NachoClientiOS[70517:13478094] Info:1:: IMAP S: A00000004 OK Success
                */
                m_imapClient.Authenticate (m_username, m_password);
            } catch (MailKit.Security.AuthenticationException e) {
                Log.Error (Log.LOG_IMAP, "Could not connect to server: {0}", e);
                throw new AuthenticationException ("Could not authenticate", e);
            }

            ImapImplementation clientId = new ImapImplementation () {
                Name = "NachoMail",
                Version = string.Format ("{0}:{1}", BuildInfo.Version, BuildInfo.BuildNumber),
                ReleaseDate = BuildInfo.Time,
                SupportUrl = "https://support.nachocove.com/",
                Vendor = "Nacho Cove, Inc",
            };
            Log.Info (Log.LOG_IMAP, "Client ID: {0}", dumpImapImplementation (clientId));

            ImapImplementation serverId = m_imapClient.Identify (clientId);
            Log.Info (Log.LOG_IMAP, "Server ID: {0}", dumpImapImplementation (serverId));

            foreach (FolderNamespace name in m_imapClient.PersonalNamespaces) {
                Log.Info (Log.LOG_IMAP, "PersonalNamespaces: Separator '{0}' Path '{1}'", name.DirectorySeparator, name.Path);
            }
            foreach (FolderNamespace name in m_imapClient.SharedNamespaces) {
                Log.Info (Log.LOG_IMAP, "SharedNamespaces: Separator '{0}' Path '{1}'", name.DirectorySeparator, name.Path);
            }
            foreach (FolderNamespace name in m_imapClient.OtherNamespaces) {
                Log.Info (Log.LOG_IMAP, "OtherNamespaces: Separator '{0}' Path '{1}'", name.DirectorySeparator, name.Path);
            }
        }


        private IMailFolder CreateFolderInPersonalNamespace (string name)
        {
            IMailFolder folder = null;
            if (client.PersonalNamespaces != null) {
                // TODO Not sure if this is the right thing to do. Do we loop over all of them? How do we pick?
                var personalRoot = client.GetFolder (client.PersonalNamespaces [0].Path);
                folder = personalRoot.Create (name, true);
            }
            if (folder == null) {
                throw new Exception ("FOO");
            }
            return folder;
        }

        private string findPreview (MessageSummary summary, IMailFolder folder)
        {
            string preview = string.Empty;
            var text = summary.Body as BodyPartText;
            if (null == text) {
                var multipart = summary.Body as BodyPartMultipart;
                if (null == multipart) {
                    throw new Exception ("FOO");
                }
                text = multipart.BodyParts.OfType<BodyPartText> ().FirstOrDefault ();
                if (null == text) {
                    foreach (var part in multipart.BodyParts) {
                        Log.Info (Log.LOG_IMAP, "Have Mime part {0}", part.ContentType);
                    }
                }
            }

            if (null != text) {
                if (text.Octets > 0 && text.Octets <= 255) {
                    // this will download *just* the text part
                    TextPart mimepart = folder.GetBodyPart (summary.UniqueId.Value, text) as TextPart;
                    preview = mimepart.Text;
                } else {
                    if (text.PartSpecifier == "") {
                        // HACK HACK to trick GetStream into getting us the TEXT part of the message.
                        // Otherwise, it returns all headers first, then the message
                        text.PartSpecifier = text.ContentType.MediaType;
                    }
                    var previewStr = folder.GetStream (summary.UniqueId.Value, text, 0, 255);
                    var buffer = new byte[255];
                    var read = previewStr.Read (buffer, 0, 255);
                    preview = Encoding.UTF8.GetString (buffer, 0, read);
                }
            }
            if (string.Empty != preview) {
                Log.Info (Log.LOG_IMAP, "IMAP uid {0} preview <{1}>", summary.UniqueId.Value, preview);
            } else {
                Log.Error (Log.LOG_IMAP, "IMAP uid {0} Could not find Content to make preview from", summary.UniqueId.Value);
            }
            // TODO if there wasn't a TEXT part, we'll want to look for others, like html or something.
            return preview;
        }

        public McEmailMessage ServerSaysAddOrChangeEmail (MessageSummary summary, IMailFolder folder, int AccountId)
        {
            var emailMessage = new McEmailMessage () {
                ServerId = summary.UniqueId.Value.Id.ToString (),
                AccountId = AccountId,
                Subject = summary.Envelope.Subject,
                InReplyTo = summary.Envelope.InReplyTo,
                cachedHasAttachments = summary.Attachments.Any (),
                MessageID = summary.Envelope.MessageId,
                DateReceived = summary.InternalDate.HasValue ? summary.InternalDate.Value.UtcDateTime : DateTime.MinValue,
            };

            // TODO: DRY this out. Perhaps via Reflection?
            if (summary.Envelope.To.Count > 0) {
                if (summary.Envelope.To.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} To entries in message.", summary.Envelope.To.Count);
                }
                emailMessage.To = summary.Envelope.To [0].Name;
            }
            if (summary.Envelope.Cc.Count > 0) {
                if (summary.Envelope.Cc.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} Cc entries in message.", summary.Envelope.Cc.Count);
                }
                emailMessage.Cc = summary.Envelope.Cc [0].Name;
            }
            if (summary.Envelope.Bcc.Count > 0) {
                if (summary.Envelope.Bcc.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} Bcc entries in message.", summary.Envelope.Bcc.Count);
                }
                emailMessage.Bcc = summary.Envelope.Bcc [0].Name;
            }
            if (summary.Envelope.From.Count > 0) {
                if (summary.Envelope.From.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} From entries in message.", summary.Envelope.From.Count);
                }
                emailMessage.From = summary.Envelope.From [0].Name;
            }
            if (summary.Envelope.ReplyTo.Count > 0) {
                if (summary.Envelope.ReplyTo.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} ReplyTo entries in message.", summary.Envelope.ReplyTo.Count);
                }
                emailMessage.ReplyTo = summary.Envelope.ReplyTo [0].Name;
            }
            if (summary.Envelope.Sender.Count > 0) {
                if (summary.Envelope.Sender.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} Sender entries in message.", summary.Envelope.Sender.Count);
                }
                emailMessage.Sender = summary.Envelope.Sender [0].Name;
            }
            if (null != summary.References && summary.References.Count > 0) {
                if (summary.References.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} References entries in message.", summary.References.Count);
                }
                emailMessage.References = summary.References [0];
            }
                                
            if (summary.Flags.HasValue && (summary.Flags.Value & MessageFlags.None) != MessageFlags.None) {
                if ((summary.Flags.Value & MessageFlags.Seen) == MessageFlags.Seen) {
                    emailMessage.IsRead = true;
                }
                // TODO Where do we set these flags?
                if ((summary.Flags.Value & MessageFlags.Answered) == MessageFlags.Answered) {
                }
                if ((summary.Flags.Value & MessageFlags.Flagged) == MessageFlags.Flagged) {
                }
                if ((summary.Flags.Value & MessageFlags.Deleted) == MessageFlags.Deleted) {
                }
                if ((summary.Flags.Value & MessageFlags.Draft) == MessageFlags.Draft) {
                }
                if ((summary.Flags.Value & MessageFlags.Recent) == MessageFlags.Recent) {
                }
                if ((summary.Flags.Value & MessageFlags.UserDefined) == MessageFlags.UserDefined) {
                    // TODO See if these are handled by the summary.UserFlags
                }
            }
            if (null != summary.UserFlags && summary.UserFlags.Count > 0) {
                // TODO Where do we set these flags?
            }

            if (null != summary.Headers) {
                foreach (var header in summary.Headers) {
                    Log.Info (Log.LOG_IMAP, "IMAP header id {0} {1} {2}", header.Id, header.Field, header.Value);
                    switch (header.Id) {
                    case HeaderId.ContentClass:
                        emailMessage.ContentClass = header.Value;
                        break;

                    case HeaderId.Importance:
                        switch (header.Value) {
                        case "low":
                            emailMessage.Importance = NcImportance.Low_0;
                            break;

                        case "normal":
                            emailMessage.Importance = NcImportance.Normal_1;
                            break;

                        case "high":
                            emailMessage.Importance = NcImportance.High_2;
                            break;

                        default:
                            Log.Error (Log.LOG_IMAP, string.Format ("Unknown importance header value '{0}'", header.Value));
                            break;
                        }
                        break;
                    }
                }
            }

            if (summary.GMailThreadId.HasValue) {
                // TODO Where to put the thread ID? Is it the ThreadTopic? Seems unlikely..
                emailMessage.ThreadTopic = summary.GMailThreadId.Value.ToString ();
            }

            if ("" == emailMessage.MessageID && summary.GMailMessageId.HasValue) {
                emailMessage.MessageID = summary.GMailMessageId.Value.ToString ();
            }

            emailMessage.BodyPreview = findPreview (summary, folder);

            // TODO common code with AS. Abstract.
            McEmailAddress fromEmailAddress;
            if (McEmailAddress.Get (AccountId, emailMessage.From, out fromEmailAddress)) {
                emailMessage.FromEmailAddressId = fromEmailAddress.Id;
                emailMessage.cachedFromLetters = EmailHelper.Initials (emailMessage.From);
                emailMessage.cachedFromColor = fromEmailAddress.ColorIndex;
            } else {
                emailMessage.FromEmailAddressId = 0;
                emailMessage.cachedFromLetters = "";
                emailMessage.cachedFromColor = 1;
            }

            emailMessage.SenderEmailAddressId = McEmailAddress.Get (AccountId, emailMessage.Sender);
            emailMessage.IsIncomplete = false;

            // TODO insert and transaction stuff
            return emailMessage;
        }

        private McFolder fromImapFolder (int AccountId, IMailFolder folder)
        {
            folder.Open (FolderAccess.ReadOnly);

            // TODO Look up folder in DB first.
            var mcFolder = new McFolder () {
                DisplayName = folder.Name,
                //ParentId = folder.ParentFolder, // Need to look up the parent folder to get the ID
                AccountId = AccountId,
                AsFolderSyncEpoch = folder.UidValidity,
                IsDistinguished = !folder.IsNamespace,
            };
            if (!folder.IsNamespace) {
                mcFolder.IsDistinguished = true;
                switch (folder.Name) {
                case "INBOX":
                    mcFolder.Type = Xml.FolderHierarchy.TypeCode.DefaultInbox_2;
                    break;

                case McFolder.DRAFTS_DISPLAY_NAME:
                    mcFolder.Type = Xml.FolderHierarchy.TypeCode.DefaultDrafts_3;
                    break;
                }
            } else {
                mcFolder.IsDistinguished = false;
                mcFolder.DisplayName = folder.Name;
            }
            return mcFolder;
        }

        private IList<IMessageSummary> folderSummary (IMailFolder folder, IList<UniqueId> uids)
        {
            HashSet<MimeKit.HeaderId> headerFields = new HashSet<MimeKit.HeaderId> ();
            headerFields.Add (HeaderId.ContentClass);
            headerFields.Add (HeaderId.Importance);

            MessageSummaryItems flags = MessageSummaryItems.BodyStructure
                                        | MessageSummaryItems.Envelope
                                        | MessageSummaryItems.Flags
                                        | MessageSummaryItems.InternalDate
                                        | MessageSummaryItems.MessageSize
                                        | MessageSummaryItems.UniqueId
                                        | MessageSummaryItems.GMailMessageId
                                        | MessageSummaryItems.GMailThreadId;
            
            // IMAP C: A00000011 UID FETCH 8622:8623,8630:8641 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE X-GM-MSGID X-GM-THRID BODY.PEEK[HEADER.FIELDS (CONTENT-CLASS PRIORITY IMPORTANCE)])
            return folder.Fetch (uids, flags, headerFields);
        }

        //            var query = SearchQuery.DeliveredAfter (DateTime.Parse ("2013-01-12"))
        //                .And (SearchQuery.SubjectContains ("MailKit")).And (SearchQuery.Seen);
        //
        //            foreach (var uid in inbox.Search (query)) {
        //                var message = inbox.GetMessage (uid);
        //                Console.WriteLine ("[match] {0}: {1}", uid, message.Subject);
        //            }
        //
        //            // let's do the same search, but this time sort them in reverse arrival order
        //            var orderBy = new [] { OrderBy.ReverseArrival };
        //            foreach (var uid in inbox.Search (query, orderBy)) {
        //                var message = inbox.GetMessage (uid);
        //                Console.WriteLine ("[match] {0}: {1}", uid, message.Subject);
        //            }
        //
        //            // you'll notice that the orderBy argument is an array... this is because you
        //            // can actually sort the search results based on multiple columns:
        //            orderBy = new [] { OrderBy.ReverseArrival, OrderBy.Subject };
        //            foreach (var uid in inbox.Search (query, orderBy)) {
        //                var message = inbox.GetMessage (uid);
        //                Console.WriteLine ("[match] {0}: {1}", uid, message.Subject);
        //            }
    }
}

