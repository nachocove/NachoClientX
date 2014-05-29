//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace Test.iOS
{
    [TestFixture]
    public class McMutablesTest
    {
        public McMutablesTest ()
        {
            NcModel.Instance.Db = new TestDb ();
        }

        [Test]
        public void Reset ()
        {
            bool gotCall = false;
            McMutables.ResetYourValues += (object sender, EventArgs e) => {
                gotCall = true;
                McMutables.Set("test", "key1", "value1");
            };
            McMutables.Reset ();
            Assert.True (gotCall);
            Assert.AreEqual ("value1", McMutables.Get ("test", "key1"));
        }

        [Test]
        public void GetMissing ()
        {
            var missing = McMutables.Get ("test", "missing");
            Assert.IsNull (missing);
        }

        [Test]
        public void SetClobber ()
        {
            McMutables.Set ("test", "key2", "value1");
            McMutables.Set ("test", "key2", "value2");
            Assert.AreEqual ("value2", McMutables.Get ("test", "key2"));
        }

        [Test]
        public void TestGetOrCreate ()
        {
            var getdef = McMutables.GetOrCreate ("test", "keyT", "default");
            Assert.AreEqual (getdef, "default");
            McMutables.Set ("test", "keyT", "notdef");
            var notdef = McMutables.GetOrCreate ("test", "keyT", "default");
            Assert.AreEqual (notdef, "notdef");
        }
    }
}

