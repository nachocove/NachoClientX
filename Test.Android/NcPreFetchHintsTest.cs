//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Utils;
using System.Collections.Generic;

namespace Test.Common
{
    public class NcPreFetchHintsTest : NcTestBase
    {
        [Test]
        public void Simple ()
        {
            List<int> hs;
            var hints = new NcPreFetchHints ();
            Assert.NotNull (hints);
            Assert.AreEqual (0, hints.Count ());
            hs = hints.GetHints (1, 5);
            Assert.AreEqual (0, hs.Count);

            hints.AddHint (1, 100);
            Assert.AreEqual (1, hints.Count ());
            hints.AddHint (1, 101);
            Assert.AreEqual (2, hints.Count ());
            Assert.AreEqual (2, hints.Count (1));
            hs = hints.GetHints (1, 5);
            Assert.AreEqual (2, hs.Count);
            Assert.AreEqual (100, hs [0]);
            Assert.AreEqual (101, hs [1]);
            Assert.AreEqual (0, hints.Count ());

            hints.AddHint (1, 10);
            hints.AddHint (1, 11);
            hints.AddHint (2, 20);
            Assert.AreEqual (3, hints.Count ());
            Assert.AreEqual (1, hints.Count (2));
            Assert.AreEqual (2, hints.Count (1));

            for (var i = 1; i < NcPreFetchHints.KMaxFetchHintsPerAccount * 2; i++) {
                hints.AddHint (3, i);
                int expectCount;
                if (i > NcPreFetchHints.KMaxFetchHintsPerAccount) {
                    expectCount = NcPreFetchHints.KMaxFetchHintsPerAccount;
                } else {
                    expectCount = i;
                }
                Console.WriteLine ("expecting size {0}", expectCount);
                Assert.AreEqual (expectCount, hints.Count (3));
            }
        }
    }
}

