﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using MimeKit;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.Brain
{
    // All standalone actions that can be taken by brain go here.
    // The goal is to separate the actions from the scheduling of actions.
    public partial class NcBrain
    {
        protected bool GleanEmailMessage (McEmailMessage emailMessage, string accountAddress, bool quickScore)
        {
            if (null == emailMessage) {
                return false;
            }
            Log.Info (Log.LOG_BRAIN, "glean contact from email message {0}", emailMessage.Id);
            if (!NcContactGleaner.GleanContactsHeaderPart2 (emailMessage)) {
                return false;
            }

            if (quickScore) {
                // Assign a version 0 score by checking if our address is in the to list
                InternetAddressList addressList = NcEmailAddress.ParseAddressListString (emailMessage.To);
                foreach (var address in addressList) {
                    if (!(address is MailboxAddress)) {
                        continue;
                    }
                    if (((MailboxAddress)address).Address == accountAddress) {
                        emailMessage.Score = McEmailMessage.minHotScore;
                        emailMessage.UpdateByBrain ();
                        break;
                    }
                }
            }
            return true;
        }

        protected bool AnalyzeEmailAddress (McEmailAddress emailAddress)
        {
            if (null == emailAddress) {
                return false;
            }
            Log.Info (Log.LOG_BRAIN, "analyze email address {0}", emailAddress.Id);
            emailAddress.ScoreObject ();
            return true;
        }

        protected bool AnalyzeEmailMessage (McEmailMessage emailMessage)
        {
            if (null == emailMessage) {
                return false;
            }
            Log.Debug (Log.LOG_BRAIN, "analyze email message {0}", emailMessage.Id);
            emailMessage.ScoreObject ();
            return true;
        }

        protected bool UpdateEmailAddressScore (McEmailAddress emailAddress, bool updateDependencies)
        {
            if (null == emailAddress) {
                return false;
            }
            if (Scoring.Version != emailAddress.ScoreVersion) {
                NcAssert.True (Scoring.Version > emailAddress.ScoreVersion);
                return true;
            }
            var newScore = emailAddress.GetScore ();
            bool scoreUpdated = newScore != emailAddress.Score;
            if (emailAddress.NeedUpdate || scoreUpdated) {
                Log.Debug (Log.LOG_BRAIN, "[McEmailAddress:{0}] update score -> {1:F6}",
                    emailAddress.Id, emailAddress.Score);
                emailAddress.Score = newScore;
                emailAddress.NeedUpdate = false;
                emailAddress.UpdateByBrain ();
            }
            if (updateDependencies && scoreUpdated) {
                emailAddress.MarkDependencies ();
            }
            return true;
        }

        protected bool UpdateEmailMessageScore (McEmailMessage emailMessage)
        {
            if (null == emailMessage) {
                return false;
            }
            if (Scoring.Version != emailMessage.ScoreVersion) {
                NcAssert.True (Scoring.Version > emailMessage.ScoreVersion);
                return true;
            }
            var newScore = emailMessage.GetScore ();
            if (emailMessage.NeedUpdate || (newScore != emailMessage.Score)) {
                Log.Debug (Log.LOG_BRAIN, "[McEmailMessage:{0}] update score -> {1:F6}",
                    emailMessage.Id, emailMessage.Score);
                emailMessage.Score = newScore;
                emailMessage.NeedUpdate = false;
                emailMessage.UpdateScoreAndNeedUpdate ();
            }
            return true;
        }

        protected bool IndexEmailMessage (Index.NcIndex index, McEmailMessage emailMessage, ref long bytesIndexed)
        {
            if (null == emailMessage) {
                return false;
            }
            Log.Info (Log.LOG_BRAIN, "IndexEmailMessage: index email message {0}", emailMessage.Id);

            // Make sure the body is there
            var body = emailMessage.GetBody ();
            if (null != body) {
                var messagePath = body.GetFilePath ();
                if (!File.Exists (messagePath)) {
                    Log.Warn (Log.LOG_BRAIN, "IndexEmailMessage: {0} does not exist", messagePath);
                    return false;
                }

                // Create the parsed object, its tokenizer, and its index document
                var message = NcObjectParser.ParseMimeMessage (messagePath);
                if (null == message) {
                    Log.Warn (Log.LOG_BRAIN, "IndexEmailMessage: Invalid MIME message (emailMessageId={0}, bodyId={1}, bodyType={2}, filePresence={3}",
                        emailMessage.Id, emailMessage.BodyId, body.BodyType, body.FilePresence);
                    return false;
                }
                var tokenizer = new NcMimeTokenizer (message);
                var content = tokenizer.Content;
                var indexDoc = new Index.IndexEmailMessage (emailMessage.Id.ToString (), content, message);

                // Index the document
                bytesIndexed += index.BatchAdd (indexDoc);
            } else {
                Log.Warn (Log.LOG_BRAIN, "IndexEmailMessage: null body (emailMessageId={0}, bodyId={1})",
                    emailMessage.Id, emailMessage.BodyId);
                return false;
            }

            // Mark the email message indexed
            emailMessage.IsIndexed = true;
            emailMessage.UpdateByBrain ();

            return true;
        }

        public void UnindexEmailMessage (McEmailMessage emailMessage)
        {
            if ((null == emailMessage) || (0 == emailMessage.Id) || (0 == emailMessage.AccountId)) {
                return;
            }
            var index = Index (emailMessage.AccountId);
            index.Remove ("message", emailMessage.Id.ToString ());
        }
    }
}

