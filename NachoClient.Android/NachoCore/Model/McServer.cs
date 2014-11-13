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
        public const string HotMail_Host = "s.outlook.com";

        public string Path { get; set; }

        public string Scheme { get; set; }

        public int Port { get; set; }

        public bool UsedBefore { get; set; }

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
