//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
using System;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.Utils;
using Test.iOS;

namespace Test.Common
{
    public class ErrorHelperTest : NcTestBase
    {

        [Test]
        public void Basic()
        {
            string s;
            NcResult nr;

            s = null;
            nr = NcResult.Error (NcResult.SubKindEnum.Error_NetworkUnavailable);
            Assert.True (ErrorHelper.ExtractErrorString (nr, out s));
            Assert.AreEqual ("The network is unavailable.", s);

            s = "monkey";
            nr = NcResult.OK ();
            Assert.False (ErrorHelper.ExtractErrorString (nr, out s));
            Assert.IsNull (s);

        }

    }
}

