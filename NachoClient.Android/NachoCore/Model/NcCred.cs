using System;
using SQLite;

namespace NachoCore.Model
{
	public class NcCred : NcObject
	{
		public string Username { get; set;}
		public string Password { get; set;}
	}
}
