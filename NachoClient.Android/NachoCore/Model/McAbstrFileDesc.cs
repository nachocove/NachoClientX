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
        protected static volatile McAbstrFileDesc instance;
        protected static object syncRoot = new Object ();
        protected bool IsInstance ()
        {
            return this == instance;
        }

        public bool IsValid { get; set; }

        public DateTime FileGCOkAfter { get; set; }

        public bool GCWaitForReLaunch { get; set; }

        public int FileSize { get; set; }

        public FileSizeAccuracyEnum FileSizeAccuracy { get; set; }

        public FilePresenceEnum FilePresence { get; set; }

        public virtual string GetFilePathSegment ()
        {
            // Pseudo-abstract.
            NcAssert.True (false);
            return null;
        }

        public string GetFilePath ()
        {
            NcAssert.True (!IsInstance () && 0 != Id);
            return instance.GetFilePath (Id);
        }

        public string GetFilePath (int descId)
        {
            return Path.Combine (NcModel.Instance.GetFileDirPath (GetFilePathSegment ()), descId.ToString ());
        }

        public virtual bool IsReferenced ()
        {
            // Pseudo-abstract. Derived class must block for Instance.
            NcAssert.True (false);
            return true;
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
            File.Copy (instance.GetFilePath (descId), desc2.GetFilePath ());
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

        public string GetContents ()
        {
            NcAssert.True (!IsInstance ());
            return _GetContents (Id);
        }

        public string GetContents (int descId)
        {
            NcAssert.True (IsInstance ());
            return _GetContents (descId);
        }

        private string _GetContents (int descId)
        {
            if (0 == descId) {
                return null;
            }
            return File.ReadAllText (GetFilePath (descId));
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

