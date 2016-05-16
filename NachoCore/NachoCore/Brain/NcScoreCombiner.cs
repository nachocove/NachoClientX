//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using NachoCore.Utils;

namespace NachoCore.Brain
{
    public delegate double NcScoreCombinerSource<T> (T obj);

    public class NcScoreCombinerException : Exception
    {
        public NcScoreCombinerException (string message) : base (message)
        {
        }
    }

    public class NcScoreCombiner<T>
    {
        protected uint NumScores { get; set; }

        public NcScoreCombiner (uint numScores)
        {
            NumScores = numScores;
        }

        protected void CheckInput (ref double[] scores)
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
                CheckOneInput (score);
            }
        }

        protected void CheckLength (int sourcesLength)
        {
            if ((0 < NumScores) && (sourcesLength != NumScores)) {
                string message = String.Format ("expect {0} sources / scores. got {1}", NumScores, sourcesLength);
                throw new NcScoreCombinerException (message);
            }
        }

        protected void CheckOneInput (double score)
        {
            if ((0.0 > score) || (1.0 < score)) {
                string message = String.Format ("invalid score {0}", score);
                throw new NcScoreCombinerException (message);
            }
        }

        protected double GetOneScore (NcScoreCombinerSource<T> source, T obj)
        {
            double score = source (obj);
            CheckOneInput (score);
            return score;
        }

        public virtual double Combine (params double[] scores)
        {
            throw new NotImplementedException (); // must override this
        }

        public virtual double Combine (T obj, params NcScoreCombinerSource<T>[] sources)
        {
            throw new NotImplementedException (); // must override this
        }
    }

    public class NcLinearScoreCombiner<T> : NcScoreCombiner<T>
    {
        private double[] Weights;

        public NcLinearScoreCombiner (params double[] weights) : base ((uint)weights.Length)
        {
            NcAssert.True (weights.Length > 0);
            if (weights.Sum () != 1) {
                throw new NcScoreCombinerException ("weights must sum up to 1.0");
            }
            Weights = new double[weights.Length];
            for (int n = 0; n < weights.Length; n++) {
                Weights [n] = weights [n];
            }
        }

        public override double Combine (params double[] scores)
        {
            CheckLength (scores.Length);
            double out_score = 0.0;
            for (int n = 0; n < Weights.Length; n++) {
                CheckOneInput (scores [n]);
                out_score += Weights [n] * scores [n];
            }
            return out_score;
        }
    }

    public class NcMaxScoreCombiner<T> : NcScoreCombiner<T>
    {
        public NcMaxScoreCombiner (uint numScores = 0) : base (numScores)
        {
        }

        public override double Combine (T obj, params NcScoreCombinerSource<T>[] sources)
        {
            double maxScore = Scoring.Min;
            CheckLength (sources.Length);
            foreach (var source in sources) {
                double score = GetOneScore (source, obj);
                if (Scoring.Max == score) {
                    return score;
                }
                maxScore = Math.Max (maxScore, score);
            }
            return maxScore;
        }
    }

    public class NcMinScoreCombiner<T> : NcScoreCombiner<T>
    {
        public NcMinScoreCombiner (uint numScores = 0) : base (numScores)
        {
        }

        public override double Combine (T obj, params NcScoreCombinerSource<T>[] sources)
        {
            double minScore = Scoring.Max;
            CheckLength (sources.Length);
            foreach (var source in sources) {
                double score = GetOneScore (source, obj);
                if (Scoring.Min == score) {
                    return score;
                }
                minScore = Math.Min (score, minScore);
            }
            return minScore;
        }
    }

    public class NcMultiplicativeScoreCombiner<T> : NcScoreCombiner<T>
    {
        public NcMultiplicativeScoreCombiner (uint numScores = 0) : base (numScores)
        {
        }

        public override double Combine (params double[] scores)
        {
            CheckLength (scores.Length);
            double score = 1.0;
            foreach (double s in scores) {
                if (Scoring.Min == s) {
                    return Scoring.Min;
                }
                CheckOneInput (s);
                score *= s;
            }
            return score;
        }
    }
}

