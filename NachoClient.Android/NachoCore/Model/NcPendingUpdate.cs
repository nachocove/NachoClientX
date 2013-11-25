using System;
using SQLite;

namespace NachoCore.Model
{
    public class NcPendingUpdate : NcObject
    {
        public enum Operations
        {
            // Write means to change the record's values, including the act of record creation.
            Write = 0,
            // Delete means to eliminate the record.
            Delete,
            // Send means to transmit the record. This only applies to EmailMessage right now.
            Send,
            // Download means to pull down a file associated with the record.
            Download}
        ;

        public enum DataTypes
        {
            EmailMessage = 0,
            Attachment,
            Folder}
        ;

        public Operations Operation { set; get; }

        public DataTypes DataType { set; get; }

        [Indexed]
        public int AccountId { set; get; }

        [Indexed]
        public bool IsDispatched { set; get; }
        // For EmailMessage Sends:
        [Indexed]
        public int EmailMessageId { set; get; }
        // For EmailMessage Deletes and Folder Creations:
        [Indexed]
        public int FolderId { set; get; }
        // For EmailMessage Deletes:
        public string ServerId { set; get; }
        // For Attachment Downloads:
        [Indexed]
        public int AttachmentId { set; get; }
    }
}

