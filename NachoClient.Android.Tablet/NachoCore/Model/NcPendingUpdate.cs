using System;
using SQLite;

namespace NachoCore.Model
{
	public class NcPendingUpdate : NcObject
	{
		public enum Operations {Write=0, Delete, Send};
		public enum DataTypes {EmailMessage=0};

		public Operations Operation { set; get;}
		public DataTypes DataType { set; get;}
		[Indexed]
		public int AccountId { set; get;}
		[Indexed]
		public bool IsDispatched { set; get;}

		// For EmailMessage Sends:
		[Indexed]
		public int EmailMessageId { set; get;}

		// For EmailMessage Deletes:
		[Indexed]
		public int FolderId { set; get; }
		public string ServerId { set; get; }
	}
}

