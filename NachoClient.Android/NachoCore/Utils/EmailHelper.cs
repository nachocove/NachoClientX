//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using MimeKit;
using System.Text;
using NachoCore.Brain;
using NachoCore.Model;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.IO;

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

        // Message is saved into Outbox
        public static void SendTheMessage (McEmailMessage messageToSend, McAbstrCalendarRoot calendarInviteItem)
        {
            var outbox = McFolder.GetClientOwnedOutboxFolder (messageToSend.AccountId);
            if (null != outbox) {
                outbox.Link (messageToSend);
            } else {
                Log.Warn (Log.LOG_EMAIL, "GetOutboxFolder returned null");
            }
            McEmailMessage referencedMessage = null;
            if (messageToSend.ReferencedEmailId != 0) {
                referencedMessage = McEmailMessage.QueryById<McEmailMessage> (messageToSend.ReferencedEmailId);
            }

            bool messageSent = false;
            List<McFolder> folders = null;
            if (calendarInviteItem != null) {
                folders = McFolder.QueryByFolderEntryId<McCalendar> (calendarInviteItem.AccountId, calendarInviteItem.Id);
                if (folders.Count == 0) {
                    Log.Error (Log.LOG_UI, "The event being forwarded or replied to is not owned by any folder. It will be sent as a regular outgoing message.");
                } else {
                    int folderId = folders [0].Id;
                    NachoCore.BackEnd.Instance.ForwardCalCmd (messageToSend.AccountId, messageToSend.Id, calendarInviteItem.Id, folderId);
                    messageSent = true;
                }
            } else {
                if (referencedMessage != null) {
                    folders = McFolder.QueryByFolderEntryId<McEmailMessage> (referencedMessage.AccountId, referencedMessage.Id);
                    if (folders.Count == 0) {
                        Log.Error (Log.LOG_UI, "The message being forwarded or replied to is not owned by any folder. It will be sent as a regular outgoing message.");
                    } else {
                        int folderId = folders [0].Id;
                        if (messageToSend.ReferencedIsForward) {
                            NachoCore.BackEnd.Instance.ForwardEmailCmd (messageToSend.AccountId, messageToSend.Id, referencedMessage.Id, folderId, true);
                            messageSent = true;
                        } else {
                            NachoCore.BackEnd.Instance.ReplyEmailCmd (messageToSend.AccountId, messageToSend.Id, referencedMessage.Id, folderId, true);
                            messageSent = true;
                        }
                    }
                }
                if (!messageSent) {
                    // A new outgoing message.  Or a forward/reply that has problems.
                    NachoCore.BackEnd.Instance.SendEmailCmd (messageToSend.AccountId, messageToSend.Id);
                    messageSent = true;
                }
            }
            if (messageSent) {
                // Send status ind because the message is in the outbox
                var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged);
                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                    Status = result,
                    Account = McAccount.QueryById<McAccount> (messageToSend.AccountId),
                });
            }
        }

        private static bool MustSaveMessageToSent (int accountId)
        {
            var account = McAccount.QueryById<McAccount> (accountId);
            return McAccount.AccountTypeEnum.IMAP_SMTP == account.AccountType &&
            McAccount.AccountServiceEnum.GoogleDefault != account.AccountService;
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
                Log.Warn (Log.LOG_EMAIL, "SendEmailRespCallback could not find msg id {0}", emailMessageId);
                return;
            }
            if (didSend) {
                if (MustSaveMessageToSent (accountId)) {
                    var defSent = McFolder.GetDefaultSentFolder (accountId);
                    var outbox = McFolder.GetClientOwnedOutboxFolder (accountId);
                    if (null != defSent && null != outbox) {
                        defSent.Link (message);
                        outbox.Unlink (message);
                        NachoCore.BackEnd.Instance.SyncCmd (accountId, defSent.Id);
                    } else {
                        Log.Error (Log.LOG_EMAIL, "SendEmailRespCallback could not find sent {0} or outbox {1}", defSent, outbox);
                        message.Delete ();
                    }
                } else {
                    message.Delete ();
                    var sentFolder = McFolder.GetDefaultSentFolder (accountId);
                    if (null != sentFolder) {
                        // Best-effort, nothing to do on non-OK retval.
                        BackEnd.Instance.SyncCmd (accountId, sentFolder.Id);
                    }
                }
            } else {
                // OutboxTableViewSource handles the details
            }
            // Send status ind after the message is deleted (and unlinked).
            var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = result,
                Account = McAccount.QueryById<McAccount> (accountId),
            });
        }

        public static McEmailMessage MoveFromOutboxToDrafts (McEmailMessage message)
        {
            var pending = McPending.QueryByEmailMessageId (message.AccountId, message.Id);
            if (null != pending) {
                McPending.Cancel (message.AccountId, pending.Token);
            }
            // Move files in client-owned folders manually
            var draftsFolder = McFolder.GetClientOwnedDraftsFolder (message.AccountId);
            var outboxFolder = McFolder.GetClientOwnedOutboxFolder (message.AccountId);
            draftsFolder.Link (message);
            outboxFolder.Unlink (message);
            // Send status ind after the message is moved
            var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = result,
                Account = McAccount.QueryById<McAccount> (message.AccountId),
            });
            return message;
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
                McPending.Cancel (message.AccountId, pending.Token);
            }
            message.Delete ();
            // Send status ind after the message is deleted (and unlinked).
            var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = result,
                Account = McAccount.QueryById<McAccount> (message.AccountId),
            });
        }

        public static void DeleteEmailThreadFromOutbox (McEmailMessageThread thread)
        {
            foreach (var message in thread) {
                DeleteEmailMessageFromOutbox (message);
            }
        }

        public static void SaveEmailMessageInDrafts (McEmailMessage message)
        {
            var draftsFolder = McFolder.GetClientOwnedDraftsFolder (message.AccountId);
            if (null != draftsFolder) {
                draftsFolder.Link (message);
            } else {
                Log.Warn (Log.LOG_EMAIL, "GetEmailDraftsFolder returned null");
            }
            // Send status ind because the drafts folder has changed
            var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = result,
                Account = McAccount.QueryById<McAccount> (message.AccountId),
            });
        }

        public static void DeleteEmailMessageFromDrafts (McEmailMessage message)
        {
            message.Delete ();
            // Send status ind after the message is deleted (and unlinked).
            var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = result,
                Account = McAccount.QueryById<McAccount> (message.AccountId),
            });
        }

        public static void DeleteEmailThreadFromDrafts (McEmailMessageThread thread)
        {
            foreach (var message in thread) {
                DeleteEmailMessageFromDrafts (message);
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

        public static string AttributionLineForMessage (McEmailMessage message)
        {
            var attribution = "";
            attribution += string.Format ("On {0} at {1}", Pretty.MediumMonthDayYear (message.DateReceived), Pretty.Time (message.DateReceived));
            if (!String.IsNullOrWhiteSpace (message.From)) {
                if (attribution.Length > 0) {
                    attribution += ", ";
                }
                var address = new NcEmailAddress (NcEmailAddress.Kind.From, message.From);
                var mailbox = address.ToMailboxAddress (true);
                if (mailbox != null) {
                    if (!String.IsNullOrWhiteSpace (mailbox.Name)) {
                        attribution += String.Format ("{0} <{1}>", mailbox.Name, mailbox.Address);
                    } else {
                        attribution += mailbox.Address;
                    }
                } else {
                    attribution += message.From;
                }
                attribution += " wrote";
            }
            if (attribution.Length > 0) {
                attribution += ":";
            }
            return attribution;
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
            FailHadUsername}

        ;

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
            } catch (UriFormatException) {
                if (serverName.StartsWith ("http://") || serverName.StartsWith ("https://")) {
                    // The massaging of the URL that happens later, namely prepending "https://",
                    // won't do any good.  In fact it will make things worse.  So give up now.
                    // Since the scheme looks valid, the problem is most likely the host name.
                    return ParseServerWhyEnum.FailBadHost;
                }
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
            if (!String.IsNullOrEmpty (serverURI.UserInfo)) {
                return ParseServerWhyEnum.FailHadUsername;
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

        public static string ParseServerWhyEnumToString (ParseServerWhyEnum why)
        {
            switch (why) {
            case ParseServerWhyEnum.FailBadHost:
                return "The host name has an error.";
            case ParseServerWhyEnum.FailBadPort:
                return "The port number has an error.";
            case ParseServerWhyEnum.FailBadScheme:
                return "The server name scheme has an error.";
            case ParseServerWhyEnum.FailHadQuery:
                return "The server name should not have a query string.";
            case ParseServerWhyEnum.FailUnknown:
                return "The server name has an error.";
            case ParseServerWhyEnum.Success_0:
                return "";
            case ParseServerWhyEnum.FailHadUsername:
                return "The server name should not have an @";
            default:
                NcAssert.CaseError ();
                break;
            }
            return "";
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

        public static McEmailMessage MessageFromMailTo (McAccount account, string urlString, out string body)
        {
            List<NcEmailAddress> addresses;
            string subject;
            if (!ParseMailTo (urlString, out addresses, out subject, out body)) {
                subject = "";
                body = null;
                addresses = new List<NcEmailAddress> ();
            }
            var message = McEmailMessage.MessageWithSubject (account, subject);
            var toList = new List<NcEmailAddress> ();
            var ccList = new List<NcEmailAddress> ();
            var bccList = new List<NcEmailAddress> ();
            foreach (var address in addresses) {
                if (address.kind == NcEmailAddress.Kind.To) {
                    toList.Add (address);
                } else if (address.kind == NcEmailAddress.Kind.Cc) {
                    ccList.Add (address);
                } else if (address.kind == NcEmailAddress.Kind.Bcc) {
                    bccList.Add (address);
                }
            }
            message.To = AddressStringFromList (toList);
            message.Cc = AddressStringFromList (ccList);
            message.Bcc = AddressStringFromList (bccList);
            return message;
        }

        public static string AddressStringFromList (List<NcEmailAddress> addresses)
        {
            MailboxAddress mailbox;
            var addressStrings = new List<string> (addresses.Count);
            foreach (var address in addresses) {
                mailbox = address.ToMailboxAddress (mustUseAddress: true);
                if (mailbox != null) {
                    addressStrings.Add (mailbox.ToString ());
                }
            }
            return String.Join (",", addressStrings);
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

        public static void PopulateMessageRecipients (McAccount account, McEmailMessage message, Action action, McEmailMessage referencedMessage)
        {
            var toList = new List<NcEmailAddress> ();
            var recipientExclusions = new List<string> ();
            recipientExclusions.Add (account.EmailAddr);
            if (EmailHelper.IsReplyAction (action)) {
                string toString = null;
                // Reply-To trumps From
                if (null != referencedMessage.ReplyTo) {
                    toString = referencedMessage.ReplyTo;
                } else if (null != referencedMessage.From) {
                    toString = referencedMessage.From;
                }
                // Some validation
                if (toString != null) {
                    InternetAddress toAddress;
                    if (MailboxAddress.TryParse (toString, out toAddress)) {
                        if (String.Equals ((toAddress as MailboxAddress).Address, account.EmailAddr, StringComparison.OrdinalIgnoreCase)) {
                            // If it looks like we're replying to ourself, we should instead reply to the entire To list from the
                            // referenced message.  This behavior is consistent with other clients, and is typically seen when
                            // replying to a message you sent.  It's an interesting case where a reply could go to multiple people
                            // even though it wasn't a reply-all.  If there was anyone in the CC list of the referenced message,
                            // they'll get picked up in the reply-all scenario in the next block.
                            toList = EmailHelper.AddressList (NcEmailAddress.Kind.To, recipientExclusions, referencedMessage.To);
                        } else {
                            toList.Add (new NcEmailAddress (NcEmailAddress.Kind.To, toString));
                        }
                    }
                }
            }
            foreach (var to in toList) {
                recipientExclusions.Add (to.address);
            }
            message.To = AddressStringFromList (toList);
            if (EmailHelper.Action.ReplyAll == action) {
                // Add the To & Cc list to the CC list, not included this user
                var ccList = EmailHelper.AddressList (NcEmailAddress.Kind.Cc, recipientExclusions, referencedMessage.To, referencedMessage.Cc);
                message.Cc = AddressStringFromList (ccList);
            }
        }

        public static List<NcEmailAddress> CcList (string accountEmailAddress, string toString, string ccString)
        {
            var exclusions = new List<string> (1);
            if (!String.IsNullOrEmpty (accountEmailAddress)) {
                exclusions.Add (accountEmailAddress);
            }
            return AddressList (NcEmailAddress.Kind.Cc, exclusions, toString, ccString);
        }

        public static List<NcEmailAddress> AddressList (NcEmailAddress.Kind kind, List<string> exclusions, params string[] addressStrings)
        {
            if (exclusions == null) {
                exclusions = new List<string> ();
            }
            var exclusionAddresses = new List<InternetAddress> ();
            InternetAddress exclusionAddress;
            foreach (var exclusion in exclusions) {
                if (MailboxAddress.TryParse (exclusion, out exclusionAddress)) {
                    exclusionAddresses.Add (exclusionAddress);
                }
            }
            var addressList = new List<NcEmailAddress> ();
            InternetAddressList addresses;
            bool excluded;
            foreach (var addressString in addressStrings) {
                if (!String.IsNullOrEmpty (addressString) && InternetAddressList.TryParse (addressString, out addresses)) {
                    foreach (var address in addresses.Mailboxes) {
                        excluded = false;
                        foreach (var exclusionAddress_ in exclusionAddresses) {
                            if (IsAccountAlias (exclusionAddress_, address.Address)) {
                                excluded = true;
                                break;
                            }
                        }
                        if (!excluded) {
                            addressList.Add (new NcEmailAddress (kind, address.ToString ()));
                        }
                    }
                }
            }
            return addressList;
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
            result.Append ("Date: ").Append (Pretty.UniversalFullDateTime (message.DateReceived));
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
            result.Append ("When: ").Append (Pretty.UniversalFullDateTime (calendar.StartTime)).Append ("\n");
            result.Append ("Where: ").Append (calendar.Location ?? "").Append ("\n");
            result.Append ("\n\n");
            return result.ToString ();
        }

        public static string QuoteForReply (string s)
        {
            if (String.IsNullOrEmpty (s)) {
                return s;
            }
            string[] lines = s.Split (new Char[] { '\n' });
            StringBuilder builder = new StringBuilder ();

            // If the split pattern matches the tail of s,
            // an extra empty string is added to the array.
            int count = lines.Length;
            if (0 == count) {
                return s;  // unexpected
            }
            if (String.IsNullOrEmpty (lines [count - 1])) {
                count -= 1; // skip the last entry
            }
            for (int i = 0; i < count; i++) {
                var line = lines [i];
                if (String.IsNullOrEmpty (line)) {
                    builder.Append (">");
                } else if ('>' == line [0]) {
                    builder.Append ('>');
                    builder.Append (line);
                } else {
                    builder.Append ("> ");
                    builder.Append (line);
                }
                builder.Append ('\n');
            }
            return builder.ToString ();
        }

        public static string Initials (string fromAddressString)
        {
            // Parse the from address
            if (String.IsNullOrEmpty (fromAddressString)) {
                Log.Error (Log.LOG_UTILS, "Initials passed a null or empty from string");
                return "";
            }
            var initials = "";
            try {
                var mailboxAddress = NcEmailAddress.ParseMailboxAddressString (fromAddressString);
                if (null != mailboxAddress) {
                    McContact contact = new McContact ();
                    NcEmailAddress.ParseName (mailboxAddress, ref contact);
                    initials = ContactsHelper.GetInitials (contact);
                }
                if (String.IsNullOrEmpty (initials)) {
                    foreach (char c in fromAddressString) {
                        if (Char.IsLetterOrDigit (c)) {
                            initials += Char.ToUpper (c);
                            break;
                        }
                    }
                }
            } catch (Exception e) {
                Log.Error (Log.LOG_UTILS, "Initials crashed\n{0}", e.StackTrace);
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

        public static void GetMessageCounts (McAccount account, out int unreadMessageCount, out int deferredMessageCount, out int deadlineMessageCount, out int likelyMessageCount)
        {
            unreadMessageCount = 0;
            deadlineMessageCount = 0;
            deferredMessageCount = 0;
            likelyMessageCount = 0;

            foreach (var accountId in McAccount.GetAllConfiguredNonDeviceAccountIds ()) {
                if (account.ContainsAccount (accountId)) {
                    var inboxFolder = NcEmailManager.InboxFolder (accountId);
                    if (null != inboxFolder) {
                        unreadMessageCount += McEmailMessage.CountOfUnreadMessageItems (inboxFolder.AccountId, inboxFolder.Id);
                        deadlineMessageCount += McEmailMessage.QueryDueDateMessageItems (inboxFolder.AccountId).Count;
                        deferredMessageCount += new NachoDeferredEmailMessages (inboxFolder.AccountId).Count ();
                        likelyMessageCount += new NachoLikelyToReadEmailMessages (inboxFolder).Count ();
                    }
                }
            }
        }


        /// <summary>
        /// Compress the message preview so it is more tightly packed with useful information.
        /// Remove some pieces that the user is unlikely to find useful.  Collapse adjacent
        /// white space into a single space character.
        /// </summary>
        public static string AdjustPreviewText (string raw)
        {
            string adjusted = null;
            if (raw.StartsWith ("<") && (raw.Contains ("<body") || raw.Contains ("<BODY"))) {
                // It looks like it might be HTML.  Parse it as such and see if the <body> tag can be found.
                HtmlDocument html = new HtmlDocument ();
                // Some tags, such as <p>, <br>, and <li>, become white space when rendered.  Since we are
                // just pulling out the text, not rendering it, we want to convert those tags to white space
                // right now.  If this is not done, then "<p>Call me Ishmael.</p><p>Some years ago" will
                // display as "Call me Ishmael.Some years ago" instead of "Call me Ishmael. Some years ago".
                html.LoadHtml (Regex.Replace (raw, @"<(/?[Pp]|/?[Ll][Ii]|[Bb][Rr]\s*/?)>", " "));
                foreach (var bodyNode in html.DocumentNode.Descendants("body")) {
                    adjusted = Regex.Replace (Regex.Replace (bodyNode.InnerText, @"\s+", " "), @"^\s", "");
                    break;
                }
            }
            if (null == adjusted) {
                adjusted = Regex.Replace (Regex.Replace (Regex.Replace (Regex.Replace (Regex.Replace (Regex.Replace (raw,
                    @"!\[(image|cid|img_).*?\]\(http.+?\)", " "),
                    @"\[(http|image|cid|img_).*?\]", " "),
                    @"<(http|mailto)\S*?>", " "),
                    @"https?:\S*", " "),
                    @"\s+", " "),
                    @"^\s", "");
                if (adjusted.EndsWith ("< ") && !adjusted.EndsWith ("<< ")) {
                    // The trailing '<' is probably left over from an incomplete "<http://...".  It is not useful
                    // by itself, so strip it off.
                    adjusted = adjusted.Substring (0, adjusted.Length - 2);
                }
            }
            if (10 > adjusted.Length || !ContainsLetter (adjusted)) {
                // The adjustments stripped out almost everything.  What's left is probably not useful.
                // Use the raw text, only collapsing the white space.
                adjusted = Regex.Replace (raw, @"\s+", " ");
            }
            return adjusted;
        }

        private static bool ContainsLetter (string s)
        {
            if (null == s) {
                return false;
            }
            foreach (char c in s) {
                if (char.IsLetter (c)) {
                    return true;
                }
            }
            return false;
        }

        public static void MarkAsRead (McEmailMessageThread thread, bool force = false)
        {
            var message = thread.SingleMessageSpecialCase ();
            if ((null != message) && !message.IsRead) {
                var body = McBody.QueryById<McBody> (message.BodyId);
                if (force || McBody.IsComplete (body)) {
                    BackEnd.Instance.MarkEmailReadCmd (message.AccountId, message.Id);
                }
            }
        }

        public static McAttachment NoteToAttachment (McNote note)
        {
            var attachment = McAttachment.InsertFile (NcApplication.Instance.Account.Id, ((FileStream stream) => {
                using (var noteStream = new MemoryStream ()) {
                    using (var noteWriter = new StreamWriter (noteStream)) {
                        noteWriter.Write (note.noteContent);
                        noteWriter.Flush ();
                        noteStream.Position = 0;
                        noteStream.CopyTo (stream);
                    }
                }
            }));
            attachment.SetDisplayName (note.DisplayName + ".txt");
            attachment.UpdateSaveFinish ();
            return attachment;
        }
    }
}

