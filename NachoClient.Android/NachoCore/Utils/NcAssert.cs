//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore
{
    public class NcAssert
    {

        public class NachoAssertionFailure : Exception
        {
        }
        public class NachoDefaultCaseFailure : Exception
        {
        }

        public NcAssert ()
        {
        }

        public static void True(bool b)
        {
            if(!b) {
                throw new NachoAssertionFailure ();
            }
        }

        public static void NotNUll(Object o)
        {
            if (null == o) {
                throw new NachoAssertionFailure ();
            }
        }

        public static void CaseError()
        {
            throw new NachoDefaultCaseFailure();
        }
    }
}

