//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

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
            //TODO: stub
        }

    }
}

