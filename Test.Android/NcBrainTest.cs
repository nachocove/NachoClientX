//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using System.Collections.Generic;
using NUnit.Framework;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.Brain;
using NachoCore;

namespace Test.Common
{
    public class NcBrainTest : NcTestBase
    {
        McEmailAddress Address;

        McEmailMessage Message;

        McEmailMessageDependency Dependency;

        List<NcResult.SubKindEnum> NotificationsReceived;

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

            Dependency = new McEmailMessageDependency (1);
            Dependency.EmailMessageId = Message.Id;
            Dependency.EmailAddressId = Address.Id;
            Dependency.EmailAddressType = (int)McEmailMessageDependency.AddressType.SENDER;
            Dependency.Insert ();

            NotificationsReceived = new List<NcResult.SubKindEnum> ();
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
            NcBrain.StopService ();
            while (!NcBrain.SharedInstance.IsQueueEmpty ()) {
                Thread.Sleep (50);
            }
            NcTask.StopService ();
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

            NcBrain.UpdateMessageScore (1, 1000000);
            WaitForBrain ();

            Assert.AreEqual (origCount, brain.McEmailAddressCounters.Update.Count);

            // Error case. Update a message who score does not change
            NcBrain.UpdateMessageScore (Message.AccountId, Message.Id);
            WaitForBrain ();

            Assert.AreEqual (origCount, brain.McEmailAddressCounters.Update.Count);
        }

        private void NotificationAction (NcResult.SubKindEnum type)
        {
            NotificationsReceived.Add (type);
        }

        [Test]
        public void NotificationRateLimiter ()
        {
            NcBrainNotification notif = new NcBrainNotification ();
            notif.Action = NotificationAction;

            Assert.True (notif.Enabled); // enabled by default

            // Send 1st notification - must receive
            notif.NotifyUpdates (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated);
            Assert.AreEqual (1, NotificationsReceived.Count);
            Assert.AreEqual (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated, NotificationsReceived [0]);
            NotificationsReceived.Clear ();

            // Wait KMinDurationMSec and send again - must received
            Thread.Sleep (NcBrainNotification.KMinDurationMsec + 200);
            var now = DateTime.Now;
            notif.NotifyUpdates (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated);
            Assert.AreEqual (1, NotificationsReceived.Count);
            Assert.AreEqual (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated, NotificationsReceived [0]);
            NotificationsReceived.Clear ();

            // Keep sending within the next KMinDurationMSec
            int count = 0;
            Assert.True (100 < NcBrainNotification.KMinDurationMsec);
            while ((DateTime.Now - now).TotalMilliseconds < (NcBrainNotification.KMinDurationMsec - 100)) {
                notif.NotifyUpdates (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated);
                count++;
            }
            Assert.True (0 < count); // should send at least one
            Assert.AreEqual (0, NotificationsReceived.Count); // must not get any
            NotificationsReceived.Clear ();
            Thread.Sleep (500); // make sure we are passed the KMinDurationMsec

            // Send two different types of notifications
            notif.NotifyUpdates (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated);
            notif.NotifyUpdates (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated);
            Assert.AreEqual (2, NotificationsReceived.Count);
            Assert.AreEqual (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated, NotificationsReceived [0]);
            Assert.AreEqual (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated, NotificationsReceived [1]);
            NotificationsReceived.Clear ();

            // Disable, send two notifications, enable
            notif.Enabled = false;
            notif.NotifyUpdates (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated);
            notif.NotifyUpdates (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated);
            Assert.AreEqual (0, NotificationsReceived.Count);
            notif.Enabled = true;
            Assert.AreEqual (2, NotificationsReceived.Count);
            if (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated == NotificationsReceived [0]) {
                Assert.AreEqual (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated, NotificationsReceived [1]);
            } else {
                Assert.AreEqual (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated, NotificationsReceived [0]);
                Assert.AreEqual (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated, NotificationsReceived [0]);
            }
        }
    }
}

