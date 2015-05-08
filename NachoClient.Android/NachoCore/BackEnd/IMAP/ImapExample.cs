//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using NachoCore.Utils;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit;
using MimeKit;
using NachoClient.Build;

namespace NachoCore.Imap
{
    public class Imap
    {
        private string m_hostname { get; set; }
        private int m_port { get; set; }
        private bool m_useSsl { get; set; }
        private string m_username { get; set;}
        private string m_password { get; set;}

        public Imap (string hostname, int port, bool useSsl, string username, string password)
        {
            m_hostname = hostname;
            m_port = port;
            m_useSsl = useSsl;
            m_username = username;
            m_password = password;
            DoImap();
        }

        public class NcProtocolLogger : MailKit.IProtocolLogger
        {
            public void LogConnect (Uri uri)
            {
                if (uri == null)
                    throw new ArgumentNullException ("uri");

                Log.Info (Log.LOG_EMAIL, "Connected to {0}", uri);
            }

            private void logBuffer (string prefix, byte[] buffer, int offset, int count)
            {
                char[] delimiterChars = { '\n' };
                var lines = Encoding.UTF8.GetString (buffer.Skip (offset).Take (count).ToArray ()).Split (delimiterChars);

                Array.ForEach (lines, (line) => {
                    if (line.Length > 0) {
                        Log.Info (Log.LOG_EMAIL, "{0}{1}", prefix, line);
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

        private ImapClient getClient ()
        {
            NcProtocolLogger logger = new NcProtocolLogger ();
            ImapClient client = new ImapClient (logger);
            client.ClientCertificates = new X509CertificateCollection ();
            /*
                2015-05-07 17:02:00.703 NachoClientiOS[70577:13497471] Info:1:: Connected to imap://imap.gmail.com:143/?starttls=when-available
                2015-05-07 17:02:00.809 NachoClientiOS[70577:13497471] Info:1:: IMAP S: * OK Gimap ready for requests from 69.145.38.236 pw12mb32849803pab
                2015-05-07 17:02:00.826 NachoClientiOS[70577:13497471] Info:1:: IMAP C: A00000000 CAPABILITY
                2015-05-07 17:02:00.855 NachoClientiOS[70577:13497471] Info:1:: IMAP S: * CAPABILITY IMAP4rev1 UNSELECT IDLE NAMESPACE QUOTA ID XLIST CHILDREN X-GM-EXT-1 XYZZY SASL-IR AUTH=XOAUTH2 AUTH=PLAIN AUTH=PLAIN-CLIENTTOKEN
                2015-05-07 17:02:00.861 NachoClientiOS[70577:13497471] Info:1:: IMAP S: A00000000 OK Thats all she wrote! pw12mb32849803pab
            */
            client.Connect (m_hostname, m_port, m_useSsl);

            // Note: since we don't have an OAuth2 token, disable
            // the XOAUTH2 authentication mechanism.
            client.AuthenticationMechanisms.Remove ("XOAUTH");
            client.AuthenticationMechanisms.Remove ("XOAUTH2");
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
                client.Authenticate (m_username, m_password);
            } catch (MailKit.Security.AuthenticationException e) {
                Log.Error (Log.LOG_EMAIL, "Could not connect to server: {0}", e);
                return null;
            }

            ImapImplementation clientId = new ImapImplementation ();
            clientId.Name = "NachoMail";
            clientId.Version = string.Format ("{0}:{1}", BuildInfo.Version, BuildInfo.BuildNumber);
            clientId.ReleaseDate = BuildInfo.Time;
            clientId.SupportUrl = "https://support.nachocove.com/";
            clientId.Vendor = "Nacho Cove, Inc";
            Log.Info (Log.LOG_EMAIL, "Client ID: {0}", dumpImapImplementation (clientId));

            ImapImplementation serverId = client.Identify (clientId);
            Log.Info (Log.LOG_EMAIL, "Server ID: {0}", dumpImapImplementation (serverId));

            foreach (FolderNamespace name in client.PersonalNamespaces) {
                Log.Info (Log.LOG_EMAIL, "PersonalNamespaces: Separator '{0}' Path '{1}'", name.DirectorySeparator, name.Path);
            }
            foreach (FolderNamespace name in client.SharedNamespaces) {
                Log.Info (Log.LOG_EMAIL, "SharedNamespaces: Separator '{0}' Path '{1}'", name.DirectorySeparator, name.Path);
            }
            foreach (FolderNamespace name in client.OtherNamespaces) {
                Log.Info (Log.LOG_EMAIL, "OtherNamespaces: Separator '{0}' Path '{1}'", name.DirectorySeparator, name.Path);
            }
            return client;
        }

        private void DoImap ()
        {
            var client = getClient ();
            if (client == null) {
                Log.Error (Log.LOG_EMAIL, "Could not connect to imap");
                return;
            }
            // The Inbox folder is always available on all IMAP servers...
            handleFolder (client.Inbox, FolderAccess.ReadOnly);

            foreach (SpecialFolder special in Enum.GetValues(typeof(SpecialFolder)).Cast<SpecialFolder>()) {
                var folder = client.GetFolder (special);
                if (folder == null) {
                    Log.Error (Log.LOG_EMAIL, "Could not get folder {0}", special);
                } else {
                    handleFolder (folder, FolderAccess.ReadOnly);
                }
            }
            client.Disconnect (true);
        }

        private void handleFolder (IMailFolder folder, FolderAccess access)
        {
            /*
                    2015-05-07 17:14:12.389 NachoClientiOS[70782:13542016] Info:1:: IMAP C: A00000006 SELECT INBOX
                    2015-05-07 17:14:12.565 NachoClientiOS[70782:13542016] Info:1:: IMAP S: * FLAGS (\Answered \Flagged \Draft \Deleted \Seen $Phishing $NotPhishing $Junk)
                    2015-05-07 17:14:12.566 NachoClientiOS[70782:13542016] Info:1:: IMAP S: * OK [PERMANENTFLAGS (\Answered \Flagged \Draft \Deleted \Seen $Phishing $NotPhishing $Junk \*)] Flags permitted.
                    2015-05-07 17:14:12.567 NachoClientiOS[70782:13542016] Info:1:: IMAP S: * OK [UIDVALIDITY 3] UIDs valid.
                    2015-05-07 17:14:12.567 NachoClientiOS[70782:13542016] Info:1:: IMAP S: * 8 EXISTS
                    2015-05-07 17:14:12.568 NachoClientiOS[70782:13542016] Info:1:: IMAP S: * 0 RECENT
                    2015-05-07 17:14:12.568 NachoClientiOS[70782:13542016] Info:1:: IMAP S: * OK [UIDNEXT 8636] Predicted next UID.
                    2015-05-07 17:14:12.569 NachoClientiOS[70782:13542016] Info:1:: IMAP S: * OK [HIGHESTMODSEQ 938719]
                    2015-05-07 17:14:12.569 NachoClientiOS[70782:13542016] Info:1:: IMAP S: A00000006 OK [READ-WRITE] INBOX selected. (Success)
                */
            folder.Open (access);
            Log.Info (Log.LOG_EMAIL, "Total {0} messages: {1}", folder.Name, folder.Count);
            Log.Info (Log.LOG_EMAIL, "Recent {0} messages: {1}", folder.Name, folder.Recent);
            //                    for (int i = 0; i < folder.Count; i++) {
            //                        var message = folder.GetMessage (i);
            //                        Log.Info (Log.LOG_EMAIL, "{0} Subject: {1}", special, message.Subject);
            //                    }
            // let's search for all messages received after Jan 12, 2013 with "MailKit" in the subject...
            //var query = SearchQuery.All ().And (SearchQuery.NotDeleted);
            var query = SearchQuery.DoesNotHaveFlags (MessageFlags.Deleted);
            /*
             * 2015-05-08 11:36:37.492 NachoClientiOS[75297:15205158] Info:1:: IMAP C: A00000007 UID SEARCH RETURN () DELETED
             * 2015-05-08 11:36:37.683 NachoClientiOS[75297:15205158] Info:1:: IMAP S: * ESEARCH (TAG "A00000007") UID
             * 2015-05-08 11:36:37.689 NachoClientiOS[75297:15205158] Info:1:: IMAP S: A00000007 OK SEARCH completed (Success)
             */
            var uids = folder.Search (query);
            Log.Info (Log.LOG_EMAIL, "Search 'NotDeleted' has {0} results", uids.Count);
            if (uids.Count > 0) {
                var messageSummaries = folder.Fetch (uids, MessageSummaryItems.Envelope | MessageSummaryItems.InternalDate | MessageSummaryItems.Flags);
                foreach (var summary in messageSummaries) {
                    Log.Info (Log.LOG_EMAIL, "IMAP: uid {0} {1} {2} {3}", summary.UniqueId, summary.Flags, summary.InternalDate, summary.Envelope);
                }
                foreach (var uid in uids) {
                    var textBody = new BodyPartText ();
                    textBody.PartSpecifier = "TEXT";
                    var message = folder.GetStream (uid, textBody, 0, 255);
                    var buffer = new byte[255];
                    message.Read (buffer, 0, 255);
                    Log.Info (Log.LOG_EMAIL, "Retrieved message <{0}>", Encoding.UTF8.GetString (buffer));
                }
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
            folder.Close ();
        }

    }
}

