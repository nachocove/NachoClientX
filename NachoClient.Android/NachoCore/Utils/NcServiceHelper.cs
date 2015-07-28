//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class NcServiceHelper
    {
        public NcServiceHelper ()
        {
        }

        class Info
        {
            public McAccount.AccountServiceEnum s;
            public string n;
        };

        static readonly Info[] services = {
            new Info { s = McAccount.AccountServiceEnum.Aol, n = "aol.com" },
            new Info { s = McAccount.AccountServiceEnum.Exchange, n = null },
            new Info { s = McAccount.AccountServiceEnum.GoogleDefault, n = "gmail.com" },
            new Info { s = McAccount.AccountServiceEnum.GoogleExchange, n = null },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "hotmail.com" },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "outlook.com" },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "live.com" },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "msn.com" },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "hotmail.com" },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "outlook.com" },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "live.com" },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "msn.com" },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "icloud.com" },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "mac.com" },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "me.com" },
            new Info { s = McAccount.AccountServiceEnum.IMAP_SMTP, n = null },
            new Info { s = McAccount.AccountServiceEnum.Office365Exchange, n = null },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "hotmail.com" },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "outlook.com" },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "live.com" },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "msn.com" },
            new Info { s = McAccount.AccountServiceEnum.Yahoo, n = "yahoo.com" },
        };

        public static bool IsServiceUnsupported (string emailAddress, out string serviceName)
        {
            serviceName = "";
            return false;
        }

        public static bool DoesAddressMatchService (string emailAddress, McAccount.AccountServiceEnum service)
        {
            foreach (var s in services) {
                if (s.s != service) {
                    continue;
                }
                if (null == s.n) {
                    return true;
                }
                if (emailAddress.EndsWith ("@" + s.n, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
                if (emailAddress.EndsWith ("." + s.n, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        public static string AccountServiceName (McAccount.AccountServiceEnum service)
        {
            switch (service) {
            case McAccount.AccountServiceEnum.None:
                return "";
            case McAccount.AccountServiceEnum.Exchange:
                return "Exchange";
            case McAccount.AccountServiceEnum.HotmailExchange:
                return "Hotmail";
            case McAccount.AccountServiceEnum.OutlookExchange:
                return "Outlook";
            case McAccount.AccountServiceEnum.Office365Exchange:
                return "Office 365";
            case McAccount.AccountServiceEnum.GoogleExchange:
                return "Google Apps";
            case McAccount.AccountServiceEnum.GoogleDefault:
                return "GMail";
            case McAccount.AccountServiceEnum.HotmailDefault:
                return "Hotmail";
            case McAccount.AccountServiceEnum.Aol:
                return "Aol";
            case McAccount.AccountServiceEnum.IMAP_SMTP:
                return "IMAP";
            case McAccount.AccountServiceEnum.Yahoo:
                return "Yahoo!";
            case McAccount.AccountServiceEnum.iCloud:
                return "iCloud";
            default:
                NcAssert.CaseError (String.Format ("AccountServiceName: unknown {0}", service));
                return "";
            }
        }

    }
}

