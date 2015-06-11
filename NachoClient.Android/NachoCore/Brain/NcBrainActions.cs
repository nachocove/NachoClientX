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
            return true;
        }

        protected bool AnalyzeEmailAddress (McEmailAddress emailAddress)
        {
            if (null == emailAddress) {
                return false;
            }
            Log.Debug (Log.LOG_BRAIN, "analyze email address {0}", emailAddress.Id);
            emailAddress.Analyze ();
            return true;
        }

        protected bool AnalyzeEmailMessage (McEmailMessage emailMessage)
        {
            if (null == emailMessage) {
                return false;
            }
            Log.Debug (Log.LOG_BRAIN, "analyze email message {0}", emailMessage.Id);
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

        protected bool UpdateEmailAddressScore (McEmailAddress emailAddress, bool updateDependencies)
        {
            if (null == emailAddress) {
                return false;
            }
            if (Scoring.Version != emailAddress.ScoreVersion) {
                NcAssert.True (Scoring.Version > emailAddress.ScoreVersion);
                return true;
            }
            var newScore = emailAddress.Classify ();
            bool scoreUpdated = newScore != emailAddress.Score;
            if (emailAddress.ShouldUpdate () || scoreUpdated) {
                Log.Debug (Log.LOG_BRAIN, "[McEmailAddress:{0}] update score -> {1:F6}",
                    emailAddress.Id, emailAddress.Score);
                emailAddress.Score = newScore;
                emailAddress.NeedUpdate += 1;
                emailAddress.UpdateByBrain ();
            }
            if (updateDependencies && scoreUpdated) {
                emailAddress.MarkDependencies (NcEmailAddress.Kind.From);
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
            var newScore = emailMessage.Classify ();
            if (emailMessage.ShouldUpdate () || (newScore != emailMessage.Score)) {
                Log.Debug (Log.LOG_BRAIN, "[McEmailMessage:{0}] update score -> {1:F6}",
                    emailMessage.Id, emailMessage.Score);
                emailMessage.Score = newScore;
                emailMessage.NeedUpdate += 1;
                emailMessage.UpdateScoreAndNeedUpdate ();
            }
            return true;
        }

        protected bool UpdateEmailMessageScore (object obj)
        {
            return UpdateEmailMessageScore ((McEmailMessage)obj);
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
                    caller, item.Id, item.BodyId, body.IsValid, body.FilePresence);
                return null;
            }

            // Make sure that the file path exists
            var filePath = body.GetFilePath ();
            if (!File.Exists (filePath)) {
                Log.Warn (Log.LOG_BRAIN, "{0}: {1} does not exist (id={2}, bodyId={3})", caller, filePath, item.Id, item.BodyId);
                body.DeleteFile (); // fix the inconsistent state
                return null;
            }

            return filePath;
        }

        protected bool IndexEmailMessage (McEmailMessage emailMessage)
        {
            if ((null == emailMessage) || (0 == emailMessage.Id) || (0 == emailMessage.AccountId)) {
                return false;
            }
            Log.Info (Log.LOG_BRAIN, "IndexEmailMessage: index email message {0}", emailMessage.Id);
            var index = OpenedIndexes.Get (emailMessage.AccountId);
            if (null == index) {
                return false;
            }

            MimeMessage message = null;
            var parameters = new EmailMessageIndexParameters () {
                From = NcEmailAddress.ParseAddressListString (emailMessage.From),
                To = NcEmailAddress.ParseAddressListString (emailMessage.To),
                Cc = NcEmailAddress.ParseAddressListString (emailMessage.Cc),
                Bcc = NcEmailAddress.ParseAddressListString (emailMessage.Bcc),
                ReceivedDate = emailMessage.DateReceived,
                Subject = emailMessage.Subject,
            };
            if (0 < emailMessage.BodyId) {
                // Make sure the body is there
                McBody body;
                var messagePath = GetValidBodypath (emailMessage, "IndexEmailMessage", out body);
                if (null != messagePath) {
                    // Create the parsed object, its tokenizer, and its index document
                    message = NcObjectParser.ParseMimeMessage (messagePath);
                    if (null == message) {
                        Log.Warn (Log.LOG_BRAIN, "IndexEmailMessage: Invalid MIME message (emailMessageId={0}, bodyId={1}, bodyType={2}, filePresence={3}",
                            emailMessage.Id, emailMessage.BodyId, body.BodyType, body.FilePresence);
                    } else {
                        var tokenizer = new NcMimeTokenizer (message);
                        parameters.Content = tokenizer.Content;
                    }
                }
            }
            try {
                var id = emailMessage.Id.ToString ();
                if (0 != emailMessage.IsIndexed) {
                    // There is an old version in the index. Remove it first.
                    OpenedIndexes.Cleanup ();
                    index.Remove ("message", id);
                    index = OpenedIndexes.Get (emailMessage.AccountId);
                }
                var indexDoc = new EmailMessageIndexDocument (id, parameters, message);

                // Index the document
                BytesIndexed += index.BatchAdd (indexDoc);
            } catch (NullReferenceException e) {
                Log.Error (Log.LOG_BRAIN, "IndexEmailmessage: caught null exception - {0}", e);
            }
 
            // Mark the email message indexed
            emailMessage.SetIndexVersion ();
            emailMessage.UpdateIsIndex ();

            return true;
        }

        protected bool IndexEmailMessage (object obj)
        {
            return IndexEmailMessage ((McEmailMessage)obj);
        }

        protected bool IndexContact (McContact contact)
        {
            if ((null == contact) || (0 == contact.Id) || (0 == contact.AccountId)) {
                return false;
            }
            var index = OpenedIndexes.Get (contact.AccountId);
            if (null == index) {
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
                if (null != notePath) {
                    try {
                        contactParams.Note = File.ReadAllText (notePath);
                    } catch (IOException) {
                        Log.Warn (Log.LOG_BRAIN, "IndexContact: fail to read {0} (id={0}, bodyId={1})", contact.Id, contact.BodyId);
                    }
                }
            }

            try {
                var id = contact.Id.ToString ();
                if (0 != contact.IndexVersion) {
                    // There is an old version in the index. Remove it first.
                    OpenedIndexes.Cleanup ();
                    index.Remove ("contact", id);
                    index = OpenedIndexes.Get (contact.AccountId);
                }
                var indexDoc = new ContactIndexDocument (id, contactParams);
                BytesIndexed += index.BatchAdd (indexDoc);
            } catch (NullReferenceException e) {
                Log.Error (Log.LOG_BRAIN, "IndexContact: caught null exception - {0}", e);
            }

            contact.SetIndexVersion ();
            contact.UpdateIndexVersion ();

            return true;
        }

        protected bool IndexContact (object obj)
        {
            return IndexContact ((McContact)obj);
        }

        protected void UnindexEmailMessage (int accountId, int emailMessageId)
        {
            OpenedIndexes.Cleanup ();
            var index = Index (accountId);
            if (null == index) {
                Log.Warn (Log.LOG_BRAIN, "fail to find index for account {0}", accountId);
                return;
            }
            index.Remove ("message", emailMessageId.ToString ());
        }

        protected void UnindexContact (int accountId, int contactId)
        {
            OpenedIndexes.Cleanup ();
            var index = Index (accountId);
            if (null == index) {
                Log.Warn (Log.LOG_BRAIN, "fail to find index for account {0}", accountId);
                return;
            }
            index.Remove ("contact", contactId.ToString ());
        }
    }
}

