using System;
using SQLite;

namespace NachoCore.Model
{
	public class NcServer
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		public string Fqdn { get; set; }
		public string Path { get; set; }
		public string Scheme { get; set; }
		public int Port { get; set; }
	}
}
