//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.Utils;

namespace Test.Common
{
    [TestFixture]
    public class McEmailMessageTest : NcTestBase
    {
        McFolder Folder;

        // Lazy. Going to fudge an account (instead of creating one)
        private const int defaultAccountId = 1;

        [SetUp]
        public new void Setup ()
        {
            base.Setup ();
            Folder = McFolder.Create (defaultAccountId, false, false, "0", "test", "Test Folder",
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

        public void CheckMessages (McEmailMessage [] messages, List<NcEmailMessageIndex> got, params int [] expectedIndices)
        {
            Assert.AreEqual (expectedIndices.Length, got.Count);
            for (int n = 0; n < expectedIndices.Length; n++) {
                Assert.AreEqual (messages [expectedIndices [n]].Id, got [n].Id);
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

            List<NcEmailMessageIndex> messageList =
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

        private void CheckScoreAndUpdate (int id, double expectedScore, bool expectedNeedUpdate)
        {
            McEmailMessage message = McEmailMessage.QueryById<McEmailMessage> (id);
            Assert.True (null != message);

            Assert.AreEqual(expectedScore, message.Score);
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
    }
}

