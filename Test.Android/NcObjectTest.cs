//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.ActiveSync;
using SQLite;

namespace Test.Android
{
    [TestFixture]
    public class NcObjectTest
    {
        public class TestDb : SQLiteConnection
        {
            public TestDb () : base (System.IO.Path.GetTempFileName (), true)
            {
                ;
            }
        }

        public class MyObject : McObject
        {
        }

        [Test]
        public void NcItems ()
        {
            TestDb db = new TestDb ();

            // Start with a clean db
            db.CreateTable<MyObject> ();
            db.DropTable<MyObject> ();

            // Create a new db
            db.CreateTable<MyObject> ();

            int r;
            MyObject i = new MyObject ();
            Assert.AreEqual (i.Id, 0);

            r = db.Insert (i);
            Assert.IsTrue (0 < r);
            Assert.AreEqual (1, i.Id);
            Assert.AreEqual (1, db.Table<MyObject> ().Count ());

            try {
                r = db.Insert (i);
                Assert.Fail ("Do not allow insertion if ID is set");
            } catch (NachoAssert.NachoAssertionFailure) {
                // Don't allow duplicate 
            }

            i.Id = 0;
            r = db.Insert (i);
            Assert.IsTrue (0 < r);
            Assert.AreEqual (2, i.Id);
            Assert.AreEqual (2, db.Table<MyObject> ().Count ());

            r = db.Update (i);
            Assert.IsTrue (0 < r);
            Assert.AreEqual (2, db.Table<MyObject> ().Count ());

            try {
                i.Id = 0;
                r = db.Update (i);
                Assert.Fail ("Do not allow update if ID is 0");
            } catch (NachoAssert.NachoAssertionFailure) {
                // Don't allow duplicate 
            }
        }
    }
}

