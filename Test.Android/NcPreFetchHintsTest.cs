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
        public void AddHintsTest ()
        {
            var hints = new NcPreFetchHints ();
            Assert.NotNull (hints);
            Assert.AreEqual (0, hints.Count ());
            hints.AddHint (1, 10);
            Assert.AreEqual (1, hints.Count ());
            hints.AddHint (1, 11);
            Assert.AreEqual (2, hints.Count ());

            hints.AddHint (2, 20);
            Assert.AreEqual (3, hints.Count ());
            Assert.AreEqual (2, hints.Count (1));
            Assert.AreEqual (1, hints.Count (2));

            hints.AddHint (3, 30);
            hints.AddHint (3, 31);
            hints.AddHint (3, 32);
            Assert.AreEqual (6, hints.Count ());
            Assert.AreEqual (2, hints.Count (1));
            Assert.AreEqual (1, hints.Count (2));
            Assert.AreEqual (3, hints.Count (3));

            for (var i = 1; i < NcPreFetchHints.KMaxFetchHintsPerAccount * 2; i++) {
                hints.AddHint (4, i);
                int expectCount;
                if (i > NcPreFetchHints.KMaxFetchHintsPerAccount) {
                    expectCount = NcPreFetchHints.KMaxFetchHintsPerAccount;
                } else {
                    expectCount = i;
                }
                Console.WriteLine ("expecting size {0}", expectCount);
                Assert.AreEqual (expectCount, hints.Count (4));
            }
        }

        [Test]
        public void GetHintsTest ()
        {
            List<int> hs;
            var hints = new NcPreFetchHints ();
            Assert.NotNull (hints);
            Assert.AreEqual (0, hints.Count ());
            hs = hints.GetHints (1, 5);
            Assert.AreEqual (0, hs.Count);

            hints.AddHint (1, 100);
            hints.AddHint (1, 101);
            hints.AddHint (3, 30);
            hints.AddHint (3, 31);
            hints.AddHint (3, 32);
            Assert.AreEqual (5, hints.Count ());

            hs = hints.GetHints (1, 5);
            Assert.AreEqual (2, hs.Count);
            Assert.AreEqual (101, hs [0]);  // 101 was added last, so should be first on the list.
            Assert.AreEqual (100, hs [1]);
            Assert.AreEqual (3, hints.Count ());
        }

        [Test]
        public void RemoveHintsTest ()
        {
            List<int> hs;
            var hints = new NcPreFetchHints ();
            Assert.NotNull (hints);
            Assert.AreEqual (0, hints.Count ());

            hints.AddHint (1, 100);
            hints.AddHint (1, 101);
            hints.AddHint (3, 30);
            hints.AddHint (3, 31);
            hints.AddHint (3, 32);
            Assert.AreEqual (5, hints.Count ());

            hints.RemoveHint (1, 100);
            Assert.AreEqual (4, hints.Count ());
            hs = hints.GetHints (1, 5);
            Assert.AreEqual (101, hs [0]);
        }
    }
}

