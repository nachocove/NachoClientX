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
        public void TestHostIsHotMail ()
        {
            var serv = new McServer ();
            serv.Host = "s.outlook.com";
            Assert.IsTrue (serv.HostIsAsHotMail ());
            serv.Host = "blu403-m.outlook.com";
            Assert.IsTrue (serv.HostIsAsHotMail ());
            serv.Host = "poutlook.com";
            Assert.IsFalse (serv.HostIsAsHotMail ());
            serv.Host = "blu403-moutlook.com";
            Assert.IsFalse (serv.HostIsAsHotMail ());
            serv.Host = "gmail.com";
            Assert.IsFalse (serv.HostIsAsHotMail ());
            serv.Host = "outlook.com";
            Assert.IsTrue (serv.HostIsAsHotMail ());
            serv.Host = "hotmail.com";
            Assert.IsTrue (serv.HostIsAsHotMail ());
        }

        [Test]
        public void TestPathIsEWS ()
        {
            Assert.IsTrue (McServer.PathIsEWS ("https://mail.bouldercolorado.gov/EWS/Exchange.asmx"));
            Assert.IsTrue (McServer.PathIsEWS ("https://mail.bouldercolorado.gov/ews/exchange.asmx"));
            Assert.IsTrue (McServer.PathIsEWS ("https://mail.bouldercolorado.gov/EWS/EXCHANGE.asmx"));
            Assert.IsTrue (McServer.PathIsEWS ("https://mail.bouldercolorado.gov/EWS/EXCHANGE.ASMX/noreally"));
            Assert.IsTrue (McServer.PathIsEWS ("EWS/Exchange.asmx/noreally"));
            Assert.IsTrue (McServer.PathIsEWS ("noreally/EWS/Exchange.asmx/"));
            Assert.IsTrue (McServer.PathIsEWS ("noreallyEWS/Exchange.asmx/"));
            Assert.IsTrue (McServer.PathIsEWS ("EWS/Exchange.asmx"));
            Assert.IsTrue (McServer.PathIsEWS ("/EWS/Exchange.asmx"));
            Assert.IsFalse (McServer.PathIsEWS ("Microsoft-Server-ActiveSync"));
            Assert.IsFalse (McServer.PathIsEWS ("/Microsoft-Server-ActiveSync"));
            Assert.IsFalse (McServer.PathIsEWS ("https://s.outlook.com/Microsoft-Server-ActiveSync"));
            Assert.IsFalse (McServer.PathIsEWS ("https://s.outlook.com"));
            Assert.IsFalse (McServer.PathIsEWS ("s.outlook.com"));
            Assert.IsFalse (McServer.PathIsEWS (""));
        }
    }
}

