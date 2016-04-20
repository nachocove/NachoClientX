//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

namespace NachoCore.Utils
{
    public class AnalysisFunctionsTable : Dictionary<int, Action>
    {
    }

    // All objects that have a score must implement this interface.
    public interface IScorable
    {
        /// Score version of this object. When an object is created, this value
        /// is set to 0. UpgradeScoreVersion() will perform all processing to
        /// upgrade the object to the current version.
        int ScoreVersion { get; set; }

        /// The cached score. This score is the current score for a given version.
        /// Note that this value can change due to either a new score version
        /// (from a new version of the app) or statistics being updated.
        double Score { get; set; }
        double Score2 { get; set; }

        /// A table of analysis function indexed by score version.
        AnalysisFunctionsTable AnalysisFunctions { get; set; }

        /// Need to update score counter. When 0, 'Score' is the same value as the returned value 
        /// of UpdateScore(). When > 0, the scheduler will call UpdateScore() save the new value to
        /// 'Score'. When the model is updated in such a way that the score of this object is affected,
        /// this counter is incremented. A large value does not necessarily mean a large change in
        /// the value of the score. Rather, it means it has more frequent update. 
        int NeedUpdate { get; set; }

        /// Time variance state machine type
        int TimeVarianceType { get; set; }

        /// Time variance state machine current state
        int TimeVarianceState { get; set; }

        /// Compute the score of an object using the current model.
        Tuple<double,double> Classify ();

        // Analyze this object and update the model. Each version involes analyzing
        // different statistics / features.
        //
        // Note that this method may affect other objects. For example, scoring
        // an email message affects contacts.
        void Analyze ();
    }
}

