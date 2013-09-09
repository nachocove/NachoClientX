using System;

namespace NachoCore.Model
{
	public class NcServer
	{
		public string Fqdn { get; set; }
		public string Path { get; set; }
		public string Scheme { get; set; }
		public int Port { get; set; }
	}
}
