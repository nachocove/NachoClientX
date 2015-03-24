//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace Test.Common
{
    [TestFixture]
    public class McEmailMessageTest : NcTestBase
    {
        McFolder Folder;

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

        private McEmailMessage SetupMessage (McEmailAddress from, DateTime dateReceived)
        {
            McEmailMessage message = new McEmailMessage ();
            message.AccountId = defaultAccountId;
            message.From = from.CanonicalEmailAddress;
            message.Score = from.Score;
            message.ScoreVersion = 3;
            message.IsAwaitingDelete = false;
            message.FlagUtcStartDate = new DateTime ();
            message.DateReceived = dateReceived;
            message.ConversationId = System.Guid.NewGuid ().ToString ();


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
        public void TestQueryDueDateMessageItemsAllAccounts ()
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

            var deadlines = McEmailMessage.QueryDueDateMessageItemsAllAccounts ();
            foreach (var deadline in deadlines) {
                Assert.AreNotEqual (0, McEmailMessage.QueryById<McEmailMessage> (deadline.FirstMessageId).FlagStatus);
                Assert.AreNotEqual ("Defer until", McEmailMessage.QueryById<McEmailMessage> (deadline.FirstMessageId).FlagType);
                NcAssert.True (!McEmailMessage.QueryById<McEmailMessage> (deadline.FirstMessageId).IsAwaitingDelete);
            }
            Assert.AreEqual (2, deadlines.Count);
        }

        [Test]
        public void TestQueryDeferredMessageItemsAllAccounts ()
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

            var deferred = McEmailMessage.QueryDeferredMessageItemsAllAccounts ();
            foreach (var d in deferred) {
                Assert.AreNotEqual (0, McEmailMessage.QueryById<McEmailMessage> (d.FirstMessageId).FlagStatus);
                NcAssert.True (McEmailMessage.QueryById<McEmailMessage> (d.FirstMessageId).FlagUtcStartDate > DateTime.UtcNow);
                NcAssert.True (!McEmailMessage.QueryById<McEmailMessage> (d.FirstMessageId).IsAwaitingDelete);
            }
            Assert.AreEqual (3, deferred.Count);
        }

        private void CheckScoreAndUpdate (int id, double expectedScore, bool expectedNeedUpdate)
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
            Assert.AreEqual (false, message.NeedUpdate);

            message.Score = 1.0;
            message.UpdateScoreAndNeedUpdate ();
            CheckScoreAndUpdate (message.Id, 1.0, false);

            message.NeedUpdate = true;
            message.UpdateScoreAndNeedUpdate ();
            CheckScoreAndUpdate (message.Id, 1.0, true);

            message.Score = 0.5;
            message.NeedUpdate = false;
            message.UpdateScoreAndNeedUpdate ();
            CheckScoreAndUpdate (message.Id, 0.5, false);
        }

        protected void InsertAndCheckBody (McBody body)
        {
            Assert.True (0 == body.Id);
            body.Insert ();
            Assert.True (0 < body.Id);
        }

        protected void InsertAndCheckMessage (McEmailMessage message)
        {
            Assert.True (0 == message.Id);
            message.Insert ();
            Assert.True (0 < message.Id);
        }

        protected void CheckMessage (McEmailMessage expected, McEmailMessage got)
        {
            Assert.AreEqual (expected.Id, got.Id);
            Assert.AreEqual (expected.AccountId, got.AccountId);
            Assert.AreEqual (expected.BodyId, got.BodyId);
            Assert.AreEqual (expected.IsIndexed, got.IsIndexed);
        }

        [Test]
        public void TestQueryNeedsIndexing ()
        {
            // Create 5 email messages:
            // 1. Has body, is complete, and not marked indexed
            // 2. Has no body
            // 3. Has body and is complete, and not marked indexed
            // 4. Has body but is incomplete
            // 5. Has body and is complete, and marked indexed
            var body1 = new McBody () {
                AccountId = 1,
                BodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.Complete
            };
            InsertAndCheckBody (body1);

            var body3 = new McBody () {
                AccountId = 1,
                BodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.Complete
            };
            InsertAndCheckBody (body3);

            var body4 = new McBody () {
                AccountId = 1,
                BodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.Partial
            };
            InsertAndCheckBody (body4);

            var body5 = new McBody () {
                AccountId = 1,
                BodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.Complete
            };
            InsertAndCheckBody (body5);

            var message1 = new McEmailMessage () {
                AccountId = 1,
                DateReceived = new DateTime (2015, 2, 20, 17, 30, 00),
                BodyId = body1.Id,
                IsIndexed = false
            };
            InsertAndCheckMessage (message1);

            var message2 = new McEmailMessage () {
                AccountId = 1,
                DateReceived = new DateTime (2015, 2, 20, 17, 35, 00),
                BodyId = 0,
                IsIndexed = false
            };
            InsertAndCheckMessage (message2);

            var message3 = new McEmailMessage () {
                AccountId = 1,
                DateReceived = new DateTime (2015, 2, 20, 17, 40, 00),
                BodyId = body3.Id,
                IsIndexed = false
            };
            InsertAndCheckMessage (message3);

            var message4 = new McEmailMessage () {
                AccountId = 1,
                DateReceived = new DateTime (2015, 2, 20, 17, 45, 00),
                BodyId = body4.Id,
                IsIndexed = false
            };
            InsertAndCheckMessage (message4);

            var message5 = new McEmailMessage () {
                AccountId = 1,
                DateReceived = new DateTime (2015, 2, 20, 17, 50, 00),
                BodyId = body5.Id,
                IsIndexed = true
            };
            InsertAndCheckMessage (message5);

            // Query up to 10 emails. Should return message3 followed by message1
            var results1 = McEmailMessage.QueryNeedsIndexing (10);
            Assert.AreEqual (2, results1.Count);
            CheckMessage (message3, results1 [0]);
            CheckMessage (message1, results1 [1]);

            // Query up to 1 email. Should only return message3
            var results2 = McEmailMessage.QueryNeedsIndexing (1);
            Assert.AreEqual (1, results2.Count);
            CheckMessage (message3, results2 [0]);
        }

        [Test]
        public void TestQueryByDateReceivedAndFrom ()
        {
            var accountId = 9;
            var magicTime1 = DateTime.UtcNow.AddDays(-5);
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
                IsIndexed = false
            };
            InsertAndCheckMessage (message1);
            // will be found.
            var message2 = new McEmailMessage () {
                AccountId = accountId,
                DateReceived = magicTime2,
                From = magicFrom1,
                To = winner2,
                BodyId = 0,
                IsIndexed = false
            };
            InsertAndCheckMessage (message2);
            // will not be found
            var message3 = new McEmailMessage () {
                AccountId = accountId,
                DateReceived = magicTime2,
                From = magicFrom1,
                To = loser,
                BodyId = 0,
                IsIndexed = false,
                IsAwaitingDelete = true,
            };
            InsertAndCheckMessage (message3);
            // will not be found
            var message4 = new McEmailMessage () {
                AccountId = accountId+1,
                DateReceived = magicTime2,
                From = magicFrom1,
                To = loser,
                BodyId = 0,
                IsIndexed = false,
            };
            InsertAndCheckMessage (message4);
            // will not be found
            var message5 = new McEmailMessage () {
                AccountId = accountId,
                DateReceived = magicTime2.AddDays (-100),
                From = magicFrom1,
                To = loser,
                BodyId = 0,
                IsIndexed = false,
            };
            InsertAndCheckMessage (message5);
            // will not be found
            var message6 = new McEmailMessage () {
                AccountId = accountId,
                DateReceived = magicTime2,
                From = magicFrom1 + "m",
                To = loser,
                BodyId = 0,
                IsIndexed = false,
            };
            InsertAndCheckMessage (message6);

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
    }
}

