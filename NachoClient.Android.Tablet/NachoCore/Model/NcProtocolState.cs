using System;
using SQLite;

namespace NachoCore.Model
{
	public class NcProtocolState
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		public string AsProtocolVersion { get; set;}
		public string AsPolicyKey { get; set;}
		public string AsSyncKey { get; set;}
		public NcProtocolState() {
			AsPolicyKey = "0";
			AsSyncKey = "0";
		}
	}
}

