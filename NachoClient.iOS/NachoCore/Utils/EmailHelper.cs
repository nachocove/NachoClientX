//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

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

        public static bool IsServiceUnsupported(string emailAddress, out string serviceName)
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
    }
}

