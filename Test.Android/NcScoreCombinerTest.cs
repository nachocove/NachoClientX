//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NachoCore.Brain;

namespace Test.Common
{
    public class NcScoreCombinerTestSource
    {
        public List<double> Values;

        public NcScoreCombinerTestSource (List<double> values)
        {
            Values = values;
        }

        public double Get ()
        {
            double retval = Values [0];
            Values.RemoveAt (0);
            return retval;
        }
    }

    [TestFixture]
    public class NcScoreCombinerTest
    {
        /// Floating point values are goofy. Error propagation and rounding leads to
        /// unexpected results. Two floating-point values are considered equal if
        /// they are closed enough.
        private bool Equal (double expected, double got)
        {
            if (Math.Abs (expected - got) < 1.0e-10) {
                return true;
            }
            return false;
        }

        [Test]
        public void LinearCombiner ()
        {
            NcLinearScoreCombiner<object> combiner = new NcLinearScoreCombiner<object> (0.625, 0.25, 0.125);
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
            // Compare 3 numbers. Pick the maximum
            var source1 = new NcScoreCombinerTestSource (new List<double> () { 0.1, 0.8, 0.4 });
            NcScoreCombinerSource<NcScoreCombinerTestSource> func = (NcScoreCombinerTestSource s) => s.Get ();

            NcMaxScoreCombiner<NcScoreCombinerTestSource> combiner1 = new NcMaxScoreCombiner<NcScoreCombinerTestSource> (3);
            Assert.AreEqual (0.8, combiner1.Combine (source1, func, func, func));

            // Expect 3 values, got only 1
            Assert.Throws<NcScoreCombinerException> (() => {
                combiner1.Combine (null, (o1) => 1.0);
            });

            // Invalid value (2.0 is out of range)
            Assert.Throws<NcScoreCombinerException> (() => {
                combiner1.Combine (null, (o1) => 2.0, (o2) => 0.1, (o3) => 0.1);
            });

            // 2nd combiner does not have a preset # of inputs
            NcMaxScoreCombiner<NcScoreCombinerTestSource> combiner2 = new NcMaxScoreCombiner<NcScoreCombinerTestSource> ();
            Assert.AreEqual (0.8, combiner2.Combine (null, (o1) => 0.1, (o2) => 0.8));
            Assert.AreEqual (0.5, combiner2.Combine (null, (o1) => 0.5));

            // Check that a 1.0 value result in early exit
            var source2 = new NcScoreCombinerTestSource (new List<double> () { 1.0, 1.0, 0.5 });
            Assert.AreEqual (1.0, combiner2.Combine (source2, func, func, func));
            Assert.AreEqual (2, source2.Values.Count);
        }

        [Test]
        public void MinCombiner ()
        {
            // Compare 3 numbers. Pick the maximum
            var source1 = new NcScoreCombinerTestSource (new List<double> () { 0.1, 0.8, 0.4 });
            NcScoreCombinerSource<NcScoreCombinerTestSource> func = (NcScoreCombinerTestSource s) => s.Get ();

            NcMinScoreCombiner<NcScoreCombinerTestSource> combiner1 = new NcMinScoreCombiner<NcScoreCombinerTestSource> (3);
            Assert.AreEqual (0.1, combiner1.Combine (source1, func, func, func));

            // Expect 3 values, got only 1
            Assert.Throws<NcScoreCombinerException> (() => {
                combiner1.Combine (null, (o1) => 1.0);
            });

            // Invalid value (2.0 is out of range)
            Assert.Throws<NcScoreCombinerException> (() => {
                combiner1.Combine (null, (o1) => 2.0, (o2) => 0.1, (o3) => 0.1);
            });

            // 2nd combiner does not have a preset # of inputs
            NcMinScoreCombiner<NcScoreCombinerTestSource> combiner2 = new NcMinScoreCombiner<NcScoreCombinerTestSource> ();
            Assert.AreEqual (0.1, combiner2.Combine (null, (o1) => 0.1, (o2) => 0.8));
            Assert.AreEqual (0.5, combiner2.Combine (null, (o1) => 0.5));

            // Check that a 0.0 value result in early exit
            var source2 = new NcScoreCombinerTestSource (new List<double> () { 0.0, 1.0, 0.5 });
            Assert.AreEqual (0.0, combiner2.Combine (source2, func, func, func));
            Assert.AreEqual (2, source2.Values.Count);
        }

        [Test]
        public void MultiplicativeCombiner ()
        {
            NcMultiplicativeScoreCombiner<object> combiner1 = new NcMultiplicativeScoreCombiner<object> (3);
            Assert.True (Equal (0.08, combiner1.Combine (0.8, 0.5, 0.2)));
            Assert.True (Equal (0.28, combiner1.Combine (0.8, 0.7, 0.5)));

            Assert.Throws<NcScoreCombinerException> (() => {
                combiner1.Combine (1.0);
            });

            Assert.Throws<NcScoreCombinerException> (() => {
                combiner1.Combine (2.0, 0.1, 0.1);
            });

            NcMultiplicativeScoreCombiner<object> combiner2 = new NcMultiplicativeScoreCombiner<object> ();
            Assert.AreEqual (0.05, combiner2.Combine (0.1, 0.5));
            Assert.AreEqual (0.6, combiner2.Combine (0.6));
        }
    }
}

