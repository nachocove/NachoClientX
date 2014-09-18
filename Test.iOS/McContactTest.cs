//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using System.Collections.Generic;

namespace Test.Common
{
    public class McContactTest : NcTestBase
    {

        [Test]
        public void Update_01 ()
        {
            var c = new McContact ();
            c.AccountId = 1;
            c.AddEmailAddressAttribute (c.AccountId, "bob", "home", "bob@foo.com");
            c.Insert ();
            var d = McContact.QueryById<McContact> (c.Id);
            // Don't assert anything about 'd' because we don't want to read it in
            d.Update ();
            var e = McContact.QueryById<McContact> (d.Id);
            Assert.AreEqual (1, e.EmailAddresses.Count);
        }
    }
}

