using System;
using System.Runtime.InteropServices;

namespace NachoPlatform
{
    public class RegDom : IPlatformRegDom
    {
        [DllImport("__Internal")]
        private static extern string nacho_get_regdom (string domain);

        public string RegDomFromFqdn (string domain) {
            return nacho_get_regdom (domain);
        }
    }
}

