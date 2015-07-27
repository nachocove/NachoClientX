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
        uint defaultSpan = 30;

        McFolder TestFolder { get; set; }
        McProtocolState ProtocolState { get; set; }

        [SetUp]
        public void Setup ()
        {
            Account = new McAccount ();
            Account.Insert ();
            TestFolder = McFolder.Create (Account.Id, false, false, true, "0", "someServerId", "MyFolder", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            TestFolder.Insert ();
            var p = new McProtocolState (){
                AccountId = Account.Id,
                ImapServerCapabilities = McProtocolState.NcImapCapabilities.None,
            };
            p.Insert ();
            ProtocolState = p;
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

            bool changed;
            bool created;
            var emailMessage = ImapSyncCommand.ServerSaysAddOrChangeEmail (Account.Id, imapSummary, TestFolder, out changed, out created);

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

            var protocolState = ProtocolState;

            // NoSelect (i.e. not a folder that can have messages).
            // Should return null, since there's no syncing we can even do.
            TestFolder.ImapNoSelect = true;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.Null (syncKit);
            TestFolder.ImapNoSelect = false;


            // Test OpenOnly syncKit
            // 
            // fresh install or new folder. UidNext is not set (i.e. 0) so we have to go open the folder.
            DoFakeFolderOpen (TestFolder, 0);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (syncKit.Method, NachoCore.IMAP.SyncKit.MethodEnum.OpenOnly);
            Assert.Null (syncKit.SyncSet);

            var pending = new McPending ();
            pending.Operation = McPending.Operations.Sync;
            pending.ServerId = TestFolder.ServerId;
            syncKit = Strategy.GenSyncKit (ref protocolState, pending);
            Assert.NotNull (syncKit);
            Assert.AreEqual (syncKit.Method, NachoCore.IMAP.SyncKit.MethodEnum.OpenOnly);

            TestFolder = DoFakeFolderOpen (TestFolder, 1, DateTime.UtcNow.AddMinutes (-(6*60)));
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (syncKit.Method, NachoCore.IMAP.SyncKit.MethodEnum.OpenOnly);
            Assert.Null (syncKit.SyncSet);


            // The rest should not ever get an OpenOnly

            // an empty folder (UidNext is 1, i.e. there's no messages at all)
            TestFolder = resetFolder (TestFolder);
            TestFolder = DoFakeFolderOpen (TestFolder, 1);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.Null (syncKit); // no synckit. Nothing to do.

            // The next few tests simulate a folder with a bunch of messages in it.
            // This is the first sync, after we've discovered 123 as the UidNext value.
            TestFolder = resetFolder (TestFolder);
            TestFolder = DoFakeFolderOpen (TestFolder, 123);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (defaultSpan, syncKit.SyncSet.Count);
            Assert.AreEqual (122, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (122-defaultSpan+1, syncKit.SyncSet.Min ().Id);
            DoFakeSync (TestFolder, syncKit);

            // This would be the second pass, where we sync the next batch.
            // In the previous 'sync' we synced UID's 113 - 122 (10 items).
            // This time, we should see 75 items, numbered 38 through 112
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (defaultSpan, syncKit.SyncSet.Count);
            Assert.AreEqual (92, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (92-defaultSpan+1, syncKit.SyncSet.Min ().Id);
            DoFakeSync (TestFolder, syncKit);

            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (defaultSpan, syncKit.SyncSet.Count);
            Assert.AreEqual (62, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (62-defaultSpan+1, syncKit.SyncSet.Min ().Id);
            DoFakeSync (TestFolder, syncKit);

            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (defaultSpan, syncKit.SyncSet.Count);
            Assert.AreEqual (32, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (32-defaultSpan+1, syncKit.SyncSet.Min ().Id);
            DoFakeSync (TestFolder, syncKit);

            // less than 30 items are left, so the span should be "the rest" (i.e. 2), numbered 1 through 22.
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (2, syncKit.SyncSet.Count);
            Assert.AreEqual (2, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (1, syncKit.SyncSet.Min ().Id);
            DoFakeSync (TestFolder, syncKit);

            // Simulate new message coming in. I.e. bump ImapUidNext by 1.
            // This will cause us to start at the top again and sync down for 30 items
            DoFakeFolderOpen (TestFolder, TestFolder.ImapUidNext + 1);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (syncKit.Method, NachoCore.IMAP.SyncKit.MethodEnum.Sync);
            Assert.AreEqual (30, syncKit.SyncSet.Count);
            Assert.AreEqual (123, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (123-defaultSpan+1, syncKit.SyncSet.Min ().Id);
            DoFakeSync (TestFolder, syncKit);

            // Simulate 12 new message coming in. I.e. bump ImapUidNext by 12
            // this sync will get a batch of 10, starting at the latest/newest message, i.e. 135 to 126.
            DoFakeFolderOpen (TestFolder, TestFolder.ImapUidNext + 12);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (30, syncKit.SyncSet.Count);
            Assert.AreEqual (135, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (135-defaultSpan+1, syncKit.SyncSet.Min ().Id);
            DoFakeSync (TestFolder, syncKit);

            // and this sync continues downwards for 30 items.
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (30, syncKit.SyncSet.Count);
            Assert.AreEqual (105, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (105-defaultSpan+1, syncKit.SyncSet.Min ().Id);
            DoFakeSync (TestFolder, syncKit);

            DeleteAllTestMail ();

            // Let's try some cornercases.
            TestFolder = resetFolder (TestFolder);
            TestFolder = DoFakeFolderOpen(TestFolder, defaultSpan);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (29, syncKit.SyncSet.Count);
            Assert.AreEqual (29, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (1, syncKit.SyncSet.Min ().Id);

            TestFolder = resetFolder (TestFolder);
            TestFolder = DoFakeFolderOpen(TestFolder, defaultSpan+1);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (30, syncKit.SyncSet.Count);
            Assert.AreEqual (30, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (1, syncKit.SyncSet.Min ().Id);

            TestFolder = resetFolder (TestFolder);
            TestFolder = DoFakeFolderOpen(TestFolder, defaultSpan+2);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder);
            Assert.NotNull (syncKit);
            Assert.AreEqual (30, syncKit.SyncSet.Count);
            Assert.AreEqual (31, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (2, syncKit.SyncSet.Min ().Id);
        }

        private void DeleteAllTestMail()
        {
            foreach (var email in McEmailMessage.QueryByAccountId<McEmailMessage> (Account.Id)) {
                email.Delete ();
            }
        }

        private McFolder DoFakeFolderOpen(McFolder testFolder, uint ImapUidNext)
        {
            return DoFakeFolderOpen (testFolder, ImapUidNext, DateTime.UtcNow);
        }

        private McFolder DoFakeFolderOpen(McFolder testFolder, uint ImapUidNext, DateTime LastExamine)
        {
            string ImapUidSet;
            switch (ImapUidNext) {
            case 0:
                ImapUidSet = null;
                break;

            case 1:
                ImapUidSet = "1";
                break;

            default:
                ImapUidSet = new UniqueIdSet (new UniqueIdRange (new UniqueId (1), new UniqueId (ImapUidNext - 1))).ToString ();
                break;
            }
            return testFolder.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.ImapUidNext = ImapUidNext;
                target.ImapUidSet = ImapUidSet;
                target.ImapLastExamine = LastExamine;
                target.ImapNeedFullSync = false;
                return true;
            });
        }

        private void DoFakeSync(McFolder testFolder, NachoCore.IMAP.SyncKit syncKit)
        {
            McEmailMessage emailMessage;
            foreach (var uid in syncKit.SyncSet) {
                var ServerId = ImapProtoControl.MessageServerId (testFolder, uid);
                emailMessage = McEmailMessage.QueryByServerId<McEmailMessage> (Account.Id, ServerId);
                if (null == emailMessage) {
                    emailMessage = new McEmailMessage () {
                        AccountId = Account.Id,
                        From = "test@example.com",
                        ServerId = ServerId,
                        IsIncomplete = true,
                        ImapUid = uid.Id,
                    };
                    emailMessage.Insert ();
                    var map = new McMapFolderFolderEntry (Account.Id) {
                        AccountId = Account.Id,
                        FolderId = testFolder.Id,
                        FolderEntryId = emailMessage.Id,
                        ClassCode = McAbstrFolderEntry.ClassCodeEnum.Email,
                        AsSyncEpoch = 1,
                    };
                    map.Insert ();
                }
            }
            testFolder = testFolder.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.ImapUidHighestUidSynced = Math.Max (target.ImapUidHighestUidSynced, syncKit.SyncSet.Max ().Id);
                target.ImapUidLowestUidSynced = Math.Min (target.ImapUidLowestUidSynced, syncKit.SyncSet.Min ().Id);
                target.ImapLastUidSynced = syncKit.SyncSet.Min ().Id;
                target.ImapLastExamine = DateTime.UtcNow;
                return true;
            });
        }

        private McFolder resetFolder(McFolder folder)
        {
            return folder.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.ImapUidLowestUidSynced = UInt32.MaxValue;
                target.ImapUidHighestUidSynced = UInt32.MinValue;
                target.ImapLastUidSynced = UInt32.MinValue;
                target.ImapUidSet = string.Empty;
                return true;
            });

        }
    }
}

