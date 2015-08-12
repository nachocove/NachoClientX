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
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;

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
            ProtocolState.Delete ();
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

        public class TestOwner : INcProtoControlOwner
        {
            public void StatusInd (NcProtoControl sender, NcResult status) {}
            public void StatusInd (NcProtoControl sender, NcResult status, string[] tokens) {}
            public void CredReq (NcProtoControl sender) {}
            public void ServConfReq (NcProtoControl sender, object arg) {}
            public void CertAskReq (NcProtoControl sender, X509Certificate2 certificate) {}
            public void SearchContactsResp (NcProtoControl sender, string prefix, string token) {}
            public void SendEmailResp (NcProtoControl sender, int emailMessageId, bool didSend) {}
        }

        [Test]
        public void TestQuickSyncSet ()
        {
            TestBEContext beContext = new TestBEContext ();
            beContext.Account = Account;
            beContext.Owner = new TestOwner ();
            var Strategy = new ImapStrategy (beContext);

            IList<UniqueId> syncSet;
            var protocolState = ProtocolState;
            NachoCore.IMAP.SyncKit syncKit;
            TestFolder = resetFolder (TestFolder);
            TestFolder = DoFakeFolderOpen (TestFolder, 10);
            syncSet = ImapStrategy.SyncSet (TestFolder, ref protocolState);
            TestFolder = DoFakeSync (TestFolder, syncSet); // creates emails 1-9

            TestFolder = DoFakeFolderOpen (TestFolder, 15);
            syncSet = ImapStrategy.QuickSyncSet (15, TestFolder, 10);
            Assert.NotNull (syncSet);
            Assert.AreEqual (5, syncSet.Count);
            Assert.AreEqual (14, syncSet.Max ().Id);
            Assert.AreEqual (10, syncSet.Min ().Id);
            // don't sync. Try another set

            TestFolder = DoFakeFolderOpen (TestFolder, 25);
            syncSet = ImapStrategy.QuickSyncSet (25, TestFolder, 10);
            Assert.NotNull (syncSet);
            Assert.AreEqual (10, syncSet.Count);
            Assert.AreEqual (24, syncSet.Max ().Id);
            Assert.AreEqual (15, syncSet.Min ().Id);
            TestFolder = DoFakeSync (TestFolder, syncSet); // creates emails 24-15

            // we now have emails 1-9 and 24-15. The next sync, looking for
            // at most 10 new emails to sync will get 10-14
            syncSet = ImapStrategy.QuickSyncSet (25, TestFolder, 10);
            Assert.NotNull (syncSet);
            Assert.AreEqual (5, syncSet.Count);
            Assert.AreEqual (14, syncSet.Max ().Id);
            Assert.AreEqual (10, syncSet.Min ().Id);
            // don't sync. See what a normal GenSyncKit does. It should give us the same range.
            // get the latest data for the folder
            TestFolder = McFolder.QueryById<McFolder> (TestFolder.Id);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.AreEqual (5, syncSet.Count);
            Assert.AreEqual (14, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (10, syncKit.SyncSet.Min ().Id);
        }

        [Test]
        public void TestQuickSyncSetPending ()
        {
            NachoCore.IMAP.SyncKit syncKit;
            var protocolState = ProtocolState;
            TestBEContext beContext = new TestBEContext ();
            beContext.Account = Account;
            beContext.Owner = new TestOwner ();
            var Strategy = new ImapStrategy (beContext);

            // an sync here will cause a QuickSync
            TestFolder = DoFakeFolderOpen (TestFolder, 10);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.AreEqual (NachoCore.IMAP.SyncKit.MethodEnum.QuickSync, syncKit.Method);

            // create some emails, simulating an initial sync
            TestFolder = resetFolder (TestFolder);
            TestFolder = DoFakeFolderOpen (TestFolder, 10);
            var syncSet = ImapStrategy.SyncSet (TestFolder, ref protocolState);
            TestFolder = DoFakeSync (TestFolder, syncSet); // creates emails 1-9
            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.ImapSyncRung = 2; // we're no longer in the initial sync
                return true;
            });

            // simulate pull-to-refresh
            var pending = new McPending (){
                AccountId = Account.Id,
                Operation = McPending.Operations.Sync,
                ServerId = TestFolder.ServerId,
            };
            pending.Insert ();
            syncKit = Strategy.GenSyncKit (ref protocolState, pending);
            Assert.NotNull (syncKit);
            Assert.AreEqual (NachoCore.IMAP.SyncKit.MethodEnum.QuickSync, syncKit.Method);
        }

        [Test]
        public void TestSyncStrategy ()
        {
            // These tests assume wifi-commstatus (for the span calculation).
            // They will fail with anything else, so would need to be adjusted.

            NachoCore.IMAP.SyncKit syncKit;
            TestBEContext beContext = new TestBEContext ();
            beContext.Account = Account;
            beContext.Owner = new TestOwner ();
            var Strategy = new ImapStrategy (beContext);

            var protocolState = ProtocolState;

            // NoSelect (i.e. not a folder that can have messages).
            // Should return null, since there's no syncing we can even do.
            TestFolder.ImapNoSelect = true;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.Null (syncKit);
            TestFolder.ImapNoSelect = false;

            // fresh install or new folder. UidNext is not set (i.e. 0) so we have to go open the folder.
            TestFolder = DoFakeFolderOpen (TestFolder, 0);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.Null (syncKit);

            TestFolder = DoFakeFolderOpen (TestFolder, 1, DateTime.UtcNow.AddMinutes (-(6*60)));
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.AreEqual (NachoCore.IMAP.SyncKit.MethodEnum.OpenOnly, syncKit.Method);
            Assert.Null (syncKit.SyncSet);

            // an empty folder (UidNext is 1, i.e. there's no messages at all)
            TestFolder = resetFolder (TestFolder);
            TestFolder = DoFakeFolderOpen (TestFolder, 1);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.Null (syncKit); // no synckit. Nothing to do.

            // The next few tests simulate a folder with a bunch of messages in it.
            // This is the first sync, after we've discovered 123 as the UidNext value.
            TestFolder = resetFolder (TestFolder);
            TestFolder = DoFakeFolderOpen (TestFolder, 126);
            TestFolder = DoFakeSync (TestFolder, new UniqueIdRange(new UniqueId(125), new UniqueId(1))); // creates emails 1-122
            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.ImapSyncRung = 2; // we're no longer in the initial sync
                return true;
            });

            // fake us having sync'd the first few.
            TestFolder = TestFolder.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.ImapLastUidSynced = 123;
                target.ImapUidHighestUidSynced = 125;
                target.ImapUidLowestUidSynced = 123;
                return true;
            });

            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.NotNull (syncKit.SyncSet);
            Assert.AreEqual (defaultSpan, syncKit.SyncSet.Count);
            Assert.AreEqual (122, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (122-defaultSpan+1, syncKit.SyncSet.Min ().Id);
            TestFolder = DoFakeSync (TestFolder, syncKit.SyncSet);

            // This would be the second pass, where we sync the next batch.
            // In the previous 'sync' we synced UID's 113 - 122 (10 items).
            // This time, we should see 75 items, numbered 38 through 112
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.AreEqual (defaultSpan, syncKit.SyncSet.Count);
            Assert.AreEqual (92, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (92-defaultSpan+1, syncKit.SyncSet.Min ().Id);
            TestFolder = DoFakeSync (TestFolder, syncKit.SyncSet);

            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.AreEqual (defaultSpan, syncKit.SyncSet.Count);
            Assert.AreEqual (62, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (62-defaultSpan+1, syncKit.SyncSet.Min ().Id);
            TestFolder = DoFakeSync (TestFolder, syncKit.SyncSet);

            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.AreEqual (defaultSpan, syncKit.SyncSet.Count);
            Assert.AreEqual (32, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (32-defaultSpan+1, syncKit.SyncSet.Min ().Id);
            TestFolder = DoFakeSync (TestFolder, syncKit.SyncSet);

            // less than 30 items are left, so the span should be "the rest" (i.e. 2), numbered 1 through 22.
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.AreEqual (2, syncKit.SyncSet.Count);
            Assert.AreEqual (2, syncKit.SyncSet.Max ().Id);
            Assert.AreEqual (1, syncKit.SyncSet.Min ().Id);
            TestFolder = DoFakeSync (TestFolder, syncKit.SyncSet);

            // Simulate new message coming in. I.e. bump ImapUidNext by 1.
            // This will cause us to start at the top again and sync down for 30 items
            TestFolder = DoFakeFolderOpen (TestFolder, TestFolder.ImapUidNext + 1);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.AreEqual (NachoCore.IMAP.SyncKit.MethodEnum.QuickSync, syncKit.Method);
            Assert.Null (syncKit.SyncSet);

            // Simulate 12 new message coming in. I.e. bump ImapUidNext by 12
            // this sync will get a batch of 12 (because it'll fetch new only)
            TestFolder = DoFakeFolderOpen (TestFolder, TestFolder.ImapUidNext + 12);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.AreEqual (NachoCore.IMAP.SyncKit.MethodEnum.QuickSync, syncKit.Method);
            Assert.Null (syncKit.SyncSet);
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

        private McFolder DoFakeSync(McFolder testFolder, IList<UniqueId> SyncSet)
        {
            McEmailMessage emailMessage;
            foreach (var uid in SyncSet) {
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
            return testFolder.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.ImapUidHighestUidSynced = Math.Max (target.ImapUidHighestUidSynced, SyncSet.Max ().Id);
                target.ImapUidLowestUidSynced = Math.Min (target.ImapUidLowestUidSynced, SyncSet.Min ().Id);
                target.ImapLastUidSynced = SyncSet.Min ().Id;
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

