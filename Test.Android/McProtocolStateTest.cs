//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;

namespace Test.iOS
{
    [TestFixture]
    public class McProtocolStateTest
    {
        [Test]
        public void TestAsProtocolVersion_GT12_1 ()
        {
            var ps = new McProtocolState ();
            ps.AsProtocolVersion = "12.0";
            Assert.IsFalse (ps.AsProtocolVersion_GT12_1 ());
            ps.AsProtocolVersion = "12.1";
            Assert.IsFalse (ps.AsProtocolVersion_GT12_1 ());
            ps.AsProtocolVersion = "14.0";
            Assert.IsTrue (ps.AsProtocolVersion_GT12_1 ());
            ps.AsProtocolVersion = "14.1";
            Assert.IsTrue (ps.AsProtocolVersion_GT12_1 ());
        }

        [Test]
        public void TestAsProtocolVersion_LT14_0 ()
        {
            var ps = new McProtocolState ();
            ps.AsProtocolVersion = "12.0";
            Assert.IsTrue (ps.AsProtocolVersion_LT14_0 ());
            ps.AsProtocolVersion = "12.1";
            Assert.IsTrue (ps.AsProtocolVersion_LT14_0 ());
            ps.AsProtocolVersion = "14.0";
            Assert.IsFalse (ps.AsProtocolVersion_LT14_0 ());
            ps.AsProtocolVersion = "14.1";
            Assert.IsFalse (ps.AsProtocolVersion_LT14_0 ());
        }
    }
}

