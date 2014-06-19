//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    // All objects that have a score must implement this interface.
    public interface IScorable
    {
        // Score version of this object. When an object is created, this value
        // is set to 0. UpgradeScoreVersion() will perform all processing to
        // upgrade the object to the current version.
        int ScoreVersion { get; set; }

        // Get the score of an object. Score combining, if required, happens
        // inside this function
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

        // Upload new delta to synchronization server. When an object's score
        // states are updated, the delta is maintained. Periodically, the
        // delta is uploaded to the synchronization server.
        void UploadScore ();

        // Look for a previously uploaded score in the synchronization server and
        // download and use it for this object.
        bool DownloadScore ();
    }
}

