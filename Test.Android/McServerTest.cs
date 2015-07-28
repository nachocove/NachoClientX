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
        public void TestHostIsHotMail ()
        {
            var serv = new McServer ();
            serv.Host = "s.outlook.com";
            Assert.IsTrue (serv.HostIsHotMail ());
            serv.Host = "blu403-m.outlook.com";
            Assert.IsTrue (serv.HostIsHotMail ());
            serv.Host = "poutlook.com";
            Assert.IsFalse (serv.HostIsHotMail ());
            serv.Host = "blu403-moutlook.com";
            Assert.IsFalse (serv.HostIsHotMail ());
            serv.Host = "gmail.com";
            Assert.IsFalse (serv.HostIsHotMail ());
            serv.Host = "outlook.com";
            Assert.IsTrue (serv.HostIsHotMail ());
            serv.Host = "hotmail.com";
            Assert.IsTrue (serv.HostIsHotMail ());
        }
    }
}

