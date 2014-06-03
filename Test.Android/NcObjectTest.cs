//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.ActiveSync;
using SQLite;

namespace Test.Common
{
    public class NcObjectTest : NcTestBase
    {
        [Test]
        public void NcItems ()
        {
            int r;
            McServer i = new McServer ();
            Assert.True (i.Id == 0);

            r = i.Insert ();
            Assert.IsTrue (0 < r);
            Assert.True (1 == i.Id);
            Assert.True (1 == NcModel.Instance.Db.Table<McServer> ().Count ());

            try {
                r = i.Insert ();
                Assert.Fail ("Do not allow insertion if ID is set");
            } catch (NachoAssert.NachoAssertionFailure) {
                // Don't allow duplicate 
            }

            i.Id = 0;
            r = i.Insert ();
            Assert.IsTrue (0 < r);
            Assert.AreEqual (2, i.Id);
            Assert.AreEqual (2, NcModel.Instance.Db.Table<McServer> ().Count ());

            r = i.Update ();
            Assert.IsTrue (0 < r);
            Assert.AreEqual (2, NcModel.Instance.Db.Table<McServer> ().Count ());

            try {
                i.Id = 0;
                r = i.Update ();
                Assert.Fail ("Do not allow update if ID is 0");
            } catch (NachoAssert.NachoAssertionFailure) {
                // Don't allow duplicate 
            }
        }
    }
}

