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
    }
}

