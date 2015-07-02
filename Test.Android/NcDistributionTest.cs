//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Utils;

namespace Test.Common
{
    public class NcDistributionTest
    {
        [TestCase]
        public void ShiftedArray ()
        {
            ShiftedArray<int> array = new ShiftedArray<int> (-5, 3);

            // Verify OutOfBound()
            Assert.True (array.OutOfRange (-6));
            Assert.True (array.OutOfRange (4));
            Assert.False (array.OutOfRange (-5));
            Assert.False (array.OutOfRange (3));

            // Verify that all values are properly initialized to 0
            for (int n = -5; n < 3; n++) {
                Assert.AreEqual (0, array [n]);
            }

            // Verify that setter works
            for (int n = -5; n < 3; n++) {
                array [n] = n;
            }
            for (int n = -5; n < 3; n++) {
                Assert.AreEqual (n, array [n]);
            }

            for (int n = -5; n < 3; n++) {
                array [n] *= -1;
            }
            for (int n = -5; n < 3; n++) {
                Assert.AreEqual (-n, array [n]);
            }
        }

        [TestCase]
        public void ShiftedArrayError ()
        {
            Assert.Throws<IndexOutOfRangeException> (() => {
                ShiftedArray<int> array = new ShiftedArray<int> (-5, 3);
                int value = array [-6];
            });

            Assert.Throws<IndexOutOfRangeException> (() => {
                ShiftedArray<int> array = new ShiftedArray<int> (-5, 3);
                int value = array [4];
            });

            Assert.Throws<IndexOutOfRangeException> (() => {
                ShiftedArray<int> array = new ShiftedArray<int> (-5, 3);
                array [-6] = 1;
            });

            Assert.Throws<IndexOutOfRangeException> (() => {
                ShiftedArray<int> array = new ShiftedArray<int> (-5, 3);
                array [4] = 1;
            });
        }

        [TestCase]
        public void Pdf ()
        {
            // Distribution 1 - Basic 0 to 9 with 1 value in each bin
            NcDistribution dist = new NcDistribution (1, 0, 9); // 10 bins

            // Add 55 samples with 10 in bin 0, 9 in bin 1, ... 1 in bin 9.
            for (int n = 0; n <= 9; n++) {
                for (int m = 0; m <= n; m++) {
                    dist.AddSample (m);
                }
            }

            // Verify pdf values
            for (int n = 0; n <= 9; n++) {
                Assert.AreEqual ((double)(10 - n) / (double)55, dist.Pdf (n));
            }

            // Verify ranges of all bins
            for (int n = -40; n <= 50; n += 10) {
                for (int m = 0; m < 10; m++) {
                    // Distribution 2 - -40 to 59 with 10 in each bin.
                    dist = new NcDistribution (10, -4, 5); // 10 bins

                    // Verify special case of 0 sample
                    Assert.AreEqual (0.0, dist.Pdf (n + m));

                    // Verify the bin can be added
                    dist.AddSample (n + m);
                    Assert.AreEqual (1.0, dist.Pdf (n + m));
                    Assert.AreEqual (1.0, dist.Pdf (n));
                }
            }

            // Verify out of range samples
            dist = new NcDistribution (10, -4, 5);

            Assert.AreEqual (0.0, dist.Pdf (-40));
            dist.AddSample (-41);
            Assert.AreEqual (1.0, dist.Pdf (-40));

            Assert.AreEqual (0.0, dist.Pdf (59));
            dist.AddSample (60);
            Assert.AreEqual (0.5, dist.Pdf (59));

            // Do a test and combines all features
            dist = new NcDistribution (3, -1, 2);
            dist.AddSample (-4);
            dist.AddSample (-3);
            dist.AddSample (-1);
            dist.AddSample (0);
            dist.AddSample (3);
            dist.AddSample (4);
            dist.AddSample (5);
            dist.AddSample (9);

            for (int n = -3; n <= -1; n++) {
                Assert.AreEqual (0.375, dist.Pdf (n));
            }
            for (int n = 0; n <= 2; n++) {
                Assert.AreEqual (0.125, dist.Pdf (n));
            }
            for (int n = 3; n <= 5; n++) {
                Assert.AreEqual (0.375, dist.Pdf (n));
            }
            for (int n = 6; n <= 8; n++) {
                Assert.AreEqual (0.125, dist.Pdf (n));
            }
        }

        [TestCase]
        public void Cdf ()
        {
            // Distribution 1 - Basic 0 to 9 with 1 value in each bin
            NcDistribution dist = new NcDistribution (1, 0, 9); // 10 bins

            // Add 55 samples with 10 in bin 0, 9 in bin 1, ... 1 in bin 9.
            for (int n = 0; n <= 9; n++) {
                for (int m = 0; m <= n; m++) {
                    dist.AddSample (m);
                }
            }

            // Verify cdf values
            int sum = 0;
            for (int n = 0; n <= 9; n++) {
                sum += (10 - n);
                Assert.AreEqual ((double)sum / (double)55, dist.Cdf (n)); 
            }

            // Verify ranges of all bins
            for (int n = -40; n <= 50; n += 10) {
                for (int m = 0; m < 10; m++) {
                    // Distribution 2 - -40 to 59 with 10 in each bin.
                    dist = new NcDistribution (10, -4, 5); // 10 bins

                    // Verify special case of 0 sample
                    Assert.AreEqual (0.0, dist.Cdf (n + m));

                    // Verify the bin can be added
                    dist.AddSample (n + m);
                    Assert.AreEqual (1.0, dist.Cdf (n + m));
                    Assert.AreEqual (1.0, dist.Cdf (n));
                }
            }

            // Verify out of range samples
            dist = new NcDistribution (10, -4, 5);

            Assert.AreEqual (0.0, dist.Cdf (59));
            dist.AddSample (60);
            Assert.AreEqual (1.0, dist.Cdf (59));

            Assert.AreEqual (0.0, dist.Cdf (-40));
            dist.AddSample (-41);
            Assert.AreEqual (0.5, dist.Cdf (-40));

            // Verify update algorithm
            dist = new NcDistribution (1, 0, 3);

            Assert.AreEqual (4, dist.DirtyIndex);

            dist.AddSample (2);
            Assert.AreEqual (2, dist.DirtyIndex); // first add. update

            dist.AddSample (3);
            Assert.AreEqual (2, dist.DirtyIndex); // new bin index is greater than current one. no update

            dist.AddSample (1);
            Assert.AreEqual (1, dist.DirtyIndex); // new index is less. update

            Assert.AreEqual (0.0, dist.Cdf (0));
            Assert.AreEqual (1, dist.DirtyIndex); // read less than current index. no update

            Assert.AreEqual ((double)1 / (double)3, dist.Cdf (1));
            Assert.AreEqual (4, dist.DirtyIndex); // read greater than current index. update

            dist.AddSample (0);
            Assert.AreEqual (0, dist.DirtyIndex);

            Assert.AreEqual (0.25, dist.Cdf (0));
            Assert.AreEqual (4, dist.DirtyIndex); // read at current index. update

            // Do a test and combines all features
            dist = new NcDistribution (3, -1, 2);
            dist.AddSample (-4);
            dist.AddSample (-3);
            dist.AddSample (-1);
            dist.AddSample (0);
            dist.AddSample (3);
            dist.AddSample (4);
            dist.AddSample (5);
            dist.AddSample (9);

            for (int n = -3; n <= -1; n++) {
                Assert.AreEqual (0.375, dist.Cdf (n));
            }
            for (int n = 0; n <= 2; n++) {
                Assert.AreEqual (0.5, dist.Cdf (n));
            }
            for (int n = 3; n <= 5; n++) {
                Assert.AreEqual (0.875, dist.Cdf (n));
            }
            for (int n = 6; n <= 8; n++) {
                Assert.AreEqual (1.0, dist.Cdf (n));
            }

        }

        [TestCase]
        public void LoadFromDb ()
        {
            NcDistribution dist = new NcDistribution (3, -1, 2);

            // Load bins. Verify that dirty index is updated properly
            Assert.AreEqual (3, dist.DirtyIndex);
            dist.LoadBin (2, 1);
            Assert.AreEqual (2, dist.DirtyIndex);
            dist.LoadBin (1, 2);
            Assert.AreEqual (1, dist.DirtyIndex);
            dist.LoadBin (0, 4);
            Assert.AreEqual (0, dist.DirtyIndex);
            dist.LoadBin (-1, 1);
            Assert.AreEqual (-1, dist.DirtyIndex);

            for (int n = -3; n <= -1; n++) {
                Assert.AreEqual (0.125, dist.Pdf (n));
                Assert.AreEqual (0.125, dist.Cdf (n));
            }
            for (int n = 0; n <= 2; n++) {
                Assert.AreEqual (0.5, dist.Pdf (n));
                Assert.AreEqual (0.625, dist.Cdf (n));
            }
            for (int n = 3; n <= 5; n++) {
                Assert.AreEqual (0.25, dist.Pdf (n));
                Assert.AreEqual (0.875, dist.Cdf (n));
            }
            for (int n = 6; n <= 8; n++) {
                Assert.AreEqual (0.125, dist.Pdf (n));
                Assert.AreEqual (1.0, dist.Cdf (n));
            }
        }
    }
}

