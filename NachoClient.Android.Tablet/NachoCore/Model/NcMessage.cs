using System;
using SQLite;

namespace NachoCore.Model
{
	public class NcMessage
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		[Indexed]
		public int AccountId { get; set; }
		[Indexed]
		public string ServerId { get; set; }
		[Indexed]
		public int FolderId { get; set; }
	}
}

