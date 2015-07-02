//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Brain;

namespace Test.Common
{
    [TestFixture]
    public class NcScoreCombinerTest
    {
        /// Floating point values are goofy. Error propagation and rounding leads to
        /// unexpected results. Two floating-point values are considered equal if
        /// they are closed enough.
        private bool Equal (double expected, double got)
        {
            if (Math.Abs(expected - got) < 1.0e-10) {
                return true;
            }
            return false;
        }

        [Test]
        public void LinearCombiner ()
        {
            NcLinearScoreCombiner combiner = new NcLinearScoreCombiner (0.625, 0.25, 0.125);
            Assert.True (Equal (0.8125, combiner.Combine (1.0, 0.5, 0.5)));
            Assert.True (Equal (0.625, combiner.Combine (0.5, 1.0, 0.5)));
            Assert.True (Equal (0.5625, combiner.Combine (0.5, 0.5, 1.0)));

            Assert.Throws<NcScoreCombinerException> (() => {
                combiner.Combine (1.0);
            });

            Assert.Throws<NcScoreCombinerException> (() => {
                combiner.Combine (2.0, 0.1, 0.1);
            });
        }

        [Test]
        public void MaxCombiner ()
        {
            NcMaxScoreCombiner combiner1 = new NcMaxScoreCombiner (3);
            Assert.AreEqual (0.8, combiner1.Combine (0.1, 0.8, 0.4));
            Assert.AreEqual (0.5, combiner1.Combine (0.5, 0.4, 0.3));

            Assert.Throws<NcScoreCombinerException> (() => {
                combiner1.Combine (1.0);
            });

            Assert.Throws<NcScoreCombinerException> (() => {
                combiner1.Combine (2.0, 0.1, 0.1);
            });

            NcMaxScoreCombiner combiner2 = new NcMaxScoreCombiner ();
            Assert.AreEqual (0.8, combiner2.Combine (0.1, 0.8));
            Assert.AreEqual (0.5, combiner2.Combine (0.5));
        }

        [Test]
        public void MultiplicativeCombiner ()
        {
            NcMultiplicativeScoreCombiner combiner1 = new NcMultiplicativeScoreCombiner (3);
            Assert.True (Equal (0.08, combiner1.Combine (0.8, 0.5, 0.2)));
            Assert.True (Equal (0.28, combiner1.Combine (0.8, 0.7, 0.5)));

            Assert.Throws<NcScoreCombinerException> (() => {
                combiner1.Combine (1.0);
            });

            Assert.Throws<NcScoreCombinerException> (() => {
                combiner1.Combine (2.0, 0.1, 0.1);
            });

            NcMultiplicativeScoreCombiner combiner2 = new NcMultiplicativeScoreCombiner ();
            Assert.AreEqual (0.05, combiner2.Combine (0.1, 0.5));
            Assert.AreEqual (0.6, combiner2.Combine (0.6));
        }
    }
}

