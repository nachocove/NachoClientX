using SQLite;
using System;

namespace NachoCore.Model
{
	public class NcObject
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
	}
}

