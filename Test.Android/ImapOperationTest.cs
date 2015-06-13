//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using MimeKit;
using MailKit;
using NachoCore.Model;
using NachoCore.ActiveSync;
using NachoCore.IMAP;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoPlatform;

namespace Test.iOS
{
    public class ImapOperationTest
    {
        public ImapOperationTest ()
        {
        }

        string TestSubject = "Foo12345";
        MailboxAddress TestFrom = new MailboxAddress ("Test From", "testfrom@example.com");
        MailboxAddress TestTo = new MailboxAddress ("Test To", "testto@example.com");
        UniqueId TestUniqueId = new UniqueId (1);
        int someIndex = 1;
        int AccountId = 244;

        McFolder TestFolder { get; set; }
        McProtocolState protocolState { get; set; }

        [SetUp]
        public void Setup ()
        {
            TestFolder = McFolder.Create (AccountId, false, false, true, "0", "someServerId", "MyFolder", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            TestFolder.Insert ();
            protocolState = new McProtocolState ();
            NcCommStatus.Instance.Speed = NetStatusSpeedEnum.WiFi_0;
        }

        [TearDown]
        public void Teardown ()
        {
            TestFolder.Delete ();
        }

        [Test]
        public void TestMakeEmailMessage ()
        {
            MessageSummary imapSummary = new MessageSummary (someIndex) {
                UniqueId = TestUniqueId,
                InternalDate = DateTimeOffset.UtcNow,
                Envelope = new Envelope (),
            };

            imapSummary.Envelope.Subject = TestSubject;
            imapSummary.Envelope.To.Add (TestTo);
            imapSummary.Envelope.From.Add (TestFrom);

            var summary = new ImapSyncCommand.MailSummary () {
                imapSummary = imapSummary,
                preview = null,
            };
            var emailMessage = ImapSyncCommand.ServerSaysAddOrChangeEmail (AccountId, summary, TestFolder);

            Assert.AreEqual (emailMessage.Subject, TestSubject);
            Assert.True (emailMessage.FromEmailAddressId > 0);
            Assert.AreEqual (emailMessage.From, TestFrom.Address);
            Assert.AreEqual (emailMessage.To, TestTo.Address);
        }

        public class TestBEContext : IBEContext
        {
            public INcProtoControlOwner Owner { set; get; }
            public NcProtoControl ProtoControl { set; get; }
            public McProtocolState ProtocolState { get; set; }
            public McServer Server { get; set; }
            public McAccount Account { get; set; }
            public McCred Cred { get; set; }
        }

        [Test]
        public void TestSyncStrategy ()
        {
            // These tests assume wifi-commstatus (for the span calculation).
            // They will fail with anything else, so would need to be adjusted.

            NachoCore.IMAP.SyncKit syncKit;
            var Strategy = new ImapStrategy (new TestBEContext());

            // NoSelect (i.e. not a folder that can have messages).
            // Should return null, since there's no syncing we can even do.
            TestFolder.ImapNoSelect = true;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.Null (syncKit);
            TestFolder.ImapNoSelect = false;

            // fresh install or new folder. UidNext is not set (i.e. 0) so we have to go open the folder.
            TestFolder.ImapUidNext = 0;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (syncKit.Method, NachoCore.IMAP.SyncKit.MethodEnum.OpenOnly);
            Assert.Null (syncKit.UidList);

            // an empty folder (UidNext is 1, i.e. there's no messages at all)
            TestFolder.ImapUidNext = 1;
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.Null (syncKit); // no synckit. Nothing to do.

            // The next few tests simulate a folder with a bunch of messages in it.
            // This is the first sync, after we've discovered 123 as the UidNext value.
            TestFolder.ImapUidNext = 123;
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (10, syncKit.Span);  // First sync is 10, subsequent will be larger
            Assert.AreEqual (113, syncKit.Start); // 113 == 122 (one less than UIDNEXT) - 10 + 1
            Assert.AreEqual (syncKit.Span, syncKit.UidList.Count);
            Assert.AreEqual (122, syncKit.UidList.Max ().Id);
            Assert.AreEqual (113, syncKit.UidList.Min ().Id);

            // This would be the second pass, where we sync the next batch.
            // In the previous 'sync' we synced UID's 113 - 122 (10 items).
            // This time, we should see 75 items, numbered 38 through 112
            DoFakeSync (TestFolder, syncKit);
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (75, syncKit.Span);  // full sync window
            Assert.AreEqual (38, syncKit.Start); // 112 - 1 - 75 (ImapUidLowestUidSynced - 1 - span) +1
            Assert.AreEqual (syncKit.Span, syncKit.UidList.Count);
            Assert.AreEqual (112, syncKit.UidList.Max ().Id);
            Assert.AreEqual (38, syncKit.UidList.Min ().Id);

            // less than 75 items are left, so the span should be "the rest" (i.e. 37), numbered 1 through 37.
            DoFakeSync (TestFolder, syncKit);
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (37, syncKit.Span); // a span of 75 would overrun into id's we've already syncd, so 35.
            Assert.AreEqual (1, syncKit.Start);  // lowest - span (75) would be negative, so 1.
            Assert.AreEqual (syncKit.Span, syncKit.UidList.Count);
            Assert.AreEqual (37, syncKit.UidList.Max ().Id);
            Assert.AreEqual (1, syncKit.UidList.Min ().Id);

            // Simulate new message coming in. I.e. bump ImapUidNext by 1.
            DoFakeSync (TestFolder, syncKit);
            TestFolder.ImapUidNext = TestFolder.ImapUidNext + 1;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (1, syncKit.Span); // a span of 75 would overrun into id's we've already syncd, so 35.
            Assert.AreEqual (123, syncKit.Start);  // lowest - span (75) would be negative, so 1.
            Assert.AreEqual (syncKit.Span, syncKit.UidList.Count);
            Assert.AreEqual (123, syncKit.UidList.Max ().Id);
            Assert.AreEqual (123, syncKit.UidList.Min ().Id);

            // Simulate 12 new message coming in. I.e. bump ImapUidNext by 12
            // this sync will get a batch of 10, starting at the last fetched +1, so 124 to 133.
            DoFakeSync (TestFolder, syncKit);
            TestFolder.ImapUidNext = TestFolder.ImapUidNext + 12;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (10, syncKit.Span); // a span of 75 would overrun into id's we've already syncd, so 35.
            Assert.AreEqual (124, syncKit.Start);  // lowest - span (75) would be negative, so 1.
            Assert.AreEqual (syncKit.Span, syncKit.UidList.Count);
            Assert.AreEqual (133, syncKit.UidList.Max ().Id);
            Assert.AreEqual (124, syncKit.UidList.Min ().Id);

            // and this sync will get the rest, i.e. 2 more.
            DoFakeSync (TestFolder, syncKit);
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (2, syncKit.Span); // a span of 75 would overrun into id's we've already syncd, so 35.
            Assert.AreEqual (134, syncKit.Start);  // lowest - span (75) would be negative, so 1.
            Assert.AreEqual (syncKit.Span, syncKit.UidList.Count);
            Assert.AreEqual (135, syncKit.UidList.Max ().Id);
            Assert.AreEqual (134, syncKit.UidList.Min ().Id);

            // Let's try some cornercases.
            TestFolder.ImapUidNext = 9;  // two less than the minimal span
                                         // (UIDNEXT 9 means there's at most 1 through 8 in the mailbox)
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (8, syncKit.Span); 
            Assert.AreEqual (1, syncKit.Start);
            Assert.AreEqual (syncKit.Span, syncKit.UidList.Count);
            Assert.AreEqual (8, syncKit.UidList.Max ().Id);
            Assert.AreEqual (1, syncKit.UidList.Min ().Id);

            TestFolder.ImapUidNext = 10;  // one less than the span (1 - 9)
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (9, syncKit.Span); // UidNext is 10, meaning the highest possible message can be 9 (8+1).
            Assert.AreEqual (1, syncKit.Start); // start at 1, because UidNext -1 - Span (i.e. 10 - 1 - 10 would be negative, and 1 is the lowest possible UID (0 is not legal)
            Assert.AreEqual (syncKit.Span, syncKit.UidList.Count);
            Assert.AreEqual (9, syncKit.UidList.Max ().Id);
            Assert.AreEqual (1, syncKit.UidList.Min ().Id);

            TestFolder.ImapUidNext = 11;
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (10, syncKit.Span); 
            Assert.AreEqual (1, syncKit.Start);
            Assert.AreEqual (syncKit.Span, syncKit.UidList.Count);
            Assert.AreEqual (10, syncKit.UidList.Max ().Id);
            Assert.AreEqual (1, syncKit.UidList.Min ().Id);

            TestFolder.ImapUidNext = 12;
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (10, syncKit.Span); 
            Assert.AreEqual (2, syncKit.Start);
            Assert.AreEqual (syncKit.Span, syncKit.UidList.Count);
            Assert.AreEqual (11, syncKit.UidList.Max ().Id);
            Assert.AreEqual (2, syncKit.UidList.Min ().Id);

            TestFolder.ImapUidNext = 13;
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (10, syncKit.Span); 
            Assert.AreEqual (3, syncKit.Start);
            Assert.AreEqual (syncKit.Span, syncKit.UidList.Count);
            Assert.AreEqual (12, syncKit.UidList.Max ().Id);
            Assert.AreEqual (3, syncKit.UidList.Min ().Id);

        }

        private void DoFakeSync(McFolder TestFolder, NachoCore.IMAP.SyncKit syncKit)
        {
            TestFolder.ImapUidHighestUidSynced = Math.Max (TestFolder.ImapUidHighestUidSynced, syncKit.UidList.Max ().Id);
            TestFolder.ImapUidLowestUidSynced = Math.Min (TestFolder.ImapUidLowestUidSynced, syncKit.UidList.Min ().Id);
            TestFolder.ImapLastExamine = DateTime.UtcNow;
        }
    }
}

