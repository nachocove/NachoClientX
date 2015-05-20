using System;
using System.Diagnostics;
using System.Linq;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McServer : McAbstrObjectPerAcc
    {
        public McServer ()
        {
            Path = Default_Path;
            Scheme = "https";
            Port = 443;
        }

        public McAccount.AccountCapabilityEnum Capabilities { set; get; }

        public string Host { get; set; }

        public const string Default_Path = "/Microsoft-Server-ActiveSync";
        public const string GMail_Host = "m.google.com";
        public const string GMail_MX_Suffix = "aspmx.l.google.com";
        public const string GMail_MX_Suffix2 = "googlemail.com";
        public const string HotMail_Host = "s.outlook.com";
        public const string HotMail_Suffix = "hotmail.com";
        public const string Outlook_Suffix = "outlook.com";

        public string Path { get; set; }

        public string Scheme { get; set; }

        public int Port { get; set; }

        public bool UsedBefore { get; set; }

        // We want to remember if the user entered their
        // own server or if we figured it out on our own.
        public string UserSpecifiedServerName { get; set; }

        /// <summary>
        /// The base URI for the server.
        /// </summary>
        public Uri BaseUri ()
        {
            return new Uri (BaseUriString ());
        }

        public string BaseUriString ()
        {
            string uriString;
            if (443 == Port && "https" == Scheme) {
                uriString = string.Format ("{0}://{1}{2}", Scheme, Host, Path);
            } else {
                uriString = string.Format ("{0}://{1}:{2}{3}", Scheme, Host, Port, Path);
            }
            return uriString;
        }

        /// <summary>
        /// The base URI for the server at the given host.
        /// </summary>
        public static Uri BaseUriForHost (string host)
        {
            McServer dummy = new McServer () {
                Host = host,
            };
            return dummy.BaseUri ();
        }

        public bool HostIsWellKnown ()
        {
            return HostIsGMail () || HostIsHotMail ();
        }

        public bool HostIsHotMail ()
        {
            return Host.EndsWith (McServer.HotMail_Suffix, StringComparison.OrdinalIgnoreCase) ||
            Host.EndsWith (McServer.Outlook_Suffix, StringComparison.OrdinalIgnoreCase);
        }

        public bool HostIsGMail ()
        {
            return Host.EndsWith (McServer.GMail_Host, StringComparison.OrdinalIgnoreCase);
        }

        public static McServer Create (int accountId, McAccount.AccountCapabilityEnum capabilities, Uri uri)
        {
            return new McServer () {
                AccountId = accountId,
                Capabilities = capabilities,
                Host = uri.Host,
                Path = uri.AbsolutePath,
                Scheme = uri.Scheme,
                Port = uri.Port,
            };
        }

        public static McServer Create (int accountId, McAccount.AccountCapabilityEnum capabilities, string host, int port)
        {
            return new McServer () {
                AccountId = accountId,
                Capabilities = capabilities,
                Host = host,
                Path = null,
                Scheme = null,
                Port = port,
            };
        }

        public void CopyFrom (McServer src)
        {
            Capabilities = src.Capabilities;
            Host = src.Host;
            Path = src.Path;
            Scheme = src.Scheme;
            Port = src.Port;
        }

        public bool IsSameServer (McServer match)
        {
            if (Capabilities != match.Capabilities) {
                return false;
            }
            if ((Host != match.Host) || (Port != match.Port)) {
                return false;
            }
            if (!String.Equals (Scheme, match.Scheme, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            if (!String.Equals (Path, match.Path, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            return true;
        }

        public static McServer QueryByHost (int accountId, string host)
        {
            return NcModel.Instance.Db.Table<McServer> ().Where (x => 
                accountId == x.AccountId &&
            host == x.Host
            ).SingleOrDefault ();
        }

        public static McServer QueryByAccountIdAndCapabilities (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            var servers = McServer.QueryByAccountId<McServer> (accountId);
            foreach (var server in servers) {
                if (capabilities == (capabilities & server.Capabilities)) {
                    return server;
                }
            }
            return null;
        }
    }
}
