using SQLite;
using System;
using System.IO;
using System.Collections.Generic;
using NachoCore.Utils;


namespace NachoCore.Model
{
    public class McAttachment : McAbstrFileDesc, IFilesViewItem
    {
        public override string GetFilePathSegment ()
        {
            return "attachments";
        }

        public static McAttachment InsertSaveStart (int accountId)
        {
            var att = new McAttachment () {
                AccountId = accountId,
            };
            att.CompleteInsertSaveStart ();
            return att;
        }

        /// <summary>
        /// Create a new McAttachment. The contents are filled in by passing a FileStream for the McAttachment's file to a delegate.
        /// </summary>
        /// <returns>A new McAttachment object that has been added to the database</returns>
        public static McAttachment InsertFile (int accountId, WriteFileDelegate writer)
        {
            var att = new McAttachment () {
                AccountId = accountId,
            };
            att.CompleteInsertFile (writer);
            return att;
        }

        public static McAttachment InsertError (int accountId)
        {
            var att = new McAttachment () {
                AccountId = accountId,
                FilePresence = FilePresenceEnum.Error,
            };
            att.CompleteInsertSaveStart ();
            return att;
        }

        /// <summary>
        /// The ID of the item that owns this attachment.
        /// </summary>
        [Indexed]
        public int ItemId { get; set; }

        /// <summary>
        /// The type of the item that owns this attachment.
        /// </summary>
        public McAbstrFolderEntry.ClassCodeEnum ClassCode
        {
            get {
                return _classCode;
            }
            set {
                // Only e-mail messages and calendar items can own attachments.
                // But an attachment will have a class code of NeverInFolder by
                // default, and the database code might try to set that value
                // when loading an unowned attachment.
                NcAssert.True (McAbstrFolderEntry.ClassCodeEnum.Email == value ||
                    McAbstrFolderEntry.ClassCodeEnum.Calendar == value ||
                    McAbstrFolderEntry.ClassCodeEnum.NeverInFolder == value,
                    "Only e-mail messages and calendar items can own attachments.");
                _classCode = value;
            }
        }
        private McAbstrFolderEntry.ClassCodeEnum _classCode = McAbstrFolderEntry.ClassCodeEnum.NeverInFolder;

        [Indexed]
        public string FileReference { get; set; }

        public uint Method { get; set; }

        public string ContentId { get; set; }

        public string ContentLocation { get; set; }

        public bool IsInline { get; set; }

        public uint VoiceSeconds { get; set; }

        public int VoiceOrder { get; set; }

        public string ContentType { get; set; }

        public static List<McAttachment> QueryByItemId (int accountId, int itemId, McAbstrFolderEntry.ClassCodeEnum classCode)
        {
            if (McAbstrFolderEntry.ClassCodeEnum.Email == classCode || McAbstrFolderEntry.ClassCodeEnum.Calendar == classCode) {
                // Only e-mail messages and calendar items can own attachments.
                // TODO We think that exceptions can own attachments, but that hasn't been confirmed.
                return NcModel.Instance.Db.Query<McAttachment> (
                    "SELECT a.* FROM McAttachment AS a WHERE " +
                    " likelihood (a.AccountId = ?, 1.0) AND " +
                    " likelihood (a.ItemId = ?, 0.01) AND " +
                    " likelihood (a.ClassCode = ?, 0.5) ",
                    accountId, itemId, (int)classCode);
            } else {
                // For other kinds of items, don't even bother looking in the database.
                return new List<McAttachment> ();
            }
        }

        public static List<McAttachment> QueryByItemId (McAbstrFolderEntry item)
        {
            return QueryByItemId (item.AccountId, item.Id, item.GetClassCode ());
        }

        public static IEnumerable<McAttachment> QueryNeedsFetch (int accountId, int limit, double minScore, int maxSize)
        {
            return NcModel.Instance.Db.Query<McAttachment> (
                "SELECT a.* FROM McAttachment AS a " +
                " JOIN McEmailMessage AS e ON e.Id = a.ItemId " +
                " WHERE " +
                " a.AccountId = ? AND " +
                " e.IsAwaitingDelete = 0 AND " +
                " e.Score >= ? AND " +
                " a.FileSize <= ? AND " +
                " a.FilePresence != ? AND " + 
                " a.FilePresence != ? AND " +
                " a.FilePresence != ? " + 
                " ORDER BY e.Score DESC, e.DateReceived DESC LIMIT ?",
                accountId, minScore, maxSize,
                (int)FilePresenceEnum.Complete, (int)FilePresenceEnum.Partial, (int)FilePresenceEnum.Error,
                limit);
        }
    }
}
