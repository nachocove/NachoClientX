using System;
using SQLite;

namespace NachoCore.Model
{
	public class NcAccount : NcEventable
	{
		public string EmailAddr { get; set; }
        public string DisplayName { get; set; }
        public string Culture { get; set; }
		// Relationships.
		public int CredId { get; set; }
		public int ServerId { get; set; }
		public int ProtocolStateId { get; set; }
	}
}

