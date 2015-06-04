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
        // Version 2 - Enforce EmailsReplied counter. Migrate EmailsRead count
        //             to EmailsReplied counter when appropriate. Add ScoreIsRead,
        //             ScoreIsReplied.
        // Version 3 - Implement VIP email addresses and hot email messages.
        public const int Version = 3;

        public static int ApplyAnalysisFunctions (AnalysisFunctionsTable analysisFunctions, int scoreVersion)
        {
            for (int ver = scoreVersion + 1; ver <= Version; ver++) {
                Action func = null;
                if (analysisFunctions.TryGetValue (ver, out func)) {
                    func ();
                }
                scoreVersion++;
                NcAssert.True (scoreVersion == ver);
            }
            return scoreVersion;
        }
    }
}

