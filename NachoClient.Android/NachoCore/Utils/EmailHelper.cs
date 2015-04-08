//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using MimeKit;
using System.Text;
using NachoCore.Brain;
using NachoCore.Model;


namespace NachoCore.Utils
{
    public class EmailHelper
    {
        public enum Action
        {
            Send,
            Reply,
            ReplyAll,
            Forward,
        };

        public static void Setup ()
        {
            NcApplication.Instance.SendEmailRespCallback = SendEmailRespCallback;
        }

        public static void SendTheMessage (Action action, McEmailMessage messageToSend, bool originalEmailIsEmbedded, McEmailMessage referencedMessage, bool calendarInviteIsSet, McAbstrCalendarRoot calendarInviteItem)
        {
            var outbox = McFolder.GetOutboxFolder (messageToSend.AccountId);
            if (null != outbox) {
                outbox.Link (messageToSend);
            }

            bool messageSent = false;
            if (EmailHelper.IsForwardOrReplyAction (action) || calendarInviteIsSet) {
                List<McFolder> folders;
                if (calendarInviteIsSet) {
                    folders = McFolder.QueryByFolderEntryId<McCalendar> (calendarInviteItem.AccountId, calendarInviteItem.Id);
                } else {
                    folders = McFolder.QueryByFolderEntryId<McEmailMessage> (referencedMessage.AccountId, referencedMessage.Id);
                }
                if (folders.Count == 0) {
                    Log.Error (Log.LOG_UI, "The message or event being forwarded or replied to is not owned by any folder. It will be sent as a regular outgoing message.");
                    // Fall through and send it as a regular message.  Or don't send it at all if it is an event.
                } else {
                    if (folders.Count > 1) {
                        Log.Warn (Log.LOG_UI, "The message or event being forwarded or replied to is owned by {0} folders. One of the folders will be picked at random as the official owner when sending the message.", folders.Count);
                    }
                    int folderId = folders [0].Id;
                    if (calendarInviteIsSet) {
                        NachoCore.BackEnd.Instance.ForwardCalCmd (
                            messageToSend.AccountId, messageToSend.Id, calendarInviteItem.Id, folderId);
                    } else if (EmailHelper.IsForwardAction (action)) {
                        NachoCore.BackEnd.Instance.ForwardEmailCmd (
                            messageToSend.AccountId, messageToSend.Id, referencedMessage.Id, folderId, originalEmailIsEmbedded);
                    } else {
                        NachoCore.BackEnd.Instance.ReplyEmailCmd (
                            messageToSend.AccountId, messageToSend.Id, referencedMessage.Id, folderId, originalEmailIsEmbedded);
                    }
                    messageSent = true;
                }
            }
            if (!messageSent && !calendarInviteIsSet) {
                // A new outgoing message.  Or a forward/reply that has problems.
                NachoCore.BackEnd.Instance.SendEmailCmd (messageToSend.AccountId, messageToSend.Id);
                messageSent = true;
            }
            if (messageSent) {
                // Send status ind because the message is in the outbox
                var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged);
                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                    Status = result,
                    Account = ConstMcAccount.NotAccountSpecific,
                });
            }

        }


        /// <summary>
        /// Emails are added to the per-account on-device outbox folder just
        /// before calling one of the send-mail APIs. After the back end has
        /// sent (or failed ot send) the email, this callback is responsible
        /// for either removing the message from outbox or for notifying the
        /// user that the message was not sent.
        /// </summary>
        public static void SendEmailRespCallback (int accountId, int emailMessageId, bool didSend)
        {
            var message = McEmailMessage.QueryById<McEmailMessage> (emailMessageId);

            if (null == message) {
                return;
            }
            if (didSend) {
                message.Delete ();
            } else {
                // OutboxTableViewSource handles the details
            }
            // Send status ind after the message is deleted (and unlinked).
            var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = result,
                Account = ConstMcAccount.NotAccountSpecific,
            });
        }

        /// <summary>
        /// Delete a message from outbox.  Need to stop
        /// the message from being sent before deleting
        /// it.
        /// </summary>
        public static void DeleteEmailMessageFromOutbox (McEmailMessage message)
        {
            var pending = McPending.QueryByEmailMessageId (message.AccountId, message.Id);
            if (null != pending) {
                BackEnd.Instance.Cancel (message.AccountId, pending.Token);
            }
            message.Delete ();
            // Send status ind after the message is deleted (and unlinked).
            var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = result,
                Account = ConstMcAccount.NotAccountSpecific,
            });
        }

        public static void DeleteEmailThreadFromOutbox (McEmailMessageThread thread)
        {
            foreach (var message in thread) {
                DeleteEmailMessageFromOutbox (message);
            }
        }

        public static bool IsSendAction (Action action)
        {
            return Action.Send == action;
        }

        public static bool IsForwardOrReplyAction (Action action)
        {
            return Action.Send != action;
        }

        // Reply or ReplyAll.  In almost all cases the two are treated the same.  There is only one case where they are different.
        public static bool IsReplyAction (Action action)
        {
            return Action.Reply == action || Action.ReplyAll == action;
        }

        public static bool IsForwardAction (Action action)
        {
            return Action.Forward == action;
        }

        public static string CreateInitialSubjectLine (Action action, string referencedSubject)
        {
            if (IsSendAction (action)) {
                return "";
            }
            if (null == referencedSubject) {
                referencedSubject = "";
            }
            if (IsForwardAction (action)) {
                return Pretty.Join ("Fwd:", referencedSubject, " ");
            }
            NcAssert.True (IsReplyAction (action));
            if (referencedSubject.StartsWith ("Re:")) {
                return referencedSubject;
            } else {
                return Pretty.Join ("Re:", referencedSubject, " ");
            }
        }


        public static bool IsValidEmail (string email)
        {
            RegexUtilities regexUtil = new RegexUtilities ();
            return regexUtil.IsValidEmail (email);
        }

        public enum ParseServerWhyEnum
        {
            Success_0 = 0,
            FailUnknown,
            FailHadQuery,
            FailBadPort,
            FailBadHost,
            FailBadScheme,
        };

        public static ParseServerWhyEnum IsValidServer (string serverName)
        {
            McServer dummy = new McServer ();
            return ParseServer (ref dummy, serverName);
        }

        public static ParseServerWhyEnum ParseServer (ref McServer server, string serverName)
        {
            NcAssert.NotNull (server);
            NcAssert.NotNull (serverName);
            Uri serverURI = null;
            try {
                // User may have entered a scheme - let's try it.
                serverURI = new Uri (serverName);
            } catch {
            }
            if (null != serverURI) {
                // Is this Uri any good at all?
                if (serverURI.IsFile ||
                    !EmailHelper.IsValidHost (serverURI.Host) ||
                    !EmailHelper.IsValidPort (serverURI.Port)) {
                    if (serverName.Contains ("://")) {
                        // The user added a scheme, and it went bad.
                        return ParseServerWhyEnum.FailBadScheme;
                    }
                    // Try with a prepended scheme.
                    serverURI = null;
                }
            }
            if (null == serverURI) {
                // We possibly need to prepend a scheme.
                try {
                    // NB using a made-up scheme will get you a -1 port number unless port was specified.
                    serverURI = new Uri ("https://" + serverName.Trim ());
                } catch {
                    // We give up
                    return ParseServerWhyEnum.FailUnknown;
                }
            }
            // We were able to create a Url object.
            if (!EmailHelper.IsValidHost (serverURI.Host)) {
                return ParseServerWhyEnum.FailBadHost;
            }
            if (!EmailHelper.IsValidPort (serverURI.Port)) {
                return ParseServerWhyEnum.FailBadPort;
            }
            // Ensure there were no Query parameters.
            if (null != serverURI.Query && string.Empty != serverURI.Query) {
                return ParseServerWhyEnum.FailHadQuery;
            }
            // Ensure that the Path is correct.
            if (null == serverURI.AbsolutePath || !serverURI.AbsolutePath.EndsWith (McServer.Default_Path)) {
                // If we don't end with the default path, we need to.
                // If no path specified, then use the default.
                // If we end with the default path and a '/', strip.
                if (null != serverURI.AbsolutePath && serverURI.AbsolutePath.EndsWith (McServer.Default_Path + "/")) {
                    serverURI = new Uri (serverURI.AbsoluteUri.Substring (0, serverURI.AbsoluteUri.Length - 1));
                } else {
                    var prefix = serverURI.AbsolutePath.TrimEnd ('/');
                    serverURI = new Uri (serverURI, prefix + McServer.Default_Path);
                }
            }
            server.Scheme = serverURI.Scheme;
            server.Host = serverURI.Host;
            server.Port = serverURI.Port;
            server.Path = serverURI.AbsolutePath;
            return ParseServerWhyEnum.Success_0;
        }

        public static bool IsValidHost (string host)
        {
            UriHostNameType fullServerUri = Uri.CheckHostName (host.Trim ());
            if (fullServerUri == UriHostNameType.Dns ||
                fullServerUri == UriHostNameType.IPv4 ||
                fullServerUri == UriHostNameType.IPv6) {
                return true;
            }
            return false;
        }

        public static bool IsValidPort (int port)
        {
            if (port < 0 || port > 65535) {
                return false;
            } else {
                return true;
            }
        }

        public static bool IsServiceUnsupported (string emailAddress, out string serviceName)
        {
            if (emailAddress.EndsWith ("@gmail.com", StringComparison.OrdinalIgnoreCase)) {
                serviceName = "Gmail";
                return true;
            }
            if (emailAddress.EndsWith ("@yahoo.com", StringComparison.OrdinalIgnoreCase)) {
                serviceName = "Yahoo!";
                return true;
            }
            if (emailAddress.EndsWith ("@aol.com", StringComparison.OrdinalIgnoreCase)) {
                serviceName = "AOL";
                return true;
            }
            if (emailAddress.EndsWith ("@mail.com", StringComparison.OrdinalIgnoreCase)) {
                serviceName = "Mail.com";
                return true;
            }
            serviceName = "";
            return false;
        }

        public static bool IsHotmailServiceAddress (string emailAddress)
        {
            if (emailAddress.EndsWith ("@hotmail.com", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            if (emailAddress.EndsWith ("@outlook.com", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            if (emailAddress.EndsWith ("@live.com", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            return false;
        }

        public static bool IsHotmailServer (string serverName, out string serviceName)
        {
            if (String.Equals ("hotmail.com", serverName, StringComparison.OrdinalIgnoreCase)) {
                serviceName = "Hotmail";
                return true;
            }
            if (String.Equals ("outlook.com", serverName, StringComparison.OrdinalIgnoreCase)) {
                serviceName = "Outlook.com";
                return true;
            }
            if (String.Equals ("live.com", serverName, StringComparison.OrdinalIgnoreCase)) {
                serviceName = "Live.com";
                return true;
            }
            if (serverName.EndsWith (".hotmail.com", StringComparison.OrdinalIgnoreCase)) {
                serviceName = "Hotmail";
                return true;
            }
            if (serverName.EndsWith (".outlook.com", StringComparison.OrdinalIgnoreCase)) {
                serviceName = "Outlook.com";
                return true;
            }
            if (serverName.EndsWith (".live.com", StringComparison.OrdinalIgnoreCase)) {
                serviceName = "Live.com";
                return true;
            }
            serviceName = "";
            return false;
        }

        public static bool IsMailToURL (string urlString)
        {
            return urlString.StartsWith (Uri.UriSchemeMailto + ":", StringComparison.OrdinalIgnoreCase);
        }

        public static Uri MailToUri (string emailAddress)
        {
            return new Uri (Uri.UriSchemeMailto + ":" + emailAddress);
        }

        public static string EmailAddressFromUri (Uri mailtoUri)
        {
            if (Uri.UriSchemeMailto.Equals (mailtoUri.Scheme, StringComparison.OrdinalIgnoreCase)) {
                return mailtoUri.AbsoluteUri.Substring (Uri.UriSchemeMailto.Length + 1);
            } else {
                return mailtoUri.ToString ();
            }
        }

        /// <summary>
        /// Parses a mailto: url.
        /// </summary>
        /// <returns><c>true</c>, if mailto: was parsed, <c>false</c> otherwise.</returns>
        /// <param name="url">The string of the URL</param>
        /// The format is mailto:<comma separated list of email addresses>.
        /// Then an & separated list of name value pairs, any of which can be empty:
        /// cc, bcc, subject, and body, all percent encoded.
        public static bool ParseMailTo (string urlString, out List<NcEmailAddress> addresses, out string subject, out string body)
        {
            addresses = new List<NcEmailAddress> ();
            subject = null;
            body = null;

            if (!urlString.StartsWith ("mailto:", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            if (7 == urlString.Length) {
                return false;
            }

            // Look for the query string '?'
            int queryIndex = urlString.IndexOf ('?');

            string encodedToString;
            if (-1 == queryIndex) {
                encodedToString = urlString.Substring (7);
            } else {
                encodedToString = urlString.Substring (7, queryIndex - 7);
            }
            var toString = Uri.UnescapeDataString (encodedToString);
            addresses = NcEmailAddress.ParseToAddressListString (toString);

            // check if we only have a to list
            if ((-1 == queryIndex) || (urlString.Length == queryIndex)) {
                return true;
            }

            var parameters = urlString.Substring (queryIndex + 1).Split (new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var parameter in parameters) {
                if (parameter.StartsWith ("to=", StringComparison.OrdinalIgnoreCase)) {
                    if (3 < parameter.Length) {
                        var toParameterString = Uri.UnescapeDataString (parameter.Substring (3));
                        var toList = NcEmailAddress.ParseToAddressListString (toParameterString);
                        addresses.AddRange (toList);
                    }
                    continue;
                }
                if (parameter.StartsWith ("cc=", StringComparison.OrdinalIgnoreCase)) {
                    if (3 < parameter.Length) {
                        var ccString = Uri.UnescapeDataString (parameter.Substring (3));
                        var ccList = NcEmailAddress.ParseCcAddressListString (ccString);
                        addresses.AddRange (ccList);
                    }
                    continue;
                }
                if (parameter.StartsWith ("bcc=", StringComparison.OrdinalIgnoreCase)) {
                    if (4 < parameter.Length) {
                        var bccString = Uri.UnescapeDataString (parameter.Substring (4));
                        var bccList = NcEmailAddress.ParseBccAddressListString (bccString);
                        addresses.AddRange (bccList);
                    }
                    continue;
                }
                if (parameter.StartsWith ("subject=", StringComparison.OrdinalIgnoreCase)) {
                    if (8 < parameter.Length) {
                        subject = Uri.UnescapeDataString (parameter.Substring (8));
                    }
                    continue;
                }
                if (parameter.StartsWith ("body=", StringComparison.OrdinalIgnoreCase)) {
                    if (5 < parameter.Length) {
                        body = Uri.UnescapeDataString (parameter.Substring (5));
                    }
                    continue;
                }
                Log.Error (Log.LOG_EMAIL, "ParseMailTo: unknown parameter {0}", parameter);
            }

            return true;
        }

        private static bool IsAccountAlias (InternetAddress accountInternetAddress, string match)
        {
            if (null == accountInternetAddress) {
                return false;
            }
            var accountMailboxAddress = accountInternetAddress as MailboxAddress;
            if (null == accountMailboxAddress) {
                return false;
            }
            if (String.IsNullOrEmpty (accountMailboxAddress.Address) || String.IsNullOrEmpty (match)) {
                return false;
            }
            var target = accountMailboxAddress.Address;
            return String.Equals (target, match, StringComparison.OrdinalIgnoreCase);

        }

        public static List<NcEmailAddress> CcList (string accountEmailAddress, string toString, string ccString)
        {
            var ccList = new List<NcEmailAddress> ();

            InternetAddress accountAddress;
            if (String.IsNullOrEmpty (accountEmailAddress) || !MailboxAddress.TryParse (accountEmailAddress, out accountAddress)) {
                accountAddress = null;
            }
            InternetAddressList addresses;
            if (!String.IsNullOrEmpty (toString) && InternetAddressList.TryParse (toString, out addresses)) {
                foreach (var mailboxAddress in addresses.Mailboxes) {
                    if (!IsAccountAlias (accountAddress, mailboxAddress.Address)) {
                        ccList.Add (new NcEmailAddress (NcEmailAddress.Kind.Cc, mailboxAddress.Address));
                    }
                }
            }
            if (!String.IsNullOrEmpty (ccString) && InternetAddressList.TryParse (ccString, out addresses)) {
                foreach (var mailboxAddress in addresses.Mailboxes) {
                    if (!IsAccountAlias (accountAddress, mailboxAddress.Address)) {
                        ccList.Add (new NcEmailAddress (NcEmailAddress.Kind.Cc, mailboxAddress.Address));
                    }
                }
            }
            return ccList;
        }

        // Build up the text for the header part of the message being forwarded or replied to.
        public static string FormatBasicHeaders (McEmailMessage message)
        {
            StringBuilder result = new StringBuilder ();
            result.Append ("-------------------\n");
            result.Append ("From: ").Append (message.From ?? "").Append ("\n");
            if (message.To != null && message.To.Length > 0) {
                result.Append ("To: ").Append (message.To).Append ("\n");
            }
            if (message.Cc != null && message.Cc.Length > 0) {
                result.Append ("Cc: ").Append (message.Cc).Append ("\n");
            }
            result.Append ("Subject: ").Append (message.Subject ?? "").Append ("\n");
            result.Append ("Date: ").Append (Pretty.UniversalFullDateTimeString (message.DateReceived));
            result.Append ("\n\n");
            return result.ToString ();
        }

        // Build up the text for the header part of the message being forwarded or replied to.
        public static string FormatBasicHeadersForCalendarForward (McCalendar calendar, string recipient)
        {
            StringBuilder result = new StringBuilder ();
            result.Append ("-------------------\n");
            result.Append ("Organizer: ").Append (calendar.OrganizerName ?? calendar.OrganizerEmail ?? "").Append ("\n");
            result.Append ("To: ").Append (recipient).Append ("\n");
            result.Append ("Subject: ").Append (calendar.Subject ?? "").Append ("\n");
            result.Append ("When: ").Append (Pretty.FullDateYearString (calendar.StartTime)).Append ("\n");
            result.Append ("Where: ").Append (calendar.Location ?? "").Append ("\n");
            result.Append ("\n\n");
            return result.ToString ();
        }

        public static string Initials (string fromAddressString)
        {
            // Parse the from address
            var mailboxAddress = NcEmailAddress.ParseMailboxAddressString (fromAddressString);
            if (null == mailboxAddress) {
                return "";
            }
            McContact contact = new McContact ();
            NcEmailAddress.SplitName (mailboxAddress, ref contact);
            // Using the name
            string initials = "";
            if (!String.IsNullOrEmpty (contact.FirstName)) {
                initials += Char.ToUpper (contact.FirstName [0]);
            }
            if (!String.IsNullOrEmpty (contact.LastName)) {
                initials += Char.ToUpper (contact.LastName [0]);
            }
            // Or, failing that, the first char
            if (String.IsNullOrEmpty (initials)) {
                if (!String.IsNullOrEmpty (fromAddressString)) {
                    foreach (char c in fromAddressString) {
                        if (Char.IsLetterOrDigit (c)) {
                            initials += Char.ToUpper (c);
                            break;
                        }
                    }
                }
            }
            return initials;
        }

        public static MimeMessage CreateMessage (McAccount account, List<NcEmailAddress> toList, List<NcEmailAddress> ccList, List<NcEmailAddress> bccList)
        {
            var mimeMessage = new MimeMessage ();
            mimeMessage.From.Add (new MailboxAddress (Pretty.UserNameForAccount (account), account.EmailAddr));
            mimeMessage.To.AddRange (NcEmailAddress.ToInternetAddressList (toList, NcEmailAddress.Kind.To));
            mimeMessage.Cc.AddRange (NcEmailAddress.ToInternetAddressList (ccList, NcEmailAddress.Kind.Cc));
            mimeMessage.Bcc.AddRange (NcEmailAddress.ToInternetAddressList (bccList, NcEmailAddress.Kind.Bcc));
            mimeMessage.Date = System.DateTime.UtcNow;
            return mimeMessage;
        }

        public static string CreateSubjectWithIntent (string rawSubject, McEmailMessage.IntentType messageIntent, MessageDeferralType messageIntentDateType, DateTime messageIntentDateTime)
        {
            var subject = Pretty.SubjectString (rawSubject);
            if (McEmailMessage.IntentType.None != messageIntent) {
                var intentString = NcMessageIntent.GetIntentString (messageIntent, messageIntentDateType, messageIntentDateTime);
                subject = Pretty.Join (intentString, subject, " - ");
            }
            return subject;
        }

        public static void SetupReferences (ref MimeMessage mimeMessage, McEmailMessage referencedMessage)
        {
            mimeMessage.InReplyTo = null;
            mimeMessage.References.Clear ();

            if (null != referencedMessage) {
                mimeMessage.InReplyTo = referencedMessage.MessageID;
                if (null != referencedMessage.References) {
                    foreach (var reference in MimeKit.Utils.MimeUtils.EnumerateReferences(referencedMessage.References)) {
                        mimeMessage.References.Add (reference);
                    }
                }
                if (null != referencedMessage.InReplyTo) {
                    foreach (var reference in MimeKit.Utils.MimeUtils.EnumerateReferences(referencedMessage.InReplyTo)) {
                        if (!mimeMessage.References.Contains (reference)) {
                            mimeMessage.References.Add (reference);
                        }
                    }
                }
            }
        }
    }
}

