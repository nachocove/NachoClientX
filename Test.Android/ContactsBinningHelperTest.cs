//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.Utils;

namespace Test.Common
{
    public class ContactsBinningHelperTest
    {
        protected bool IsAlpha (char c)
        {
            return (('A' <= c) && ('Z' >= c)) || (('a' <= c) && ('z' >= c));
        }

        [Test]
        public void TestBinningContacts ()
        {
            var contacts = new List<NcContactIndex> () {
                new NcContactIndex () {
                    Id = 10,
                    FirstLetter = "*",
                },
                new NcContactIndex () {
                    Id = 2,
                    FirstLetter = "+",
                },
                new NcContactIndex () {
                    Id = 3,
                    FirstLetter = "A",
                },
                new NcContactIndex () {
                    Id = 5,
                    FirstLetter = "a",
                },
                new NcContactIndex () {
                    Id = 20,
                    FirstLetter = "C",
                },
                new NcContactIndex () {
                    Id = 11,
                    FirstLetter = "d"
                },
                new NcContactIndex () {
                    Id = 12,
                    FirstLetter = "_", // contact of death in qa#630
                },
                new NcContactIndex () {
                    Id = 9,
                    FirstLetter = null,
                },
                new NcContactIndex () {
                    Id = 4,
                    FirstLetter = "李",
                },
            };

            var bins = ContactsBinningHelper.BinningContacts (ref contacts);
            Assert.AreEqual (27, bins.Length);
            for (int n = 0; n < 27; n++) {
                var bin = bins [n];
                switch (bin.FirstLetter) {
                case 'A':
                    Assert.AreEqual (0, bin.Start);
                    Assert.AreEqual (2, bin.Length);
                    break;
                case 'C':
                    Assert.AreEqual (2, bin.Start);
                    Assert.AreEqual (1, bin.Length);
                    break;
                case 'D':
                    Assert.AreEqual (3, bin.Start);
                    Assert.AreEqual (1, bin.Length);
                    break;
                case '#':
                    Assert.AreEqual (4, bin.Start);
                    Assert.AreEqual (5, bin.Length);
                    break;
                default:
                    Assert.AreEqual (0, bin.Length);
                    break;
                }
            }

            // Verify the sorted order
            for (int n = 0; n < 2; n++) {
                Assert.AreEqual ("A", contacts [n].FirstLetter.ToUpper ());
            }
            Assert.AreEqual ("C", contacts [2].FirstLetter.ToUpper ());
            Assert.AreEqual ("D", contacts [3].FirstLetter.ToUpper ());
            for (int n = 4; n < 9; n++) {
                Assert.False (IsAlpha (contacts [n].FirstLetter [0]));
            }
        }
    }
}

