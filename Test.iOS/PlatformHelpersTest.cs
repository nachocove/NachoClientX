//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoClient;
using NUnit.Framework;

namespace Test.iOS
{
    public class PlatformHelpersTest
    {
        public PlatformHelpersTest ()
        {
        }

        [Test]
        public void TestCID()
        {
            int bodyId;
            string value;
            string message;

            message = PlatformHelpers.CheckCID (null, out bodyId, out value);
            Assert.AreEqual ("null prefix for cid", message);

            message = PlatformHelpers.CheckCID ("", out bodyId, out value);
            Assert.AreEqual ("no prefix for cid", message);

            message = PlatformHelpers.CheckCID ("/", out bodyId, out value);
            Assert.AreEqual ("no prefix for cid", message);

            message = PlatformHelpers.CheckCID ("/ ", out bodyId, out value);
            Assert.AreEqual ("no prefix for cid", message);

            message = PlatformHelpers.CheckCID ("//", out bodyId, out value);
            Assert.AreEqual ("no body id for cid", message);

            message = PlatformHelpers.CheckCID ("///", out bodyId, out value);
            Assert.AreEqual ("no value for cid", message);

            message = PlatformHelpers.CheckCID ("///abc", out bodyId, out value);
            Assert.AreEqual ("malformed body id", message);

            message = PlatformHelpers.CheckCID ("///123", out bodyId, out value);
            Assert.AreEqual ("malformed body id", message);

            message = PlatformHelpers.CheckCID ("//1", out bodyId, out value);
            Assert.AreEqual ("no trailing slash for cid", message);

            message = PlatformHelpers.CheckCID ("//12", out bodyId, out value);
            Assert.AreEqual ("no trailing slash for cid", message);

            message = PlatformHelpers.CheckCID ("//123", out bodyId, out value);
            Assert.AreEqual ("no trailing slash for cid", message);

            message = PlatformHelpers.CheckCID ("//1/", out bodyId, out value);
            Assert.AreEqual ("no value for cid", message);

            message = PlatformHelpers.CheckCID ("//12/", out bodyId, out value);
            Assert.AreEqual ("no value for cid", message);

            message = PlatformHelpers.CheckCID ("//123/", out bodyId, out value);
            Assert.AreEqual ("no value for cid", message);

            message = PlatformHelpers.CheckCID ("//1/a", out bodyId, out value);
            Assert.IsNull (message);
            Assert.AreEqual (1, bodyId);
            Assert.AreEqual ("a", value);

            message = PlatformHelpers.CheckCID ("//12/a", out bodyId, out value);
            Assert.IsNull (message);
            Assert.AreEqual (12, bodyId);
            Assert.AreEqual ("a", value);

            message = PlatformHelpers.CheckCID ("//123/a", out bodyId, out value);
            Assert.IsNull (message);
            Assert.AreEqual (123, bodyId);
            Assert.AreEqual ("a", value);

            message = PlatformHelpers.CheckCID ("//1/ab", out bodyId, out value);
            Assert.IsNull (message);
            Assert.AreEqual (1, bodyId);
            Assert.AreEqual ("ab", value);

            message = PlatformHelpers.CheckCID ("//12/ab", out bodyId, out value);
            Assert.IsNull (message);
            Assert.AreEqual (12, bodyId);
            Assert.AreEqual ("ab", value);

            message = PlatformHelpers.CheckCID ("//123/ab", out bodyId, out value);
            Assert.IsNull (message);
            Assert.AreEqual (123, bodyId);
            Assert.AreEqual ("ab", value);

            message = PlatformHelpers.CheckCID ("//1a/ab", out bodyId, out value);
            Assert.AreEqual ("malformed body id", message);

            message = PlatformHelpers.CheckCID ("//12a/ab", out bodyId, out value);
            Assert.AreEqual ("malformed body id", message);

            message = PlatformHelpers.CheckCID ("//123a/ab", out bodyId, out value);
            Assert.AreEqual ("malformed body id", message);

            message = PlatformHelpers.CheckCID ("//a/ab", out bodyId, out value);
            Assert.AreEqual ("malformed body id", message);

            message = PlatformHelpers.CheckCID ("//ab/ab", out bodyId, out value);
            Assert.AreEqual ("malformed body id", message);

            message = PlatformHelpers.CheckCID ("//abc/ab", out bodyId, out value);
            Assert.AreEqual ("malformed body id", message);

        }
    }
}

