//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using NachoCore.Utils;
using Foundation;

namespace NachoPlatform
{
    public class NcFileHandler : IPlatformFileHandler
    {
        private static volatile NcFileHandler instance;
        private static object syncRoot = new Object ();

        private NcFileHandler ()
        {
        }

        public static NcFileHandler Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new NcFileHandler ();
                    }
                }
                return instance;
            }
        }

        public void MarkFileForSkipBackup (string filename)
        {
            Log.Info (Log.LOG_DB, "Backup: Marking file/dir for skip backup {0}", filename);
            if (File.Exists (filename) || Directory.Exists (filename)) {
                // FIXME:
//                NSError error = NSFileManager.SetSkipBackupAttribute (filename, true);
//                if (error != null) {
//                    Log.Info (Log.LOG_DB, "Backup: Error marking file/dir for skip backup {0} : {1}", filename, error.LocalizedDescription);
//                }
            } else {
                Log.Error (Log.LOG_DB, "Backup: File/Directory does not exist {0}", filename);
            }
        }
        public bool SkipFile (string filename)
        {
            // Not used in iOS
            throw new NotImplementedException ();
        }
    }
}

