//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore
{
    public class NcMdmConfig
    {
        private static volatile NcMdmConfig instance;
        private static object syncRoot = new Object ();

        public static NcMdmConfig Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new NcMdmConfig ();
                        }
                    }
                }
                return instance; 
            }
        }

        private NcMdmConfig ()
        {
        }
    }
}
