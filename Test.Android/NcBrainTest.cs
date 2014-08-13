//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using NUnit.Framework;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.Brain;

namespace Test.Common
{
    public class NcBrainTest
    {
        static bool Initialized = false;

        McEmailAddress Address;

        McEmailMessage Message;

        McEmailMessageDependency Dependency;

        [SetUp]
        public void SetUp ()
        {
            Telemetry.ENABLED = false;
            if (!Initialized) {
                NcTask.StartService ();
                Initialized = true;
            }
            Address = new McEmailAddress ();
            Address.AccountId = 1;
            Address.CanonicalEmailAddress = "bob@company.com";
            Address.Insert ();

            Message = new McEmailMessage ();
            Message.AccountId = 1;
            Message.From = "bob@company.com";
            Message.DateReceived = DateTime.Now;
            Message.Insert ();

            Dependency = new McEmailMessageDependency ();
            Dependency.EmailMessageId = Message.Id;
            Dependency.EmailAddressId = Address.Id;
            Dependency.EmailAddressType = "Sender";
            Dependency.Insert ();
        }

        [TearDown]
        public void TearDown ()
        {
            if (0 != Dependency.Id) {
                Dependency.Delete ();
            }
            if (0 != Message.Id) {
                Message.Delete ();
            }
            if (0 != Address.Id) {
                Address.Delete ();
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

        [TestCase]
        public void UpdateEmailAddress ()
        {
            // Imagine initially 1 out of 3 emails are read
            Address.Score = 1.0 / 3.0;
            // Then receive one more and read it.
            Address.EmailsReceived = 4;
            Address.EmailsRead = 2;
            Address.ScoreVersion = Scoring.Version;
            Address.Update ();

            long origCount = NcBrain.SharedInstance.McEmailAddressCounters.Update.Count;
            NcBrain.UpdateAddressScore (Address.Id);
            WaitForBrain ();

            // The new score should be 0.5 with one update
            Address = McEmailAddress.QueryById<McEmailAddress> (Address.Id);
            Assert.AreEqual (0.5, Address.Score);
            Assert.AreEqual (origCount + 1, NcBrain.SharedInstance.McEmailAddressCounters.Update.Count);

            // Update again. Should get the same score with no update
            NcBrain.UpdateAddressScore (Address.Id);
            WaitForBrain ();
        }

        private void TestUpdateMessageScore (ref McEmailMessage message)
        {
            NcBrain.UpdateMessageScore (message.Id);
            WaitForBrain ();
            message = McEmailMessage.QueryById<McEmailMessage> (message.Id);
        }

        [TestCase]
        public void UpdateEmailMessage ()
        {
            Address.IsVip = false;
            Address.EmailsRead = 2;
            Address.EmailsReceived = 3;
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

            NcBrain.UpdateMessageScore (1000000);
            WaitForBrain ();

            Assert.AreEqual (origCount, brain.McEmailAddressCounters.Update.Count);

            // Error case. Update a message who score does not change
            NcBrain.UpdateMessageScore (Message.Id);
            WaitForBrain ();

            Assert.AreEqual (origCount, brain.McEmailAddressCounters.Update.Count);
        }
    }
}

