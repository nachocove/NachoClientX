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
            Path = "/Microsoft-Server-ActiveSync";
            Scheme = "https";
            Port = 443;
        }

        public string Host { get; set; }

        public const string GMail_Host = "m.google.com";
        public const string GMail_Uri = "https://" + GMail_Host;
        public const string GMail_MX_Suffix = "ASPMX.L.GOOGLE.com";
        public const string HotMail_Host = "s.outlook.com";
        public const string HotMail_Suffix = "hotmail.com";
        public const string Outlook_Suffix = "outlook.com";

        public string Path { get; set; }

        public string Scheme { get; set; }

        public int Port { get; set; }

        public bool UsedBefore { get; set; }

        // We want to remember if the user entered their
        // own server or if we figured it out on our own.
        public bool UserSpecifiedServer { get; set; }

        public bool HostIsHotMail ()
        {
            return Host.EndsWith (McServer.HotMail_Suffix, StringComparison.OrdinalIgnoreCase) ||
            Host.EndsWith (McServer.Outlook_Suffix, StringComparison.OrdinalIgnoreCase);
        }

        public bool HostIsGMail ()
        {
            return Host.EndsWith (McServer.GMail_Host, StringComparison.OrdinalIgnoreCase);
        }

        public static McServer Create (int accountId, Uri uri)
        {
            return new McServer () {
                AccountId = accountId,
                Host = uri.Host,
                Path = uri.AbsolutePath,
                Scheme = uri.Scheme,
                Port = uri.Port
            };
        }

        public void CopyFrom (McServer src)
        {
            Host = src.Host;
            Path = src.Path;
            Scheme = src.Scheme;
            Port = src.Port;
        }

        // <DEBUG>
        public override int Delete ()
        {
            Log.Error (Log.LOG_AS, "McServer.Delete called by {0}", new StackTrace ().ToString ());
            return base.Delete ();
        }
        // </DEBUG>
        public static McServer QueryByHost (string host)
        {
            return NcModel.Instance.Db.Table<McServer> ().Where (x => host == x.Host).SingleOrDefault ();
        }
    }
}
