//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using MimeKit;

namespace NachoCore.Utils
{
    public class EmailHelper
    {
        public EmailHelper ()
        {
        }


        public static bool IsValidEmail (string email)
        {
            RegexUtilities regexUtil = new RegexUtilities ();
            return regexUtil.IsValidEmail (email);
        }

        public static bool IsValidServer (string server)
        {
            if (EmailHelper.IsValidHost (server)) {
                return true;
            }

            //fullServerUri didn't pass...validate host/port separately
            Uri serverURI;
            try {
                serverURI = new Uri ("my://" + server.Trim ());
            } catch {
                return false;
            }

            var host = serverURI.Host;
            var port = serverURI.Port;

            if (!EmailHelper.IsValidHost (host)) {
                return false;
            }

            //host cleared, checking port
            if (!EmailHelper.IsValidPort (port)) {
                return false;
            }

            return true;
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
            return urlString.StartsWith ("mailto:", StringComparison.OrdinalIgnoreCase);
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

        private static bool IsAccountAlias(InternetAddress accountInternetAddress, string match)
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
            Console.WriteLine ("match: '{0}' '{1}' {2}", target, match, String.Equals (target, match, StringComparison.OrdinalIgnoreCase));
            return String.Equals (target, match, StringComparison.OrdinalIgnoreCase);

        }

        public static List<NcEmailAddress> CcList (string accountEmailAddress, string toString, string ccString)
        {
            var ccList = new List<NcEmailAddress> ();

            InternetAddress accountAddress;
            if (String.IsNullOrEmpty(accountEmailAddress) || !MailboxAddress.TryParse (accountEmailAddress, out accountAddress)) {
                accountAddress = null;
            }
            InternetAddressList addresses;
            if (!String.IsNullOrEmpty(toString) && InternetAddressList.TryParse (toString, out addresses)) {
                foreach (var mailboxAddress in addresses.Mailboxes) {
                    if (!IsAccountAlias(accountAddress, mailboxAddress.Address)) {
                        ccList.Add (new NcEmailAddress (NcEmailAddress.Kind.Cc, mailboxAddress.Address));
                    }
                }
            }
            if (!String.IsNullOrEmpty(ccString) && InternetAddressList.TryParse (ccString, out addresses)) {
                foreach (var mailboxAddress in addresses.Mailboxes) {
                    if (!IsAccountAlias(accountAddress, mailboxAddress.Address)) {
                        ccList.Add (new NcEmailAddress (NcEmailAddress.Kind.Cc, mailboxAddress.Address));
                    }
                }
            }
            return ccList;
        }
    }
}

