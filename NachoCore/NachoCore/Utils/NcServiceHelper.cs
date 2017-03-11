//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using System.Linq;

namespace NachoCore.Utils
{
    public class NcServiceHelper
    {
        public NcServiceHelper ()
        {
        }

        static readonly Dictionary<McAccount.AccountServiceEnum, DomainVerifier> services = new Dictionary<McAccount.AccountServiceEnum, DomainVerifier> {
            { McAccount.AccountServiceEnum.Aol, new AolVerifier () },
            { McAccount.AccountServiceEnum.Exchange, new AlwaysValid () },
            { McAccount.AccountServiceEnum.GoogleDefault, new GoogleDefaultVerifier () },
            { McAccount.AccountServiceEnum.GoogleExchange, new AlwaysValid () },
            { McAccount.AccountServiceEnum.HotmailDefault, new HotmailOutLookVerifier () },
            { McAccount.AccountServiceEnum.HotmailExchange, new HotmailOutLookVerifier () },
            { McAccount.AccountServiceEnum.iCloud, new iCloudVerifier () },
            { McAccount.AccountServiceEnum.IMAP_SMTP, new AlwaysValid () },
            { McAccount.AccountServiceEnum.Office365Exchange, new AlwaysValid () },
            { McAccount.AccountServiceEnum.OutlookExchange, new HotmailOutLookVerifier () },
            { McAccount.AccountServiceEnum.Yahoo, new YahooVerifier () },
        };

        public static bool IsServiceUnsupported (string emailAddress, out string serviceName)
        {
            serviceName = "";
            return false;
        }

        public static bool DoesAddressMatchService (string emailAddress, McAccount.AccountServiceEnum service)
        {
            if (!services.Keys.Contains (service)) {
                Log.Error (Log.LOG_SYS, "Unhandled service {0} in services", service);
                return false;
            }
            return services [service].isValid (emailAddress);
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
                return "Gmail";
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
            case McAccount.AccountServiceEnum.SalesForce:
                return "Salesforce";
            default:
                NcAssert.CaseError (String.Format ("AccountServiceName: unknown {0}", service));
                return "";
            }
        }

        #region DomainVerifier
        abstract public class DomainVerifier
        {
            public abstract string[] validSuffixes { get; }

            public virtual bool isValid (string emailAddress)
            {
                foreach (string suffix in validSuffixes) {
                    if (emailAddress.EndsWith ("@" + suffix, StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                    if (emailAddress.EndsWith ("." + suffix, StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
                return false;
            }
        }

        public class AlwaysValid : DomainVerifier
        {
            public override string[] validSuffixes {
                get {
                    return new string[]{ };
                }
            }
            public override bool isValid (string emailAddress)
            {
                return true;
            }
        }

        public class AolVerifier : DomainVerifier
        {
            public override string[] validSuffixes {
                get {
                    return new string[]{ McServer.Aol_Suffix, };
                }
            }
        }

        public class GoogleDefaultVerifier : DomainVerifier
        {
            public override string[] validSuffixes {
                get {
                    return new string[]{ McServer.GMail_Suffix, };
                }
            }
        }

        public class HotmailOutLookVerifier : DomainVerifier
        {
            // TODO: replace this code with data that gets pushed to the app.
            public override string[] validSuffixes {
                get {
                    return new string[] {
                        "outlook.com",
                        "live.com",
                        "hotmail.com",
                        "msn.com",
                        "outlook.sa",
                        "hotmail.com.ar",
                        "outlook.com.ar",
                        "live.com.ar",
                        "hotmail.com.au",
                        "outlook.com.au",
                        "live.com.au",
                        "windowslive.com",
                        "outlook.at",
                        "live.at",
                        "hotmail.be",
                        "outlook.be",
                        "live.be",
                        "hotmail.com.br",
                        "outlook.com.br",
                        "hotmail.ca",
                        "live.ca",
                        "hotmail.cz",
                        "outlook.cz",
                        "hotmail.cl",
                        "outlook.cl",
                        "live.cl",
                        "live.cn",
                        "hotmail.dk",
                        "outlook.dk",
                        "live.dk",
                        "hotmail.fi",
                        "live.fi",
                        "hotmail.fr",
                        "outlook.fr",
                        "live.fr",
                        "hotmail.gr",
                        "outlook.com.gr",
                        "hotmail.de",
                        "outlook.de",
                        "live.de",
                        "hotmail.com.hk",
                        "live.hk",
                        "hotmail.hu",
                        "outlook.hu",
                        "hotmail.co.in",
                        "outlook.in",
                        "live.in",
                        "hotmail.co.id",
                        "outlook.co.id",
                        "outlook.ie",
                        "live.ie",
                        "hotmail.co.il",
                        "outlook.co.il",
                        "hotmail.it",
                        "outlook.it",
                        "live.it",
                        "hotmail.co.jp",
                        "outlook.jp",
                        "live.jp",
                        "hotmail.co.kr",
                        "outlook.kr",
                        "live.co.kr",
                        "hotmail.lv",
                        "outlook.lv",
                        "hotmail.lt",
                        "hotmail.my",
                        "outlook.my",
                        "live.com.my",
                        "live.com.mx",
                        "hotmail.nl",
                        "live.nl",
                        "hotmail.no",
                        "live.no",
                        "hotmail.ph",
                        "outlook.ph",
                        "live.com.ph",
                        "outlook.pt",
                        "live.com.pt",
                        "live.ru",
                        "hotmail.rs",
                        "hotmail.sg",
                        "outlook.sg",
                        "live.com.sg",
                        "hotmail.sk",
                        "outlook.sk",
                        "hotmail.co.za",
                        "live.co.za",
                        "hotmail.es",
                        "outlook.es",
                        "hotmail.se",
                        "live.se",
                        "hotmail.com.tw",
                        "livemail.tw",
                        "hotmail.co.th",
                        "outlook.co.th",
                        "hotmail.com.tr",
                        "outlook.com.tr",
                        "hotmail.com.vn",
                        "outlook.com.vn",
                        "hotmail.co.uk",
                        "live.co.uk",
                    };
                }
            }
        }

        public class iCloudVerifier : DomainVerifier
        {
            public override string[] validSuffixes {
                get {
                    return new string[]{ McServer.ICloud_Suffix, McServer.ICloud_Suffix3, McServer.ICloud_Suffix2 };
                }
            }
        }

        public class YahooVerifier : DomainVerifier
        {
            public override string[] validSuffixes {
                get {
                    return McServer.Yahoo_Suffixes;
                }
            }
        }
        #endregion
    }
}

