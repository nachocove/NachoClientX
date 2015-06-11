//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Index;
using NachoCore;
using Test.iOS;

namespace Test.Common
{
    [TestFixture]
    public class McEmailMessageTest : NcTestBase
    {
        McFolder Folder;

        DateTime CurrentReceivedDate = new DateTime (2015, 2, 20, 17, 30, 00);

        // Lazy. Going to fudge an account (instead of creating one)
        private const int defaultAccountId = 1;

        [SetUp]
        public new void SetUp ()
        {
            base.SetUp ();
            Folder = McFolder.Create (defaultAccountId, false, false, false, "0", "test", "Test Folder",
                NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1);
            Folder.Insert ();
            NcAssert.True (0 < Folder.Id);
        }

        private McEmailAddress SetupAddress (string canonicalAddress, int received, int read, bool isVip)
        {
            McEmailAddress address = new McEmailAddress ();
            address.CanonicalEmailAddress = canonicalAddress;
            address.AccountId = defaultAccountId;
            address.EmailsReceived = received;
            address.EmailsRead = read;
            address.ScoreVersion = 3;
            address.Score = (double)read / (double)received;
            address.IsVip = isVip;

            address.Insert ();
            NcAssert.True (0 < address.Id);

            return address;
        }

        private McEmailMessage SetupMessage (McEmailAddress from, DateTime dateReceived, string conversationId = null)
        {
            McEmailMessage message = new McEmailMessage ();
            message.AccountId = defaultAccountId;
            message.From = from.CanonicalEmailAddress;
            message.Score = from.Score;
            message.ScoreVersion = 3;
            message.IsAwaitingDelete = false;
            message.FlagUtcStartDate = new DateTime ();
            message.DateReceived = dateReceived;
            message.ConversationId = conversationId ?? System.Guid.NewGuid ().ToString ();


            message.Insert ();
            NcAssert.True (0 < message.Id);

            return message;
        }

        public void SetupDependency (McEmailMessage message, McEmailAddress address)
        {
            McEmailMessageDependency dep = new McEmailMessageDependency (message.AccountId);
            dep.EmailAddressId = address.Id;
            dep.EmailMessageId = message.Id;
            dep.EmailAddressType = (int)McEmailMessageDependency.AddressType.SENDER;

            dep.Insert ();
            NcAssert.True (0 < dep.Id);
        }

        public void CheckMessages (McEmailMessage[] messages, List<McEmailMessageThread> got, params int[] expectedIndices)
        {
            Assert.AreEqual (expectedIndices.Length, got.Count);
            for (int n = 0; n < expectedIndices.Length; n++) {
                Assert.AreEqual (messages [expectedIndices [n]].Id, got [n].FirstMessageId);
            }
        }

        [Test]
        public void QueryUnreadAndHotAfter ()
        {
            var since = new DateTime (2014, 8, 15, 1, 23, 0);
            var retardedSince = since.AddDays (-1.0);

            // excluded because of IsRead == true.
            var ex1 = new McEmailMessage ();
            ex1.AccountId = 1;
            ex1.IsRead = true;
            ex1.DateReceived = retardedSince.AddMinutes (1.0);
            ex1.ShouldNotify = true;
            ex1.Insert ();
            ex1.CreatedAt = since.AddMinutes (1.0);
            ex1.HasBeenNotified = false;
            ex1.Update ();
            // excluded because of CreatedAt too long ago.
            var ex2 = new McEmailMessage ();
            ex2.AccountId = 1;
            ex2.IsRead = false;
            ex2.DateReceived = retardedSince.AddMinutes (1.0);
            ex2.ShouldNotify = true;
            ex2.ConversationId = "ex2";
            ex2.Insert ();
            ex2.CreatedAt = since.AddYears (-1);
            ex2.HasBeenNotified = false;
            ex2.Update ();
            // excluded because of DateReceived too long ago.
            var ex3 = new McEmailMessage ();//!!!
            ex3.AccountId = 1;
            ex3.IsRead = false;
            ex3.DateReceived = retardedSince.AddYears (-1);
            ex3.ShouldNotify = true;
            ex3.ConversationId = "ex3";
            ex3.Insert ();
            ex3.CreatedAt = since.AddMinutes (1.0);
            ex3.HasBeenNotified = false;
            ex3.Update ();
            // excluded because of HasBeenNotified == true && ShouldNotify == false.
            var ex4 = new McEmailMessage ();
            ex4.AccountId = 1;
            ex4.IsRead = false;
            ex4.DateReceived = retardedSince.AddMinutes (1.0);
            ex4.ShouldNotify = false;
            ex4.ConversationId = "ex4";
            ex4.Insert ();
            ex4.CreatedAt = since.AddMinutes (1.0);
            ex4.HasBeenNotified = true;
            ex4.Update ();

            // included with early DateReceived. HasBeenNotified == false && ShouldNotify == false.
            var early = new McEmailMessage ();
            early.AccountId = 1;
            early.IsRead = false;
            early.DateReceived = retardedSince.AddMinutes (1.0);
            early.ShouldNotify = false;
            early.ConversationId = "early";
            early.Insert ();
            early.CreatedAt = since.AddMinutes (1.0);
            early.HasBeenNotified = false;
            early.Update ();

            // included with late DateRecieved. HasBeenNotified == true && ShouldNotify == true.
            var late = new McEmailMessage ();
            late.AccountId = 1;
            late.IsRead = false;
            late.DateReceived = retardedSince.AddMinutes (2.0);
            late.ShouldNotify = true;
            late.ConversationId = "late";
            late.Insert ();
            late.CreatedAt = since.AddMinutes (1.0);
            late.HasBeenNotified = true;
            late.Update ();

            var unha = McEmailMessage.QueryUnreadAndHotAfter (since);
            Assert.AreEqual (2, unha.Count);
            Assert.AreEqual (early.ConversationId, unha.First ().ConversationId);
            Assert.AreEqual (late.ConversationId, unha.Last ().ConversationId);
        }

        [Test]
        public void QueryActiveMessageItemsByScore ()
        {
            // Create 8 email messages in the folder. 2 emails have scores over 0.5.
            // 1 email is from VIP. 1 email is from VIP and has score over 0.5. 2
            // emails have scores below 0.5. 1 email is delete pending. 1 email
            // has a start date in the future.
            McEmailAddress[] addresses = new McEmailAddress[10];
            addresses [0] = SetupAddress ("aaron@company.net", 4, 2, false);
            addresses [1] = SetupAddress ("bob@company.net", 5, 1, false);
            addresses [2] = SetupAddress ("charles@compnay.net", 3, 3, false);
            addresses [3] = SetupAddress ("david@company.net", 2, 0, false);
            addresses [4] = SetupAddress ("ellen@company.net", 5, 3, true);
            addresses [5] = SetupAddress ("fred@company.net", 3, 1, true);
            addresses [6] = SetupAddress ("gary@company.net", 6, 6, false);
            addresses [7] = SetupAddress ("harry@company.net", 7, 7, false);
            addresses [8] = SetupAddress ("ivan@company.net", 3, 1, false);
            addresses [9] = SetupAddress ("jolene@company.net", 5, 5, false);

            McEmailMessage[] messages = new McEmailMessage[10];
            messages [0] = SetupMessage (addresses [0], new DateTime (2014, 8, 15, 1, 23, 0));
            messages [1] = SetupMessage (addresses [1], new DateTime (2014, 8, 15, 2, 0, 0));
            // Intentional set this one received date earlier to test "SORT BY DateReceived"
            messages [2] = SetupMessage (addresses [2], new DateTime (2014, 8, 15, 1, 15, 0));
            messages [3] = SetupMessage (addresses [3], new DateTime (2014, 8, 15, 3, 0, 0));
            messages [4] = SetupMessage (addresses [4], new DateTime (2014, 8, 15, 4, 0, 0));
            messages [5] = SetupMessage (addresses [5], new DateTime (2014, 8, 15, 5, 0, 0));
            messages [6] = SetupMessage (addresses [6], new DateTime (2014, 8, 15, 6, 0, 0));
            messages [6].IsAwaitingDelete = true;
            messages [6].Update ();
            messages [7] = SetupMessage (addresses [7], new DateTime (2014, 8, 15, 7, 0, 0));
            messages [7].FlagUtcStartDate = DateTime.MaxValue;
            messages [7].Update ();
            messages [8] = SetupMessage (addresses [8], new DateTime (2014, 8, 15, 8, 0, 0));
            messages [8].UserAction = 1;
            messages [8].Update ();
            messages [9] = SetupMessage (addresses [9], new DateTime (2014, 8, 15, 9, 0, 0));
            messages [9].UserAction = -1;
            messages [9].Update ();

            for (int n = 0; n < 10; n++) {
                Folder.Link (messages [n]);
                SetupDependency (messages [n], addresses [n]);
            }

            List<McEmailMessageThread> messageList =
                McEmailMessage.QueryActiveMessageItemsByScore (defaultAccountId, Folder.Id, 0.6);
            CheckMessages (messages, messageList, 8, 5, 4, 2);

            messageList = McEmailMessage.QueryActiveMessageItemsByScore (defaultAccountId, Folder.Id, 0.4);
            CheckMessages (messages, messageList, 8, 5, 4, 0, 2);
        }

        [Test]
        public void TestQueryByBodyIdIncAwaitDel ()
        {
            var outAccount = new McEmailMessage () {
                AccountId = 2,
                BodyId = 55,
            };
            NcAssert.Equals (1, outAccount.Insert ());
            var outBody = new McEmailMessage () {
                AccountId = 1,
                BodyId = 77,
            };
            NcAssert.Equals (1, outBody.Insert ());
            var in1 = new McEmailMessage () {
                AccountId = 1,
                BodyId = 55,
            };
            NcAssert.Equals (1, in1.Insert ());
            var in2 = new McEmailMessage () {
                AccountId = 1,
                BodyId = 55,
            };
            NcAssert.Equals (1, in2.Insert ());
            var result = McEmailMessage.QueryByBodyIdIncAwaitDel<McEmailMessage> (1, 55);
            NcAssert.Equals (2, result.Count ());
            NcAssert.True (result.All (x => x.AccountId == 1 && x.BodyId == 55));
        }

        [Test]
        public void TestCountOfUnreadMessageItems ()
        {
            McEmailMessage message = new McEmailMessage () {
                AccountId = 1,
                IsRead = false,
            };
            message.Insert ();
            Folder.Link (message);

            McEmailMessage message1 = new McEmailMessage () {
                AccountId = 1,
                IsRead = false,
            };
            message1.Insert ();
            Folder.Link (message1);

            McEmailMessage message2 = new McEmailMessage () {
                AccountId = 1,
                IsRead = true,
            };
            message2.Insert ();
            Folder.Link (message2);

            McEmailMessage message3 = new McEmailMessage () {
                AccountId = 1,
                IsRead = false,
            };
            message3.Insert ();
            Folder.Link (message3);

            var count = McEmailMessage.CountOfUnreadMessageItems (1, Folder.Id);
            Assert.AreEqual (3, count);
        }

        [Test]
        public void TestQueryDueDateMessageItems ()
        {
            McEmailMessage message = new McEmailMessage () {
                AccountId = 1,
                FlagType = "Defer until",
                FlagStatus = 1,
            };
            message.Insert ();

            McEmailMessage message1 = new McEmailMessage () {
                AccountId = 2,
                FlagType = "Defer until",
                FlagStatus = 1,
            };
            message1.Insert ();

            McEmailMessage message2 = new McEmailMessage () {
                AccountId = 1,
                FlagType = "For follow up by",
                FlagStatus = 2,
            };
            message2.Insert ();

            McEmailMessage message3 = new McEmailMessage () {
                AccountId = 1,
                FlagType = "For follow up by",
                FlagStatus = 1,
            };
            message3.Insert ();

            McEmailMessage message4 = new McEmailMessage () {
                AccountId = 1,
                FlagType = "For follow up by",
                FlagStatus = 0,
            };
            message4.Insert ();

            McEmailMessage message5 = new McEmailMessage () {
                AccountId = 1,
                FlagType = "For follow up by",
                FlagStatus = 1,
                IsAwaitingDelete = true,
            };
            message5.Insert ();

            var deadlines = McEmailMessage.QueryDueDateMessageItems (1);
            foreach (var deadline in deadlines) {
                Assert.AreNotEqual (0, McEmailMessage.QueryById<McEmailMessage> (deadline.FirstMessageId).FlagStatus);
                Assert.AreNotEqual ("Defer until", McEmailMessage.QueryById<McEmailMessage> (deadline.FirstMessageId).FlagType);
                NcAssert.True (!McEmailMessage.QueryById<McEmailMessage> (deadline.FirstMessageId).IsAwaitingDelete);
            }
            Assert.AreEqual (2, deadlines.Count);
        }

        [Test]
        public void TestQueryDeferredMessageItems ()
        {
            McEmailMessage message = new McEmailMessage () {
                AccountId = 1,
                FlagType = "Defer until",
                FlagStatus = 1,
                FlagUtcStartDate = DateTime.UtcNow.AddHours (1),

            };
            message.Insert ();

            McEmailMessage message1 = new McEmailMessage () {
                AccountId = 2,
                FlagType = "Defer until",
                FlagStatus = 1,
                FlagUtcStartDate = DateTime.UtcNow.AddHours (1),
            };
            message1.Insert ();

            McEmailMessage message2 = new McEmailMessage () {
                AccountId = 1,
                FlagType = "Defer until",
                FlagStatus = 1,
                FlagUtcStartDate = DateTime.UtcNow.AddHours (-1),
            };
            message2.Insert ();

            McEmailMessage message3 = new McEmailMessage () {
                AccountId = 1,
                FlagType = "Defer until",
                FlagStatus = 0,
                FlagUtcStartDate = DateTime.UtcNow.AddHours (1),
            };
            message3.Insert ();

            McEmailMessage message4 = new McEmailMessage () {
                AccountId = 2,
                FlagStatus = 1,
                FlagUtcStartDate = DateTime.UtcNow.AddHours (1),
                IsAwaitingDelete = true,
            };
            message4.Insert ();

            McEmailMessage message5 = new McEmailMessage () {
                AccountId = 3,
                FlagStatus = 2,
                FlagUtcStartDate = DateTime.UtcNow.AddHours (100),
            };
            message5.Insert ();

            var deferred = McEmailMessage.QueryDeferredMessageItems (1);
            foreach (var d in deferred) {
                Assert.AreNotEqual (0, McEmailMessage.QueryById<McEmailMessage> (d.FirstMessageId).FlagStatus);
                NcAssert.True (McEmailMessage.QueryById<McEmailMessage> (d.FirstMessageId).FlagUtcStartDate > DateTime.UtcNow);
                NcAssert.True (!McEmailMessage.QueryById<McEmailMessage> (d.FirstMessageId).IsAwaitingDelete);
            }
            Assert.AreEqual (1, deferred.Count);

            deferred = McEmailMessage.QueryDeferredMessageItems (2);
            foreach (var d in deferred) {
                Assert.AreNotEqual (0, McEmailMessage.QueryById<McEmailMessage> (d.FirstMessageId).FlagStatus);
                NcAssert.True (McEmailMessage.QueryById<McEmailMessage> (d.FirstMessageId).FlagUtcStartDate > DateTime.UtcNow);
                NcAssert.True (!McEmailMessage.QueryById<McEmailMessage> (d.FirstMessageId).IsAwaitingDelete);
            }
            Assert.AreEqual (1, deferred.Count);

            deferred = McEmailMessage.QueryDeferredMessageItems (3);
            foreach (var d in deferred) {
                Assert.AreNotEqual (0, McEmailMessage.QueryById<McEmailMessage> (d.FirstMessageId).FlagStatus);
                NcAssert.True (McEmailMessage.QueryById<McEmailMessage> (d.FirstMessageId).FlagUtcStartDate > DateTime.UtcNow);
                NcAssert.True (!McEmailMessage.QueryById<McEmailMessage> (d.FirstMessageId).IsAwaitingDelete);
            }
            Assert.AreEqual (1, deferred.Count);
        }

        private void CheckScoreAndUpdate (int id, double expectedScore, int expectedNeedUpdate)
        {
            McEmailMessage message = McEmailMessage.QueryById<McEmailMessage> (id);
            Assert.True (null != message);

            Assert.AreEqual (expectedScore, message.Score);
            Assert.AreEqual (expectedNeedUpdate, message.NeedUpdate);
        }

        [Test]
        public void TestUpdateScoreAndNeedUpdate ()
        {
            McEmailMessage message = new McEmailMessage () {
                AccountId = 1,
            };
            message.Insert ();
            NcAssert.True (0 < message.Id);

            Assert.AreEqual (0.0, message.Score);
            Assert.AreEqual (0, message.NeedUpdate);

            message.Score = 1.0;
            message.UpdateScoreAndNeedUpdate ();
            CheckScoreAndUpdate (message.Id, 1.0, 0);

            message.NeedUpdate = 1;
            message.UpdateScoreAndNeedUpdate ();
            CheckScoreAndUpdate (message.Id, 1.0, 1);

            message.Score = 0.5;
            message.NeedUpdate = 0;
            message.UpdateScoreAndNeedUpdate ();
            CheckScoreAndUpdate (message.Id, 0.5, 0);
        }

        protected void InsertAndCheck (McAbstrObjectPerAcc item)
        {
            Assert.True (0 == item.Id);
            var rows = item.Insert ();
            Assert.True (1 == rows);
            Assert.True (0 < item.Id);
        }

        protected void CheckMessage (McEmailMessage expected, McEmailMessage got)
        {
            Assert.AreEqual (expected.Id, got.Id);
            Assert.AreEqual (expected.AccountId, got.AccountId);
            Assert.AreEqual (expected.BodyId, got.BodyId);
            Assert.AreEqual (expected.IsIndexed, got.IsIndexed);
        }

        protected McBody BodyIndexing (McAbstrFileDesc.FilePresenceEnum filePresence)
        {
            return new McBody () {
                AccountId = 1,
                BodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4,
                FilePresence = filePresence,
            };
        }

        protected McEmailMessage EmailMessageIndexing (int bodyId, int indexVersion)
        {
            CurrentReceivedDate.AddMinutes (5);
            return new McEmailMessage () {
                AccountId = 1,
                DateReceived = CurrentReceivedDate,
                BodyId = bodyId,
                IsIndexed = indexVersion
            };
        }

        [Test]
        public void TestQueryNeedsIndexing ()
        {
            // Create all combinations of states. Here the dimensions:
            // a. Has body or not
            // b. If has body, is completely downloaded or not.
            // c. Not indexed (< Version-1), partially (== Version-1), or fully indexed (== Version)

            // Create 8 email messages:
            // 1. Body, complete, not indexed
            // 2. Body, complete, partially indexed
            // 3. Body, complete, fully indexed -> NOT MATCHED
            // 4. Body, incomplete, not indexed
            // 5. Body, incomplete, partially indexed -> NOT MATCHED
            // 6. No body, --, not indexed
            // 7. No body, --, partially indexed
            // 8, No body, --, fully indexed -> NOT MATCHED
            var body1 = BodyIndexing (McAbstrFileDesc.FilePresenceEnum.Complete);
            InsertAndCheck (body1);
            var message1 = EmailMessageIndexing (body1.Id, 0);
            InsertAndCheck (message1);

            var body2 = BodyIndexing (McAbstrFileDesc.FilePresenceEnum.Complete);
            InsertAndCheck (body2);
            var message2 = EmailMessageIndexing (body2.Id, EmailMessageIndexDocument.Version - 1);
            InsertAndCheck (message2);

            var body3 = BodyIndexing (McAbstrFileDesc.FilePresenceEnum.Complete);
            InsertAndCheck (body3);
            var message3 = EmailMessageIndexing (body3.Id, EmailMessageIndexDocument.Version);
            InsertAndCheck (message3);

            var body4 = BodyIndexing (McAbstrFileDesc.FilePresenceEnum.None);
            InsertAndCheck (body4);
            var message4 = EmailMessageIndexing (body4.Id, 0);
            InsertAndCheck (message4);

            var body5 = BodyIndexing (McAbstrFileDesc.FilePresenceEnum.Partial);
            InsertAndCheck (body5);
            var message5 = EmailMessageIndexing (body5.Id, EmailMessageIndexDocument.Version - 1);
            InsertAndCheck (message5);

            var message6 = EmailMessageIndexing (0, 0);
            InsertAndCheck (message6);

            var message7 = EmailMessageIndexing (0, EmailMessageIndexDocument.Version - 1);
            InsertAndCheck (message7);

            var message8 = EmailMessageIndexing (0, EmailMessageIndexDocument.Version);
            InsertAndCheck (message8);

            // Query up to 10 emails. Should return messages 7, 6, 4, 2, 1
            var results1 = McEmailMessage.QueryNeedsIndexing (10);
            Assert.AreEqual (5, results1.Count);
            CheckMessage (message7, results1 [0]);
            CheckMessage (message6, results1 [1]);
            CheckMessage (message4, results1 [2]);
            CheckMessage (message2, results1 [3]);
            CheckMessage (message1, results1 [4]);

            // Query up to 1 email. Should only return message3
            var results2 = McEmailMessage.QueryNeedsIndexing (1);
            Assert.AreEqual (1, results2.Count);
            CheckMessage (message7, results2 [0]);
        }

        [Test]
        public void TestQueryByDateReceivedAndFrom ()
        {
            var accountId = 9;
            var magicTime1 = DateTime.UtcNow.AddDays (-5);
            var magicTime2 = magicTime1.AddDays (1);
            var magicFrom1 = "foo@bar.com";
            var winner1 = "winner@foo.com";
            var winner2 = "winner@bar.com";
            var loser = "bad@foo.com";
            // will be found.
            var message1 = new McEmailMessage () {
                AccountId = accountId,
                DateReceived = magicTime1,
                From = magicFrom1,
                To = winner1,
                BodyId = 0,
                IsIndexed = 0
            };
            InsertAndCheck (message1);
            // will be found.
            var message2 = new McEmailMessage () {
                AccountId = accountId,
                DateReceived = magicTime2,
                From = magicFrom1,
                To = winner2,
                BodyId = 0,
                IsIndexed = 0
            };
            InsertAndCheck (message2);
            // will not be found
            var message3 = new McEmailMessage () {
                AccountId = accountId,
                DateReceived = magicTime2,
                From = magicFrom1,
                To = loser,
                BodyId = 0,
                IsIndexed = 0,
                IsAwaitingDelete = true,
            };
            InsertAndCheck (message3);
            // will not be found
            var message4 = new McEmailMessage () {
                AccountId = accountId + 1,
                DateReceived = magicTime2,
                From = magicFrom1,
                To = loser,
                BodyId = 0,
                IsIndexed = 0,
            };
            InsertAndCheck (message4);
            // will not be found
            var message5 = new McEmailMessage () {
                AccountId = accountId,
                DateReceived = magicTime2.AddDays (-100),
                From = magicFrom1,
                To = loser,
                BodyId = 0,
                IsIndexed = 0,
            };
            InsertAndCheck (message5);
            // will not be found
            var message6 = new McEmailMessage () {
                AccountId = accountId,
                DateReceived = magicTime2,
                From = magicFrom1 + "m",
                To = loser,
                BodyId = 0,
                IsIndexed = 0,
            };
            InsertAndCheck (message6);

            var result = McEmailMessage.QueryByDateReceivedAndFrom (accountId, magicTime1, magicFrom1);
            Assert.IsNotNull (result);
            Assert.AreEqual (1, result.Count);
            var index = result.First ();
            var chosen = McEmailMessage.QueryById<McEmailMessage> (index.Id);
            Assert.IsNotNull (chosen);
            Assert.AreEqual (winner1, chosen.To);
            Assert.AreEqual (magicFrom1, chosen.From);
            Assert.AreEqual (magicTime1, chosen.DateReceived);

            result = McEmailMessage.QueryByDateReceivedAndFrom (accountId, magicTime2, magicFrom1);
            Assert.IsNotNull (result);
            Assert.AreEqual (1, result.Count);
            index = result.First ();
            chosen = McEmailMessage.QueryById<McEmailMessage> (index.Id);
            Assert.IsNotNull (chosen);
            Assert.AreEqual (winner2, chosen.To);
            Assert.AreEqual (magicFrom1, chosen.From);
            Assert.AreEqual (magicTime2, chosen.DateReceived);
        }

        [Test]
        public void QueryActiveMessageItems ()
        {
            // Create 8 email messages in the folder. 2 emails have scores over 0.5.
            // 1 email is from VIP. 1 email is from VIP and has score over 0.5. 2
            // emails have scores below 0.5. 1 email is delete pending. 1 email
            // has a start date in the future.
            McEmailAddress[] addresses = new McEmailAddress[10];
            addresses [0] = SetupAddress ("aaron@company.net", 4, 2, false);
            addresses [1] = SetupAddress ("bob@company.net", 5, 1, false);
            addresses [2] = SetupAddress ("charles@compnay.net", 3, 3, false);
            addresses [3] = SetupAddress ("david@company.net", 2, 0, false);
            addresses [4] = SetupAddress ("ellen@company.net", 5, 3, true);
            addresses [5] = SetupAddress ("fred@company.net", 3, 1, true);
            addresses [6] = SetupAddress ("gary@company.net", 6, 6, false);
            addresses [7] = SetupAddress ("harry@company.net", 7, 7, false);
            addresses [8] = SetupAddress ("ivan@company.net", 3, 1, false);
            addresses [9] = SetupAddress ("jolene@company.net", 5, 5, false);

            McEmailMessage[] messages = new McEmailMessage[10];
            messages [0] = SetupMessage (addresses [0], new DateTime (2014, 8, 15, 1, 23, 0), conversationId: "foo");
            messages [1] = SetupMessage (addresses [1], new DateTime (2014, 8, 15, 2, 0, 0));
            // Intentional set this one received date earlier to test "SORT BY DateReceived"
            messages [2] = SetupMessage (addresses [2], new DateTime (2014, 8, 15, 1, 15, 0), conversationId: "foo");
            messages [3] = SetupMessage (addresses [3], new DateTime (2014, 8, 15, 3, 0, 0));
            messages [4] = SetupMessage (addresses [4], new DateTime (2014, 8, 15, 4, 0, 0));
            messages [5] = SetupMessage (addresses [5], new DateTime (2014, 8, 15, 5, 0, 0), conversationId: "foo");
            messages [6] = SetupMessage (addresses [6], new DateTime (2014, 8, 15, 6, 0, 0));
            messages [6].IsAwaitingDelete = true;
            messages [6].Update ();
            messages [7] = SetupMessage (addresses [7], new DateTime (2014, 8, 15, 7, 0, 0));
            messages [7].FlagUtcStartDate = DateTime.MaxValue;
            messages [7].Update ();
            messages [8] = SetupMessage (addresses [8], new DateTime (2014, 8, 15, 8, 0, 0));
            messages [8].UserAction = 1;
            messages [8].Update ();
            messages [9] = SetupMessage (addresses [9], new DateTime (2014, 8, 15, 9, 0, 0));
            messages [9].UserAction = -1;
            messages [9].Update ();

            for (int n = 0; n < 10; n++) {
                Folder.Link (messages [n]);
                SetupDependency (messages [n], addresses [n]);
            }

            List<McEmailMessageThread> messageList = McEmailMessage.QueryActiveMessageItems (defaultAccountId, Folder.Id);
            CheckMessages (messages, messageList, 9, 8, 5, 4, 3, 1);

            messageList = McEmailMessage.QueryActiveMessageItems (defaultAccountId, Folder.Id, groupBy: true);
            CheckMessages (messages, messageList, 9, 8, 5, 4, 3, 1);

            messageList = McEmailMessage.QueryActiveMessageItems (defaultAccountId, Folder.Id, groupBy: false);
            CheckMessages (messages, messageList, 9, 8, 5, 4, 3, 1, 0, 2);
        }

        [Test]
        public void QueryForSetTest ()
        {
            var message1 = new McEmailMessage () {
                AccountId = 1,
                DateReceived = new DateTime (2015, 2, 20, 17, 30, 00),
                BodyId = 0,
                IsIndexed = 0
            };
            InsertAndCheck (message1);

            var message2 = new McEmailMessage () {
                AccountId = 1,
                DateReceived = new DateTime (2015, 2, 20, 17, 35, 00),
                BodyId = 0,
                IsIndexed = 0
            };
            InsertAndCheck (message2);

            var message3 = new McEmailMessage () {
                AccountId = 1,
                DateReceived = new DateTime (2015, 2, 20, 17, 40, 00),
                BodyId = 0,
                IsIndexed = 0
            };
            InsertAndCheck (message3);

            var none = new List<int> ();
            CheckSetQuery (none, new List<McEmailMessage> ());

            var some = new List<int> () { 1, 2 };
            CheckSetQuery (some, new List<McEmailMessage> () { message1, message2 });

            var gone = new List<int> () { 1, 2, 3, 4 };
            CheckSetQuery (gone, new List<McEmailMessage> () { message1, message2, message3, null });

            var nulz = new List<int> () { 4, 5, 6 };
            CheckSetQuery (nulz, new List<McEmailMessage> () { null, null, null });

            var full = new List<int> () { 1, 2, 3 };
            CheckSetQuery (full, new List<McEmailMessage> () { message1, message2, message3 });

            var spin = new List<int> () { 3, 1, 2 };
            CheckSetQuery (spin, new List<McEmailMessage> () { message3, message1, message2 });

        }

        void CheckSetQuery (List<int> request, List<McEmailMessage> expected)
        {
            var resultList = McEmailMessage.QueryForSet (request);
            var cleanList = new List<McEmailMessage> ();

            foreach (var i in request) {
                var result = resultList.Find (x => x.Id == i);
                cleanList.Add (result);
            }

            Assert.AreEqual (expected.Count (), cleanList.Count ());

            for (var i = 0; i < expected.Count (); i++) {
                if (null == expected [i]) {
                    Assert.AreEqual (expected [i], cleanList [i]);
                } else {
                    Assert.AreEqual (expected [i].Id, cleanList [i].Id);
                }
            }
        }

        [Test]
        public void TestQueryNeedGleaning ()
        {
            // Set up a junk, a spam and an inbox folders.
            var junkFolder = FolderOps.CreateFolder (accountId: defaultAccountId, name: "Junk");
            Assert.True (0 != junkFolder.Id);
            var spamFolder = FolderOps.CreateFolder (accountId: defaultAccountId, name: "spam");
            Assert.True (0 != spamFolder.Id);
            var inboxFolder = FolderOps.CreateFolder (accountId: defaultAccountId, name: "Inbox");
            Assert.True (0 != inboxFolder.Id);

            // Set up emails in each to be gleaned
            var junkEmail = new McEmailMessage () {
                AccountId = defaultAccountId,
                From = "junk@company.net",
                Subject = "Junk email",
            };
            junkEmail.Insert ();
            Assert.True (0 != junkEmail.Id);
            junkFolder.Link (junkEmail);

            var spamEmail = new McEmailMessage () {
                AccountId = defaultAccountId,
                From = "spam@company.net",
                Subject = "Spam email",
            };
            spamEmail.Insert ();
            Assert.True (0 != spamEmail.Id);
            spamFolder.Link (spamEmail);

            var email1 = new McEmailMessage () {
                AccountId = defaultAccountId,
                From = "bob@company.net",
                Subject = "Hello",
            };
            email1.Insert ();
            Assert.True (0 != email1.Id);
            inboxFolder.Link (email1);
              
            var email2 = new McEmailMessage () {
                AccountId = defaultAccountId,
                From = "john@company.net",
                Subject = "Hello again",
            };
            email2.Insert ();
            Assert.True (0 != email2.Id);
            inboxFolder.Link (email2);

            // Query for up to 1 email that need gleaning. Should return email1 or email2
            var emailMessageList1 = McEmailMessage.QueryNeedGleaning (defaultAccountId, 1);
            Assert.AreEqual (1, emailMessageList1.Count);
            Assert.True ((email1.Id == emailMessageList1 [0].Id) || (email2.Id == emailMessageList1 [1].Id));

            var emailMessageList2 = McEmailMessage.QueryNeedGleaning (defaultAccountId, 10);
            Assert.AreEqual (2, emailMessageList2.Count);
            Assert.True (
                ((email1.Id == emailMessageList2 [0].Id) && (email2.Id == emailMessageList2 [1].Id)) ||
                ((email2.Id == emailMessageList2 [1].Id) && (email1.Id == emailMessageList2 [0].Id))
            );

            // Mark one of the email gleaned. The gleaning functions are unit tested in NcContactGleanerTest
            email1.MarkAsGleaned (McEmailMessage.GleanPhaseEnum.GLEAN_PHASE2);
            var emailMessageList3 = McEmailMessage.QueryNeedGleaning (defaultAccountId, 2);
            Assert.AreEqual (1, emailMessageList3.Count);
            Assert.AreEqual (email2.Id, emailMessageList3 [0].Id);

            // Query a different account id and all accounts
            var emailMessageList4 = McEmailMessage.QueryNeedGleaning (-1, 2);
            Assert.AreEqual (1, emailMessageList4.Count);
            Assert.AreEqual (email2.Id, emailMessageList4 [0].Id);

            var emailMessageList5 = McEmailMessage.QueryNeedGleaning (defaultAccountId + 1, 2);
            Assert.AreEqual (0, emailMessageList5.Count);

            // Mark the other email in inbox as phase1 gleaned.
            email2.MarkAsGleaned (McEmailMessage.GleanPhaseEnum.GLEAN_PHASE1);
            var emailMessageList6 = McEmailMessage.QueryNeedGleaning (defaultAccountId, 2);
            Assert.AreEqual (1, emailMessageList6.Count);
            Assert.AreEqual (email2.Id, emailMessageList6 [0].Id);
            email2.MarkAsGleaned (McEmailMessage.GleanPhaseEnum.GLEAN_PHASE2);
            var emailMessageList7 = McEmailMessage.QueryNeedGleaning (defaultAccountId, 2);
            Assert.AreEqual (0, emailMessageList7.Count);

            // Move the junk email back to inbox
            inboxFolder.Link (junkEmail);
            junkFolder.Unlink (junkEmail);
            var emailMessageList8 = McEmailMessage.QueryNeedGleaning (defaultAccountId, 2);
            Assert.AreEqual (1, emailMessageList8.Count);
            Assert.AreEqual (junkEmail.Id, emailMessageList8 [0].Id);

            junkEmail.Delete ();
            spamEmail.Delete ();
            email1.Delete ();
            email2.Delete ();

            junkFolder.Delete ();
            spamFolder.Delete ();
            inboxFolder.Delete ();
        }

        [Test]
        public void TestQueryNeedAnalysis ()
        {
            var messages = new McEmailMessage[4];

            messages [0] = new McEmailMessage () {
                Subject = "Do not need analysis",
                ScoreVersion = Scoring.Version,
                HasBeenGleaned = 1,
            };
            messages [1] = new McEmailMessage () {
                Subject = "Is analyzed for current version only",
                ScoreVersion = Scoring.Version - 1,
                HasBeenGleaned = 1,
            };
            messages [2] = new McEmailMessage () {
                Subject = "Is analyzed for all versions",
                ScoreVersion = 0,
                HasBeenGleaned = 1,
            };
            messages [3] = new McEmailMessage () {
                Subject = "Is not analyzed coz not gleaned",
                ScoreVersion = 0,
                HasBeenGleaned = 0,
            };

            foreach (var message in messages) {
                message.AccountId = 1;
                message.From = "bob@company.net";
                int rows = message.Insert ();
                Assert.AreEqual (1, rows);
                Assert.True (0 < message.Id);
            }

            // Query for 4 latest version. Should get two
            var results1 = McEmailMessage.QueryNeedAnalysis (4);
            Assert.AreEqual (2, results1.Count);
            Assert.AreEqual (messages [2].Id, results1 [0].Id);
            Assert.AreEqual (messages [1].Id, results1 [1].Id);

            // Query for 1 latest version. Should get 1
            var results2 = McEmailMessage.QueryNeedAnalysis (1);
            Assert.AreEqual (1, results2.Count);
            Assert.AreEqual (messages [2].Id, results2 [0].Id);

            // Query for 2 version 1. Should get 1
            var results3 = McEmailMessage.QueryNeedAnalysis (2, 1);
            Assert.AreEqual (1, results3.Count);
            Assert.AreEqual (messages [2].Id, results3 [0].Id);
        }

        [Test]
        public void TestQueryNeedUpdate ()
        {
            var messages = new McEmailMessage[6];

            messages [0] = new McEmailMessage () {
                Subject = "Do not need update",
                ScoreVersion = Scoring.Version,
                NeedUpdate = 0,
            };
            messages [1] = new McEmailMessage () {
                Subject = "Need update but is not updated",
                ScoreVersion = Scoring.Version - 1,
                NeedUpdate = 1,
            };
            messages [2] = new McEmailMessage () {
                Subject = "Is updated",
                ScoreVersion = Scoring.Version,
                NeedUpdate = 25,
            };
            messages [3] = new McEmailMessage () {
                Subject = "Is updated",
                ScoreVersion = Scoring.Version,
                NeedUpdate = 21,
            };
            messages [4] = new McEmailMessage () {
                Subject = "Is updated",
                ScoreVersion = Scoring.Version,
                NeedUpdate = 20,
            };
            messages [5] = new McEmailMessage () {
                Subject = "Is updated",
                ScoreVersion = Scoring.Version,
                NeedUpdate = 1,
            };

            foreach (var message in messages) {
                message.AccountId = 1;
                message.From = "bob@company.net";
                int rows = message.Insert ();
                Assert.AreEqual (1, rows);
                Assert.True (0 < message.Id);
            }

            // Query for 5 above. Should get 2
            var results1 = McEmailMessage.QueryNeedUpdate (5, above: true);
            Assert.AreEqual (2, results1.Count);
            Assert.AreEqual (messages [2].Id, results1 [0].Id);
            Assert.AreEqual (messages [3].Id, results1 [1].Id);

            // Query for 5 below. Should get 2
            var results2 = McEmailMessage.QueryNeedUpdate (5, above: false);
            Assert.AreEqual (2, results2.Count);
            Assert.AreEqual (messages [4].Id, results2 [0].Id);
            Assert.AreEqual (messages [5].Id, results2 [1].Id);

            // Query for 4 above 21. Should get 1
            var results3 = McEmailMessage.QueryNeedUpdate (4, above: true, threshold: 21);
            Assert.AreEqual (1, results3.Count);
            Assert.AreEqual (messages [2].Id, results3 [0].Id);

            // Query for 2 below 21. SHould get 2
            var results4 = McEmailMessage.QueryNeedUpdate (2, above: false, threshold: 21);
            Assert.AreEqual (2, results4.Count);
            Assert.AreEqual (messages [3].Id, results4 [0].Id);
            Assert.AreEqual (messages [4].Id, results4 [1].Id);
        }
    }
}

