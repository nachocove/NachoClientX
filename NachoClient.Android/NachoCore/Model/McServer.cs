using System;
using SQLite;

namespace NachoCore.Model
{
    public class McServer : McObject
    {
        public McServer ()
        {
            // FIXME - need a per-protocol subclass. This is AS-specific.
            Path = "/Microsoft-Server-ActiveSync";
            Scheme = "https";
            Port = 443;
        }

        public string Fqdn { get; set; }

        public string Path { get; set; }

        public string Scheme { get; set; }

        public int Port { get; set; }

        public static McServer Create (Uri uri)
        {
            return new McServer () {
                Fqdn = uri.Host,
                Path = uri.AbsolutePath,
                Scheme = uri.Scheme,
                Port = uri.Port
            };
        }

        public void Update (McServer src)
        {
            // FIXME Do we need a generic way to do this, using reflection?
            Fqdn = src.Fqdn;
            Path = src.Path;
            Scheme = src.Scheme;
            Port = src.Port;
        }
    }
}
