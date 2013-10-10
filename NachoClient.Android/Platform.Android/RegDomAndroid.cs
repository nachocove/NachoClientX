using System;
using System.Runtime.InteropServices;

namespace NachoPlatform
{
    public class RegDom : IPlatformRegDom
	{
		[DllImport("libnachoplatform.so")]
		private static extern string nacho_get_regdom (string domain);

		public string RegDomFromFqdn (string domain) {
			return nacho_get_regdom (domain);
		}
	}
}

