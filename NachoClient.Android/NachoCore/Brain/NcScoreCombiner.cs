//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using NachoCore.Utils;

namespace NachoCore.Brain
{
    public class NcScoreCombinerException : Exception
    {
        public NcScoreCombinerException (string message) : base (message)
        {
        }
    }

    public class NcScoreCombiner
    {
        protected uint NumScores { get; set; }

        public NcScoreCombiner (uint numScores)
        {
            NumScores = numScores;
        }

        protected void CheckInput (ref double [] scores)
        {
            NcAssert.True (scores.Length > 0);
            if (0 == NumScores) {
                return;
            }
            if (scores.Length != NumScores) {
                string message = String.Format ("expect {0} scores. got {1}", NumScores, scores.Length);
                throw new NcScoreCombinerException (message);
            }
            foreach (double score in scores) {
                if ((0.0 > score) || (1.0 < score)) {
                    string message = String.Format ("invalid score {0}", score);
                    throw new NcScoreCombinerException (message);
                }
            }
        }

        public virtual double Combine (params double[] scores)
        {
            throw new NotImplementedException (); // must override this
        }
    }

    public class NcLinearScoreCombiner : NcScoreCombiner
    {
        private double [] Weights;

        public NcLinearScoreCombiner (params double [] weights) : base ((uint)weights.Length)
        {
            NcAssert.True (weights.Length > 0);
            if (weights.Sum() != 1) {
                throw new NcScoreCombinerException ("weights must sum up to 1.0");
            }
            Weights = new double[weights.Length];
            for (int n = 0; n < weights.Length; n++) {
                Weights [n] = weights [n];
            }
        }

        public override double Combine (params double [] scores)
        {
            CheckInput (ref scores);
            double out_score = 0.0;
            for (int n = 0; n < Weights.Length; n++) {
                out_score += Weights [n] * scores [n];
            }
            return out_score;
        }
    }

    public class NcMaxScoreCombiner : NcScoreCombiner
    {
        public NcMaxScoreCombiner (uint numScores=0) : base (numScores)
        {
            NumScores = numScores;
        }

        public override double Combine (params double [] scores)
        {
            CheckInput (ref scores);
            return scores.Max ();
        }
    }

    public class NcMultiplicativeScoreCombiner : NcScoreCombiner
    {
        public NcMultiplicativeScoreCombiner (uint numScores = 0) : base (numScores)
        {
            NumScores = numScores;
        }

        public override double Combine (params double [] scores)
        {
            CheckInput (ref scores);
            double score = 1.0;
            foreach (double s in scores) {
                score *= s;
            }
            return score;
        }
    }
}

