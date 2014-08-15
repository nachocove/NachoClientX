using SQLite;
using System;
using System.IO;
using System.Collections.Generic;
using NachoCore.Utils;


namespace NachoCore.Model
{
    public class McAttachment : McAbstrObjectPerAcc, IFilesViewItem
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

        public uint AttachedTo { get; set; }

        public enum OwnerTypes {Email = 0, Event};

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
                " a.PercentDownloaded = 0 AND " +
                " a.IsDownloaded != 0 " +
                " ORDER BY e.Score DESC, e.DateReceived DESC LIMIT ?",
                accountId, accountId, limit);
        }

        public override int Delete ()
        {
            RemoveFromStorage ();
            return base.Delete ();
        }

        // best-effort attempt to find and remove the file
        public void RemoveFromStorage ()
        {
            if (IsDownloaded) {
                try {
                    File.Delete (FilePath ());
                    // reset fields
                    IsDownloaded = false;
                    PercentDownloaded = 0;
                    LocalFileName = null;
                    base.Update ();
                } catch (Exception e) {
                    Log.Error (Log.LOG_STATE, "Exception thrown while removing attachment from storage: {0}", e.Message);
                }
            }
        }

        public static FileStream TempFileStream (string guidString)
        {
            // Intentionally above all the Id-dirs.
            return File.OpenWrite (TempPath (guidString));
        }

        public static string TempPath (string guidString)
        {
            return Path.Combine (NcModel.Instance.AttachmentsDir, guidString);
        }

        public string FilePath ()
        {
            NcAssert.True (0 != Id);
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
