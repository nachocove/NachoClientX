using System;
using SQLite;

namespace NachoCore.Model
{
	public class NcAccount
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		public string EmailAddr { get; set; }
		// Relationships.
		public int CredId { get; set; }
		public int ServerId { get; set; }
		public int ProtocolStateId { get; set; }
	}
}

