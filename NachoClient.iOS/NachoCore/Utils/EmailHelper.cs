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
    }
}

