//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;

namespace Test.iOS
{
    [TestFixture]
    public class ExtensionTest
    {
        [Test]
        public void String_SanitizeFileName ()
        {
            Assert.AreEqual ("bad_name", "bad/name".SantizeFileName ());
        }
    }
}

