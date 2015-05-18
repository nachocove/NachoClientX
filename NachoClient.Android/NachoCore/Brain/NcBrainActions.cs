﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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
        protected bool GleanEmailMessage (McEmailMessage emailMessage, string accountAddress, bool quickScore)
        {
            if (null == emailMessage) {
                return false;
            }
            Log.Debug (Log.LOG_BRAIN, "glean contact from email message {0}", emailMessage.Id);
            if ((int)McEmailMessage.GleanPhaseEnum.GLEAN_PHASE1 > emailMessage.HasBeenGleaned) {
                if (!NcContactGleaner.GleanContactsHeaderPart1 (emailMessage)) {
                    return false;
                }
            }
            if ((int)McEmailMessage.GleanPhaseEnum.GLEAN_PHASE2 > emailMessage.HasBeenGleaned) {
                if (!NcContactGleaner.GleanContactsHeaderPart2 (emailMessage)) {
                    return false;
                }
            }

            if (quickScore && (0 == emailMessage.ScoreVersion) && (0.0 == emailMessage.Score)) {
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
            Log.Debug (Log.LOG_BRAIN, "analyze email address {0}", emailAddress.Id);
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

        // Try to get the file path of the body of an McAbstrItem (or its derived classes)
        private string GetValidBodypath (McAbstrItem item, string caller, out McBody outBody)
        {
            // Make sure that there is a body
            var body = item.GetBody ();
            outBody = body;
            if (null == body) {
                Log.Warn (Log.LOG_BRAIN, "{0}: null body (id={1}, bodyId={2})", caller, item.Id, item.BodyId);
                return null;
            }

            // Make sure the body is completely downloaded
            if (!body.IsValid || (McAbstrFileDesc.FilePresenceEnum.Complete != body.FilePresence)) {
                Log.Warn (Log.LOG_BRAIN, "{0}: not a valid, downloaded body (id={1}, bodyId={2}, isValid={3}, filePresence={4})",
                    item.Id, item.BodyId, body.IsValid, body.FilePresence);
                return null;
            }

            // Make sure that the file path exists
            var filePath = body.GetFilePath ();
            if (!File.Exists (filePath)) {
                Log.Warn (Log.LOG_BRAIN, "{0}: {1} does not exist (id={2}, bodyId={3})", filePath, item.Id, item.BodyId);
                body.DeleteFile (); // fix the inconsistent state
                return null;
            }

            return filePath;
        }

        protected bool IndexEmailMessage (NcIndex index, McEmailMessage emailMessage, ref long bytesIndexed)
        {
            if (null == emailMessage) {
                return false;
            }
            Log.Info (Log.LOG_BRAIN, "IndexEmailMessage: index email message {0}", emailMessage.Id);

            // Make sure the body is there
            McBody body;
            var messagePath = GetValidBodypath (emailMessage, "IndexEmailMessage", out body);
            if (null == messagePath) {
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
            try {
                var indexDoc = new EmailMessageIndexDocument (emailMessage.Id.ToString (), content, message);

                // Index the document
                bytesIndexed += index.BatchAdd (indexDoc);
            } catch (NullReferenceException e) {
                Log.Error (Log.LOG_BRAIN, "IndexEmailmessage: caught null exception - {0}", e);
            }
 
            // Mark the email message indexed
            emailMessage.IsIndexed = true;
            emailMessage.UpdateByBrain ();

            return true;
        }

        protected bool IndexContact (NcIndex index, McContact contact, ref long bytesIndex)
        {
            if ((null == index) || (null == contact)) {
                return false;
            }

            // Create a server-agnostic contact parameter object
            var contactParams = new Index.ContactIndexParameters () {
                FirstName = contact.FirstName,
                MiddleName = contact.MiddleName,
                LastName = contact.LastName,
                CompanyName = contact.CompanyName,
            };

            // Add all email address, phone numbers and addresses
            foreach (var emailAddress in contact.EmailAddresses) {
                contactParams.EmailAddresses.Add (emailAddress.Value);
            }
            foreach (var phoneNumber in contact.PhoneNumbers) {
                contactParams.PhoneNumbers.Add (phoneNumber.Value);
            }
            foreach (var address in contact.Addresses) {
                string addressString = address.Street + "\n" +
                                       address.City + "\n" +
                                       address.State + "\n" +
                                       address.PostalCode + "\n" +
                                       address.Country + "\n";
                contactParams.Addresses.Add (addressString);
            }

            // If there is a note, try to add it
            if (0 != contact.BodyId) {
                McBody dummy;
                var notePath = GetValidBodypath (contact, "IndexContact", out dummy);
                if (null == notePath) {
                    return false;
                }

                try {
                    contactParams.Note = File.ReadAllText (notePath);
                } catch (IOException) {
                    Log.Warn (Log.LOG_BRAIN, "IndexContact: fail to read {0} (id={0}, bodyId={1})", contact.Id, contact.BodyId);
                    return false;
                }
            }

            try {
                var id = contact.Id.ToString ();
                if (0 != contact.IndexVersion) {
                    // There is an old version in the index. Remove it first.
                    index.Remove ("contact", id);
                }
                var indexDoc = new ContactIndexDocument (id, contactParams);
                bytesIndex += index.BatchAdd (indexDoc);
            } catch (NullReferenceException e) {
                Log.Error (Log.LOG_BRAIN, "IndexContact: caught null exception - {0}", e);
            }

            contact.SetIndexVersion ();
            contact.UpdateIndexVersion ();

            return true;
        }

        protected void UnindexEmailMessage (int accountId, int emailMessageId)
        {
            var index = Index (accountId);
            if (null == index) {
                Log.Warn (Log.LOG_BRAIN, "fail to find index for account {0}", accountId);
                return;
            }
            index.Remove ("message", emailMessageId.ToString ());
        }

        protected void UnindexContact (int accountId, int contactId)
        {
            var index = Index (accountId);
            if (null == index) {
                Log.Warn (Log.LOG_BRAIN, "fail to find index for account {0}", accountId);
                return;
            }
            index.Remove ("contact", contactId.ToString ());
        }
    }
}

