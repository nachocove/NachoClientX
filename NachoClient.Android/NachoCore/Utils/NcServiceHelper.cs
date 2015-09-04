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
            new Info { s = McAccount.AccountServiceEnum.Aol, n = McServer.Aol_Suffix },
            new Info { s = McAccount.AccountServiceEnum.Exchange, n = null },
            new Info { s = McAccount.AccountServiceEnum.GoogleDefault, n = McServer.GMail_Suffix },
            new Info { s = McAccount.AccountServiceEnum.GoogleExchange, n = null },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = McServer.HotMail_Suffix },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = McServer.Outlook_Suffix },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = McServer.Live_Suffix },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = McServer.Msn_Suffix },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = McServer.HotMail_Suffix },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = McServer.Outlook_Suffix },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = McServer.Live_Suffix },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = McServer.Msn_Suffix },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = McServer.ICloud_Suffix },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = McServer.ICloud_Suffix3 },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = McServer.ICloud_Suffix2 },
            new Info { s = McAccount.AccountServiceEnum.IMAP_SMTP, n = null },
            new Info { s = McAccount.AccountServiceEnum.Office365Exchange, n = null },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = McServer.HotMail_Suffix },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = McServer.Outlook_Suffix },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = McServer.Live_Suffix },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = McServer.Msn_Suffix },
            new Info { s = McAccount.AccountServiceEnum.Yahoo, n = McServer.Yahoo_Suffix },
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

