//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore
{
    public class NachoAssert
    {

        public class NachoAssertionFailure : Exception
        {
        }

        public NachoAssert ()
        {
        }

        public static void True(bool b)
        {
            if(!b) {
                throw new NachoAssertionFailure ();
            }
        }
    }
}

