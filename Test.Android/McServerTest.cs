//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Utils;
using NachoCore.Model;

namespace Test.Android
{

    public class McServerTest
    {
        public McServerTest ()
        {
        }

        [Test]
        public void QueryByAccountIdAndCapabilities ()
        {
            var yes = new McServer () {
                AccountId = 1,
                Capabilities = McAccount.AccountCapabilityEnum.EmailSender,
            };
            yes.Insert ();
            var no1 = new McServer () {
                // rejected on account id.
                AccountId = 2,
                Capabilities = McAccount.AccountCapabilityEnum.EmailSender,
            };
            no1.Insert ();
            var no2 = new McServer () {
                // rejected on caps.
                AccountId = 1,
                Capabilities = McAccount.AccountCapabilityEnum.CalReader,
            };
            no2.Insert ();
            var fetched = McServer.QueryByAccountIdAndCapabilities (1, McAccount.AccountCapabilityEnum.EmailSender);
            Assert.AreEqual (yes.Id, fetched.Id);
        }

        [Test]
        public void IsSameNulls()
        {
            var a = new McServer ();
            var b = new McServer ();

            Assert.IsTrue (a.IsSameServer (b));

            McServer server;
            server = new McServer ();
            Assert.AreEqual (EmailHelper.ParseServerWhyEnum.Success_0, EmailHelper.ParseServer (ref server, "foo."));
            Assert.AreEqual ("https", server.Scheme);
            Assert.AreEqual ("foo.", server.Host);
            Assert.AreEqual (443, server.Port);
            Assert.AreEqual (McServer.Default_Path, server.Path);
            a.CopyFrom (server);
            b.CopyFrom (server);
            Assert.IsTrue (a.IsSameServer (b));

            a.CopyFrom (server);
            b.CopyFrom (server);
            a.Scheme = null;
            Assert.IsFalse (a.IsSameServer (b));
            a.CopyFrom (server);
            b.CopyFrom (server);
            b.Scheme = null;
            Assert.IsFalse (a.IsSameServer (b));

            a.CopyFrom (server);
            b.CopyFrom (server);
            a.Port = 99;
            Assert.IsFalse (a.IsSameServer (b));
            a.CopyFrom (server);
            b.CopyFrom (server);
            b.Port = 99;
            Assert.IsFalse (a.IsSameServer (b));

            a.CopyFrom (server);
            b.CopyFrom (server);
            a.Host = null;
            Assert.IsFalse (a.IsSameServer (b));
            a.CopyFrom (server);
            b.CopyFrom (server);
            b.Host = null;
            Assert.IsFalse (a.IsSameServer (b));

            a.CopyFrom (server);
            b.CopyFrom (server);
            a.Path = null;
            Assert.IsFalse (a.IsSameServer (b));
            a.CopyFrom (server);
            b.CopyFrom (server);
            b.Path = null;
            Assert.IsFalse (a.IsSameServer (b));
        }

        [Test]
        public void TestQueryByHost ()
        {
            var a = new McServer ();
            a.AccountId = 2;
            a.Host = "foo";
            a.Port = 99;
            a.Insert ();
            var b = new McServer ();
            b.AccountId = 3;
            b.Host = a.Host;
            b.Port = 100;
            b.Insert ();
            var x = McServer.QueryByHost (3, a.Host);
            Assert.AreEqual (100, x.Port);
        }

        [Test]
        public void TestIsHotMail ()
        {
            Assert.IsTrue (McServer.IsHotMail ("hotmail.com"));
            Assert.IsTrue (McServer.IsHotMail ("outlook.com"));
            Assert.IsTrue (McServer.IsHotMail ("live.com"));
            Assert.IsTrue (McServer.IsHotMail ("msn.com"));
            Assert.IsFalse (McServer.IsHotMail ("shotmail.com"));
            Assert.IsFalse (McServer.IsHotMail ("poutlook.com"));
            Assert.IsFalse (McServer.IsHotMail ("cookedalive.com"));
            Assert.IsFalse (McServer.IsHotMail ("whohasmsn.com"));
            Assert.IsTrue (McServer.IsHotMail ("Hotmail.com"));
            Assert.IsTrue (McServer.IsHotMail ("OUTLOOK.com"));
            Assert.IsTrue (McServer.IsHotMail ("LIve.com"));
            Assert.IsTrue (McServer.IsHotMail ("msN.com"));
            Assert.IsFalse (McServer.IsHotMail (" msn.com"));
            Assert.IsFalse (McServer.IsHotMail ("msn.com "));
        }
    }
}

