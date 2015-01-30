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
        public void TestGetAddressListOfKind ()
        {
            string[] toEmails = new string[] {
                "toOne@yahoo.com",
                "toTwo@yahoo.com",
            };

            string[] ccEmails = new string[] {
                "ccOne@yahoo.com",
                "ccTwo@yahoo.com",
                "ccThree@yahoo.com",
            };

            string[] bccEmails = new string[] {
                "bccOne@yahoo.com",
                "bccTwo@yahoo.com",
                "bccThree@yahoo.com",
                "bccFour@yahoo.com",
            };

            McEmailMessage testMessage = new McEmailMessage ();
            Assert.True (0 == EmailHelper.GetAddressListOfKind (NcEmailAddress.Kind.To, testMessage).Count);
            Assert.True (0 == EmailHelper.GetAddressListOfKind (NcEmailAddress.Kind.Cc, testMessage).Count);
            Assert.True (0 == EmailHelper.GetAddressListOfKind (NcEmailAddress.Kind.Bcc, testMessage).Count);

            testMessage.To = string.Join (",", toEmails);
            testMessage.Cc = string.Join (",", ccEmails);
            testMessage.Bcc = string.Join (",", bccEmails);

            var toList = EmailHelper.GetAddressListOfKind (NcEmailAddress.Kind.To, testMessage);
            var ccList = EmailHelper.GetAddressListOfKind (NcEmailAddress.Kind.Cc, testMessage);
            var bccList = EmailHelper.GetAddressListOfKind (NcEmailAddress.Kind.Bcc, testMessage);

            Assert.True (2 == toList.Count);
            Assert.True (toEmails [0] == toList [0].address);
            Assert.True (toEmails [1] == toList [1].address);

            Assert.True (3 == ccList.Count);
            Assert.True (ccEmails [0] == ccList [0].address);
            Assert.True (ccEmails [1] == ccList [1].address);
            Assert.True (ccEmails [2] == ccList [2].address);

            Assert.True (4 == bccList.Count);
            Assert.True (bccEmails [0] == bccList [0].address);
            Assert.True (bccEmails [1] == bccList [1].address);
            Assert.True (bccEmails [2] == bccList [2].address);
            Assert.True (bccEmails [3] == bccList [3].address);

            string[] malformedToEmails = new string[] {
                "to!One*@@yahoo.com",
                "toTwo@yah&^%$oo...com",
                "joebob@apple.com",
            };
            testMessage.To = string.Join (",", malformedToEmails);
            Assert.True (1 == EmailHelper.GetAddressListOfKind (NcEmailAddress.Kind.To, testMessage).Count);
        }

        [Test]
        public void TestEmailMessageRecipientsToString ()
        {
            McEmailMessage testMessage = new McEmailMessage ();
            Assert.True (String.IsNullOrEmpty(EmailHelper.EmailMessageRecipientsToString (testMessage)));

            testMessage.To = "adam@yahoo.com, bill@yahoo.com";
            Assert.True (testMessage.To == EmailHelper.EmailMessageRecipientsToString (testMessage));

            testMessage.Cc = "colin <colin@yahoo.com>";
            Assert.True ("adam@yahoo.com, bill@yahoo.com, colin" == EmailHelper.EmailMessageRecipientsToString (testMessage));

            testMessage.Bcc = "dave <dave@yahoo.com>";
            Assert.True ("adam@yahoo.com, bill@yahoo.com, colin, dave" == EmailHelper.EmailMessageRecipientsToString (testMessage));
        }
    }
}

