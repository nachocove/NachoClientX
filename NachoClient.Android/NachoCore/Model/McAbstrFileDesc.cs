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

        protected virtual bool IsInstance ()
        {
            // Pseudo-abstract.
            NcAssert.True (false);
            return false;
        }

        public virtual string GetFilePathSegment ()
        {
            // Pseudo-abstract.
            NcAssert.True (false);
            return null;
        }

        public virtual bool IsReferenced ()
        {
            // Pseudo-abstract. Derived class must not allow for Instance.
            NcAssert.True (false);
            return true;
        }

        public string GetFilePath ()
        {
            NcAssert.True (!IsInstance () && 0 != Id);
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
            NcAssert.True (!IsInstance ());
            return File.OpenWrite (GetFilePath ());
        }

        protected string CompleteGetFilePath (McAbstrFileDesc desc)
        {
            NcAssert.True (IsInstance ());
            if (null == desc) {
                return null;
            }
            return desc.GetFilePath ();
        }

        private string DirPath ()
        {
            NcAssert.True (!IsInstance ());
            return Path.Combine (NcModel.Instance.GetFileDirPath (GetFilePathSegment ()), Id.ToString ());
        }

        private void CheckInsertMkDir (McAbstrFileDesc desc, bool isInstance)
        {
            NcAssert.True (isInstance == IsInstance ());
            desc.Insert ();
            Directory.CreateDirectory (desc.DirPath ());
        }

        // Derived class must implement McXxx InsertSaveStart (). This calls the derived class 
        // constructor and passes it through CompleteInsertSaveStart. Must be IsInstance only.
        protected McAbstrFileDesc CompleteInsertSaveStart (McAbstrFileDesc desc)
        {
            CheckInsertMkDir (desc, true);
            return desc;
        }

        // Derived class must implement McXxx InsertFile (). This calls the derived class 
        // constructor and passes it through CompleteInsertFile. Must be IsInstance only.
        protected McAbstrFileDesc CompleteInsertFile (McAbstrFileDesc desc, string content)
        {
            CheckInsertMkDir (desc, true);
            File.WriteAllText (desc.GetFilePath (), content);
            desc.UpdateSaveFinish ();
            return desc;
        }

        // Derived class must implement McXxx InsertFile (). This calls the derived class 
        // constructor and passes it through CompleteInsertFile. Must be IsInstance only.
        protected McAbstrFileDesc CompleteInsertFile (McAbstrFileDesc desc, byte[] content)
        {
            CheckInsertMkDir (desc, true);
            File.WriteAllBytes (desc.GetFilePath (), content);
            desc.UpdateSaveFinish ();
            return desc;
        }

        // Derived class must implement McXxx InsertDuplicate (). This calls the derived class 
        // constructor and passes it through CompleteInsertDuplicate. Must not be IsInstance.
        protected McAbstrFileDesc CompleteInsertDuplicate (McAbstrFileDesc desc)
        {
            CheckInsertMkDir (desc, false);
            File.Copy (GetFilePath (), desc.GetFilePath ());
            desc.UpdateSaveFinish ();
            return desc;
        }

        // Derived class must implement McXxx InsertDuplicate (int descId). This calls the derived class 
        // constructor and passes it through CompleteInsertDuplicate. Must be IsInstance only.
        protected McAbstrFileDesc CompleteInsertDuplicate (McAbstrFileDesc destDesc, McAbstrFileDesc srcDesc)
        {
            CheckInsertMkDir (destDesc, true);
            File.Copy (destDesc.GetFilePath (), srcDesc.GetFilePath ());
            destDesc.UpdateSaveFinish ();
            return destDesc;
        }

        protected string CompleteGetContentsString (McAbstrFileDesc desc)
        {
            NcAssert.True (IsInstance ());
            if (null == desc) {
                return null;
            }
            return desc.GetContentsString ();
        }

        protected byte[] CompleteGetContentsByteArray (McAbstrFileDesc desc)
        {
            NcAssert.True (IsInstance ());
            if (null == desc) {
                return null;
            }
            return desc.GetContentsByteArray ();
        }

        public void SetDisplayName (string displayName)
        {
            NcAssert.True (!IsInstance ());
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
                File.OpenWrite (target);
                LocalFileName = justName;
                Directory.Delete (tmp, true);
            } catch {
                // Add appropriate extension to id, and see if that works as a file name.
                var ext = Path.GetExtension (DisplayName);
                if (null != ext) {
                    var idExt = Id.ToString () + ext;
                    target = Path.Combine (tmp, idExt);
                    try {
                        File.OpenWrite (target);
                        LocalFileName = idExt;
                        Directory.Delete (tmp, true);
                    } catch {
                        Directory.Delete (tmp, true);
                    }
                }
            }
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
            if (1 < FilePresenceFraction) {
                // Rely on UpdateSaveFinish to change FilePresence.
                FilePresenceFraction = 1;
            }
            Update ();
        }

        public virtual void UpdateSaveFinish ()
        {
            NcAssert.True (!IsInstance ());
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
            NcAssert.True (!IsInstance ());
            return File.ReadAllText (GetFilePath ());
        }
        /// <summary>
        /// Gets the contents as a byte array.
        /// Not callable on Instance.
        /// </summary>
        /// <returns>The contents byte array.</returns>
        public byte[] GetContentsByteArray ()
        {
            NcAssert.True (!IsInstance ());
            return File.ReadAllBytes (GetFilePath ());
        }

        public override int Delete ()
        {
            DeleteFile ();
            Directory.Delete (DirPath (), true);
            return base.Delete ();
        }

        public void DeleteFile ()
        {
            var path = GetFilePath ();
            SetFilePresence (FilePresenceEnum.None);
            Update ();
            File.Delete (path);
        }
    }
}
