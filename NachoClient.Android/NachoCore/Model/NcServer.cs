using System;
using SQLite;

namespace NachoCore.Model
{
    public class NcServer : NcObject
    {
        public NcServer () {
            // FIXME - need a per-protocol subclass. This is AS-specific.
            Path = "/Microsoft-Server-ActiveSync";
            Scheme = "https";
            Port = 443;
        }
        public string Fqdn { get; set; }
        public string Path { get; set; }
        public string Scheme { get; set; }
        public int Port { get; set; }

        public static NcServer Create (Uri uri) {
            return new NcServer () {
                Fqdn = uri.Host,
                Path = uri.AbsolutePath,
                Scheme = uri.Scheme,
                Port = uri.Port
            };
        }
    }
}
