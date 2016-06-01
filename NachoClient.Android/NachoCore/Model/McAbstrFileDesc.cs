//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class NcFileIndex
    {
        public int Id { set; get; }

        public int AccountId { set; get; }

        public string DisplayName { set; get; }

        public int FileType { set; get; }

        public DateTime CreatedAt { set; get; }

        public string Contact { set; get; }

    }
    // If SQLite.Net would tolerate an abstract class, we'd be one.
    // hybrid - needs a singleton Instance to make virtual static functions.
    // Derived classes must implement singleton logic (C# FAIL).
    public class McAbstrFileDesc : McAbstrObjectPerAcc
    {

        public bool IsValid { get; set; }

        public DateTime FileGCOkAfter { get; set; }

        public bool GCWaitForReLaunch { get; set; }

        /// Type of the transferred body
        public BodyTypeEnum BodyType { get; set; }

        public long FileSize { get; set; }

        public bool Truncated { get; set; }

        public FileSizeAccuracyEnum FileSizeAccuracy { get; set; }

        public FilePresenceEnum FilePresence { get; set; }

        public double FilePresenceFraction { get; set; }
        // Use of LocalFileName is optional. If not used, it must be null.
        public string LocalFileName { get; set; }
        // Use of DisplayName is optional. If not used, it must be null.
        [Indexed]
        public string DisplayName { get; set; }

        /// The delegate type that will be used to write the contents of a McAbstrFileDesc.
        /// The stream passed to the delegate can be used to write directly to the
        /// McAbstrFileDesc's underlying file.
        public delegate void WriteFileDelegate (FileStream stream);

        public virtual string GetFilePathSegment ()
        {
            // Pseudo-abstract.
            NcAssert.True (false);
            return null;
        }

        public string GetFilePath ()
        {
            NcAssert.True (0 != Id);
            var dirPath = GetFileDirectory ();
            if (!Directory.Exists (dirPath)) {
                Directory.CreateDirectory (dirPath);
            }
            return Path.Combine (dirPath, GetFileName ());
        }

        public string GetFileName ()
        {
            if (null == LocalFileName) {
                return Id.ToString ();
            } else {
                return LocalFileName;
            }
        }

        public enum FileSizeAccuracyEnum
        {
            Invalid,
            Estimate,
            Actual,
        };

        public enum FilePresenceEnum
        {
            None,
            Partial,
            Complete,
            Error,
        }

        /// FYI, AirSync.TypeCode PlainText_1, Html_2, Rtf_3, Mime_4
        public enum BodyTypeEnum
        {
            None = 0,
            PlainText_1 = 1,
            HTML_2 = 2,
            RTF_3 = 3,
            MIME_4 = 4,
        }

        public string GetFileDirectory ()
        {
            return Path.Combine (NcModel.Instance.GetFileDirPath (AccountId, GetFilePathSegment ()), Id.ToString ());
        }

        private void Prep ()
        {
            Insert ();
            Directory.CreateDirectory (GetFileDirectory ());
        }

        protected void CompleteInsertSaveStart ()
        {
            Prep ();
        }

        protected void CompleteInsertFile (string content)
        {
            Prep ();
            File.WriteAllText (GetFilePath (), content);
            UpdateSaveFinish ();
        }

        protected void CompleteInsertFile (byte[] content)
        {
            Prep ();
            File.WriteAllBytes (GetFilePath (), content);
            UpdateSaveFinish ();
        }

        protected void CompleteInsertFile (WriteFileDelegate writer)
        {
            Prep ();
            using (var stream = File.OpenWrite (GetFilePath ())) {
                writer (stream);
            }
            UpdateSaveFinish ();
        }

        protected void CompleteInsertDuplicate (McAbstrFileDesc srcDesc)
        {
            Prep ();
            File.Copy (srcDesc.GetFilePath (), GetFilePath ());
            UpdateSaveFinish ();
        }

        public void SetDisplayName (string displayName)
        {
            DisplayName = displayName;
            LocalFileName = null;
            if (null == displayName) {
                return;
            }
            string oldPath = null;
            if (0 < Id) {
                oldPath = GetFilePath ();
            }
            // See if we can make a legit LocalFileName. If we can't, leave it null.
            var tmp = NcModel.Instance.TmpPath (AccountId);
            Directory.CreateDirectory (tmp);
            try {
                var justName = displayName.SantizeFileName ();
                var target = Path.Combine (tmp, justName);
                try {
                    // Test to see if sanitized display name will work as a file name.
                    using (var dummy1 = File.OpenWrite (target)) {
                        LocalFileName = justName;
                    }
                } catch {
                    // Try adding appropriate extension to id, and see if that works as a file name.
                    var ext = Path.GetExtension (DisplayName);
                    if (null != ext) {
                        var idExt = Id.ToString () + ext;
                        target = Path.Combine (tmp, idExt);
                        using (var dummy2 = File.OpenWrite (target)) {
                            LocalFileName = idExt;
                        }
                    }
                }
            } finally {
                Directory.Delete (tmp, true);
            }
            // If there is a pre-existing file, move it to where it needs to be.
            if (null != oldPath && File.Exists (oldPath)) {
                File.Move (oldPath, GetFilePath ());
            }
        }

        public void SetFilePresence (FilePresenceEnum presence)
        {
            switch (presence) {
            case FilePresenceEnum.None:
            case FilePresenceEnum.Error:
                FilePresenceFraction = 0.0;
                break;
            case FilePresenceEnum.Partial:
                if (0.0 >= FilePresenceFraction || 1.0 <= FilePresenceFraction) {
                    FilePresenceFraction = 0.01;
                }
                break;
            case FilePresenceEnum.Complete:
                FilePresenceFraction = 1.0;
                break;

            default:
                NcAssert.CaseError (string.Format ("{0}", presence.ToString ()));
                break;
            }
            FilePresence = presence;
        }

        public void UpdateSavePresence (long incBytes)
        {
            if (FileSizeAccuracyEnum.Invalid == FileSizeAccuracy) {
                return;
            }
            var soFar = FilePresenceFraction * FileSize;
            soFar += incBytes;
            FilePresenceFraction = soFar / FileSize;
            if (1 <= FilePresenceFraction) {
                // Rely on UpdateSaveFinish to change FilePresence.
                FilePresenceFraction = 0.999;
            }
            Update ();
        }

        public virtual void UpdateSaveFinish ()
        {
            IsValid = true;
            var fileInfo = new FileInfo (GetFilePath ());
            FileSize = fileInfo.Length;
            FileSizeAccuracy = FileSizeAccuracyEnum.Actual;
            SetFilePresence (FilePresenceEnum.Complete);
            Update ();
        }

        /// Replace the contents of this McAbstrFileDesc with the given string.
        public void UpdateData (string content)
        {
            var tmp = NcModel.Instance.TmpPath (AccountId);
            File.WriteAllText (tmp, content);
            ReplaceFile (tmp);
        }

        /// Replace the contents of this McAbstrFileDesc with the given bytes.
        public void UpdateData (byte[] content)
        {
            var tmp = NcModel.Instance.TmpPath (AccountId);
            File.WriteAllBytes (tmp, content);
            ReplaceFile (tmp);
        }

        /// Replace the contents of this McAbstrFileDesc with whatever the delegate
        /// writes to the stream passed to it.
        public void UpdateData (WriteFileDelegate writer)
        {
            var tmp = NcModel.Instance.TmpPath (AccountId);
            using (var stream = File.OpenWrite (tmp)) {
                writer (stream);
            }
            ReplaceFile (tmp);
        }

        /// Replace the contents of this McAbstrFileDesc with a copy of the given file.
        public void UpdateFileCopy (string srcPath)
        {
            var tmp = NcModel.Instance.TmpPath (AccountId);
            File.Copy (srcPath, tmp);
            ReplaceFile (tmp);
        }

        /// <summary>
        /// Replace the contents of this McAbstrFileDesc with the given file. The file
        /// will be moved into the file store area and will no longer exist at the
        /// given location.
        /// </summary>
        public void UpdateFileMove (string srcPath)
        {
            ReplaceFile (srcPath);
        }

        /// <summary>
        /// Gets the contents as a string.
        /// Not callable on Instance.
        /// </summary>
        /// <returns>The contents string.</returns>
        public string GetContentsString ()
        {
            return File.ReadAllText (GetFilePath ());
        }

        /// <summary>
        /// Gets the contents as a byte array.
        /// Not callable on Instance.
        /// </summary>
        /// <returns>The contents byte array.</returns>
        public byte[] GetContentsByteArray ()
        {
            return File.ReadAllBytes (GetFilePath ());
        }

        public static List<NcFileIndex> GetAllFiles (int accountId)
        {
            var unified = McAccount.GetUnifiedAccount ();
            if (accountId == unified.Id) {
                return (NcModel.Instance.Db.Query<NcFileIndex> (
                    "SELECT t1.Id, t1.AccountId, t1.DisplayName, t1.CreatedAt, t1.FileType, t1.Contact " +
                    "FROM(SELECT a.Id, a.AccountId, a.DisplayName, e.DateReceived AS CreatedAt, 0 AS FileType, e.[From] AS Contact " +
                    "FROM McAttachment a, McEmailMessage e, McMapAttachmentItem m " +
                    "WHERE m.ItemId=e.Id AND m.AttachmentId=a.Id AND a.IsInline = 0 AND m.ClassCode = ?" +
                    "UNION " +
                    "SELECT a.Id, a.AccountId, a.DisplayName, c.CreatedAt AS CreatedAt, 0 AS FileType, c.OrganizerName AS Contact " +
                    "FROM McAttachment a, McCalendar c, McMapAttachmentItem m " +
                    "WHERE m.ItemId=c.Id AND m.AttachmentId=a.Id AND a.IsInline = 0 AND m.ClassCode = ? " +
                    "UNION " +
                    "SELECT Id, AccountId, DisplayName, CreatedAt, 1 AS FileType, 'Me' AS Contact " +
                    "FROM McNote " +
                    "UNION " +
                    "SELECT Id, AccountId, DisplayName, CreatedAt, 2 AS FileType, 'Me' AS Contact " +
                    "FROM McDocument " +
                    ") t1 WHERE t1.DisplayName NOT LIKE 'ATT00%' ORDER BY LOWER(t1.DisplayName) + 0, LOWER(t1.DisplayName)",
                    (int)McAbstrFolderEntry.ClassCodeEnum.Email, 
                    (int)McAbstrFolderEntry.ClassCodeEnum.Calendar
                ));
            } else {
                return (NcModel.Instance.Db.Query<NcFileIndex> (
                    "SELECT t1.Id, t1.AccountId, t1.DisplayName, t1.CreatedAt, t1.FileType, t1.Contact " +
                    "FROM(SELECT a.Id, a.AccountId, a.DisplayName, e.DateReceived AS CreatedAt, 0 AS FileType, e.[From] AS Contact " +
                    "FROM McAttachment a, McEmailMessage e, McMapAttachmentItem m " +
                    "WHERE m.ItemId=e.Id AND m.AttachmentId=a.Id AND a.AccountId=? AND a.IsInline = 0 AND m.ClassCode = ?" +
                    "UNION " +
                    "SELECT a.Id, a.AccountId, a.DisplayName, c.CreatedAt AS CreatedAt, 0 AS FileType, c.OrganizerName AS Contact " +
                    "FROM McAttachment a, McCalendar c, McMapAttachmentItem m " +
                    "WHERE m.ItemId=c.Id AND m.AttachmentId=a.Id AND a.AccountId=? AND a.IsInline = 0 AND m.ClassCode = ? " +
                    "UNION " +
                    "SELECT Id, AccountId, DisplayName, CreatedAt, 1 AS FileType, 'Me' AS Contact " +
                    "FROM McNote " +
                    "WHERE AccountId = ? " +
                    "UNION " +
                    "SELECT Id, AccountId, DisplayName, CreatedAt, 2 AS FileType, 'Me' AS Contact " +
                    "FROM McDocument " +
                    "WHERE AccountId=?) " +
                    "t1 WHERE t1.DisplayName NOT LIKE 'ATT00%' ORDER BY LOWER(t1.DisplayName) + 0, LOWER(t1.DisplayName)",
                    accountId, (int)McAbstrFolderEntry.ClassCodeEnum.Email, 
                    accountId, (int)McAbstrFolderEntry.ClassCodeEnum.Calendar,
                    accountId,
                    accountId
                ));
            }
        }

        public override int Delete ()
        {
            // We Must delete the file first. Complete the delete even if something goes wrong with the file delete.
            DeleteFile ();
            try {
                Directory.Delete (GetFileDirectory (), true);
            } catch (Exception ex) {
                Log.Error (Log.LOG_DB, "McAbstrFileDesc: Exception trying to delete DirPath: {0}", ex);
            }
            return base.Delete ();
        }

        public void DeleteFile ()
        {
            var path = GetFilePath ();
            SetFilePresence (FilePresenceEnum.None);
            Update ();
            try {
                File.Delete (path);
            } catch (Exception ex) {
                Log.Error (Log.LOG_DB, "McAbstrFileDesc: Exception trying to delete file: {0}", ex);
            }
        }

        private void ReplaceFile (string newFile)
        {
            var tmp = NcModel.Instance.TmpPath (AccountId);
            var filePath = GetFilePath ();
            if (File.Exists (filePath)) {
                File.Move (filePath, tmp); // Shouldn't fail, since destination shouldn't exist
            }
            File.Move (newFile, filePath); // Shouldn't fail, since we just made sure the destination doesn't exist
            if (File.Exists (tmp)) {
                try {
                    File.Delete (tmp); // This can fail if the file is in use.
                } catch (Exception ex) {
                    Log.Error (Log.LOG_DB, "McAbstrFileDesc: Exception trying to delete file: {0}", ex);
                    // Leave the file there. It will get cleaned up when the temporary files are cleaned up.
                }
            }
            UpdateSaveFinish ();
        }

        public static bool IsComplete (McAbstrFileDesc file)
        {
            return ((null != file) && (FilePresenceEnum.Complete == file.FilePresence));
        }

        public bool IsComplete ()
        {
            return (FilePresenceEnum.Complete == FilePresence);
        }

        public static bool IsNontruncatedBodyComplete (McAbstrFileDesc file)
        {
            return (IsComplete (file) && !file.Truncated);
        }

        public virtual bool IsImageFile ()
        {
            string[] subtype = {
                ".tiff",
                ".jpeg",
                ".jpg",
                ".gif",
                ".png",
            };

            var extension = Pretty.GetExtension (DisplayName);

            if (String.IsNullOrEmpty (extension) && !String.IsNullOrEmpty (LocalFileName)) {
                extension = Pretty.GetExtension (LocalFileName);
            }

            foreach (var s in subtype) {
                if (String.Equals (s, extension, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }
    }
}
