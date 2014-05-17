using SQLite;
using System;
using System.IO;
using System.Collections.Generic;
using NachoCore.Utils;

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
            // ActiveSync only supports email attachments.
            NachoAssert.True (typeof(T) == typeof(McEmailMessage));
            return NcModel.Instance.Db.Query<McAttachment> ("SELECT a.* FROM McAttachment AS a WHERE " +
            " a.AccountId = ? AND " +
            " a.EmailMessageId = ? ",
                accountId, itemId);
        }

        public override int Delete ()
        {
            if (IsDownloaded) {
                File.Delete (FilePath ());
            }
            return base.Delete ();
        }

        public static FileStream TempFileStream (string guidString)
        {
            // Intentionally above all the Id-dirs.
            return File.OpenWrite (TempPath (guidString));
        }

        private static string TempPath (string guidString)
        {
            return Path.Combine (NcModel.Instance.AttachmentsDir, guidString);
        }

        public string FilePath ()
        {
            NachoAssert.True (0 != Id);
            return Path.Combine (NcModel.Instance.AttachmentsDir, Id.ToString (), LocalFileName);
        }

        public void SaveFromTemp (string guidString)
        {
            var savePath = Path.Combine (NcModel.Instance.AttachmentsDir, Id.ToString ());
            Directory.CreateDirectory (savePath);
            try {
                LocalFileName = DisplayName.SantizeFileName ();
                File.Move (TempPath (guidString), FilePath ());
            } catch {
                LocalFileName = Id.ToString ();
                try {
                    var ext = Path.GetExtension (DisplayName);
                    if (null != ext) {
                        LocalFileName += ext;
                    }
                } catch {
                    // Give up on extension. TODO - generate correct extension based on ContentType.
                }
                File.Move (TempPath (guidString), FilePath ());
            }
        }
    }
}
