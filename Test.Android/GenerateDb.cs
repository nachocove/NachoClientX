//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;

namespace Test.Common
{
    [TestFixture]
    public class GenerateDb
    {
        [Test]
        public void GenerateDbFile ()
        {
            var path = System.IO.Path.GetTempFileName ();
            NcModel.Instance.Reset (path, false);
            Console.WriteLine ("GenerateDbFile output at {0}", path);
        }
    }
}

