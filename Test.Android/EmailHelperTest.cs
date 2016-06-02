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
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.FailHadUsername, EmailHelper.IsValidServer ("chuck@taylor.com"));
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
        public void QuoteForReplyTest ()
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
        public void InitialsTest ()
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
            Assert.AreEqual ("R", initials);

            // Fixme: Look for '' strings?
            emailAddress = "'Real Use Case' <user@company.org>";
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("RC", initials);

            // Fixme: Look for '' strings?
            emailAddress = "'Amy Davis' Via Team Unify <notifications+azgsc@teamunify.com>";
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("AU", initials);

            emailAddress = "Microsoft Outlook"; // This was an actual From address from Outlook
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("M", initials);

            emailAddress = "\"MS\" lov2cod@gmail.com"; // This was an actual From address from Jan's imap
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("M", initials);

            emailAddress = "MS lov2cod@gmail.com"; // This was an actual From address from Jan's imap
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("M", initials);

            emailAddress = "<ec2ubuntu+bncCNaemKzOFBDrw_vqBBoEJSf8eg@googlegroups.com>"; // This was an actual From address from Jan's imap
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("E", initials);

            emailAddress = "<jan.vilhuber+caf_=jan=vilhuber.com@gmail.com>"; // This was an actual From address from Jan's imap
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("J", initials);

            emailAddress = "MS <lov2cod@gmail.com>"; // This was an actual From address from Jan's imap
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("M", initials);

            emailAddress = "ec2ubuntu <ec2ubuntu@googlegroups.com>"; // This was an actual From address from Jan's imap
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("E", initials);

            emailAddress = "ec2ubuntu+unsubscribe@googlegroups.com"; // This was an actual From address from Jan's imap
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("E", initials);

            emailAddress = "ec2ubuntu-unsubscribe@googlegroups.com"; // This was an actual From address from Jan's imap
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("E", initials);

            emailAddress = "ec2ubuntu@googlegroups.com"; // This was an actual From address from Jan's imap
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("E", initials);

            // FIXME: Should be "JV"
            emailAddress = "jan.vilhuber@gmail.com"; // This was an actual From address from Jan's imap
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("J", initials);

            emailAddress = "jan@vilhuber.com"; // This was an actual From address from Jan's imap
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("J", initials);

            emailAddress = "lov2cod@gmail.com"; // This was an actual From address from Jan's imap
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("L", initials);

            emailAddress = "Phd <bob@gmail.com>";
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("P", initials);

            emailAddress = "Mr. Phd <bob@gmail.com>";
            initials = EmailHelper.Initials (emailAddress);
            Assert.AreEqual ("P", initials);
        }

        class Info
        {
            public McAccount.AccountServiceEnum s;
            public string n;
            public bool r;
        };

        static readonly Info[] services = {
            new Info { s = McAccount.AccountServiceEnum.Aol, n = "bob@aol.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.GoogleDefault, n = "bob@gmail.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "bob@hotmail.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "bob@outlook.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "bob@live.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "bob@msn.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "bob@hotmail.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "bob@outlook.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "bob@live.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "bob@msn.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "bob@icloud.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "bob@mac.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "bob@me.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "bob@hotmail.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "bob@outlook.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "bob@live.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "bob@msn.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.Yahoo, n = "bob@yahoo.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.Aol, n = "bob@aOl.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.GoogleDefault, n = "bob@gmAil.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "bob@hoTmail.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "bob@ouTlook.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "bob@liVe.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "bob@mSn.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "bob@hOtmail.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "bob@outlOok.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "bob@liVe.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "bob@mSn.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "bob@iclOud.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "bob@mAc.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "bob@mE.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "bob@hotmAil.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "bob@oUtlook.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "bob@liVe.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "bob@mSn.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.Yahoo, n = "bob@yahoo.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.Aol, n = "aol.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.GoogleDefault, n = "gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "hotmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "outlook.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "live.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "msn.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "hotmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "outlook.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "live.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "msn.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "icloud.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "mac.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "me.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "hotmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "outlook.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "live.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "msn.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.Yahoo, n = "yahoo.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.Aol, n = "bob@gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.GoogleDefault, n = "bob@aol.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "bob@gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "bob@gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.HotmailDefault, n = "bob@gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "bob@gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "bob@gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.HotmailExchange, n = "bob@gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "bob@gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "bob@gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.iCloud, n = "bob@gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "bob@gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "bob@gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.OutlookExchange, n = "bob@gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.Yahoo, n = "bob@gmail.com", r = false },
            new Info { s = McAccount.AccountServiceEnum.Exchange, n = "bob@nachocove.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.GoogleExchange, n = "bob@nachocove.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.IMAP_SMTP, n = "bob@nachocove.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.Office365Exchange, n = "bob@server.nachocove.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.Exchange, n = "bob@server.nachocove.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.GoogleExchange, n = "bob@server.nachocove.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.IMAP_SMTP, n = "bob@server.nachocove.com", r = true },
            new Info { s = McAccount.AccountServiceEnum.Office365Exchange, n = "bob@nachocove.com", r = true },
        };

        [Test]
        public void ServiceCheck ()
        {
            foreach (var s in services) {
                var r = NcServiceHelper.DoesAddressMatchService (s.n, s.s);
                if (r != s.r) {
                    Console.WriteLine ("{0} {1} {2}", s.s, s.n, s.r);
                    Assert.AreEqual (r, s.r);
                }
            }
        }

        #region ParseSubject

        class SubjectTestInfo
        {
            public string Subject;
            public DateTime FromDate;
            public string ExpectedSubject;
            public McEmailMessage.IntentType ExpectedIntent;
            public MessageDeferralType ExpectedDeferralType;
            public DateTime ExpectedIntentDate;

            public override string ToString ()
            {
                return string.Format ("SubjectTestInfo: subject=<{0}> fromDate={1}", Subject, FromDate);
            }
        }

        readonly SubjectTestInfo[] Subjects = {
            new SubjectTestInfo () {
                Subject = "",
                FromDate = DateTime.UtcNow,
                ExpectedSubject = "",
                ExpectedIntent = McEmailMessage.IntentType.None,
                ExpectedDeferralType = MessageDeferralType.None,
                ExpectedIntentDate = DateTime.MinValue,
            },
            new SubjectTestInfo () {
                Subject = "Some witty subject here.",
                FromDate = DateTime.UtcNow,
                ExpectedSubject = "Some witty subject here.",
                ExpectedIntent = McEmailMessage.IntentType.None,
                ExpectedDeferralType = MessageDeferralType.None,
                ExpectedIntentDate = DateTime.MinValue,
            },
            new SubjectTestInfo () {
                Subject = "URGENT",
                FromDate = DateTime.UtcNow,
                ExpectedSubject = "",
                ExpectedIntent = McEmailMessage.IntentType.Urgent,
                ExpectedDeferralType = MessageDeferralType.None,
                ExpectedIntentDate = DateTime.MinValue,
            },
            new SubjectTestInfo () {
                Subject = "URGENT - Foo",
                FromDate = DateTime.UtcNow,
                ExpectedSubject = "Foo",
                ExpectedIntent = McEmailMessage.IntentType.Urgent,
                ExpectedDeferralType = MessageDeferralType.None,
                ExpectedIntentDate = DateTime.MinValue,
            },
            new SubjectTestInfo () {
                Subject = "URGENT By End of Day",
                FromDate = new DateTime(2016, 5, 31, 01, 02, 03, DateTimeKind.Utc),
                ExpectedSubject = "",
                ExpectedIntent = McEmailMessage.IntentType.Urgent,
                ExpectedDeferralType = MessageDeferralType.EndOfDay,
                ExpectedIntentDate = new DateTime(2016, 5, 31, 05, 00, 00, DateTimeKind.Utc),
            },
            new SubjectTestInfo () {
                Subject = "URGENT By End of Day - Foo",
                FromDate = new DateTime(2016, 5, 31, 01, 02, 03, DateTimeKind.Utc),
                ExpectedSubject = "Foo",
                ExpectedIntent = McEmailMessage.IntentType.Urgent,
                ExpectedDeferralType = MessageDeferralType.EndOfDay,
                ExpectedIntentDate = new DateTime(2016, 5, 31, 05, 00, 00, DateTimeKind.Utc),
            },
            new SubjectTestInfo () {
                Subject = "URGENT By 6/1/2016",
                FromDate = new DateTime(2016, 5, 31, 02, 03, 04, DateTimeKind.Utc),
                ExpectedSubject = "",
                ExpectedIntent = McEmailMessage.IntentType.Urgent,
                ExpectedDeferralType = MessageDeferralType.Custom,
                ExpectedIntentDate = new DateTime(2016, 6, 1, 00, 00, 00, DateTimeKind.Utc),
            },
            new SubjectTestInfo () {
                Subject = "URGENT By 6/1/2016 - Bar",
                FromDate = new DateTime(2016, 5, 31, 02, 03, 04, DateTimeKind.Utc),
                ExpectedSubject = "Bar",
                ExpectedIntent = McEmailMessage.IntentType.Urgent,
                ExpectedDeferralType = MessageDeferralType.Custom,
                ExpectedIntentDate = new DateTime(2016, 6, 1, 00, 00, 00, DateTimeKind.Utc),
            },
            new SubjectTestInfo () {
                Subject = "URGENT: Send us money!",
                FromDate = new DateTime(2016, 5, 31, 02, 03, 04, DateTimeKind.Utc),
                ExpectedSubject = "URGENT: Send us money!",
                ExpectedIntent = McEmailMessage.IntentType.None,
                ExpectedDeferralType = MessageDeferralType.None,
                ExpectedIntentDate = DateTime.MinValue,
            },
        };

        [Test]
        public void ParseSubjectTest ()
        {
            foreach (var subjectInfo in Subjects) {
                string subject;
                McEmailMessage.IntentType intent;
                MessageDeferralType deferralType;
                DateTime intentDate;
                EmailHelper.ParseSubject (subjectInfo.Subject, subjectInfo.FromDate, out subject, out intent, out deferralType, out intentDate);
                Assert.AreEqual (subjectInfo.ExpectedSubject, subject, string.Format ("Parsed subject is wrong. raw={0}, parsed={1}", subjectInfo.Subject, subject));
                Assert.AreEqual (subjectInfo.ExpectedIntent, intent, string.Format ("Parsed intent is wrong. raw={0}, parsed={1}", subjectInfo.Subject, intent));
                Assert.AreEqual (subjectInfo.ExpectedDeferralType, deferralType, string.Format ("Parsed deferralType is wrong. raw={0}, parsed={1}", subjectInfo.Subject, deferralType));
                Assert.AreEqual (subjectInfo.ExpectedIntentDate, intentDate, string.Format ("Parsed intentDate is wrong. raw={0}, parsed={1}", subjectInfo.Subject, intentDate));
            }
        }

        #endregion
    }
}

