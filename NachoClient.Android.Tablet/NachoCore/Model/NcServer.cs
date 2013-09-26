using System;
using SQLite;

namespace NachoCore.Model
{
	public class NcServer
	{
		public NcServer () {
			// FIXME - need a per-protocol subclass. This is AS-specific.
			Path = "/Microsoft-Server-ActiveSync";
			Scheme = "https";
			Port = 443;
		}
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		public string Fqdn { get; set; }
		public string Path { get; set; }
		public string Scheme { get; set; }
		public int Port { get; set; }
	}
}
