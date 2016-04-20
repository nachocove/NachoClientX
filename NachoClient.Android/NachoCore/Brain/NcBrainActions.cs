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
            NcContactGleaner.GleanContactsHeader (emailMessage);
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
                emailAddress.MarkDependencies (NcEmailAddress.Kind.From);
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

        // Try to get the file path of the body of an McAbstrItem (or its derived classes)
        private string GetValidBodyPath (McAbstrItem item, string caller, out McBody outBody)
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

        protected bool IndexExists (int accountId)
        {
            return Directory.Exists (NcModel.Instance.GetAccountDirPath (accountId));
        }

        protected bool IndexEmailMessage (McEmailMessage emailMessage)
        {
            if ((null == emailMessage) || (0 == emailMessage.Id) || (0 == emailMessage.AccountId)) {
                return false;
            }
            if (!IndexExists (emailMessage.AccountId)) {
                Log.Warn (Log.LOG_SEARCH, "Account {0} no longer exists. Ignore indexing email message {1}", emailMessage.AccountId, emailMessage.Id);
                return false;
            }
            var index = OpenedIndexes.Get (emailMessage.AccountId);
            if (null == index) {
                Log.Error (Log.LOG_SEARCH, "IndexEmailMessage: no index for email message {0}/{1}", emailMessage.AccountId, emailMessage.Id);
                return false;
            }

            if (emailMessage.IsJunk) {
                Log.Info (Log.LOG_SEARCH, "IndexEmailMessage: junk email message not indexed {0}", emailMessage.Id);
                emailMessage.UpdateIsIndex (emailMessage.SetIndexVersion ());
                return true;
            }

            var parameters = new EmailMessageIndexParameters () {
                From = NcEmailAddress.ParseAddressListString (emailMessage.From),
                To = NcEmailAddress.ParseAddressListString (emailMessage.To),
                Cc = NcEmailAddress.ParseAddressListString (emailMessage.Cc),
                Bcc = NcEmailAddress.ParseAddressListString (emailMessage.Bcc),
                ReceivedDate = emailMessage.DateReceived,
                Subject = emailMessage.Subject,
                Preview = emailMessage.BodyPreview,
            };
            if (0 < emailMessage.BodyId) {
                // Make sure the body is there
                McBody body;
                var messagePath = GetValidBodyPath (emailMessage, "IndexEmailMessage", out body);
                if ((null != messagePath) && NcMimeTokenizer.CanProcessMessage (emailMessage)) {
                    switch (body.BodyType) {
                    case McAbstrFileDesc.BodyTypeEnum.PlainText_1:
                        var textMessage = NcObjectParser.ParseFileMessage (messagePath);
                        if (null == textMessage) {
                            Log.Warn (Log.LOG_SEARCH, "IndexEmailMessage: Invalid plain text message (emailMesssageId={0}, bodyId={1}, filePresence={2}",
                                emailMessage.Id, emailMessage.BodyId, body.FilePresence);
                        } else {
                            var tokenizer = new NcPlainTextTokenizer (textMessage, NcTask.Cts.Token);
                            parameters.Content = tokenizer.Content;
                        }
                        break;
                    case McAbstrFileDesc.BodyTypeEnum.HTML_2:
                        var htmlMessage = NcObjectParser.ParseFileMessage (messagePath);
                        if (null == htmlMessage) {
                            Log.Warn (Log.LOG_SEARCH, "IndexEmailMessage: Invalid HTML message (emailMessageId={0}, bodyId={1], filePresence={2}",
                                emailMessage.Id, emailMessage.BodyId, body.FilePresence);
                        } else {
                            var tokenizer = new NcHtmlTokenizer (htmlMessage, NcTask.Cts.Token);
                            parameters.Content = tokenizer.Content;
                        }
                        break;
                    case McAbstrFileDesc.BodyTypeEnum.RTF_3:
                        Log.Warn (Log.LOG_SEARCH, "IndexEmailMessage: do not support indexing RTF content yet (emailMessageId={0})", emailMessage.Id);
                        break;
                    case McAbstrFileDesc.BodyTypeEnum.MIME_4:
                        // Create the parsed object, its tokenizer, and its index document
                        var mimeMessage = NcObjectParser.ParseMimeMessage (messagePath, NcTask.Cts.Token);
                        if (null == mimeMessage) {
                            Log.Warn (Log.LOG_SEARCH, "IndexEmailMessage: Invalid MIME message (emailMessageId={0}, bodyId={1}, filePresence={2}",
                                emailMessage.Id, emailMessage.BodyId, body.FilePresence);
                        } else {
                            var tokenizer = new NcMimeTokenizer (mimeMessage.Message, NcTask.Cts.Token);
                            parameters.Content = tokenizer.Content;
                            mimeMessage.Dispose ();
                        }
                        break;
                    }
                }
            }
            try {
                var id = emailMessage.Id.ToString ();
                if (0 != emailMessage.IsIndexed) {
                    // There is an old version in the index. Remove it first.
                    OpenedIndexes.Release (emailMessage.AccountId);
                    index.Remove ("message", id);
                    index = OpenedIndexes.Get (emailMessage.AccountId);
                    Log.Info (Log.LOG_BRAIN, "IndexEmailMessage: replacing index for {0}", id);
                }
                var indexDoc = new EmailMessageIndexDocument (id, parameters);

                Log.Debug (Log.LOG_SEARCH, "IndexEmailMessage: params {0} '{1}' '{2}' '{3}' '{4}'", id, parameters.To, parameters.From, parameters.Subject, parameters.Preview);
                if (null == parameters.Content) {
                    Log.Debug (Log.LOG_SEARCH, "IndexEmailMessage: content {0}/{1} is null", id, emailMessage.SetIndexVersion());
                } else {
                    Log.Debug (Log.LOG_SEARCH, "IndexEmailMessage: content {0}/{1} '{2}'", id, emailMessage.SetIndexVersion(), parameters.Content.Substring (0, Math.Min (40, parameters.Content.Length)));
                }

                // Index the document
                BytesIndexed += index.BatchAdd (indexDoc);
            } catch (NullReferenceException e) {
                Log.Error (Log.LOG_SEARCH, "IndexEmailmessage: caught null exception - {0}", e);
            }
 
            // Mark the email message indexed
            var newIsIndexed = emailMessage.SetIndexVersion ();
            emailMessage.UpdateIsIndex (newIsIndexed);

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
            if (!IndexExists (contact.AccountId)) {
                Log.Warn (Log.LOG_SEARCH, "Account {0} no longer exists. Ignore indexing contact {1}",
                    contact.AccountId, contact.Id);
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
                var addr = NcEmailAddress.ParseMailboxAddressString (emailAddress.Value);
                if (null != addr) {
                    var idx = emailAddress.Value.IndexOf ("@");
                    contactParams.EmailDomains.Add (emailAddress.Value.Substring (idx + 1));
                }
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
                var notePath = GetValidBodyPath (contact, "IndexContact", out dummy);
                if (null != notePath) {
                    try {
                        contactParams.Note = File.ReadAllText (notePath);
                    } catch (IOException) {
                        Log.Warn (Log.LOG_SEARCH, "IndexContact: fail to read {0} (id={0}, bodyId={1})", contact.Id, contact.BodyId);
                    }
                }
            }

            try {
                var id = contact.Id.ToString ();
                if (0 != contact.IndexVersion) {
                    // There is an old version in the index. Remove it first.
                    OpenedIndexes.Release (contact.AccountId);
                    index.Remove ("contact", id);
                    index = OpenedIndexes.Get (contact.AccountId);
                }
                var indexDoc = new ContactIndexDocument (id, contactParams);
                BytesIndexed += index.BatchAdd (indexDoc);
            } catch (NullReferenceException e) {
                Log.Error (Log.LOG_SEARCH, "IndexContact: caught null exception - {0}", e);
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
            if (!IndexExists (accountId)) {
                Log.Info (Log.LOG_SEARCH, "Account {0} no longer exists. Ignore unindexing email message {1}", accountId, emailMessageId);
                return;
            }
            OpenedIndexes.Release (accountId);
            var index = Index (accountId);
            if (null == index) {
                Log.Warn (Log.LOG_SEARCH, "fail to find index for account {0}", accountId);
                return;
            }
            index.Remove ("message", emailMessageId.ToString ());
        }

        protected void UnindexContact (int accountId, int contactId)
        {
            if (!IndexExists (accountId)) {
                Log.Info (Log.LOG_SEARCH, "Account {0} no longer exists. Ignore unindexing contact {1}", accountId, contactId);
                return;
            }
            OpenedIndexes.Release (accountId);
            var index = Index (accountId);
            if (null == index) {
                Log.Warn (Log.LOG_SEARCH, "fail to find index for account {0}", accountId);
                return;
            }
            index.Remove ("contact", contactId.ToString ());
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
                emailAddress.MarkDependencies (NcEmailAddress.Kind.From);
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

