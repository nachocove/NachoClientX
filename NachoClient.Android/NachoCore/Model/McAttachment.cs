using SQLite;
using System;
using System.IO;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class McAttachment : McObjectPerAccount
    {
        [Indexed]
        public int EmailMessageId { get; set; }

        [Indexed]
        public string DisplayName { get; set; }

        [Indexed]
        public string FileReference { get; set; }

        public uint Method { get; set; }

        public uint EstimatedDataSize { get; set; }

        public string ContentId { get; set; }

        public string ContentLocation { get; set; }

        public bool IsInline { get; set; }

        public uint VoiceSeconds { get; set; }

        public int VoiceOrder { get; set; }

        public bool IsDownloaded { get; set; }

        public uint PercentDownloaded { get; set; }

        public int DataSize { get; set; }

        public string LocalFileName { get; set; }

        public string ContentType { get; set; }

        public static List<McAttachment> QueryByItemId<T> (int accountId, int itemId)
        {
            NachoAssert.True (typeof(T) == typeof(McEmailMessage));
            // TODO: support attachments in other items (e.g. Calendar).
            return BackEnd.Instance.Db.Query<McAttachment> ("SELECT a.* FROM McAttachment AS a WHERE " +
                " a.AccountId = ? AND " +
                " a.EmailMessageId = ? ",
                accountId, itemId);
        }

        public override int Delete ()
        {
            if (IsDownloaded) {
                File.Delete (Path.Combine (BackEnd.Instance.AttachmentsDir, LocalFileName));
            }
            return base.Delete ();
        }
    }
}
