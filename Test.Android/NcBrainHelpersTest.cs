//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using NUnit.Framework;
using NachoCore.Brain;

namespace Test.Common
{
    public class NcBrainHelpersTest
    {
        int OriginalStartupDelayMsec;

        // Use sufficiently high fake account ids so they can never collide with actual account ids
        const int Account1 = 1000;
        const int Account2 = 1001;

        [SetUp]
        public void SetUp ()
        {
            OriginalStartupDelayMsec = NcBrain.StartupDelayMsec;
            NcBrain.StartupDelayMsec = 0;
        }

        [TearDown]
        public void TearDown ()
        {
            NcBrain.StartupDelayMsec = OriginalStartupDelayMsec;
        }

        [Test]
        public void TestOpenedIndexSet ()
        {
            var openedIndexes = new OpenedIndexSet (NcBrain.SharedInstance);
            Assert.AreEqual (0, openedIndexes.Count);

            var index1 = openedIndexes.Get (Account1);
            Assert.NotNull (index1);
            Assert.AreEqual (1, openedIndexes.Count);
            Assert.True (index1.IsWriting);

            var index2 = openedIndexes.Get (Account2);
            Assert.NotNull (index2);
            Assert.AreEqual (2, openedIndexes.Count);
            Assert.True (index2.IsWriting);

            openedIndexes.Cleanup ();
            Assert.AreEqual (0, openedIndexes.Count);
            Assert.False (index1.IsWriting);
            Assert.False (index2.IsWriting);

            // Open 2nd account again
            var index2b = openedIndexes.Get (Account2);
            Assert.NotNull (index2b);
            Assert.AreEqual (1, openedIndexes.Count);
            Assert.True (index2b.IsWriting);
            Assert.True (index2.IsWriting);
            Assert.False (index1.IsWriting);

            openedIndexes.Cleanup ();
            Assert.AreEqual (0, openedIndexes.Count);
            Assert.False (index1.IsWriting);
            Assert.False (index2.IsWriting);
            Assert.False (index2b.IsWriting);
        }

        [Test]
        public void TestRoundRobinSource ()
        {
        }

        [Test]
        public void TestRoundRobinList ()
        {
        }
    }
}

