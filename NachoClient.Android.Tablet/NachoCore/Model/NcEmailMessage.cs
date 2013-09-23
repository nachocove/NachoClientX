using System;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
	public class NcEmailMessage : NcMessage
	{
		public string Body { set; get; }
		public string Encoding { set; get; }
		[Indexed]
		public string From { set; get; }
		[Indexed]
		public string To { set; get; }
		[Indexed]
		public string Subject { set; get; }
		public string ReplyTo { set; get; }
		public DateTime DateReceived { set; get; }
		public string DisplayTo { set; get; }
		[Indexed]
		public uint Importance { set; get; }
		[Indexed]
		public bool Read { set; get; }
		public string MessageClass { set; get; }
	}
}

