//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;

namespace NachoCore.Utils
{
    public class Scoring
    {
        // Add a brief summary of what each version introduces.

        // Version 0 - A quick-n-dirty experimentation. Lots of lessons learned.
        // Version 1 - Add contact scoring, threading statistics.
        public const int Version = 1;
    }

    public interface IScoreCombiner
    {
        double Combine (params double[] scores);
    }

    public class LinearScoreCombiner : IScoreCombiner
    {
        private double [] Weights;

        public LinearScoreCombiner (params double [] weights)
        {
            Weights = new double[weights.Length];
            for (int n = 0; n < weights.Length; n++) {
                Weights [n] = weights [n];
            }
        }

        public double Combine (params double [] scores)
        {
            NcAssert.True (scores.Length == Weights.Length);
            double out_score = 0.0;
            for (int n = 0; n < Weights.Length; n++) {
                out_score += Weights [n] * scores [n];
            }
            return out_score;
        }
    }

    public class MaxScoreCombiner : IScoreCombiner
    {
        private int NumScores { get; set; }

        public MaxScoreCombiner (int numScores=0)
        {
            NumScores = numScores;
        }

        public double Combine (params double [] scores)
        {
            NcAssert.True (scores.Length > 0);
            return scores.Max ();
        }
    }
}

