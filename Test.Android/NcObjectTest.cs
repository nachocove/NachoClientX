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
        public NcObjectTest ()
        {
        }

        public class TestDb : SQLiteConnectionWithEvents
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

            NcResult r;
            MyObject i = new MyObject ();
            Assert.AreEqual(i.Id, 0);

            r = db.Insert (i);
            Assert.IsTrue (r.isOK ());
            Assert.AreEqual(r.GetObject(), 1);
            Assert.AreEqual (1, db.Table<MyObject> ().Count());

            r = db.Insert (i);
            Assert.IsTrue (r.isOK ());
            Assert.AreEqual(r.GetObject(), 2);
            Assert.AreEqual (2, db.Table<MyObject> ().Count());


            r = db.Update (i);
            Assert.IsTrue (r.isOK ());
            Assert.AreEqual (2, db.Table<MyObject> ().Count());

        }
    }
}

