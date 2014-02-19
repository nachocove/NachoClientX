using System;
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

		[DllImport("libnachoplatform.so")]
		private static extern string nacho_get_regdom (string domain);

		public string RegDomFromFqdn (string domain) {
			return nacho_get_regdom (domain);
		}
	}
}

