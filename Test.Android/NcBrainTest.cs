//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NUnit.Framework;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.Brain;
using NachoCore.Index;
using NachoCore;
using Test.iOS;

namespace Test.Common
{
    public class WrappedNcBrain : NcBrain
    {
        public bool TestAnalyzeEmailMessage (McEmailMessage emailMessage)
        {
            return AnalyzeEmailMessage (emailMessage);
        }

        public bool TestIndexEmailMessage (McEmailMessage emailMessage)
        {
            return IndexEmailMessage (emailMessage);
        }

        public bool TestIndexContact (McContact contact)
        {
            return IndexContact (contact);
        }

        public void TestCloseAllOpenedIndexes ()
        {
            OpenedIndexes.Cleanup ();
        }

        public WrappedNcBrain (string prefix) : base (prefix)
        {
        }
    }

    public class NcBrainTest : NcTestBase
    {
        const int TestIndexEmailMessageAccountId = 4;

        const int TestIndexContactAccountId = 5;

        McEmailAddress Address;

        McEmailMessage Message;

        WrappedNcBrain Brain;

        [SetUp]
        public new void SetUp ()
        {
            base.SetUp ();
            NcApplication.Instance.TestOnlyInvokeUseCurrentThread = true;
            NcTask.StartService ();
            NcBrain.StartupDelayMsec = 0;
            NcBrain.StartService ();
            Telemetry.ENABLED = false;
            Address = new McEmailAddress ();
            Address.AccountId = 1;
            Address.CanonicalEmailAddress = "bob@company.com";
            Address.Insert ();

            Message = new McEmailMessage ();
            Message.AccountId = 1;
            Message.From = "bob@company.com";
            Message.DateReceived = DateTime.Now;
            Message.Insert ();

            Brain = null;

            Directory.CreateDirectory (NcModel.Instance.GetAccountDirPath (TestIndexContactAccountId));
            Directory.CreateDirectory (NcModel.Instance.GetAccountDirPath (TestIndexEmailMessageAccountId));
        }

        [TearDown]
        public void TearDown ()
        {
            if (0 != Message.Id) {
                Message.Delete ();
            }
            if (0 != Address.Id) {
                Address.Delete ();
            }
            NcBrain.StopService ();
            while (!NcBrain.SharedInstance.IsQueueEmpty ()) {
                Thread.Sleep (50);
            }
            NcTask.StopService ();

            if (null != Brain) {
                Brain.TestCloseAllOpenedIndexes ();
                Brain.Cleanup ();
            }

            // Clean up all test indexes
            SafeDirectoryDelete (NcModel.Instance.GetIndexPath (TestIndexEmailMessageAccountId));
            SafeDirectoryDelete (NcModel.Instance.GetIndexPath (TestIndexContactAccountId));
        }

        public void SafeDirectoryDelete (string dirPath)
        {
            try {
                Directory.Delete (dirPath, true);
            } catch (IOException) {
            }
        }

        private void WaitForBrain ()
        {
            while (!NcBrain.SharedInstance.IsQueueEmpty ()) {
                Thread.Sleep (50);
            }
            NcBrain.SharedInstance.Enqueue (new NcBrainEvent (NcBrainEventType.TEST));
            while (!NcBrain.SharedInstance.IsQueueEmpty ()) {
                Thread.Sleep (50);
            }
        }

        [Test]
        public void TestUpdateEmailAddress ()
        {
            // Imagine initially 1 out of 3 emails are read
            Address.Score = 1.0 / 3.0;
            // Then receive one more and read it.
            Address.ScoreStates.EmailsReceived = 4;
            Address.ScoreStates.EmailsRead = 2;
            Address.ScoreStates.Update ();
            Address.ScoreVersion = Scoring.Version;
            Address.Update ();

            long origCount = NcBrain.SharedInstance.McEmailAddressCounters.Update.Count;
            NcBrain.UpdateAddressScore (Address.AccountId, Address.Id);
            WaitForBrain ();

            // The new score should be 0.5 with one update
            Address = McEmailAddress.QueryById<McEmailAddress> (Address.Id);
            Assert.AreEqual (0.5, Address.Score);
            Assert.AreEqual (origCount + 1, NcBrain.SharedInstance.McEmailAddressCounters.Update.Count);

            // Update again. Should get the same score with no update
            NcBrain.UpdateAddressScore (Address.AccountId, Address.Id);
            WaitForBrain ();
        }

        private void TestUpdateMessageScore (ref McEmailMessage message)
        {
            NcBrain.UpdateMessageScore (message.AccountId, message.Id);
            WaitForBrain ();
            message = McEmailMessage.QueryById<McEmailMessage> (message.Id);
        }

        [Test]
        public void TestUpdateEmailMessage ()
        {
            Address.IsVip = false;
            Address.ScoreStates.EmailsRead = 2;
            Address.ScoreStates.EmailsReceived = 3;
            Address.ScoreStates.Update ();
            Address.Score = 2.0 / 3.0;
            Address.Update ();

            // Setting UserAction to +1 changes the score to VipScore. 
            Message.Score = 2.0 / 3.0;
            Message.ScoreVersion = Scoring.Version;
            Message.UserAction = +1;
            Message.Update ();

            TestUpdateMessageScore (ref Message);
            Assert.AreEqual (McEmailMessage.VipScore, Message.Score);

            // Setting UserAction to -1 changes the score to less than minHotScore
            Message.UserAction = -1;
            Message.Update ();

            TestUpdateMessageScore (ref Message);
            Assert.True (McEmailMessage.VipScore > Message.Score);

            // Settting UserAction back to 0 changes it back to 2/3
            Message.UserAction = 0;
            Message.Update ();

            TestUpdateMessageScore (ref Message);
            Assert.AreEqual (2.0 / 3.0, Message.Score);

            // Setting McEmailAddress.IsVip to true changes the score to VipScore again.
            Address.IsVip = true;
            Address.Update ();

            TestUpdateMessageScore (ref Message);
            Assert.AreEqual (McEmailMessage.VipScore, Message.Score);

            // Error case. Update a message that does not exist
            NcBrain brain = NcBrain.SharedInstance;
            long origCount = brain.McEmailAddressCounters.Update.Count;

            NcBrain.UpdateMessageScore (1, 1000000);
            WaitForBrain ();

            Assert.AreEqual (origCount, brain.McEmailAddressCounters.Update.Count);

            // Error case. Update a message who score does not change
            NcBrain.UpdateMessageScore (Message.AccountId, Message.Id);
            WaitForBrain ();

            Assert.AreEqual (origCount, brain.McEmailAddressCounters.Update.Count);
        }

        protected void CheckGleanedContact (int accountId, string emailAddressString)
        {
            var contacts = McContact.QueryGleanedContactsByEmailAddress (accountId, emailAddressString);
            Assert.AreEqual (1, contacts.Count);
            Assert.AreEqual (McAbstrItem.ItemSource.Internal, contacts [0].Source);
            Assert.AreEqual (1, contacts [0].EmailAddresses.Count);
            Assert.AreEqual (emailAddressString, contacts [0].EmailAddresses [0].Value);
        }

        protected void InsertAndCheck (McAbstrObjectPerAcc item)
        {
            var rows = item.Insert ();
            Assert.True ((1 == rows) && (0 < item.Id));
        }

        [Test]
        public void TestAnalyzeEmail ()
        {
            int accountId = 2;
            Brain = new WrappedNcBrain ("TestAnalyzeEmail");

            // Create a glean folder
            McFolder expectedGleaned = FolderOps.CreateFolder (accountId, serverId: McFolder.ClientOwned_Gleaned, isClientOwned: true);
            Assert.NotNull (expectedGleaned); // for avoiding compilation warning

            var alan = "alan@company.net";
            var bob = "bob@company.net";
            var charles = "charles@company.net";
            var david = "david@company.net";
            var ellen = "ellen@company.net";

            // Insert one email that isn't read
            var message1 = new McEmailMessage () {
                AccountId = accountId,
                From = alan,
                To = String.Join (",", bob, charles),
                Cc = String.Join (",", david, ellen),
                IsRead = false,
                LastVerbExecuted = (int)AsLastVerbExecutedType.UNKNOWN,

            };
            InsertAndCheck (message1);
            Brain.TestAnalyzeEmailMessage (message1);

            // Verify that addresses are gleaned
            CheckGleanedContact (accountId, alan);
            CheckGleanedContact (accountId, bob);
            CheckGleanedContact (accountId, charles);
            CheckGleanedContact (accountId, david);
            CheckGleanedContact (accountId, ellen);

            // Verify the score version
            var message2 = McEmailMessage.QueryById<McEmailMessage> (message1.Id);
            Assert.AreEqual (Scoring.Version, message2.ScoreVersion);
        }

        protected void CheckOneEmailMessage (int expectedId, List<MatchedItem> matches)
        {
            Assert.AreEqual (1, matches.Count);
            Assert.AreEqual ("message", matches [0].Type);
            Assert.AreEqual (expectedId.ToString (), matches [0].Id);
        }

        [Test]
        public void TestIndexEmailMessage ()
        {
            Brain = new WrappedNcBrain ("TestIndexEmailMessage");
            var index = Brain.Index (TestIndexEmailMessageAccountId);
            Assert.NotNull (index);

            // Index an email message that does not have a body
            var message1 = new McEmailMessage () {
                AccountId = TestIndexEmailMessageAccountId,
                From = "alan@company.net",
                To = "bob@company.net",
                Subject = "test email 1 - short",
                BodyId = 0,
            };
            InsertAndCheck (message1);
            Brain.TestIndexEmailMessage (message1);
            Brain.TestCloseAllOpenedIndexes (); // need to commit before search will return match

            // Make sure the index version (IsIndexed) is correct and the document is really in the index
            Assert.AreEqual (EmailMessageIndexDocument.Version, message1.IsIndexed);
            var matches = index.SearchAllEmailMessageFields ("short");
            CheckOneEmailMessage (message1.Id, matches);
            // Not doing a thorough test of the indexing because that is done in IndexTest.

            // Index an email message that has a body but it is not downloaded.
            var body2 = new McBody () {
                AccountId = TestIndexEmailMessageAccountId,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            InsertAndCheck (body2);
            var message2 = new McEmailMessage () {
                AccountId = TestIndexEmailMessageAccountId,
                From = "charles@company.net",
                To = "david@company.net",
                Subject = "test email 2 - normal",
                BodyId = body2.Id,
            };
            InsertAndCheck (message2);
            Brain.TestIndexEmailMessage (message2);
            Brain.TestCloseAllOpenedIndexes ();

            Assert.AreEqual (EmailMessageIndexDocument.Version - 1, message2.IsIndexed);
            matches = index.SearchAllEmailMessageFields ("normal");
            CheckOneEmailMessage (message2.Id, matches);

            // Index an email message that has a downloaded body
            var body3 = new McBody () {
                AccountId = TestIndexEmailMessageAccountId,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.Complete,
            };
            InsertAndCheck (body3);
            var message3 = new McEmailMessage () {
                AccountId = TestIndexEmailMessageAccountId,
                From = "ellen@company.net",
                To = "fred@company.net",
                Subject = "test email 3 - hot",
                BodyId = body3.Id,
            };
            InsertAndCheck (message3);
            Brain.TestIndexEmailMessage (message3);
            Brain.TestCloseAllOpenedIndexes ();

            Assert.AreEqual (EmailMessageIndexDocument.Version, message3.IsIndexed);
            matches = index.SearchAllEmailMessageFields ("hot");
            CheckOneEmailMessage (message3.Id, matches);
        }

        protected void CheckOneContact (int expectedId, List<MatchedItem> matches)
        {
            Assert.AreEqual (1, matches.Count);
            Assert.AreEqual ("contact", matches [0].Type);
            Assert.AreEqual (expectedId.ToString (), matches [0].Id);
        }

        [Test]
        public void TestIndexContact ()
        {
            Brain = new WrappedNcBrain ("TestIndexContact");
            var index = Brain.Index (TestIndexContactAccountId);
            Assert.NotNull (index);

            // Index a contact that does not have a note (body).
            var contact1 = new McContact () {
                AccountId = TestIndexContactAccountId,
                FirstName = "Alan",
                BodyId = 0
            };
            contact1.AddEmailAddressAttribute (TestIndexContactAccountId, "Email1Address", "Email", "alan@company.net");
            InsertAndCheck (contact1);
            Brain.TestIndexContact (contact1);
            Brain.TestCloseAllOpenedIndexes ();

            Assert.AreEqual (ContactIndexDocument.Version, contact1.IndexVersion);
            var matches = index.SearchAllContactFields ("alan");
            CheckOneContact (contact1.Id, matches);

            // Index a contact that has a note but it is not downloaded.
            var body2 = new McBody () {
                AccountId = TestIndexContactAccountId,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None
            };
            InsertAndCheck (body2);
            var contact2 = new McContact () {
                AccountId = TestIndexContactAccountId,
                FirstName = "Bob",
                BodyId = body2.Id,
            };
            InsertAndCheck (contact2);
            Brain.TestIndexContact (contact2);
            Brain.TestCloseAllOpenedIndexes ();

            Assert.AreEqual (ContactIndexDocument.Version - 1, contact2.IndexVersion);
            matches = index.SearchAllContactFields ("bob");
            CheckOneContact (contact2.Id, matches);

            // Index a contact that has a downloaded note.
            var body3 = new McBody () {
                AccountId = TestIndexContactAccountId,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.Complete
            };
            InsertAndCheck (body3);
            var contact3 = new McContact () {
                AccountId = TestIndexContactAccountId,
                FirstName = "Charles",
                BodyId = body3.Id,
            };
            InsertAndCheck (contact3);
            Brain.TestIndexContact (contact3);
            Brain.TestCloseAllOpenedIndexes ();

            Assert.AreEqual (ContactIndexDocument.Version, contact3.IndexVersion);
            matches = index.SearchAllContactFields ("charles");
            CheckOneContact (contact3.Id, matches);
        }
    }
}

