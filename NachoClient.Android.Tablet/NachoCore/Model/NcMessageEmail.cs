using System;
using SQLite;

namespace NachoCore.Model
{
	public class NcMessageEmail : NcMessage
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		public string Body { set; get;}
		public string Encoding { set; get; }
		public string From { set; get; }
		[Indexed]
		public string To { set; get; }
		[Indexed]
		public string Subject { set; get; }
		public string ReplyTo { set; get; }
	}
}

