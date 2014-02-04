using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;

namespace NachoCore.Model
{
    public class McPending : McObjectPerAccount
    {
        public class ReWrite
        {
            public enum ActionEnum
            {
                Replace,
                Delete,
            };

            public enum FieldEnum
            {
                ServerId,
                ParentId,
            };

            public ActionEnum Action { get; set; }

            public FieldEnum Field { get; set; }

            public string Match { get; set; }

            public string ReplaceWith { get; set; }
        }

        public enum Operations
        {
            FolderCreate,
            FolderUpdate,
            FolderDelete,
            // Send means to transmit the record. This only applies to EmailMessage right now.
            EmailSend,
            // Download means to pull down a file associated with the record.
            AttachmentDownload,
            // Search for something on the server. Note that pending searches aren't considered relevant across app
            // re-starts, and so they are purged from the pending update queue on app launch.
            ContactSearch,
            EmailDelete,
            EmailMove,
            EmailMarkRead,
        };
        // Parameterless constructor only here for use w/LINQ.
        public McPending ()
        {
        }

        public McPending (int accountId)
        {
            AccountId = accountId;
            Token = DateTime.UtcNow.Ticks.ToString ();

        }

        public static List<McPending> ToList (int accountId)
        {
            return BackEnd.Instance.Db.Table<McPending> ()
                    .Where (x => x.AccountId == accountId)
                    .OrderBy (x => x.Id).ToList ();
        }

        public void Update ()
        {
            BackEnd.Instance.Db.Update (this);
        }

        public void Delete ()
        {
            BackEnd.Instance.Db.Delete (this);
        }

        public Operations Operation { set; get; }
        // For FolderCreate, the value of ServerId is a provisional GUID.
        // The BE uses the GUID until the FolderCreate can be executed by the
        // server. After that, the GUID is then replaced by the server-supplied
        // ServerId value throughout the DB.
        [Indexed]
        public string ServerId { set; get; }

        [Indexed]
        public string ParentId { set; get; }

        [Indexed]
        public bool IsDispatched { set; get; }

        [Indexed]
        public string DisplayName { set; get; }

        [Indexed]
        // For use by SendMail & MoveItem ONLY!
        public int EmailMessageId { set; get; }

        [Indexed]
        // For use by any command.
        public string EmailMessageServerId { set; get; }

        [Indexed]
        public string FolderServerId { set; get; }

        [Indexed]
        public int AttachmentId { set; get; }

        public string Prefix { set; get; }

        public uint MaxResults { set; get; }

        [Indexed]
        public string Token { set; get; }

        public string DestFolderServerId { set; get; }

        public enum ActionEnum {
            DoNothing,
            Update,
            Delete,
        };

        public ActionEnum ApplyReWrites (List<ReWrite> reWrites)
        {
            bool updateNeeded = false;
            foreach (var reWrite in reWrites) {
                switch (reWrite.Field) {
                case ReWrite.FieldEnum.ServerId:
                    if (null != ServerId && ServerId == reWrite.Match) {
                        ServerId = reWrite.ReplaceWith;
                        updateNeeded = true;
                    }
                    break;
                }
            }
            return (updateNeeded) ? ActionEnum.Update : ActionEnum.DoNothing;
        }
    }
}

