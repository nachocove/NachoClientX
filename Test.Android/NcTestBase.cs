//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;

namespace Test.Common
{
    [TestFixture]
    public class NcTestBase
    {
        [SetUp]
        public void Setup ()
        {
            NcModel.Instance.Reset (System.IO.Path.GetTempFileName ());
            NcModel.Instance.InitalizeDirs (1);
            NcModel.Instance.InitalizeDirs (2);
        }
    }
}

