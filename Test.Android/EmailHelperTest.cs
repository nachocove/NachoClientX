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
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.FailBadScheme, EmailHelper.IsValidServer ("badscheme://foo.com"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.FailUnknown, EmailHelper.IsValidServer ("://foo.com"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.FailUnknown, EmailHelper.IsValidServer ("//foo.com"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.FailUnknown, EmailHelper.IsValidServer ("/foo.com"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.IsValidServer ("foo."));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.IsValidServer ("foo"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.FailUnknown, EmailHelper.IsValidServer ("foo.com:100000"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.FailUnknown, EmailHelper.IsValidServer ("foo.com:-1"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.FailUnknown, EmailHelper.IsValidServer ("foo.com:bar"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.IsValidServer ("http://foo.com"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.IsValidServer ("https://foo.com"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.IsValidServer ("https://foo.com:8080"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.IsValidServer ("https://foo.com:8080/traveler"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.IsValidServer ("https://foo.com/traveler"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.IsValidServer ("foo.com"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.IsValidServer ("foo.com:8080"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.IsValidServer ("foo.com:8080/traveler"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.IsValidServer ("foo.com/traveler"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.FailHadQuery, EmailHelper.IsValidServer ("foo.com/traveler?cat=dog"));
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.IsValidServer ("chuck@taylor.com"));
        }

        [Test]
        public void TestParseServer ()
        {
            McServer server;
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "foo."));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "foo"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "http://foo.com"));
            Assert.AreEqual ("http", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (80, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "https://foo.com"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "https://foo.com:8080"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (8080, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "https://foo.com:8080/traveler"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (8080, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "https://foo.com/traveler"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "foo.com"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "foo.com:8080"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (8080, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "foo.com:8080/traveler"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (8080, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "foo.com/traveler"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "foo.com/traveler/"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "foo.com/"));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "foo.com/traveler" + McServer.Default_Path));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "foo.com/traveler" + McServer.Default_Path + "/"));
            server.CopyFrom (server);
            Assert.IsTrue (server.IsSameServer (server));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
        }

        [Test]
        public void TestCopyServer ()
        {
            McServer src;
            McServer server;
            src = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "foo."));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            src = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "foo"));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "http://foo.com"));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("http", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (80, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            src = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "https://foo.com"));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            src = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "https://foo.com:8080"));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (8080, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            src = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "https://foo.com:8080/traveler"));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (8080, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            src = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "https://foo.com/traveler"));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            src = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "foo.com"));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            src = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "foo.com:8080"));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (8080, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            src = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "foo.com:8080/traveler"));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (8080, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            src = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "foo.com/traveler"));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            src = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "foo.com/traveler/"));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            src = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "foo.com/"));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            src = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "foo.com/traveler" + McServer.Default_Path));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
            src = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref src, "foo.com/traveler" + McServer.Default_Path + "/"));
            server = new McServer ();
            server.CopyFrom (src);
            Assert.IsTrue (server.IsSameServer (src));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.com", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual ("/traveler" + McServer.Default_Path, server.Path);
        }

        [Test]
        public void QuoteForReplyTest()
        {
            string quoted;

            quoted = EmailHelper.QuoteForReply (null);
            Assert.IsNull (quoted);
            quoted = EmailHelper.QuoteForReply ("");
            Assert.AreEqual ("", quoted);
            quoted = EmailHelper.QuoteForReply ("\n");
            Assert.AreEqual (">\n", quoted);
            quoted = EmailHelper.QuoteForReply (" \n");
            Assert.AreEqual (">  \n", quoted);
            quoted = EmailHelper.QuoteForReply (">  \n");
            Assert.AreEqual (">>  \n", quoted);
            quoted = EmailHelper.QuoteForReply ("hello\n");
            Assert.AreEqual ("> hello\n", quoted);
            quoted = EmailHelper.QuoteForReply ("goodbye\n> hello\n");
            Assert.AreEqual ("> goodbye\n>> hello\n", quoted);
        }

        [Test]
        public void InitialsTest()
        {
            string initials;
            string emailAddress;

            emailAddress = "\"Bank of America\" <BankofAmerica@loyaltycard.bankofamerica.com>";
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("BA", initials);

            emailAddress = "\"Bob Jones (ECMA)\" <BobJones@ecma.org>";
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("BJ", initials);

            emailAddress = "wmonline@wm.com";
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("W", initials);

            emailAddress = "\"rascal2210@hotmail.com\" <rascal2210@hotmail.com>";
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual("R", initials);
        }
    }
}

