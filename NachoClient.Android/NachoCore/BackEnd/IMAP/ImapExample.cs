//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit;
using MimeKit;
using NachoClient.Build;
using NachoPlatform;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using System.Collections.Generic;
using NachoCore.ActiveSync;
using NachoCore.Brain;

namespace NachoCore.IMAPExample
{
    public class NcImap
    {
        public int AccountId { get; set; }

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

        NcStateMachine Sm { get; set; }

        public NcImap (int accountId, string hostname, int port, bool useSsl, string username, string password)
        {
            m_hostname = hostname;
            m_port = port;
            m_useSsl = useSsl;
            m_username = username;
            m_password = password;
            AccountId = accountId;
        }

        public async void ConnectAsync (CancellationToken cToken)
        {
            ImapProtocolLogger logger = new ImapProtocolLogger ();
            m_imapClient = new ImapClient (logger);
            m_imapClient.ClientCertificates = new X509CertificateCollection ();
            /*
                    2015-05-07 17:02:00.703 NachoClientiOS[70577:13497471] Info:1:: Connected to imap://imap.gmail.com:143/?starttls=when-available
                    2015-05-07 17:02:00.809 NachoClientiOS[70577:13497471] Info:1:: IMAP S: * OK Gimap ready for requests from 69.145.38.236 pw12mb32849803pab
                    2015-05-07 17:02:00.826 NachoClientiOS[70577:13497471] Info:1:: IMAP C: A00000000 CAPABILITY
                    2015-05-07 17:02:00.855 NachoClientiOS[70577:13497471] Info:1:: IMAP S: * CAPABILITY IMAP4rev1 UNSELECT IDLE NAMESPACE QUOTA ID XLIST CHILDREN X-GM-EXT-1 XYZZY SASL-IR AUTH=XOAUTH2 AUTH=PLAIN AUTH=PLAIN-CLIENTTOKEN
                    2015-05-07 17:02:00.861 NachoClientiOS[70577:13497471] Info:1:: IMAP S: A00000000 OK Thats all she wrote! pw12mb32849803pab
            */
            try {
                var task = m_imapClient.ConnectAsync (m_hostname, m_port, m_useSsl, cToken);
                task.ConfigureAwait (false);
                task.Wait ();
            } catch (ImapProtocolException e) {
                Log.Error (Log.LOG_IMAP, "Connect command failed {0}", e);
                throw;
            }
        }

        public async void AuthenticateAndIdAsync (CancellationToken cToken)
        {
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
                if (cToken.IsCancellationRequested) {
                    return;
                }
                var authtask = m_imapClient.AuthenticateAsync (m_username, m_password, cToken);
                authtask.ConfigureAwait (false);
                authtask.Wait ();
                if (cToken.IsCancellationRequested) {
                    return;
                }
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

            var idtask = m_imapClient.IdentifyAsync (clientId, cToken);
            idtask.ConfigureAwait (false);
            idtask.Wait ();
            var serverId = idtask.Result;
            if (cToken.IsCancellationRequested) {
                return;
            }
            Log.Info (Log.LOG_IMAP, "Server ID: {0}", dumpImapImplementation (serverId));
        }

        public void DoImap (bool readSpecial = false)
        {
            var cts = new CancellationTokenSource ();
            ConnectAsync (cts.Token);
            AuthenticateAndIdAsync (cts.Token);

            IMailFolder folder;

            folder = client.GetFolder ("Nacho");
            if (null != folder) {
                folder.Delete ();
            }

            folder = CreateFolderInPersonalNamespace ("Nacho");
            folder.Delete ();

            // The Inbox folder is always available on all IMAP servers...
            SyncFolder (fromImapFolder (AccountId, client.Inbox));

            if (readSpecial) {
                foreach (SpecialFolder special in Enum.GetValues(typeof(SpecialFolder)).Cast<SpecialFolder>()) {
                    folder = client.GetFolder (special);
                    if (folder == null) {
                        Log.Error (Log.LOG_IMAP, "Could not get folder {0}", special);
                    } else {
                        SyncFolder (fromImapFolder (AccountId, folder));
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

        public void SyncFolder (McFolder mcFolder)
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

            // TODO Do not do this. Figure out a different way.
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
                    var eMsg = ServerSaysAddOrChangeEmail (summary as MessageSummary, mcFolder);
                    Log.Info (Log.LOG_IMAP, "IMAP: uid {0} Flags {1} Size {2}", 
                        eMsg.ServerId,
                        summary.Flags,
                        summary.MessageSize);
                    eMsg.BodyPreview = findPreview (summary as MessageSummary, folder);
                }
            }
            folder.Close ();
        }

        public class ImapProtocolLogger : IProtocolLogger
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
            ImapProtocolLogger logger = new ImapProtocolLogger ();
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
            Log.Info (Log.LOG_IMAP, "UID {0} TextBody {1} HtmlBody {2}", summary.UniqueId, summary.TextBody, summary.HtmlBody);
            string preview = string.Empty;

            preview = findPreviewTexBody (summary, folder);
            if (string.Empty != preview) {
                return preview;
            }

            preview = findPreviewHtml (summary, folder);
            if (string.Empty != preview) {
                return preview;
            }

            preview = findPreviewText (summary, folder);
            if (string.Empty != preview) {
                return preview;
            }

            if (string.Empty != preview) {
                Log.Info (Log.LOG_IMAP, "IMAP uid {0} preview <{1}>", summary.UniqueId.Value, preview);
            } else {
                Log.Error (Log.LOG_IMAP, "IMAP uid {0} Could not find Content to make preview from", summary.UniqueId.Value);
            }
            return preview;
        }

        private string findPreviewText (MessageSummary summary, IMailFolder folder)
        {
            string preview = string.Empty;
            BodyPartText text;
            text = summary.Body as BodyPartText;
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
                try {
                    var previewStr = folder.GetStream (summary.UniqueId.Value, text, true, 0, 255);
                    var buffer = new byte[255];
                    var read = previewStr.Read (buffer, 0, 255);
                    preview = Encoding.UTF8.GetString (buffer, 0, read);
                } catch (ImapCommandException e) {
                    Log.Error (Log.LOG_IMAP, "{0}", e);
                }
            }
            return preview;
        }

        private string findPreviewTexBody (MessageSummary summary, IMailFolder folder)
        {
            string preview = string.Empty;
            var text = summary.TextBody;
            if (null != text && text.Octets < 4096) {
                TextPart mime = folder.GetBodyPart (summary.UniqueId.Value, text) as TextPart;
                if (null != mime) {
                    Log.Info (Log.LOG_IMAP, "TEXT {0}", mime.Text);
                }
                //preview = mime.Text.Substring (0, 255);
            }
            return preview;
        }

        private string findPreviewHtml (MessageSummary summary, IMailFolder folder)
        {
            string preview = string.Empty;
            var html = summary.HtmlBody;
            if (null != html && html.Octets < 10240) {
                TextPart mime = folder.GetBodyPart (summary.UniqueId.Value, html) as TextPart;
                if (null != mime) {
                    Log.Info (Log.LOG_IMAP, "HTML {0}", mime.Text);
                }
            }
            return preview;
        }

        public NcResult ParseEmail (MessageSummary summary)
        {
            var emailMessage = new McEmailMessage () {
                ServerId = summary.UniqueId.Value.Id.ToString (),
                AccountId = AccountId,
                Subject = summary.Envelope.Subject,
                InReplyTo = summary.Envelope.InReplyTo,
                cachedHasAttachments = summary.Attachments.Any (),
                MessageID = summary.Envelope.MessageId,
                DateReceived = summary.InternalDate.HasValue ? summary.InternalDate.Value.UtcDateTime : DateTime.MinValue,
                FromEmailAddressId = 0,
                cachedFromLetters = "",
                cachedFromColor = 1,
            };

            // TODO: DRY this out. Perhaps via Reflection?
            if (summary.Envelope.To.Count > 0) {
                if (summary.Envelope.To.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} To entries in message.", summary.Envelope.To.Count);
                }
                emailMessage.To = ((MailboxAddress)summary.Envelope.To [0]).Address;
            }
            if (summary.Envelope.Cc.Count > 0) {
                if (summary.Envelope.Cc.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} Cc entries in message.", summary.Envelope.Cc.Count);
                }
                emailMessage.Cc = ((MailboxAddress)summary.Envelope.Cc [0]).Address;
            }
            if (summary.Envelope.Bcc.Count > 0) {
                if (summary.Envelope.Bcc.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} Bcc entries in message.", summary.Envelope.Bcc.Count);
                }
                emailMessage.Bcc = ((MailboxAddress)summary.Envelope.Bcc [0]).Address;
            }

            McEmailAddress fromEmailAddress;
            if (summary.Envelope.From.Count > 0) {
                if (summary.Envelope.From.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} From entries in message.", summary.Envelope.From.Count);
                }
                emailMessage.From = ((MailboxAddress)summary.Envelope.From [0]).Address;
                if (McEmailAddress.Get (AccountId, summary.Envelope.From [0] as MailboxAddress, out fromEmailAddress)) {
                    emailMessage.FromEmailAddressId = fromEmailAddress.Id;
                    emailMessage.cachedFromLetters = EmailHelper.Initials (emailMessage.From);
                    emailMessage.cachedFromColor = fromEmailAddress.ColorIndex;
                }
            }

            if (summary.Envelope.ReplyTo.Count > 0) {
                if (summary.Envelope.ReplyTo.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} ReplyTo entries in message.", summary.Envelope.ReplyTo.Count);
                }
                emailMessage.ReplyTo = ((MailboxAddress)summary.Envelope.ReplyTo [0]).Address;
            }
            if (summary.Envelope.Sender.Count > 0) {
                if (summary.Envelope.Sender.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} Sender entries in message.", summary.Envelope.Sender.Count);
                }
                emailMessage.Sender = ((MailboxAddress)summary.Envelope.Sender [0]).Address;
                if (McEmailAddress.Get (AccountId, summary.Envelope.Sender [0] as MailboxAddress, out fromEmailAddress)) {
                    emailMessage.SenderEmailAddressId = fromEmailAddress.Id;
                }
            }
            if (null != summary.References && summary.References.Count > 0) {
                if (summary.References.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} References entries in message.", summary.References.Count);
                }
                emailMessage.References = summary.References [0];
            }

            if (summary.Flags.HasValue) {
                if ((summary.Flags.Value & MessageFlags.None) != MessageFlags.None) {
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
                emailMessage.ConversationId = summary.GMailThreadId.Value.ToString ();
            }

            if ("" == emailMessage.MessageID && summary.GMailMessageId.HasValue) {
                emailMessage.MessageID = summary.GMailMessageId.Value.ToString ();
            }
            emailMessage.IsIncomplete = false;

            return NcResult.OK (emailMessage);
        }
        
        public void InsertAttachments (McEmailMessage msg)
        {
            
        }

        public McEmailMessage ServerSaysAddOrChangeEmail (MessageSummary summary, McFolder folder)
        {
            var ServerId = summary.UniqueId;

            if (null == ServerId || string.Empty == ServerId.Value.ToString ()) {
                Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: No ServerId present.");
                return null;
            }
            // If the server attempts to overwrite, delete the pre-existing record first.
            var eMsg = McEmailMessage.QueryByServerId<McEmailMessage> (folder.AccountId, ServerId.Value.ToString ());
            if (null != eMsg) {
                eMsg.Delete ();
                eMsg = null;
            }

            McEmailMessage emailMessage = null;
            try {
                var r = ParseEmail (summary);
                emailMessage = r.GetValue<McEmailMessage> ();
            } catch (Exception ex) {
                Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: Exception parsing: {0}", ex.ToString ());
                if (null == emailMessage || null == emailMessage.ServerId || string.Empty == emailMessage.ServerId) {
                    emailMessage = new McEmailMessage () {
                        ServerId = summary.UniqueId.Value.ToString (),
                    };
                }
                emailMessage.IsIncomplete = true;
            }

            // TODO move the rest to parent class or into the McEmailAddress class before insert or update?
            NcModel.Instance.RunInTransaction (() => {
                if ((0 != emailMessage.FromEmailAddressId) || !String.IsNullOrEmpty(emailMessage.To)) {
                    if (!folder.IsJunkFolder ()) {
                        NcContactGleaner.GleanContactsHeaderPart1 (emailMessage);
                    }
                }

                bool justCreated = false;
                if (null == eMsg) {
                    justCreated = true;
                    emailMessage.AccountId = folder.AccountId;
                }
                if (justCreated) {
                    emailMessage.Insert ();
                    folder.Link (emailMessage);
                    InsertAttachments (emailMessage);
                } else {
                    emailMessage.AccountId = folder.AccountId;
                    emailMessage.Id = eMsg.Id;
                    folder.UpdateLink (emailMessage);
                    emailMessage.Update ();
                }
            });

            if (!emailMessage.IsIncomplete) {
                // Extra work that needs to be done, but doesn't need to be in the same database transaction.
            }

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

namespace NachoCore.IMAPExample
{
    public interface IImapCommand
    {
        void Execute (NcStateMachine sm);

        void Cancel ();
    }
}

namespace NachoCore.IMAPExample
{
    public partial class ImapProtoControl : NcProtoControl
    {
        public ImapProtoControl ProtoControl { set; get; }

        private CancellationTokenSource Cts;

        NcImap client { get; set; }


        public enum Lst : uint
        {
            DiscW = (St.Last + 1),
            // TODO Move to parent
            UiDCrdW,
            UiPCrdW,
            UiServConfW,
            UiCertOkW,
            SettingsW,
            Pick,
            SyncW,
            QOpW,
            HotQOpW,
            // we are active, but choosing not to execute.
            IdleW,
            // we are not active. when we re-activate on Launch, we pick-up at the saved state.
            // TODO: make Parked part of base SM functionality.
            Parked,
        }

        public override BackEndStateEnum BackEndState {
            get {
                var state = Sm.State;
                if ((uint)Lst.Parked == state) {
                    state = ProtocolState.ProtoControlState;
                }
                // Every state above must be mapped here.
                switch (state) {
                case (uint)St.Start:
                    return BackEndStateEnum.NotYetStarted;

                case (uint)Lst.DiscW:
                    return BackEndStateEnum.Running;

                case (uint)Lst.UiDCrdW:
                case (uint)Lst.UiPCrdW:
                    return BackEndStateEnum.CredWait;

                case (uint)Lst.UiServConfW:
                    return BackEndStateEnum.ServerConfWait;

                case (uint)Lst.UiCertOkW:
                    return BackEndStateEnum.CertAskWait;

                case (uint)Lst.SettingsW:
                case (uint)Lst.Pick:
                case (uint)Lst.SyncW:
                case (uint)Lst.QOpW:
                case (uint)Lst.HotQOpW:
                case (uint)Lst.IdleW:
                    return (ProtocolState.HasSyncedInbox) ? 
                        BackEndStateEnum.PostAutoDPostInboxSync : 
                        BackEndStateEnum.PostAutoDPreInboxSync;

                default:
                    NcAssert.CaseError (string.Format ("Unhandled state {0}", Sm.State));
                    return BackEndStateEnum.PostAutoDPostInboxSync;
                }
            }
        }

        public class ImapEvt : PcEvt
        {
            new public enum E : uint
            {
                ReDisc = (PcEvt.E.Last + 1),
                ReProv,
                ConnW,
                AuthW,
                AuthFail,
                Auth,
                // base IMAP state
                SelectW,
                Selected,
                // base IMAP state for folder actions (marking messages, syncing, etc)
                Last = Selected,
            };
        }

        // Events of the form UiXxYy are events coming directly from the UI/App toward the controller.
        // DB-based events (even if UI-driven) and server-based events lack the Ui prefix.
        public class CtlEvt : ImapEvt
        {
            // QUESTION: Why the various event classes? CtlEvt, ImapEvt, Lst, etc.
            new public enum E : uint
            {
                UiSetCred = (ImapEvt.E.Last + 1),
                GetServConf,
                UiSetServConf,
                GetCertOk,
                UiCertOkYes,
                UiCertOkNo,
                ReFSync,
                PkPing,
                PkQOp,
                PkHotQOp,
                PkFetch,
                PkWait,
                Cancel,
            };
        }

        public ImapProtoControl (INcProtoControlOwner owner, NcImap imap_client) : base (owner, imap_client.AccountId)
        {
            ProtoControl = this;
            EstablishService ();
            client = imap_client;
            Cts = new CancellationTokenSource ();

            /*
             * State Machine design:
             * * Events from the UI can come at ANY time. They are not always relevant, and should be dropped when not.
             * * ForceStop can happen at any time, and must Cancel anything that is going on immediately.
             * * ForceSync can happen at any time, and must Cancel anything that is going on immediately and initiate Sync.
             * * Objects can be added to the McPending Q at any time.
             * * All other events must come from the orderly completion of commands or internal forced transitions.
             * 
             * The SM Q is an event-Q not a work-Q. Where we need to "remember" to do more than one thing, that
             * memory must be embedded in the state machine.
             * 
             * Sync, Provision, Discovery and FolderSync can be forced by posting the appropriate event.
             * 
             * TempFail: for scenarios where a command can return TempFail, just keep re-trying:
             *  - NcCommStatus will eventually shut us down as TempFail counts against Quality. 
             *  - Max deferrals on pending will pull "bad" pendings out of the Q.
             */
            Sm = new NcStateMachine ("IMAPC") { 
                Name = string.Format ("IMAPC({0})", AccountId),
                LocalEventType = typeof(CtlEvt),
                LocalStateType = typeof(Lst),
                StateChangeIndication = UpdateSavedState,
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.UiCertOkNo, 
                            (uint)CtlEvt.E.UiCertOkYes,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)ImapEvt.E.ReProv,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOp,
                            (uint)CtlEvt.E.PkHotQOp,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                            (uint)CtlEvt.E.Cancel,
                            (uint)ImapEvt.E.ConnW,
                            (uint)ImapEvt.E.AuthW,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.Auth,
                            (uint)ImapEvt.E.SelectW,
                            (uint)ImapEvt.E.Selected,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },
                    // There is no HardFail. Can't pass DiscW w/out a working server - period.
                    new Node {
                        State = (uint)Lst.DiscW, 
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOp,
                            (uint)CtlEvt.E.PkHotQOp,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)ImapEvt.E.ReProv,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.Cancel,
                            (uint)ImapEvt.E.ConnW,
                            (uint)ImapEvt.E.AuthW,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.Auth,
                            (uint)ImapEvt.E.SelectW,
                            (uint)ImapEvt.E.Selected,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoConn, State = (uint)ImapEvt.E.ConnW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans {
                                Event = (uint)ImapEvt.E.AuthFail,
                                Act = DoUiCredReq,
                                State = (uint)Lst.UiDCrdW
                            },
                            new Trans {
                                Event = (uint)CtlEvt.E.GetCertOk,
                                Act = DoUiCertOkReq,
                                State = (uint)Lst.UiCertOkW
                            },
                            new Trans {
                                Event = (uint)CtlEvt.E.UiSetServConf,
                                Act = DoSetServConf,
                                State = (uint)Lst.DiscW
                            },
                            new Trans { Event = (uint)CtlEvt.E.UiSetCred, Act = DoSetCred, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                        }
                    },
                    new Node {
                        State = (uint)ImapEvt.E.ConnW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.ReProv,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOp,
                            (uint)CtlEvt.E.PkHotQOp,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoAuth, State = (uint)ImapEvt.E.AuthW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                        }
                    },
                    new Node {
                        State = (uint)ImapEvt.E.AuthW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.ReProv,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOp,
                            (uint)CtlEvt.E.PkHotQOp,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPark, State = (uint)ImapEvt.E.Auth },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                        }
                    },
                }
            };
            Sm.Validate ();
            Sm.State = ProtocolState.ProtoControlState;
            McPending.ResolveAllDispatchedAsDeferred (ProtoControl, Account.Id);
            NcCommStatus.Instance.CommStatusNetEvent += NetStatusEventHandler;
            NcCommStatus.Instance.CommStatusServerEvent += ServerStatusEventHandler;
        }

        public void ServerStatusEventHandler (Object sender, NcCommStatusServerEventArgs e)
        {
            if (e.ServerId == Server.Id) {
                switch (e.Quality) {
                case NcCommStatus.CommQualityEnum.OK:
                    Log.Info (Log.LOG_AS, "Server {0} communication quality OK.", Server.Host);
                    Execute ();
                    break;

                default:
                case NcCommStatus.CommQualityEnum.Degraded:
                    Log.Info (Log.LOG_AS, "Server {0} communication quality degraded.", Server.Host);
                    break;

                case NcCommStatus.CommQualityEnum.Unusable:
                    Log.Info (Log.LOG_AS, "Server {0} communication quality unusable.", Server.Host);
                    Sm.PostEvent ((uint)PcEvt.E.Park, "SSEHPARK");
                    break;
                }
            }
        }
        public void NetStatusEventHandler (Object sender, NetStatusEventArgs e)
        {
            if (NachoPlatform.NetStatusStatusEnum.Up == e.Status) {
                Execute ();
            } else {
                // The "Down" case.
                Sm.PostEvent ((uint)PcEvt.E.Park, "NSEHPARK");
            }
        }

        private NcTimer PendingOnTimeTimer { set; get; }
        public override bool Execute ()
        {
            if (!base.Execute ()) {
                return false;
            }
            if (null == PendingOnTimeTimer) {
                PendingOnTimeTimer = new NcTimer ("ImapProtoControl:PendingOnTimeTimer", state => {
                    McPending.MakeEligibleOnTime ();
                }, null, 1000, 2000);
                PendingOnTimeTimer.Stfu = true;
            }
            if (null == Server) {
                Sm.PostEvent ((uint)ImapEvt.E.ReDisc, "ASPCEXECAUTOD");
            } else {
                // All states are required to handle the Launch event gracefully.
                Sm.PostEvent ((uint)SmEvt.E.Launch, "ASPCEXE");
            }
            return true;
        }

        IImapCommand Cmd;

        private void ExecuteCmd ()
        {
            Cmd.Execute (Sm);
        }

        private void SetCmd (IImapCommand nextCmd)
        {
            if (null != Cmd) {
                Cmd.Cancel ();
            }
            Cmd = nextCmd;
        }

        public void Cancel ()
        {
            if (null != Cts) {
                Cts.Cancel ();
                Cts = null;
            }
            Sm.PostEvent ((uint)CtlEvt.E.Cancel, "SRCANCEL");
        }

        private void DoDisc ()
        {
            Sm.PostEvent ((uint)ImapEvt.E.ConnW, "IMAPDISCSKIPPED");
        }

        private void DoConn ()
        {
            try {
                client.ConnectAsync (Cts.Token);
                Sm.PostEvent ((uint)ImapEvt.E.AuthW, "IMCONNSUC");
            } catch (ImapCommandException e) {
                Log.Error (Log.LOG_IMAP, "ConnectAsync failed: {0}", e);
                Sm.PostEvent ((uint)SmEvt.E.HardFail, "IMCNFAIL");
            }
        }

        private void DoAuth ()
        {
            try {
                client.AuthenticateAndIdAsync (Cts.Token);
                Sm.PostEvent ((uint)ImapEvt.E.Auth, "IMAUTHSUCC");
            } catch (ImapCommandException e) {
                Log.Error (Log.LOG_IMAP, "AuthenticateAndIdAsync failed: {0}", e);
                Sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAUTHFAIL");
            }
        }

        private void DoPark ()
        {
            SetCmd (null);
            // Because we are going to stop for a while, we need to fail any
            // pending that aren't allowed to be delayed.
            McPending.ResolveAllDelayNotAllowedAsFailed (ProtoControl, Account.Id);
        }

        private X509Certificate2 _ServerCertToBeExamined;

        public override X509Certificate2 ServerCertToBeExamined {
            get {
                return _ServerCertToBeExamined;
            }
        }

        private void DoUiCertOkReq ()
        {
            _ServerCertToBeExamined = (X509Certificate2)Sm.Arg;
            Owner.CertAskReq (this, _ServerCertToBeExamined);
        }

        private void DoCertOkNo ()
        {
            throw new Exception ("Not implemented");
        }

        private void DoCertOkYes ()
        {
            throw new Exception ("Not implemented");
        }

        private void DoSetServConf ()
        {
            throw new Exception ("Not implemented");
        }

        private void DoUiCredReq ()
        {
            Owner.CredReq (this);
        }

        private void DoSetCred ()
        {
            throw new Exception ("Not implemented");
        }

        // State-machine's state persistance callback.
        private void UpdateSavedState ()
        {
            // TODO Move to parent?
            var protocolState = ProtocolState;
            uint stateToSave = Sm.State;
            switch (stateToSave) {
            case (uint)Lst.UiDCrdW:
            case (uint)Lst.UiServConfW:
            case (uint)Lst.UiCertOkW:
                stateToSave = (uint)Lst.DiscW;
                break;
            case (uint)Lst.Parked:
                // We never save Parked.
                return;
            }
            protocolState.ProtoControlState = stateToSave;
            protocolState.Update ();
        }

        private void EstablishService ()
        {
            // TODO Abstract to parent.

            // Hang our records off Account.
            NcModel.Instance.RunInTransaction (() => {
                var account = Account;
                var policy = McPolicy.QueryByAccountId<McPolicy> (account.Id).SingleOrDefault ();
                if (null == policy) {
                    policy = new McPolicy () {
                        AccountId = account.Id,
                    };
                    policy.Insert ();
                }
                var protocolState = McProtocolState.QueryByAccountId<McProtocolState> (account.Id).SingleOrDefault ();
                if (null == protocolState) {
                    protocolState = new McProtocolState () {
                        AccountId = account.Id,
                    };
                    protocolState.Insert ();
                }
            });
        }
    }
}
namespace NachoCore.IMAPExample
{
    public abstract class ImapCommand : IImapCommand
    {
        public string CommandName;

        public TimeSpan Timeout { get; set; }

        protected IBEContext BEContext;
        protected List<McPending> PendingList;
        protected object PendingResolveLockObj;

        public ImapCommand (string commandName, IBEContext beContext)
        {
            Timeout = TimeSpan.Zero;
            CommandName = commandName;
            BEContext = beContext;
            PendingList = new List<McPending> ();
            PendingResolveLockObj = new object ();
        }

        public void Execute (NcStateMachine sm)
        {
            throw new Exception ("SubClass must override");
        }

        public void Cancel ()
        {
            throw new Exception ("SubClass must override");
        }
    }

    public partial class ImapGeneric : ImapCommand
    {
        public ImapGeneric (IBEContext dataSource) : base ("Foo", dataSource)
        {
        }

        // Virtual Methods.
        protected virtual void Execute (NcStateMachine sm)
        {
        }

        // Cancel() must be safe to call even when the command has already completed.
        public virtual void Cancel ()
        {
        }
    }
}

