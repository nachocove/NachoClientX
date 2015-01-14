//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Utils;

namespace Test.Android
{
    public class EmailHelperTest
    {
        [Test]
        public void TestIsValidServer ()
        {
            try {
                EmailHelper.IsValidServer (null);
            } catch (NcAssert.NachoAssertionFailure) {
                // expected.
            }
            Assert.IsFalse (EmailHelper.IsValidServer ("badscheme://foo.com"));
            Assert.IsFalse (EmailHelper.IsValidServer ("://foo.com"));
            Assert.IsFalse (EmailHelper.IsValidServer ("//foo.com"));
            Assert.IsFalse (EmailHelper.IsValidServer ("/foo.com"));
            Assert.IsTrue (EmailHelper.IsValidServer ("foo."));
            Assert.IsTrue (EmailHelper.IsValidServer ("foo"));
            Assert.IsFalse (EmailHelper.IsValidServer ("foo.com:-1"));
            Assert.IsFalse (EmailHelper.IsValidServer ("foo.com:bar"));
            Assert.IsTrue (EmailHelper.IsValidServer ("http://foo.com"));
            Assert.IsTrue (EmailHelper.IsValidServer ("https://foo.com"));
            Assert.IsTrue (EmailHelper.IsValidServer ("https://foo.com:8080"));
            Assert.IsTrue (EmailHelper.IsValidServer ("https://foo.com:8080/traveler"));
            Assert.IsTrue (EmailHelper.IsValidServer ("https://foo.com/traveler"));
            Assert.IsTrue (EmailHelper.IsValidServer ("foo.com"));
            Assert.IsTrue (EmailHelper.IsValidServer ("foo.com:8080"));
            Assert.IsTrue (EmailHelper.IsValidServer ("foo.com:8080/traveler"));
            Assert.IsTrue (EmailHelper.IsValidServer ("foo.com/traveler"));
        }
    }
}

