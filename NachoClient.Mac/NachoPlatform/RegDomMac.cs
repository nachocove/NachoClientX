using System;
using System.Text;
using System.Runtime.InteropServices;

namespace NachoPlatform
{
    public class RegDom : IPlatformRegDom
    {
        private static volatile RegDom instance;
        private static object syncRoot = new Object();

        private RegDom () {}

        public static RegDom Instance
        {
            get 
            {
                if (instance == null) 
                {
                    lock (syncRoot) 
                    {
                        if (instance == null) 
                            instance = new RegDom ();
                    }
                }
                return instance;
            }
        }

        [DllImport("__Internal")]
        private static extern void nacho_get_regdom (StringBuilder dest, uint limit, string domain);

        public string RegDomFromFqdn (string domain)
        {
            // RFC 2818: The DNS itself places only one restriction on the particular labels that can be used to identify resource records. That one restriction relates to the length of the label and the full name. The length of any one label is limited to between 1 and 63 octets. A full domain name is limited to 255 octets (including the separators)."
            lock (syncRoot) {
                StringBuilder sb = new StringBuilder (256);
                nacho_get_regdom (sb, (uint)sb.Capacity, domain);
                return sb.ToString ();
            }
        }
    }
}
