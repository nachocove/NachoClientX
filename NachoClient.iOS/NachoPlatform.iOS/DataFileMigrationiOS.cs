//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore;

namespace NachoPlatform
{
    public class DataFileMigration
    {
        public static void MigrateDataFilesIfNeeded ()
        {
            string KDataPathSegment = "Data";
            string Documents = NcApplication.GetDocumentsPath ();
            string DataDirPath = Path.Combine (Documents, KDataPathSegment);

            if (!Directory.Exists (DataDirPath)) {
                Log.Info (Log.LOG_DB, "Moving Data files from Documents to Documents/Data");

                Directory.CreateDirectory (DataDirPath);
                var dirs = Directory.GetDirectories (Documents);
                foreach (var dir in dirs) {
                    if (dir != DataDirPath) {
                        // move dir to DataDir
                        string destDir = Path.Combine (DataDirPath, Path.GetFileName (dir));
                        Log.Info (Log.LOG_DB, "Moving directory {0} to {1}", Path.GetFileName (dir), destDir);
                        try {
                            Directory.Move (dir, destDir);
                        } catch (Exception ex) {
                            Log.Error (Log.LOG_DB, "Cannot move dir {0} to {1} - {2}", dir, destDir, ex);
                        }
                    }
                }
                var files = Directory.GetFiles (Documents);
                foreach (var file in files) {
                    // move file to DataDir
                    string destFile = Path.Combine (DataDirPath, Path.GetFileName (file));
                    Log.Info (Log.LOG_DB, "Moving file {0} to {1}", Path.GetFileName (file), destFile);
                    try {
                        Directory.Move (file, destFile);
                    } catch (Exception ex) {
                        Log.Error (Log.LOG_DB, "Cannot move file {0} to {1} - {2}", file, destFile, ex);
                    }
                }
            }
        }
    }
}

