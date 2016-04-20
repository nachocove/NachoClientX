//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

namespace NachoPlatform
{
    public class NcFileHandler : IPlatformFileHandler
    {
        private static volatile NcFileHandler instance;
        private static object syncRoot = new Object ();
        List<string> ExcludePaths;

        private NcFileHandler ()
        {
            ExcludePaths = new List<string> ();
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
            ExcludePaths.Add (filename);
        }

        public bool SkipFile (string filename)
        {
            return ExcludePaths.Contains (filename);
        }
    }
}

