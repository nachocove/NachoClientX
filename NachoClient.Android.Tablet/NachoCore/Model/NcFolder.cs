using System;
using SQLite;

namespace NachoCore.Model
{
	public class NcFolder
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		[Indexed]
		public int AccountId { get; set; }
		[Indexed]
		public string ServerId { get; set; }
		[Indexed]
		public string ParentId { get; set; }
		public string AsSyncKey { get; set; }
		public bool AsSyncRequired { get; set; }
		[Indexed]
		public string DisplayName { get; set; }
		public string Type { get; set; }
	}
}

