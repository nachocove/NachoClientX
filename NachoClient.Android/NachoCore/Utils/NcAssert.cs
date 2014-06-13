//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;

namespace NachoCore.Utils
{
    public class NcAssert
    {
        // All subsequent assertion exceptions must be derived of NachoAssertionFailure.
        public class NachoAssertionFailure : Exception
        {
            public NachoAssertionFailure (string message) : base (message)
            {
                Log.Error (Log.LOG_ASSERT, "{0}:::{1}", message, System.Environment.StackTrace);
            }
        }

        public class NachoDefaultCaseFailure : NachoAssertionFailure
        {
            public NachoDefaultCaseFailure (string message) : base (message)
            {
            }
        }

        private NcAssert ()
        {
        }

        public static void True (bool b, string message = "")
        {
            if (!b) {
                throw new NachoAssertionFailure ("NcAssert.True: " + message);
            }
        }

        public static void NotNull (Object o, string message = "")
        {
            if (null == o) {
                throw new NachoAssertionFailure ("NcAssert.NotNull: " + message);
            }
        }

        public static void CaseError (string message = "")
        {
            throw new NachoDefaultCaseFailure ("NcAssert.CaseError: " + message);
        }
    }
}

