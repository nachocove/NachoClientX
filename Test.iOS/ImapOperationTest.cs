//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
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
            public McAccount Account { get; }
            public McCred Cred { get; }
        }

        [Test]
        public void TestSyncStrategy ()
        {
            NachoCore.IMAP.SyncKit syncKit;
            var Strategy = new ImapStrategy (new TestBEContext());

            TestFolder.ImapNoSelect = true;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.Null (syncKit);
            TestFolder.ImapNoSelect = false;

            TestFolder.ImapUidNext = 0;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (syncKit.Method, NachoCore.IMAP.SyncKit.MethodEnum.OpenOnly);

            TestFolder.ImapUidNext = 123;
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (10, syncKit.Span);  // First sync is 10, subsequent will be larger
            Assert.AreEqual (112, syncKit.Start); // 112 = TestFolder.ImapUidNext - 1 - syncKit.Span

            TestFolder.ImapUidLowestUidSynced = 112;
            TestFolder.ImapUidHighestUidSynced = 122;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (75, syncKit.Span);  // full sync window
            Assert.AreEqual (36, syncKit.Start); // 112 - 1 - 75 (ImapUidLowestUidSynced - 1 - span)

            TestFolder.ImapUidLowestUidSynced = 36;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (AccountId, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (35, syncKit.Span); // a span of 75 would overrun into id's we've already syncd, so 35.
            Assert.AreEqual (1, syncKit.Start);  // lowest - span (75) would be negative, so 1.
            }
    }
}

