//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoPlatform
{
    public class MdmConfig : IPlatformMdmConfig
    {
        private static volatile MdmConfig instance;
        private static object syncRoot = new Object ();

        public static MdmConfig Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new MdmConfig ();
                    }
                }
                return instance;
            }
        }

        private MdmConfig ()
        {
        }

        public void ExtractValues ()
        {
            // FIXME.
            NcAssert.True (false);
        }
    }
}
