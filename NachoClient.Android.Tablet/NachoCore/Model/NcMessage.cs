using System;
using SQLite;

namespace NachoCore.Model
{
	public abstract class NcMessage : NcEventable
	{
		[Indexed]
		public string ServerId { get; set; }
		[Indexed]
		public int FolderId { get; set; }
	}
}

