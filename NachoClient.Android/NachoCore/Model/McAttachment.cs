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
            var body = new McAttachment () {
                AccountId = accountId,
            };
            body.CompleteInsertFile (writer);
            return body;
        }

        [Indexed]
        public int EmailMessageId { get; set; }

        [Indexed]
        public string FileReference { get; set; }

        public uint Method { get; set; }

        public string ContentId { get; set; }

        public string ContentLocation { get; set; }

        public bool IsInline { get; set; }

        public uint VoiceSeconds { get; set; }

        public int VoiceOrder { get; set; }

        public string ContentType { get; set; }

        public static List<McAttachment> QueryByItemId<T> (int accountId, int itemId)
        {
            // ActiveSync only supports email attachments.
            NcAssert.True (typeof(T) == typeof(McEmailMessage));
            return NcModel.Instance.Db.Query<McAttachment> ("SELECT a.* FROM McAttachment AS a WHERE " +
            " a.AccountId = ? AND " +
            " a.EmailMessageId = ? ",
                accountId, itemId);
        }

        public static IEnumerable<McAttachment> QueryNeedsFetch (int accountId, int limit, double minScore, int maxSize)
        {
            return NcModel.Instance.Db.Query<McAttachment> (
                "SELECT a.* FROM McAttachment AS a " +
                " JOIN McEmailMessage AS e ON e.Id = a.EmailMessageId " +
                " WHERE " +
                " a.AccountId = ? AND " +
                " e.AccountId = ? AND " +
                " e.IsAwaitingDelete = 0 AND " +
                " e.Score >= ? AND " +
                " a.FileSize <= ? AND " +
                " a.FilePresence = ? " + 
                " ORDER BY e.Score DESC, e.DateReceived DESC LIMIT ?",
                accountId, accountId, minScore, maxSize, (int)FilePresenceEnum.None, limit);
        }
    }
}
