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

        McAccount Account;

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

            var bobCanonicalAddress = "bob@company.net";
            var bobEmailAddress = "Bob <bob@company.net>";

            Account = new McAccount () {
                EmailAddr = bobCanonicalAddress,
            };
            Account.Insert ();

            Address = new McEmailAddress ();
            Address.AccountId = Account.Id;
            Address.CanonicalEmailAddress = bobCanonicalAddress;
            Address.Insert ();

            Message = new McEmailMessage ();
            Message.AccountId = Account.Id;
            Message.From = bobCanonicalAddress;
            Message.To = bobEmailAddress;
            Message.DateReceived = DateTime.Now;
            Message.Insert ();

            Brain = null;

            Directory.CreateDirectory (NcModel.Instance.GetAccountDirPath (TestIndexContactAccountId));
            Directory.CreateDirectory (NcModel.Instance.GetAccountDirPath (TestIndexEmailMessageAccountId));
            Directory.CreateDirectory (System.IO.Path.Combine (NcModel.Instance.GetAccountDirPath (TestIndexEmailMessageAccountId), "tmp"));
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

            Assert.AreEqual (0.5, Address.Score);
            Assert.AreEqual (origCount + 1, NcBrain.SharedInstance.McEmailAddressCounters.Update.Count); // no new update

            // Adjust # replied
            Address.ScoreStates.EmailsReplied = 1;
            Address.ScoreStates.Update ();

            NcBrain.UpdateAddressScore (Address.AccountId, Address.Id);
            WaitForBrain ();

            Address = McEmailAddress.QueryById<McEmailAddress> (Address.Id);
            Assert.AreEqual (0.75, Address.Score);
            Assert.AreEqual (origCount + 2, NcBrain.SharedInstance.McEmailAddressCounters.Update.Count);

            // Adjust # sent
            Address.ScoreStates.EmailsSent = 4;
            Address.ScoreStates.Update ();

            NcBrain.UpdateAddressScore (Address.AccountId, Address.Id);
            WaitForBrain ();

            Address = McEmailAddress.QueryById<McEmailAddress> (Address.Id);
            Assert.AreEqual (0.875, Address.Score);
            Assert.AreEqual (origCount + 3, NcBrain.SharedInstance.McEmailAddressCounters.Update.Count);
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
            Address.ScoreVersion = Scoring.Version;
            Address.Update ();

            // Setting UserAction to +1 changes the score to VipScore.
            Message = Message.UpdateWithOCApply<McEmailMessage> ((record) => {
                var target = (McEmailMessage)record;
                target.FromEmailAddressId = Address.Id;
                target.Score = 2.0 / 3.0;
                target.ScoreVersion = Scoring.Version;
                target.UserAction = +1;
                return true;
            });

            TestUpdateMessageScore (ref Message);
            Assert.AreEqual (McEmailMessage.VipScore, Message.Score);

            // Setting UserAction to -1 changes the score to less than minHotScore
            Message = Message.UpdateWithOCApply<McEmailMessage> ((record) => {
                var target = (McEmailMessage)record;
                target.UserAction = -1;
                return true;
            });

            TestUpdateMessageScore (ref Message);
            Assert.True (McEmailMessage.VipScore > Message.Score);

            // Settting UserAction back to 0 changes it back to 2/3
            Message = Message.UpdateWithOCApply<McEmailMessage> ((record) => {
                var target = (McEmailMessage)record;
                target.UserAction = 0;
                return true;
            });

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

        protected McEmailAddress GetAddress (int accountId, string emailAddress)
        {
            var id = McEmailAddress.Get (accountId, emailAddress);
            if (0 == id) {
                return null;
            }
            return McEmailAddress.QueryById<McEmailAddress> (id);
        }

        [Test]
        public void TestAnalyzeEmail ()
        {
            int accountId = Account.Id;
            Brain = new WrappedNcBrain ("TestAnalyzeEmail");

            // Create a glean folder
            McFolder expectedGleaned = FolderOps.CreateFolder (accountId, serverId: McFolder.ClientOwned_Gleaned, isClientOwned: true);
            Assert.NotNull (expectedGleaned); // for avoiding compilation warning

            var alan = "alan@company.net";
            var bob = "bob@company.net";
            var charles = "charles@company.net";
            var david = "david@company.net";
            var ellen = "ellen@company.net";

            double testPenalty = 0.375;
            McEmailMessage.MarketingMailDisqualifier.Penalty = testPenalty;
            McEmailMessage.YahooBulkEmailDisqualifier.Penalty = testPenalty;

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

            // Verify gleaned email address statistics.
            var alanAddress = GetAddress (accountId, alan);
            Assert.AreEqual (1, alanAddress.ScoreStates.EmailsReceived);

            var bobAddress = GetAddress (accountId, bob);
            Assert.AreEqual (1, bobAddress.ScoreStates.ToEmailsReceived);

            var charlesAddress = GetAddress (accountId, charles);
            Assert.AreEqual (1, charlesAddress.ScoreStates.ToEmailsReceived);

            var davidAddress = GetAddress (accountId, david);
            Assert.AreEqual (1, davidAddress.ScoreStates.CcEmailsReceived);

            var ellenAddress = GetAddress (accountId, ellen);
            Assert.AreEqual (1, ellenAddress.ScoreStates.CcEmailsReceived);

            // Verify the score version
            var message1b = McEmailMessage.QueryById<McEmailMessage> (message1.Id);
            Assert.AreEqual (Scoring.Version, message1b.ScoreVersion);

            // Insert an email that is originated from the user account. Verify AnalyzeSendaddresses()
            var message2 = new McEmailMessage () {
                AccountId = accountId,
                From = bob,
                To = alan,
                IsRead = false,
                LastVerbExecuted = (int)AsLastVerbExecutedType.UNKNOWN,
            };

            InsertAndCheck (message2);
            Brain.TestAnalyzeEmailMessage (message2);

            var address = GetAddress (accountId, alan);
            Assert.AreEqual (1, address.ScoreStates.EmailsSent);
            var message2b = McEmailMessage.QueryById<McEmailMessage> (message2.Id);
            Assert.AreEqual (Scoring.Version, message2b.ScoreVersion);

            // Insert message 1 again but with the message being read. Also, no gleaning this time.
            var message3 = new McEmailMessage () {
                AccountId = accountId,
                From = alan,
                To = String.Join (",", bob, charles),
                Cc = String.Join (",", david, ellen),
                IsRead = true,
                LastVerbExecuted = (int)AsLastVerbExecutedType.UNKNOWN,
                DateReceived = DateTime.UtcNow,
            };

            alanAddress = GetAddress (accountId, alan);
            alanAddress.ScoreStates.EmailsSent = 4;
            alanAddress.ScoreStates.Update (); // top += 0+1+4, bottom += 1+1+4

            bobAddress = GetAddress (accountId, bob);
            bobAddress.ScoreStates.ToEmailsRead = 2;
            bobAddress.ScoreStates.ToEmailsReceived = 3;
            bobAddress.ScoreStates.Update (); // top += 1+2, bottom += 1+3

            charlesAddress = GetAddress (accountId, charles);
            charlesAddress.ScoreStates.ToEmailsReplied = 1;
            charlesAddress.ScoreStates.ToEmailsReceived = 5;
            charlesAddress.ScoreStates.Update (); // top += 1+1, bottom += 1+5

            davidAddress = GetAddress (accountId, david);
            davidAddress.ScoreStates.CcEmailsRead = 3;
            davidAddress.ScoreStates.CcEmailsReceived = 5;
            davidAddress.ScoreStates.Update (); // top += 1+3, bottom += 1+5

            ellenAddress = GetAddress (accountId, ellen);
            ellenAddress.ScoreStates.CcEmailsReplied = 0;
            ellenAddress.ScoreStates.CcEmailsReceived = 2;
            ellenAddress.ScoreStates.Update (); // top += 1+0, bottom += 2+1

            InsertAndCheck (message3);
            Brain.TestAnalyzeEmailMessage (message3);

            var message3b = McEmailMessage.QueryById<McEmailMessage> (message3.Id);
            Assert.AreEqual (Scoring.Version, message3b.ScoreVersion);
            Assert.AreEqual (12.0 / 21.0, message3b.Score);

            // Insert an email with marketing headers
            var message4 = new McEmailMessage () {
                AccountId = accountId,
                From = charles,
                IsRead = true,
                LastVerbExecuted = (int)AsLastVerbExecutedType.UNKNOWN,
                DateReceived = DateTime.UtcNow,
                Headers =
                    @"X-Apparently-To: henrykwok2000@yahoo.com; Thu, 30 Jul 2015 19:51:45 +0000
Return-Path: <newsletter@response.sourceforge.com>
Received-SPF: pass (domain of response.sourceforge.com designates 74.116.233.70 as permitted sender)
X-Originating-IP: [74.116.233.70]
Authentication-Results: mta1214.mail.ne1.yahoo.com  from=resources.sourceforge.com; domainkeys=neutral (no sig);  from=resources.sourceforge.com; dkim=pass (ok)
Received: from 127.0.0.1  (EHLO response.sourceforge.com) (74.116.233.70)
  by mta1214.mail.ne1.yahoo.com with SMTP; Thu, 30 Jul 2015 19:51:45 +0000
Received: from mail4.elabs10.com (10.10.10.54) by response.sourceforge.com id hna0521lf14l for <henrykwok2000@yahoo.com>; Thu, 30 Jul 2015 12:51:23 -0700 (envelope-from <newsletter@response.sourceforge.com>)
To: <henrykwok2000@yahoo.com>
Subject: =?utf-8?Q?Network=20Requirements=20for=20Cloud=20Deployment?=
Date: Thu, 30 Jul 2015 12:51:23 -0700
DKIM-Signature: v=1; a=rsa-sha1; c=relaxed/relaxed; d=resources.sourceforge.com; s=s2010001400b;
    h=Reply-To:From:MIME-Version:List-Unsubscribe:Content-description:Content-Type:Subject:To:Date;
    bh=4B/2NWeFPI+QvcbL3TCtT6JZWYw=;
    b=a+d58RsSlu+XjPp7Mb6QLt084YdiKCye27Tm5UC2Rshig4O3yCb4dCpXLp1Z3qdg7Tq
    AR+h466luHhCmRHE8H4xpmKPPVTKJWuRnBLOxh2yo3ZunlrocNhqc81XavyMFaYwNvs
    ncNR1PtWBXKqBYFXdY7NKqzaScXoEPUW/ypYc=
X-EmailAdvisor: 3868089
X-Delivery: Custom 2010001400
Reply-To: sourceforge@resources.sourceforge.com
List-Unsubscribe: <mailto:unsubscribe-6640@elabs10.com?subject=henrykwok2000@yahoo.com>
Content-description: fa7443f781henrykwok2000%40yahoo.com!19f0!3b05b9!77ce2ff8!rynof10.pbz!
X-Complaints-To: abuse@elabs10.com
Message-Id: <20150730195142.FA7443F78166@elabs10.com>
MIME-Version: 1.0
Content-Type: multipart/alternative;
    boundary=""=_e3adbac3ddba403fea6831b29113cd8c""
From: ""=?utf-8?Q?SourceForge=20Resources?="" <sourceforge@resources.sourceforge.com>
Content-Length: 7096"
            };
            InsertAndCheck (message4);

            Brain.TestAnalyzeEmailMessage (message4);

            var message4b = McEmailMessage.QueryById<McEmailMessage> (message4.Id);
            Assert.AreEqual (Scoring.Version, message4b.ScoreVersion);
            Assert.AreEqual (McEmailMessage.MarketingMailDisqualifier.Penalty, message4b.Score);

            // Add a sent email and a reply.
            bobAddress.ScoreStates.ToEmailsRead = 0;
            bobAddress.ScoreStates.ToEmailsReceived = 100;
            bobAddress.ScoreStates.Update ();

            var messageId = "<123@bob.company.net>";
            var message5 = new McEmailMessage () {
                AccountId = accountId,
                From = bob,
                To = charles,
                LastVerbExecuted = (int)AsLastVerbExecutedType.UNKNOWN,
                DateReceived = DateTime.UtcNow,
                MessageID = messageId,
            };
            InsertAndCheck (message5);

            var message6 = new McEmailMessage () {
                AccountId = accountId,
                From = charles,
                To = bob,
                LastVerbExecuted = (int)AsLastVerbExecutedType.UNKNOWN,
                DateReceived = DateTime.UtcNow,
                MessageID = "<456@bob.company.net>",
                InReplyTo = messageId,
            };
            InsertAndCheck (message6);

            Brain.TestAnalyzeEmailMessage (message6);
            var message6b = McEmailMessage.QueryById<McEmailMessage> (message6.Id);

            Assert.AreEqual (Scoring.Version, message6b.ScoreVersion);
            Assert.AreEqual (McEmailMessage.RepliesToMyEmailsQualifier.Weight, message6b.Score);
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
            message1 = McEmailMessage.QueryById<McEmailMessage> (message1.Id);
            Assert.AreEqual (EmailMessageIndexDocument.Version - 1, message1.IsIndexed);
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

            message2 = McEmailMessage.QueryById<McEmailMessage> (message2.Id);
            Assert.AreEqual (EmailMessageIndexDocument.Version - 1, message2.IsIndexed);
            matches = index.SearchAllEmailMessageFields ("normal");
            CheckOneEmailMessage (message2.Id, matches);

            // Index an email message that has a downloaded plain text body
            var body3 = new McBody () {
                AccountId = TestIndexEmailMessageAccountId,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.Complete,
                BodyType = McAbstrFileDesc.BodyTypeEnum.PlainText_1,
            };
            InsertAndCheck (body3);
            body3.UpdateData ("This is a plaintext email");
            var message3 = new McEmailMessage () {
                AccountId = TestIndexEmailMessageAccountId,
                From = "ellen@company.net",
                To = "fred@company.net",
                BodyId = body3.Id,
            };
            InsertAndCheck (message3);
            Brain.TestIndexEmailMessage (message3);
            Brain.TestCloseAllOpenedIndexes ();

            message3 = McEmailMessage.QueryById<McEmailMessage> (message3.Id);
            Assert.AreEqual (EmailMessageIndexDocument.Version, message3.IsIndexed);
            matches = index.SearchAllEmailMessageFields ("plaintext");
            CheckOneEmailMessage (message3.Id, matches);

            // Index an email message that has a downloaded HTML body
            var body4 = new McBody () {
                AccountId = TestIndexEmailMessageAccountId,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.Complete,
                BodyType = McAbstrFileDesc.BodyTypeEnum.HTML_2,
            };
            InsertAndCheck (body4);
            body4.UpdateData ("<html><p><ul><li>This is a HTML email</li></ul></p></html>");
            var message4 = new McEmailMessage () {
                AccountId = TestIndexEmailMessageAccountId,
                From = "ellen@company.net",
                To = "fred@company.net",
                BodyId = body4.Id,
            };
            InsertAndCheck (message4);
            Brain.TestIndexEmailMessage (message4);
            Brain.TestCloseAllOpenedIndexes ();

            message4 = McEmailMessage.QueryById<McEmailMessage> (message4.Id);
            Assert.AreEqual (EmailMessageIndexDocument.Version, message4.IsIndexed);
            matches = index.SearchAllEmailMessageFields ("html");
            CheckOneEmailMessage (message4.Id, matches);

            // Index an email message that has a downloaded MIME body
            var body5 = new McBody () {
                AccountId = TestIndexEmailMessageAccountId,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.Complete,
                BodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4,
            };
            InsertAndCheck (body5);
            body5.UpdateData (@"Content-Type: text/plain; charset=""us-ascii""
MIME-Version: 1.0
Content-Transfer-Encoding: 7bit
From: ellen@company.net
To: fred@company.net

This is a MIME email");
            var message5 = new McEmailMessage () {
                AccountId = TestIndexEmailMessageAccountId,
                From = "ellen@company.net",
                To = "fred@company.net",
                BodyId = body5.Id,
            };
            InsertAndCheck (message5);
            Brain.TestIndexEmailMessage (message5);
            Brain.TestCloseAllOpenedIndexes ();

            message5 = McEmailMessage.QueryById<McEmailMessage> (message5.Id);
            Assert.AreEqual (EmailMessageIndexDocument.Version, message5.IsIndexed);
            matches = index.SearchAllEmailMessageFields ("mime");
            CheckOneEmailMessage (message5.Id, matches);
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

            Assert.AreEqual (ContactIndexDocument.Version - 1, contact1.IndexVersion);
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

        [Test]
        public void TestQuickScore ()
        {
            var stateMachineEvent = new NcBrainStateMachineEvent (Message.AccountId);
            Assert.AreEqual (0, Message.ScoreVersion);
            Assert.AreEqual (0, Message.Score);

            NcBrain.SharedInstance.Enqueue (stateMachineEvent);
            WaitForBrain ();

            var updatedMessage = McEmailMessage.QueryById<McEmailMessage> (Message.Id);
            Assert.AreEqual (0, updatedMessage.ScoreVersion);
            Assert.AreEqual (McEmailMessage.minHotScore, updatedMessage.Score);
        }
    }
}

