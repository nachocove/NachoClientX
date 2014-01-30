using System;
using SQLite;

namespace NachoCore.Model
{
    public class McPendingUpdate : McObjectPerAccount
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
            Download,
            // Search for something on the server. Note that pending searches aren't considered relevant across app
            // re-starts, and so they are purged from the pending update queue on app launch.
            Search,
            Move,
            MarkRead,
        };

        public enum DataTypes
        {
            EmailMessage = 0,
            Attachment,
            Folder,
            Contact}
        ;

        // Parameterless constructor only here for use w/LINQ.
        public McPendingUpdate ()
        {
        }

        public McPendingUpdate (int accountId)
        {
            AccountId = accountId;
            Token = DateTime.UtcNow.Ticks.ToString ();

        }

        public Operations Operation { set; get; }

        public DataTypes DataType { set; get; }

        [Indexed]
        public bool IsDispatched { set; get; }

        [Indexed]
        // For use by SendMail & MoveItem ONLY!
        public int EmailMessageId { set; get; }

        [Indexed]
        // For use by any command.
        public string EmailMessageServerId { set; get; }

        [Indexed]
        public string FolderServerId { set; get; }

        public string ServerId { set; get; }

        [Indexed]
        public int AttachmentId { set; get; }

        public string Prefix { set; get; }

        public uint MaxResults { set; get; }

        [Indexed]
        public string Token { set; get; }

        public string DestFolderServerId { set; get; }
    }
}

