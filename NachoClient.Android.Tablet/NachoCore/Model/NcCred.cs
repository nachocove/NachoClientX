using System;
using SQLite;

namespace NachoCore.Model
{
	public class NcCred
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		public string Username { get; set;}
		public string Password { get; set;}
	}
}
