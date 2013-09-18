using System;
using SQLite;

namespace NachoCore.Model
{
	public class NcMessage : NcEventable
	{
		[Indexed]
		public int AccountId { get; set; }
		[Indexed]
		public string ServerId { get; set; }
		[Indexed]
		public int FolderId { get; set; }
	}
}

