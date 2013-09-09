using System;
using SQLite;

namespace NachoCore.Model
{
	public class NcAccount
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		public string Username { get; set; }
		public string EmailAddr { get; set; }
	}
}

