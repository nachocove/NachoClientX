//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace Test.Common
{
    [TestFixture]
    public class NcTestBase
    {
        [SetUp]
        public void SetUp ()
        {
            NcModel.Instance.Reset (System.IO.Path.GetTempFileName ());
            NcModel.Instance.InitializeDirs (1);
            NcModel.Instance.InitializeDirs (2);
            McPendingHelper.IsUnitTest = true;
        }

        [TearDown]
        public void TearDown ()
        {
        }
    }
}

