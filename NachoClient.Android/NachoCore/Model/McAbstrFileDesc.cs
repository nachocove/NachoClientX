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
    // If SQLite.Net would tolerate an abstract class, we'd be one.
    // hybrid - needs a singleton Instance to make virtual static functions.
    // Derived classes must implement singleton logic (C# FAIL).
    public class McAbstrFileDesc : McAbstrObjectPerAcc
    {
        [Indexed]
        public bool IsValid { get; set; }

        public DateTime FileGCOkAfter { get; set; }

        public bool GCWaitForReLaunch { get; set; }

        public long FileSize { get; set; }

        public FileSizeAccuracyEnum FileSizeAccuracy { get; set; }

        public FilePresenceEnum FilePresence { get; set; }

        public double FilePresenceFraction { get; set; }
        // Use of LocalFileName is optional. If not used, it must be null.
        public string LocalFileName { get; set; }
        // Use of DisplayName is optional. If not used, it must be null.
        [Indexed]
        public string DisplayName { get; set; }

        public virtual string GetFilePathSegment ()
        {
            // Pseudo-abstract.
            NcAssert.True (false);
            return null;
        }

        public string GetFilePath ()
        {
            NcAssert.True (0 != Id);
            var dirPath = DirPath ();
            if (!Directory.Exists (dirPath)) {
                Directory.CreateDirectory (dirPath);
            }
            if (null == LocalFileName) {
                return Path.Combine (dirPath, Id.ToString ());
            } else {
                return Path.Combine (dirPath, LocalFileName);
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
        }

        public FileStream SaveFileStream ()
        {
            return File.OpenWrite (GetFilePath ());
        }

        private string DirPath ()
        {
            return Path.Combine (NcModel.Instance.GetFileDirPath (AccountId, GetFilePathSegment ()), Id.ToString ());
        }

        private void Prep ()
        {
            Insert ();
            Directory.CreateDirectory (DirPath ());
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

        protected void CompleteInsertDuplicate (McAbstrFileDesc srcDesc)
        {
            Prep ();
            File.Copy (GetFilePath (), srcDesc.GetFilePath ());
            UpdateSaveFinish ();
        }

        public void SetDisplayName (string displayName)
        {
            string oldPath = null;
            if (0 < Id) {
                oldPath = GetFilePath ();
            }
            DisplayName = displayName;
            LocalFileName = null;
            // See if we can make a legit LocalFileName. If we can't, leave it null.
            var tmp = NcModel.Instance.TmpPath (AccountId);
            Directory.CreateDirectory (tmp);
            var justName = displayName.SantizeFileName ();
            var target = Path.Combine (tmp, justName);
            try {
                // Test to see if sanitized display name will work as a file name.
                using (var dummy1 = File.OpenWrite (target)) {
                    LocalFileName = justName;
                    Directory.Delete (tmp, true);
                }
            } catch {
                // Try adding appropriate extension to id, and see if that works as a file name.
                var ext = Path.GetExtension (DisplayName);
                if (null != ext) {
                    var idExt = Id.ToString () + ext;
                    target = Path.Combine (tmp, idExt);
                    try {
                        using (var dummy2 = File.OpenWrite (target)) {
                            LocalFileName = idExt;
                            Directory.Delete (tmp, true);
                        }
                    } catch {
                        Directory.Delete (tmp, true);
                    }
                }
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

        public void UpdateData (string content)
        {
            File.WriteAllText (GetFilePath (), content);
            UpdateSaveFinish ();
        }

        public void UpdateData (byte[] content)
        {
            File.WriteAllBytes (GetFilePath (), content);
            UpdateSaveFinish ();
        }

        public void UpdateFileCopy (string srcPath)
        {
            File.Copy (srcPath, GetFilePath ());
            UpdateSaveFinish ();           
        }

        public void UpdateFileMove (string srcPath)
        {
            File.Move (srcPath, GetFilePath ());
            UpdateSaveFinish ();           
        }

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

        public override int Delete ()
        {
            // We Must delete the file first. Complete the delete even if something goes wrong with the file delete.
            DeleteFile ();
            try {
                Directory.Delete (DirPath (), true);
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
    }
}
