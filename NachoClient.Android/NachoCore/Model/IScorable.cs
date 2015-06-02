//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
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

        /// Need to update. Set when statistics that affects this score is updated
        /// Brain will recompute scores in the background task.
        bool NeedUpdate { get; set; }

        /// Time variance state machine type
        int TimeVarianceType { get; set; }

        /// Time variance state machine current state
        int TimeVarianceState { get; set; }

        /// Get the score of an object. Score combining, if required, happens
        /// inside this function
        double GetScore ();

        // Perform all actions that affects scoring of an object. That includes:
        //   1. Analysis of the object content
        //   2. Adjustment of collected statistics
        //   3. Recompute the score using a new function.
        // Each version may introduce new actions. This method will perform all
        // actions from the object current version to the (score) version of
        // NachoClient software.
        //
        // Note that this method may affect other objects. For example, scoring
        // an email message affects contacts.
        void ScoreObject ();
    }
}

