//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace Test.Common
{
    public class McMutablesTest : NcTestBase
    {
        [Test]
        public void Reset ()
        {
            bool gotCall = false;
            McMutables.ResetYourValues += (object sender, EventArgs e) => {
                gotCall = true;
                McMutables.Set(1, "test", "key1", "value1");
            };
            McMutables.Reset ();
            Assert.True (gotCall);
            Assert.AreEqual ("value1", McMutables.Get (1, "test", "key1"));
        }

        [Test]
        public void GetMissing ()
        {
            var missing = McMutables.Get (1, "test", "missing");
            Assert.IsNull (missing);
        }

        [Test]
        public void SetClobber ()
        {
            McMutables.Set (1, "test", "key2", "value1");
            McMutables.Set (1, "test", "key2", "value2");
            Assert.AreEqual ("value2", McMutables.Get (1, "test", "key2"));
        }

        [Test]
        public void TestGetOrCreate ()
        {
            var getdef = McMutables.GetOrCreate (1, "test", "keyT", "default");
            Assert.AreEqual (getdef, "default");
            McMutables.Set (1, "test", "keyT", "notdef");
            var notdef = McMutables.GetOrCreate (1, "test", "keyT", "default");
            Assert.AreEqual (notdef, "notdef");
        }

        [Test]
        public void DeleteOne ()
        {
            McMutables.Set (1, "test", "key3", "value3");
            var gotit = McMutables.Get (1, "test", "key3");
            Assert.AreEqual ("value3", gotit);
            McMutables.Delete (1, "test", "key3");
            var notgot = McMutables.Get (1, "test", "key3");
            Assert.IsNull (notgot);
        }

        [Test]
        public void TwoAccounts ()
        {
            McMutables.Set (1, "test", "key3", "value3");
            McMutables.Set (2, "test", "key3", "value4");
            var gotit = McMutables.Get (1, "test", "key3");
            Assert.AreEqual ("value3", gotit);
            gotit = McMutables.Get (2, "test", "key3");
            Assert.AreEqual ("value4", gotit);
            var underlay = McMutables.QueryByAccountId<McMutables> (2);
            Assert.AreEqual (1, underlay.Count ());
            underlay = McMutables.QueryByAccountId<McMutables> (3);
            Assert.AreEqual (0, underlay.Count ());
        }
    }
}

