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
        McAccount Account;

        McFolder TestFolder { get; set; }
        McProtocolState protocolState { get; set; }

        [SetUp]
        public void Setup ()
        {
            Account = new McAccount ();
            Account.Insert ();
            TestFolder = McFolder.Create (Account.Id, false, false, true, "0", "someServerId", "MyFolder", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            TestFolder.Insert ();
            protocolState = new McProtocolState ();
            NcCommStatus.Instance.Speed = NetStatusSpeedEnum.WiFi_0;
        }

        [TearDown]
        public void Teardown ()
        {
            DeleteAllTestMail ();
            TestFolder.Delete ();
            Account.Delete ();
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
            var emailMessage = ImapSyncCommand.ServerSaysAddOrChangeEmail (Account.Id, summary, TestFolder);

            Assert.AreEqual (emailMessage.Subject, TestSubject);
            Assert.True (emailMessage.FromEmailAddressId > 0);
            Assert.AreEqual (emailMessage.From, TestFrom.ToString ());
            Assert.AreEqual (emailMessage.To, TestTo.ToString ());
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
            TestBEContext beContext = new TestBEContext ();
            beContext.Account = Account;
            var Strategy = new ImapStrategy (beContext);

            // NoSelect (i.e. not a folder that can have messages).
            // Should return null, since there's no syncing we can even do.
            TestFolder.ImapNoSelect = true;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.Null (syncKit);
            TestFolder.ImapNoSelect = false;

            // fresh install or new folder. UidNext is not set (i.e. 0) so we have to go open the folder.
            TestFolder.ImapUidNext = 0;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (syncKit.Method, NachoCore.IMAP.SyncKit.MethodEnum.OpenOnly);
            Assert.Null (syncKit.SyncSet);

            // an empty folder (UidNext is 1, i.e. there's no messages at all)
            TestFolder.ImapUidNext = 1;
            TestFolder.ImapUidSet = string.Empty;
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.Null (syncKit); // no synckit. Nothing to do.

            // The next few tests simulate a folder with a bunch of messages in it.
            // This is the first sync, after we've discovered 123 as the UidNext value.
            TestFolder.ImapUidNext = 123;
            var uidlist = new UniqueIdRange (new UniqueId(1), new UniqueId(122));
            TestFolder.ImapUidSet = uidlist.ToString ();
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (10, syncKit.SyncSet.Count);
            Assert.AreEqual (122, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (113, syncKit.SyncSet.Min ().Id);

            // This would be the second pass, where we sync the next batch.
            // In the previous 'sync' we synced UID's 113 - 122 (10 items).
            // This time, we should see 75 items, numbered 38 through 112
            DoFakeSync (TestFolder, syncKit);
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (30, syncKit.SyncSet.Count);
            Assert.AreEqual (112, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (83, syncKit.SyncSet.Min ().Id);

            DoFakeSync (TestFolder, syncKit);
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (30, syncKit.SyncSet.Count);
            Assert.AreEqual (82, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (53, syncKit.SyncSet.Min ().Id);

            DoFakeSync (TestFolder, syncKit);
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (30, syncKit.SyncSet.Count);
            Assert.AreEqual (52, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (23, syncKit.SyncSet.Min ().Id);

            // less than 30 items are left, so the span should be "the rest" (i.e. 2), numbered 1 through 22.
            DoFakeSync (TestFolder, syncKit);
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (22, syncKit.SyncSet.Count);
            Assert.AreEqual (22, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (1, syncKit.SyncSet.Min ().Id);

            // Simulate new message coming in. I.e. bump ImapUidNext by 1.
            DoFakeSync (TestFolder, syncKit);
            TestFolder.ImapUidNext = TestFolder.ImapUidNext + 1;
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (syncKit.Method, NachoCore.IMAP.SyncKit.MethodEnum.OpenOnly);
            Assert.Null (syncKit.SyncSet);

            uidlist = new UniqueIdRange (new UniqueId(1), new UniqueId(TestFolder.ImapUidNext-1));
            TestFolder.ImapUidSet = uidlist.ToString ();
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (1, syncKit.SyncSet.Count);
            Assert.AreEqual (123, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (123, syncKit.SyncSet.Min ().Id);

            // Simulate 12 new message coming in. I.e. bump ImapUidNext by 12
            // this sync will get a batch of 10, starting at the latest/newest message, i.e. 135 to 126.
            DoFakeSync (TestFolder, syncKit);
            TestFolder.ImapUidNext = TestFolder.ImapUidNext + 12;
            uidlist = new UniqueIdRange (new UniqueId(1), new UniqueId(TestFolder.ImapUidNext-1));
            TestFolder.ImapUidSet = uidlist.ToString ();
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (10, syncKit.SyncSet.Count);
            Assert.AreEqual (135, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (126, syncKit.SyncSet.Min ().Id);

            // and this sync will get the rest, i.e. 2 more.
            DoFakeSync (TestFolder, syncKit);
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (2, syncKit.SyncSet.Count);
            Assert.AreEqual (125, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (124, syncKit.SyncSet.Min ().Id);

            DeleteAllTestMail ();

            // Let's try some cornercases.
            TestFolder.ImapUidNext = 9;  // two less than the minimal span
                                         // (UIDNEXT 9 means there's at most 1 through 8 in the mailbox)
            uidlist = new UniqueIdRange (new UniqueId(1), new UniqueId(TestFolder.ImapUidNext-1));
            TestFolder.ImapUidSet = uidlist.ToString ();
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (8, syncKit.SyncSet.Count);
            Assert.AreEqual (8, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (1, syncKit.SyncSet.Min ().Id);

            TestFolder.ImapUidNext = 10;  // one less than the span (1 - 9)
            uidlist = new UniqueIdRange (new UniqueId(1), new UniqueId(TestFolder.ImapUidNext-1));
            TestFolder.ImapUidSet = uidlist.ToString ();
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (9, syncKit.SyncSet.Count);
            Assert.AreEqual (9, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (1, syncKit.SyncSet.Min ().Id);

            TestFolder.ImapUidNext = 11;
            uidlist = new UniqueIdRange (new UniqueId(1), new UniqueId(TestFolder.ImapUidNext-1));
            TestFolder.ImapUidSet = uidlist.ToString ();
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (10, syncKit.SyncSet.Count);
            Assert.AreEqual (10, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (1, syncKit.SyncSet.Min ().Id);

            TestFolder.ImapUidNext = 12;
            uidlist = new UniqueIdRange (new UniqueId(1), new UniqueId(TestFolder.ImapUidNext-1));
            TestFolder.ImapUidSet = uidlist.ToString ();
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (10, syncKit.SyncSet.Count);
            Assert.AreEqual (11, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (2, syncKit.SyncSet.Min ().Id);

            TestFolder.ImapUidNext = 13;
            uidlist = new UniqueIdRange (new UniqueId(1), new UniqueId(TestFolder.ImapUidNext-1));
            TestFolder.ImapUidSet = uidlist.ToString ();
            TestFolder.ImapUidLowestUidSynced = UInt32.MaxValue;
            TestFolder.ImapUidHighestUidSynced = UInt32.MinValue;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (Account.Id, protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (10, syncKit.SyncSet.Count);
            Assert.AreEqual (12, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (3, syncKit.SyncSet.Min ().Id);

        }

        private void DeleteAllTestMail()
        {
            foreach (var email in McEmailMessage.QueryByAccountId<McEmailMessage> (Account.Id)) {
                email.Delete ();
            }
        }

        private void DoFakeSync(McFolder testFolder, NachoCore.IMAP.SyncKit syncKit)
        {
            foreach (var uid in syncKit.SyncSet) {
                var emailMessage = new McEmailMessage () {
                    From = "test@example.com",
                    AccountId = Account.Id,
                    ServerId = ImapProtoControl.MessageServerId (testFolder, uid),
                    IsIncomplete = true,
                };
                emailMessage.Insert ();
                var map = new McMapFolderFolderEntry (Account.Id) {
                    FolderId = testFolder.Id,
                    FolderEntryId = emailMessage.Id,
                    ClassCode = McAbstrFolderEntry.ClassCodeEnum.Email,
                    AsSyncEpoch = 1,
                };
                map.Insert ();
            }

            testFolder.ImapUidHighestUidSynced = Math.Max (testFolder.ImapUidHighestUidSynced, syncKit.SyncSet.Max ().Id);
            testFolder.ImapUidLowestUidSynced = Math.Min (testFolder.ImapUidLowestUidSynced, syncKit.SyncSet.Min ().Id);
            testFolder.ImapLastUidSynced = syncKit.SyncSet.Min ().Id;
            testFolder.ImapLastExamine = DateTime.UtcNow;
        }
    }
}

