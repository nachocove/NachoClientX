using System;
using SQLite;

namespace NachoCore.Model
{
	public class NcPendingUpdate : NcObject
	{
		public enum Operations {
			Write=0, // Write means to change the record's values, including the act of record creation.
			Delete,  // Delete means to eliminate the record.
			Send, // Send means to transmit the record. This only applies to EmailMessage right now.
			Download // Download means to pull down a file associated with the record.
		};
		public enum DataTypes {EmailMessage=0, Attachment};

		public Operations Operation { set; get; }
		public DataTypes DataType { set; get; }
		[Indexed]
		public int AccountId { set; get; }
		[Indexed]
		public bool IsDispatched { set; get; }

		// For EmailMessage Sends:
		[Indexed]
		public int EmailMessageId { set; get; }

		// For EmailMessage Deletes:
		[Indexed]
		public int FolderId { set; get; }
		public string ServerId { set; get; }

		// For Attachment Downloads:
		[Indexed]
		public int AttachmentId { set; get; }
	}
}

