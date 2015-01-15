//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Utils;
using NachoCore.Model;

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
            Assert.IsFalse (EmailHelper.IsValidServer ("foo.com/traveler?cat=dog"));
        }

        [Test]
        public void TestParseServer ()
        {
            McServer server = new McServer ();
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "foo."));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "foo"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "http://foo.com"));
            Assert.AreEqual ("http", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (80, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "https://foo.com"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "https://foo.com:8080"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (8080, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "https://foo.com:8080/traveler"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (8080, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "https://foo.com/traveler"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "foo.com"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "foo.com:8080"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (8080, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "foo.com:8080/traveler"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (8080, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "foo.com/traveler"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "foo.com/traveler/"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "foo.com/"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "foo.com/traveler" + McServer.Default_Path));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            Assert.IsTrue (EmailHelper.ParseServer (ref server, "foo.com/traveler" + McServer.Default_Path + "/"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
        }
    }
}

