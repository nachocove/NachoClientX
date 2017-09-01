//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using MimeKit;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Index;

namespace NachoCore.Brain
{
    // All standalone actions that can be taken by brain go here.
    // The goal is to separate the actions from the scheduling of actions.
    public partial class NcBrain
    {
        protected bool GleanEmailMessage (McEmailMessage emailMessage)
        {
            if (null == emailMessage) {
                return false;
            }
            Log.Debug (Log.LOG_BRAIN, "glean contact from email message {0}", emailMessage.Id);
            emailMessage.GleanContactsIfNeeded (McEmailMessage.GleanPhaseEnum.GLEAN_PHASE2);
            return true;
        }

        protected bool AnalyzeEmailAddress (McEmailAddress emailAddress)
        {
            if (null == emailAddress) {
                return false;
            }
            emailAddress.Analyze ();
            return true;
        }

        protected bool AnalyzeEmailMessage (McEmailMessage emailMessage)
        {
            if (null == emailMessage) {
                return false;
            }
            if (!GleanEmailMessage (emailMessage)) {
                return false;
            }
            emailMessage.Analyze ();
            return true;
        }

        protected bool AnalyzeEmailMessage (object obj)
        {
            return AnalyzeEmailMessage ((McEmailMessage)obj);
        }

        protected bool UpdateEmailAddressScores (McEmailAddress emailAddress, bool updateDependencies)
        {
            if (null == emailAddress) {
                return false;
            }
            if (Scoring.Version != emailAddress.ScoreVersion) {
                NcAssert.True (Scoring.Version > emailAddress.ScoreVersion);
                return true;
            }
            var newScores = emailAddress.Classify ();
            bool scoreUpdated = newScores.Item1 != emailAddress.Score ||
                                newScores.Item2 != emailAddress.Score2;
            if (emailAddress.ShouldUpdate () || scoreUpdated) {
                emailAddress.Score = newScores.Item1;
                emailAddress.Score2 = newScores.Item2;
                Log.Debug (Log.LOG_BRAIN, "[McEmailAddress:{0}] update score -> {1:F6},{2:F6}",
                    emailAddress.Id, emailAddress.Score, emailAddress.Score2);
                emailAddress.NeedUpdate = 0;
                emailAddress.UpdateByBrain ();
            }
            if (updateDependencies && scoreUpdated) {
                emailAddress.MarkDependencies (EmailMessageAddressType.From);
            }
            return true;
        }

        protected bool UpdateEmailMessageScores (McEmailMessage emailMessage)
        {
            if (null == emailMessage) {
                return false;
            }
            if (Scoring.Version != emailMessage.ScoreVersion) {
                NcAssert.True (Scoring.Version > emailMessage.ScoreVersion);
                return true;
            }
            var newScores = emailMessage.Classify ();
            if (emailMessage.ShouldUpdate () ||
                newScores.Item1 != emailMessage.Score ||
                newScores.Item2 != emailMessage.Score2) {
                emailMessage.Score = newScores.Item1;
                emailMessage.Score2 = newScores.Item2;
                Log.Debug (Log.LOG_BRAIN, "[McEmailMessage:{0}] update score -> {1:F6},{2:F6}",
                    emailMessage.Id, emailMessage.Score, emailMessage.Score2);
                emailMessage.UpdateScores ();
                McEmailMessageNeedsUpdate.Update (emailMessage, 0);
            }
            return true;
        }

        protected bool UpdateEmailMessageScores (object obj)
        {
            return UpdateEmailMessageScores ((McEmailMessage)obj);
        }

        protected bool UpdateAddressUserAction (McEmailAddress emailAddress, int action)
        {
            if ((null == emailAddress) || (0 == emailAddress.Id)) {
                return false;
            }

            NcModel.Instance.RunInTransaction (() => {
                if (+1 == action) {
                    emailAddress.ScoreStates.MarkedHot += 1;
                } else if (-1 == action) {
                    emailAddress.ScoreStates.MarkedNotHot += 1;
                } else {
                    NcAssert.True (false);
                }
                emailAddress.ScoreStates.Update ();
                emailAddress.MarkDependencies (EmailMessageAddressType.From);
            });

            return true;
        }

        protected bool UpdateEmailMessageReadStatus (McEmailMessage emailMessage, DateTime readTime, double readVariance)
        {
            if (null == emailMessage) {
                return false;
            }
            emailMessage.UpdateReadAnalysis (readTime, readVariance);
            return true;
        }

        protected bool UpdateEmailMessageReplyStatus (McEmailMessage emailMessage, DateTime replyTime, double replyVariance)
        {
            if (null == emailMessage) {
                return false;
            }
            emailMessage.UpdateReplyAnalysis (replyTime, replyVariance);
            return true;
        }
    }
}

