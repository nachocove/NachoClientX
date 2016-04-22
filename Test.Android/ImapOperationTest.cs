//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using MimeKit;
using MailKit;
using NachoCore.Model;
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
        string TestSubject = "Foo12345";
        MailboxAddress TestFrom = new MailboxAddress ("Test From", "testfrom@example.com");
        MailboxAddress TestTo = new MailboxAddress ("Test To", "testto@example.com");
        UniqueId TestUniqueId = new UniqueId (1);
        int someIndex = 1;
        McAccount Account;
        uint defaultSpan;
        uint resyncDefaultSpan;
        uint quickSyncSpan;

        McFolder TestFolder { get; set; }
        McProtocolState ProtocolState { get; set; }

        [SetUp]
        public void Setup ()
        {
            Account = new McAccount ();
            Account.Insert ();
            TestFolder = McFolder.Create (Account.Id, false, false, true, "0", "someServerId", "MyFolder", NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            TestFolder.Insert ();
            var p = new McProtocolState (){
                AccountId = Account.Id,
                ImapServerCapabilities = McProtocolState.NcImapCapabilities.None,
            };
            p.Insert ();
            ProtocolState = p;
            NcCommStatus.Instance.Speed = NetStatusSpeedEnum.WiFi_0;
            defaultSpan = ImapStrategy.KBaseFetchSize * 3 * ImapStrategy.KInboxWindowMultiplier;
            quickSyncSpan = ImapStrategy.KBaseFetchSize * 3;
            resyncDefaultSpan = defaultSpan * ImapStrategy.KResyncMultiplier;
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
            imapSummary.Body = new BodyPartBasic ();
            imapSummary.Body.ContentType = new ContentType ("text", "html");

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
            public void ServConfReq (NcProtoControl sender, BackEnd.AutoDFailureReasonEnum arg) {}
            public void CertAskReq (NcProtoControl sender, X509Certificate2 certificate) {}
            public void SearchContactsResp (NcProtoControl sender, string prefix, string token) {}
            public void SendEmailResp (NcProtoControl sender, int emailMessageId, bool didSend) {}
            public void BackendAbateStart () {}
            public void BackendAbateStop () {}
        }

        [Test]
        public void TestQuickSyncSet ()
        {
            IList<UniqueId> syncSet;
            var protocolState = ProtocolState;

            // start with an empty folder with no emails. Do some boundary checking.

            TestFolder = resetFolder (TestFolder);

            syncSet = ImapStrategy.FastSyncSet (1, TestFolder, 10);
            Assert.Null (syncSet); // nothing to sync. empty folder.

            // completely new sync. 5 messages (5 less than span), nothing synced yet.
            syncSet = ImapStrategy.FastSyncSet (6, TestFolder, 10);
            Assert.NotNull (syncSet);
            Assert.AreEqual (5, syncSet.Count);
            Assert.AreEqual (5, syncSet.Max ().Id);
            Assert.AreEqual (1, syncSet.Min ().Id);

            // completely new sync. 9 new messages (1 less than span), nothing synced yet.
            syncSet = ImapStrategy.FastSyncSet (10, TestFolder, 10);
            Assert.NotNull (syncSet);
            Assert.AreEqual (9, syncSet.Count);
            Assert.AreEqual (9, syncSet.Max ().Id);
            Assert.AreEqual (1, syncSet.Min ().Id);

            // completely new sync. 10 new messages (== span), nothing synced yet.
            syncSet = ImapStrategy.FastSyncSet (11, TestFolder, 10);
            Assert.NotNull (syncSet);
            Assert.AreEqual (10, syncSet.Count);
            Assert.AreEqual (10, syncSet.Max ().Id);
            Assert.AreEqual (1, syncSet.Min ().Id);

            // completely new sync. 15 new messages (5 more than span), nothing synced yet.
            syncSet = ImapStrategy.FastSyncSet (16, TestFolder, 10);
            Assert.NotNull (syncSet);
            Assert.AreEqual (10, syncSet.Count);
            Assert.AreEqual (15, syncSet.Max ().Id);
            Assert.AreEqual (6, syncSet.Min ().Id);

            var syncInst = ImapStrategy.SyncInstructionForNewMails (ref protocolState, SyncKit.MustUniqueIdSet(new UniqueIdRange (new UniqueId (1), new UniqueId (10))));
            var syncKit = new SyncKit (TestFolder, new List<SyncInstruction> () { syncInst });
            TestFolder = DoFakeSync (TestFolder, syncKit);
            // Highest sync'd is 10. 1 new message.
            syncSet = ImapStrategy.FastSyncSet (12, TestFolder, 10);
            Assert.NotNull (syncSet);
            Assert.AreEqual (1, syncSet.Count);
            Assert.AreEqual (11, syncSet.Max ().Id);
            Assert.AreEqual (11, syncSet.Min ().Id);

            // Highest sync'd is 10. 5 new messages, ImapLastUidSynced reset to UidNext
            TestFolder.ImapUidNext = TestFolder.ImapLastUidSynced = 16;
            TestFolder.ImapUidHighestUidSynced = 10;
            syncSet = ImapStrategy.FastSyncSet (TestFolder.ImapUidNext, TestFolder, 10);
            Assert.NotNull (syncSet);
            Assert.AreEqual (5, syncSet.Count);
            Assert.AreEqual (15, syncSet.Max ().Id);
            Assert.AreEqual (11, syncSet.Min ().Id);

            // Highest sync'd is 10. 20 new messages, ImapLastUidSynced reset to UidNext
            TestFolder.ImapUidNext = TestFolder.ImapLastUidSynced = 31;
            TestFolder.ImapUidHighestUidSynced = 10;
            syncInst = ImapStrategy.SyncInstructionForNewMails (ref protocolState, SyncKit.MustUniqueIdSet(ImapStrategy.FastSyncSet (TestFolder.ImapUidNext, TestFolder, 10)));
            Assert.NotNull (syncInst);
            Assert.AreEqual (10, syncInst.UidSet.Count);
            Assert.AreEqual (30, syncInst.UidSet.Max ().Id);
            Assert.AreEqual (21, syncInst.UidSet.Min ().Id);
            syncKit = new SyncKit(TestFolder, new List<SyncInstruction> () { syncInst });
            TestFolder = DoFakeSync (TestFolder, syncKit);
            Assert.AreEqual (30, TestFolder.ImapUidHighestUidSynced);
            Assert.AreEqual (21, TestFolder.ImapLastUidSynced);

            // proceed with sync. Since there's no new mail, QuickSync will return a null.
            syncSet = ImapStrategy.FastSyncSet (TestFolder.ImapUidNext, TestFolder, 10);
            Assert.Null (syncSet);

            DeleteAllTestMail ();

            TestFolder = resetFolder (TestFolder);
            TestFolder = DoFakeFolderOpen (TestFolder, 10);
            var syncInstList = ImapStrategy.SyncInstructions (TestFolder, ref protocolState, false);
            Assert.AreEqual (1, syncInstList.Count);
            syncKit = new SyncKit(TestFolder, new List<SyncInstruction> () { syncInstList.First () });
            TestFolder = DoFakeSync (TestFolder, syncKit); // creates emails 1-9

            TestFolder = DoFakeFolderOpen (TestFolder, 15);
            syncSet = ImapStrategy.FastSyncSet (15, TestFolder, 10);
            Assert.NotNull (syncSet);
            Assert.AreEqual (5, syncSet.Count);
            Assert.AreEqual (14, syncSet.Max ().Id);
            Assert.AreEqual (10, syncSet.Min ().Id);
            // don't sync. Try another set

            TestFolder = DoFakeFolderOpen (TestFolder, 25);
            syncInst = ImapStrategy.SyncInstructionForNewMails (ref protocolState, SyncKit.MustUniqueIdSet(ImapStrategy.FastSyncSet (25, TestFolder, 10)));
            Assert.NotNull (syncInst);
            Assert.AreEqual (10, syncInst.UidSet.Count);
            Assert.AreEqual (24, syncInst.UidSet.Max ().Id);
            Assert.AreEqual (15, syncInst.UidSet.Min ().Id);
            syncKit = new SyncKit(TestFolder, new List<SyncInstruction> () { syncInst });
            TestFolder = DoFakeSync (TestFolder, syncKit); // creates emails 24-15

            TestFolder = McFolder.QueryById<McFolder> (TestFolder.Id);
            TestBEContext beContext = new TestBEContext ();
            beContext.Account = Account;
            beContext.Owner = new TestOwner ();
            var Strategy = new ImapStrategy (beContext);

            // this uses the default span of 30
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.AreEqual (2, syncKit.SyncInstructions.Count);
            Assert.AreEqual (14, syncKit.CombinedUidSet.Count);
            Assert.AreEqual (14, syncKit.MaxSynced);
            Assert.AreEqual (1, syncKit.MinSynced);
        }

        [Test]
        public void TestQuickSyncSetPending ()
        {
            SyncKit syncKit;
            var protocolState = ProtocolState;
            TestBEContext beContext = new TestBEContext ();
            beContext.Account = Account;
            beContext.Owner = new TestOwner ();
            var Strategy = new ImapStrategy (beContext);

            // an sync here will cause a QuickSync
            TestFolder = DoFakeFolderOpen (TestFolder, 10);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.AreEqual (SyncKit.MethodEnum.FastSync, syncKit.Method);

            // create some emails, simulating an initial sync
            TestFolder = resetFolder (TestFolder);
            TestFolder = DoFakeFolderOpen (TestFolder, 10);
            var syncInstList = ImapStrategy.SyncInstructions (TestFolder, ref protocolState, false);
            Assert.AreEqual (1, syncInstList.Count);
            syncKit = new SyncKit(TestFolder, new List<SyncInstruction> () { syncInstList.First () });
            TestFolder = DoFakeSync (TestFolder, syncKit); // creates emails 1-9
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
            Assert.AreEqual (SyncKit.MethodEnum.FastSync, syncKit.Method);
        }

        [Test]
        public void TestSyncStrategy ()
        {
            // These tests assume wifi-commstatus (for the span calculation).
            // They will fail with anything else, so would need to be adjusted.

            SyncKit syncKit;
            TestBEContext beContext = new TestBEContext ();
            beContext.Account = Account;
            beContext.Owner = new TestOwner ();
            var Strategy = new ImapStrategy (beContext);

            var protocolState = ProtocolState;
            TestFolder = resetFolder (TestFolder);

            // NoSelect (i.e. not a folder that can have messages).
            // Should return null, since there's no syncing we can even do.
            TestFolder.ImapNoSelect = true;
            TestFolder.ImapLastExamine = DateTime.UtcNow;
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.Null (syncKit);
            TestFolder.ImapNoSelect = false;

            TestFolder = resetFolder (TestFolder);
            // UidNext of 0 isn't valid. Expect an exception
            TestFolder = DoFakeFolderOpen (TestFolder, 0);
            Assert.Throws<NcAssert.NachoAssertionFailure> (() => Strategy.GenSyncKit (ref protocolState, TestFolder, null, false));

            TestFolder = DoFakeFolderOpen (TestFolder, 1, DateTime.UtcNow.AddMinutes (-(6*60)));
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.AreEqual (SyncKit.MethodEnum.FastSync, syncKit.Method);
            Assert.NotNull (syncKit.SyncInstructions);
            Assert.AreEqual (0, syncKit.SyncInstructions.Count);

            // an empty folder (UidNext is 1, i.e. there's no messages at all)
            TestFolder = resetFolder (TestFolder);
            TestFolder = DoFakeFolderOpen (TestFolder, 1);
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.Null (syncKit); // no synckit. Nothing to do.

            // The next few tests simulate a folder with a bunch of messages in it.
            // This is the first sync, after we've discovered 501 as the UidNext value.
            TestFolder = resetFolder (TestFolder);
            TestFolder = DoFakeFolderOpen (TestFolder, 501);
            var uidSet = SyncKit.MustUniqueIdSet (new UniqueIdRange (new UniqueId (500), new UniqueId (400)));
            var syncInst = ImapStrategy.SyncInstructionForNewMails (ref protocolState, uidSet);
            syncKit = new SyncKit(TestFolder, new List<SyncInstruction> () { syncInst });
            TestFolder = DoFakeSync (TestFolder, syncKit); // creates emails 1-122
            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.ImapSyncRung = 2; // we're no longer in the initial sync
                return true;
            });

            uint expected_max = 399;
            uint expected_min = 0;
            for (var i = 0; i < 10; i++) {
                expected_min = expected_max - defaultSpan + 1;
                syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
                Assert.NotNull (syncKit);
                Assert.NotNull (syncKit.SyncInstructions);
                Assert.AreEqual (1, syncKit.SyncInstructions.Count);
                syncInst = syncKit.SyncInstructions.First ();
                Assert.AreEqual (defaultSpan, syncInst.UidSet.Count);
                Assert.AreEqual (expected_max, syncKit.MaxSynced);
                Assert.AreEqual (expected_min, syncKit.MinSynced);
                TestFolder = DoFakeSync (TestFolder, syncKit);
                expected_max = expected_min - 1;
            }

            Assert.AreEqual (expected_min, TestFolder.ImapUidLowestUidSynced);

            // Now let's assume the sync was reset for some reason. Let's add another few messages
            uint newMessages = 2;
            TestFolder = DoFakeFolderOpen (TestFolder, TestFolder.ImapUidNext+newMessages);
            var testFolder = TestFolder;
            ImapStrategy.resetLastSyncPoint (ref testFolder);
            TestFolder = testFolder;
            expected_max = 502;
            expected_min = expected_max - quickSyncSpan + 1;

            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.AreEqual (SyncKit.MethodEnum.FastSync, syncKit.Method);
            Assert.NotNull (syncKit.SyncInstructions);
            Assert.AreEqual (0, syncKit.SyncInstructions.Count);
            ImapStrategy.FillInQuickSyncKit (ref protocolState, ref syncKit, TestFolder.AccountId);
            Assert.AreEqual (2, syncKit.SyncInstructions.Count); // 1 for new mails, 1 for resyncs

            // check the new mails
            syncInst = syncKit.SyncInstructions[0];
            Assert.NotNull (syncInst.UidSet);
            Assert.AreEqual (newMessages, syncInst.UidSet.Count);
            Assert.AreEqual (expected_max, syncInst.UidSet.Max ().Id);
            Assert.AreEqual (expected_max - newMessages + 1, syncInst.UidSet.Min ().Id);

            Assert.AreEqual (quickSyncSpan, syncKit.CombinedUidSet.Count);
            Assert.AreEqual (expected_max, syncKit.MaxSynced);
            Assert.AreEqual (expected_min, syncKit.MinSynced);

            TestFolder = DoFakeSync (TestFolder, syncKit);

            expected_max = expected_min - 1;
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.NotNull (syncKit.SyncInstructions);
            Assert.AreEqual (2, syncKit.SyncInstructions.Count);
            syncInst = syncKit.SyncInstructions [0];
            Assert.AreEqual (expected_max - TestFolder.ImapUidLowestUidSynced + 1, syncInst.UidSet.Count);
            Assert.AreEqual (expected_max, syncInst.UidSet.Max ().Id);
            Assert.AreEqual (TestFolder.ImapUidLowestUidSynced, syncInst.UidSet.Min ().Id);
            uint usedSpan = (uint)(syncInst.UidSet.Count / ImapStrategy.KResyncMultiplier);
            var leftSpanSlots = defaultSpan - usedSpan;

            syncInst = syncKit.SyncInstructions [1];
            Assert.AreEqual (leftSpanSlots, syncInst.UidSet.Count);
            Assert.AreEqual (TestFolder.ImapUidLowestUidSynced - 1, syncInst.UidSet.Max ().Id);
            Assert.AreEqual (TestFolder.ImapUidLowestUidSynced - leftSpanSlots, syncInst.UidSet.Min ().Id);

            Assert.AreEqual (expected_max, syncKit.MaxSynced);
            Assert.AreEqual (TestFolder.ImapUidLowestUidSynced - leftSpanSlots, syncKit.MinSynced);

            expected_max = TestFolder.ImapUidLowestUidSynced - leftSpanSlots - 1;
            expected_min = expected_max - defaultSpan + 1;

            TestFolder = DoFakeSync (TestFolder, syncKit);

            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.NotNull (syncKit.SyncInstructions);
            Assert.AreEqual (1, syncKit.SyncInstructions.Count);
            syncInst = syncKit.SyncInstructions.First ();
            Assert.AreEqual (defaultSpan, syncInst.UidSet.Count);
            Assert.AreEqual (expected_max, syncInst.UidSet.Max ().Id);
            Assert.AreEqual (expected_min, syncInst.UidSet.Min ().Id);
            TestFolder = DoFakeSync (TestFolder, syncKit);

            expected_max = expected_min - 1;
            expected_min = expected_max - defaultSpan + 1;

            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.NotNull (syncKit.SyncInstructions);
            Assert.AreEqual (1, syncKit.SyncInstructions.Count);
            syncInst = syncKit.SyncInstructions.First ();
            Assert.AreEqual (defaultSpan, syncInst.UidSet.Count);
            Assert.AreEqual (expected_max, syncInst.UidSet.Max ().Id);
            Assert.AreEqual (expected_min, syncInst.UidSet.Min ().Id);
            TestFolder = DoFakeSync (TestFolder, syncKit);

            expected_max = expected_min - 1;
            Assert.IsTrue (defaultSpan > expected_max);
            expected_min = 1;

            // less than 30 items are left, so the span should be "the rest"
            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.NotNull (syncKit.SyncInstructions);
            Assert.AreEqual (1, syncKit.SyncInstructions.Count);
            syncInst = syncKit.SyncInstructions.First ();
            Assert.AreEqual (expected_max, syncInst.UidSet.Count);
            Assert.AreEqual (expected_max, syncInst.UidSet.Max ().Id);
            Assert.AreEqual (expected_min, syncInst.UidSet.Min ().Id);
            TestFolder = DoFakeSync (TestFolder, syncKit);

            // Let's add another few messages
            newMessages = 50;
            TestFolder = DoFakeFolderOpen (TestFolder, TestFolder.ImapUidNext+newMessages);
            testFolder = TestFolder;
            ImapStrategy.resetLastSyncPoint (ref testFolder);
            TestFolder = testFolder;
            expected_max = 552;
            expected_min = expected_max - quickSyncSpan + 1;

            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.AreEqual (SyncKit.MethodEnum.FastSync, syncKit.Method);
            Assert.NotNull (syncKit.SyncInstructions);
            Assert.AreEqual (0, syncKit.SyncInstructions.Count);
            ImapStrategy.FillInQuickSyncKit (ref protocolState, ref syncKit, TestFolder.AccountId);
            Assert.AreEqual (1, syncKit.SyncInstructions.Count); // 1 for new mails
            syncInst = syncKit.SyncInstructions.First ();
            Assert.AreEqual (quickSyncSpan, syncInst.UidSet.Count);
            Assert.AreEqual (expected_max, syncInst.UidSet.Max ().Id);
            Assert.AreEqual (expected_min, syncInst.UidSet.Min ().Id);
            TestFolder = DoFakeSync (TestFolder, syncKit);
            newMessages -= (uint)syncInst.UidSet.Count;
            expected_max = expected_min - 1;
            expected_min = expected_max - defaultSpan + 1;

            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.NotNull (syncKit.SyncInstructions);
            Assert.AreEqual (1, syncKit.SyncInstructions.Count);
            syncInst = syncKit.SyncInstructions.First ();
            Assert.AreEqual (defaultSpan, syncInst.UidSet.Count);
            Assert.AreEqual (expected_max, syncInst.UidSet.Max ().Id);
            Assert.AreEqual (expected_min, syncInst.UidSet.Min ().Id);
            TestFolder = DoFakeSync (TestFolder, syncKit);
            newMessages -= (uint)syncInst.UidSet.Count;

            expected_max = expected_min - 1;
            expected_min = expected_max - newMessages + 1;

            syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            Assert.NotNull (syncKit);
            Assert.NotNull (syncKit.SyncInstructions);
            Assert.AreEqual (2, syncKit.SyncInstructions.Count);

            syncInst = syncKit.SyncInstructions[0];
            Assert.AreEqual (newMessages, syncInst.UidSet.Count);
            Assert.AreEqual (expected_max, syncInst.UidSet.Max ().Id);
            Assert.AreEqual (expected_min, syncInst.UidSet.Min ().Id);

            expected_max = expected_min - 1;
            expected_min = 1;

            syncInst = syncKit.SyncInstructions[1];
            Assert.AreEqual (expected_max, syncInst.UidSet.Count);
            Assert.AreEqual (expected_max, syncInst.UidSet.Max ().Id);
            Assert.AreEqual (expected_min, syncInst.UidSet.Min ().Id);
        }

        [Test]
        public void TestSyncHoleAtTop ()
        {
            // This is a bug that pops up periodically, so let's make sure we test for it:
            // If there's a 'hole' at the top of the sync range, we tend to sync forever.
            // Conditions:
            // ImapUidNext = X, but there's no messages there for at least 1 entry, i.e
            // There were new messages at one point, but they got deleted from the mailbox.
            // ImapUidHighestUidSynced = X -Y, where Y > 1, and no matter how often we try,
            // We will never make ImapUidHighestUidSynced == ImapUidNext, because there's no messages
            // to sync.
            // 
            var protocolState = ProtocolState;
            TestFolder = resetFolder (TestFolder);

            // UidNext 100 means the highest Uid that HAS existed in the folder is 99.
            // Since HighestSynced is 97, that means there's two messages that apparently got instantiated
            // and then deleted: 98, and 99.
            uint UidNext = 100;
            uint HighestSynced = 97;

            MakeFakeEmails (TestFolder.Id, 1, HighestSynced);
            var expected_count = HighestSynced + 1; // add one because we'll add a fake Uid for 99.
            var expected_max = 99;
            var expected_min = 1;
            TestFolder = DoFakeFolderOpen (TestFolder, UidNext, DateTime.UtcNow.AddMinutes (-(6*60)));
            TestFolder = TestFolder.UpdateWithOCApply<McFolder> (((record) => {
                var target = (McFolder)record;
                target.ImapUidHighestUidSynced = HighestSynced;
                target.ImapUidSet = string.Format ("{0}:{1}", 1, HighestSynced);
                return true;
            }));
            var syncInstList = ImapStrategy.SyncInstructions (TestFolder, ref protocolState, 10, false);
            Assert.NotNull (syncInstList);
            Assert.AreEqual (1, syncInstList.Count); // since there's existing emails, and a new email, there will be 2 instructions
            var syncInst = syncInstList.First ();
            Assert.AreEqual (expected_count, syncInst.UidSet.Count);
            Assert.AreEqual (expected_max, syncInst.UidSet.Max ().Id);
            Assert.AreEqual (expected_min, syncInst.UidSet.Min ().Id);
            Assert.IsFalse (syncInst.UidSet.Contains (new UniqueId (98)));
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

        private McFolder DoFakeSync(McFolder testFolder, SyncKit syncKit)
        {
            Assert.IsTrue (syncKit.MinSynced.HasValue);
            Assert.IsTrue (syncKit.MaxSynced.HasValue);
            MakeFakeEmails (testFolder.Id, syncKit.MinSynced.Value, syncKit.MaxSynced.Value);
            return testFolder.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.ImapUidHighestUidSynced = Math.Max (target.ImapUidHighestUidSynced, syncKit.MaxSynced.Value);
                target.ImapUidLowestUidSynced = Math.Min (target.ImapUidLowestUidSynced, syncKit.MinSynced.Value);
                target.ImapLastUidSynced = syncKit.MinSynced.Value;
                target.ImapLastExamine = DateTime.UtcNow;
                return true;
            });
        }

        private void MakeFakeEmails(int folderId, uint min, uint max)
        {
            McEmailMessage emailMessage;
            NcModel.Instance.RunInTransaction (() => {
                for (var id = min; id <= max; id++) {
                    var ServerId = string.Format ("{0}:{1}", folderId, id);
                    emailMessage = McEmailMessage.QueryByServerId<McEmailMessage> (Account.Id, ServerId);
                    if (null == emailMessage) {
                        emailMessage = new McEmailMessage () {
                            AccountId = Account.Id,
                            From = "test@example.com",
                            ServerId = ServerId,
                            IsIncomplete = true,
                            ImapUid = id,
                        };
                        emailMessage.Insert ();
                        var map = new McMapFolderFolderEntry (Account.Id) {
                            AccountId = Account.Id,
                            FolderId = folderId,
                            FolderEntryId = emailMessage.Id,
                            ClassCode = McAbstrFolderEntry.ClassCodeEnum.Email,
                            AsSyncEpoch = 1,
                        };
                        map.Insert ();
                    }
                }
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
                target.ImapLastExamine = DateTime.MinValue;
                return true;
            });

        }
    }
}

