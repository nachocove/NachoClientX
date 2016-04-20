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
        // Version 4 - Fill in address maps (McMapEmailAddressEntry) for To and Cc. Fill
        //             in the statistics for those two types of addresses.
        // Version 5 - 3-D approximate Bayesian estimator.
        // Version 6 - Message header filtering.
        // Version 7 - Reply-to-me and marked hot qualifiers. Marked not hot disqualifier.
        // Version 8 - Yahoo bulk mails disqualifier.
        // Version 9 - Two scores: Hot and Likely-To-Read.
        public const int Version = 9;

        public const double Max = 1.0;

        public const double Min = 0.0;

        // The default weight for an email from a VIP
        public static double VipWeight = 1.0;

        // The default weight for an email manually marked hot (UserAction = 1)
        public static double MarkedHotWeight = 1.0;

        // The default weight for an email that is a reply to another email originated from me
        public static double RepliesToMyEmailsWeight = 1.0;

        // The default penalty for an email manually marked not hot (UserAction = -1)
        public static double MarkedNotHotPenalty = 0.0;

        // The default penalty factor for email contains marketing headers.
        public static double HeadersFilteringPenalty = 0.0;

        public static int ApplyAnalysisFunctions (AnalysisFunctionsTable analysisFunctions, int scoreVersion)
        {
            for (int ver = scoreVersion + 1; ver <= Version; ver++) {
                Action func = null;
                if (analysisFunctions.TryGetValue (ver, out func)) {
                    func ();
                }
                scoreVersion++;
                NcAssert.True (scoreVersion == ver);

                if (NcTask.Cts.Token.IsCancellationRequested) {
                    break;
                }
            }
            return scoreVersion;
        }
    }
}

