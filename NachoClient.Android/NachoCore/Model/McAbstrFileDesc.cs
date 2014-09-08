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

        public int FileSize { get; set; }

        public FileSizeAccuracyEnum FileSizeAccuracy { get; set; }

        public FilePresenceEnum FilePresence { get; set; }

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
            return GetFilePath (Id);
        }

        public string GetFilePath (int descId)
        {
            return Path.Combine (NcModel.Instance.GetFileDirPath (GetFilePathSegment ()), descId.ToString ());
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

        private McAbstrFileDesc _CompleteInsertSaveStart (McAbstrFileDesc desc)
        {
            desc.Insert ();
            return desc;
        }

        // Derived class must implement McXxx InsertSaveStart (). This calls the derived class 
        // constructor and passes it through CompleteInsertSaveStart. Must be IsInstance only.
        protected McAbstrFileDesc CompleteInsertSaveStart (McAbstrFileDesc desc)
        {
            NcAssert.True (IsInstance ());
            return _CompleteInsertSaveStart (desc);
        }

        // Derived class must implement McXxx InsertFile (). This calls the derived class 
        // constructor and passes it through CompleteInsertFile. Must be IsInstance only.
        protected McAbstrFileDesc CompleteInsertFile (McAbstrFileDesc desc, string content)
        {
            NcAssert.True (IsInstance ());
            var desc2 = _CompleteInsertSaveStart (desc);
            File.WriteAllText (desc2.GetFilePath (), content);
            desc2.UpdateSaveFinish ();
            return desc2;
        }

        // Derived class must implement McXxx InsertFile (). This calls the derived class 
        // constructor and passes it through CompleteInsertFile. Must be IsInstance only.
        protected McAbstrFileDesc CompleteInsertFile (McAbstrFileDesc desc, byte[] content)
        {
            NcAssert.True (IsInstance ());
            var desc2 = _CompleteInsertSaveStart (desc);
            File.WriteAllBytes (desc2.GetFilePath (), content);
            desc2.UpdateSaveFinish ();
            return desc2;
        }

        // Derived class must implement McXxx InsertDuplicate (). This calls the derived class 
        // constructor and passes it through CompleteInsertDuplicate. Must not be IsInstance only.
        protected McAbstrFileDesc CompleteInsertDuplicate (McAbstrFileDesc desc)
        {
            NcAssert.True (!IsInstance ());
            var desc2 = _CompleteInsertSaveStart (desc);
            File.Copy (GetFilePath (), desc2.GetFilePath ());
            desc2.UpdateSaveFinish ();
            return desc;
        }

        // Derived class must implement McXxx InsertDuplicate (int descId). This calls the derived class 
        // constructor and passes it through CompleteInsertDuplicate. Must be IsInstance only.
        protected McAbstrFileDesc CompleteInsertDuplicate (McAbstrFileDesc desc, int descId)
        {
            NcAssert.True (IsInstance ());
            var desc2 = _CompleteInsertSaveStart (desc);
            File.Copy (GetFilePath (descId), desc2.GetFilePath ());
            desc2.UpdateSaveFinish ();
            return desc;
        }

        public virtual void UpdateSaveFinish ()
        {
            NcAssert.True (!IsInstance ());
            IsValid = true;
            Update ();
        }

        public void UpdateFile (string content)
        {
            File.WriteAllText (GetFilePath (), content);

            if (!IsValid) {
                UpdateSaveFinish ();
            } else {
                Update ();
            }
        }
        /// <summary>
        /// Gets the contents as a string.
        /// Not callable on Instance.
        /// </summary>
        /// <returns>The contents string.</returns>
        public string GetContentsString ()
        {
            NcAssert.True (!IsInstance ());
            return _GetContentsString (Id);
        }
        /// <summary>
        /// Gets the contents as a byte array.
        /// Not callable on Instance.
        /// </summary>
        /// <returns>The contents byte array.</returns>
        public byte[] GetContentsByteArray ()
        {
            NcAssert.True (!IsInstance ());
            return _GetContentsByteArray (Id);
        }
        /// <summary>
        /// Gets the contents as string.
        /// Callable only via Instance.
        /// </summary>
        /// <returns>The contents string.</returns>
        /// <param name="descId">Desc identifier.</param>
        public string GetContentsString (int descId)
        {
            NcAssert.True (IsInstance ());
            return _GetContentsString (descId);
        }
        /// <summary>
        /// Gets the contents as byte array.
        /// Callable only via Instance.
        /// </summary>
        /// <returns>The contents byte array.</returns>
        /// <param name="descId">Desc identifier.</param>
        public byte[] GetContentsByteArray (int descId)
        {
            NcAssert.True (IsInstance ());
            return _GetContentsByteArray (descId);
        }

        private string _GetContentsString (int descId)
        {
            if (0 == descId) {
                return null;
            }
            return File.ReadAllText (GetFilePath (descId));
        }

        private byte[] _GetContentsByteArray (int descId)
        {
            if (0 == descId) {
                return null;
            }
            return File.ReadAllBytes (GetFilePath (descId));
        }

        public override int Delete ()
        {
            DeleteFile ();
            return base.Delete ();
        }

        public void DeleteFile ()
        {
            FilePresence = FilePresenceEnum.None;
            Update ();
            File.Delete (GetFilePath ());
        }
    }
}

