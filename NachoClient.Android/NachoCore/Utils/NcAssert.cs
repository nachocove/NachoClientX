//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    public class NcAssert
    {
        // All subsequent assertion exceptions must be derived of NachoAssertionFailure.
        public class NachoAssertionFailure : Exception
        {
            public NachoAssertionFailure (string message) : base (message)
            {
                Log.Error (Log.LOG_ASSERT, message);
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

        public static void True (bool b)
        {
            if (!b) {
                throw new NachoAssertionFailure ("No message");
            }
        }

        public static void True (bool b, string message)
        {
            if (!b) {
                throw new NachoAssertionFailure (message);
            }
        }

        public static void NotNull (Object o)
        {
            if (null == o) {
                throw new NachoAssertionFailure ("No message");
            }
        }

        public static void NotNull (Object o, string message)
        {
            if (null == o) {
                throw new NachoAssertionFailure (message);
            }
        }

        public static void CaseError ()
        {
            throw new NachoDefaultCaseFailure ("No message");
        }

        public static void CaseError (string message)
        {
            throw new NachoDefaultCaseFailure (message);
        }
    }
}

