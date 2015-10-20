//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;
using Test.Common;

namespace Test.iOS
{
    [TestFixture]
    public class AsStrategyTest : NcTestBase
    {
        private AsStrategy Strategy;
        private McAccount Account;

        [SetUp]
        public new void SetUp ()
        {
            base.SetUp ();
            Account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            Account.Insert ();
            var context = new MockContext (Account);
            Strategy = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
        }

        // Note: many of these tests rely on the underlying Ladder definition.
        [Test]
        public void TestScopeFlagIsSet ()
        {
            Assert.False (AsStrategy.Scope.FlagIsSet (0, AsStrategy.Scope.FlagEnum.RicSynced));
            Assert.True (AsStrategy.Scope.FlagIsSet (1, AsStrategy.Scope.FlagEnum.RicSynced));
            Assert.True (AsStrategy.Scope.FlagIsSet (2, AsStrategy.Scope.FlagEnum.RicSynced));
            for (int i = 3; i < AsStrategy.Scope.Ladder.GetLength (0); ++i) {
                Assert.True (AsStrategy.Scope.FlagIsSet (i, AsStrategy.Scope.FlagEnum.RicSynced));
                Assert.True (AsStrategy.Scope.FlagIsSet (i, AsStrategy.Scope.FlagEnum.NarrowSyncOk));
            }
        }

        [Test]
        public void TestScopeRequiredToAdvance ()
        {
            List<AsStrategy.Scope.ItemType> val;
            val = AsStrategy.Scope.RequiredToAdvance (0);
            Assert.AreEqual (1, val.Count);
            Assert.True (val.Contains (AsStrategy.Scope.ItemType.Contact));
            val = AsStrategy.Scope.RequiredToAdvance (1);
            Assert.AreEqual (1, val.Count);
            Assert.True (val.Contains (AsStrategy.Scope.ItemType.Email));
            val = AsStrategy.Scope.RequiredToAdvance (4);
            Assert.AreEqual (2, val.Count);
            Assert.True (val.Contains (AsStrategy.Scope.ItemType.Email));
            Assert.True (val.Contains (AsStrategy.Scope.ItemType.Cal));
            val = AsStrategy.Scope.RequiredToAdvance (5);
            Assert.AreEqual (2, val.Count);
            Assert.True (val.Contains (AsStrategy.Scope.ItemType.Cal));
            Assert.True (val.Contains (AsStrategy.Scope.ItemType.Contact));
        }

        [Test]
        public void TestEmailScope ()
        {
            Assert.AreEqual (AsStrategy.Scope.EmailEnum.Def1w, AsStrategy.Scope.EmailScope (3));
        }

        [Test]
        public void TestCalScope ()
        {
            Assert.AreEqual (AsStrategy.Scope.CalEnum.Def2w, AsStrategy.Scope.CalScope (3));
        }

        [Test]
        public void TestContactScope ()
        {
            Assert.AreEqual (AsStrategy.Scope.ContactEnum.RicInf, AsStrategy.Scope.ContactScope (3));
        }

        [Test]
        public void TestMaxRung ()
        {
            Assert.AreEqual (8, AsStrategy.Scope.MaxRung ());
        }

        [Test]
        public void TestEmailFolderListProvider ()
        {
            McFolder folder;
            List<McFolder> result;
            // Check missing folder scenarios.
            result = Strategy.EmailFolderListProvider (AsStrategy.Scope.EmailEnum.None, true);
            Assert.AreEqual (0, result.Count);
            result = Strategy.EmailFolderListProvider (AsStrategy.Scope.EmailEnum.None, false);
            Assert.AreEqual (0, result.Count);
            result = Strategy.EmailFolderListProvider (AsStrategy.Scope.EmailEnum.Def2w, true);
            Assert.AreEqual (0, result.Count);
            result = Strategy.EmailFolderListProvider (AsStrategy.Scope.EmailEnum.Def2w, false);
            Assert.AreEqual (0, result.Count);
            result = Strategy.EmailFolderListProvider (AsStrategy.Scope.EmailEnum.AllInf, true);
            Assert.AreEqual (0, result.Count);
            result = Strategy.EmailFolderListProvider (AsStrategy.Scope.EmailEnum.AllInf, false);
            Assert.AreEqual (0, result.Count);
            // Normal conditions testing.
            folder = McFolder.Create (Account.Id, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            folder.Insert ();
            folder = McFolder.Create (Account.Id, false, false, false, "0", "user", "User", Xml.FolderHierarchy.TypeCode.UserCreatedMail_12);
            folder.Insert ();
            folder = McFolder.Create (Account.Id, false, false, true, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            folder.Insert ();
            result = Strategy.EmailFolderListProvider (AsStrategy.Scope.EmailEnum.None, true);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.DefaultInbox_2, result [0].Type);
            result = Strategy.EmailFolderListProvider (AsStrategy.Scope.EmailEnum.None, false);
            Assert.AreEqual (0, result.Count);
            result = Strategy.EmailFolderListProvider (AsStrategy.Scope.EmailEnum.Def1w, false);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.DefaultInbox_2, result [0].Type);
            result = Strategy.EmailFolderListProvider (AsStrategy.Scope.EmailEnum.AllInf, true);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.DefaultInbox_2, result [0].Type);
            result = Strategy.EmailFolderListProvider (AsStrategy.Scope.EmailEnum.AllInf, false);
            Assert.AreEqual (2, result.Count);
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.UserCreatedMail_12 == x.Type));
        }

        [Test]
        public void TestCalFolderListProvider ()
        {
            McFolder folder;
            List<McFolder> result;
            // Check missing folder scenarios.
            result = Strategy.CalFolderListProvider (AsStrategy.Scope.CalEnum.None, true);
            Assert.AreEqual (0, result.Count);
            result = Strategy.CalFolderListProvider (AsStrategy.Scope.CalEnum.None, false);
            Assert.AreEqual (0, result.Count);
            result = Strategy.CalFolderListProvider (AsStrategy.Scope.CalEnum.Def2w, true);
            Assert.AreEqual (0, result.Count);
            result = Strategy.CalFolderListProvider (AsStrategy.Scope.CalEnum.Def2w, false);
            Assert.AreEqual (0, result.Count);
            result = Strategy.CalFolderListProvider (AsStrategy.Scope.CalEnum.AllInf, true);
            Assert.AreEqual (0, result.Count);
            result = Strategy.CalFolderListProvider (AsStrategy.Scope.CalEnum.AllInf, false);
            Assert.AreEqual (0, result.Count);
            // Normal conditions testing.
            folder = McFolder.Create (Account.Id, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            folder.Insert ();
            folder = McFolder.Create (Account.Id, false, false, false, "0", "user", "User", Xml.FolderHierarchy.TypeCode.UserCreatedCal_13);
            folder.Insert ();
            folder = McFolder.Create (Account.Id, false, false, true, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            folder.Insert ();
            result = Strategy.CalFolderListProvider (AsStrategy.Scope.CalEnum.None, true);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.DefaultCal_8, result [0].Type);
            result = Strategy.CalFolderListProvider (AsStrategy.Scope.CalEnum.None, false);
            Assert.AreEqual (0, result.Count);
            result = Strategy.CalFolderListProvider (AsStrategy.Scope.CalEnum.Def2w, false);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.DefaultCal_8, result [0].Type);
            result = Strategy.CalFolderListProvider (AsStrategy.Scope.CalEnum.All3m, true);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.DefaultCal_8, result [0].Type);
            result = Strategy.CalFolderListProvider (AsStrategy.Scope.CalEnum.All3m, false);
            Assert.AreEqual (2, result.Count);
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultCal_8 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.UserCreatedCal_13 == x.Type));
        }

        [Test]
        public void TestContactFolderListProvider ()
        {
            McFolder folder;
            List<McFolder> result;
            // Check missing folder scenarios.
            result = Strategy.ContactFolderListProvider (AsStrategy.Scope.ContactEnum.DefRicInf, true);
            Assert.AreEqual (0, result.Count);
            result = Strategy.ContactFolderListProvider (AsStrategy.Scope.ContactEnum.DefRicInf, false);
            Assert.AreEqual (0, result.Count);
            // Normal conditions testing.
            folder = McFolder.Create (Account.Id, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            folder.Insert ();
            folder = McFolder.Create (Account.Id, false, false, false, "0", "user", "User", Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14);
            folder.Insert ();
            folder = McFolder.Create (Account.Id, false, false, true, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
            folder.Insert ();
            folder = McFolder.Create (Account.Id, false, false, true, "0", "ric", "RIC", Xml.FolderHierarchy.TypeCode.Ric_19);
            folder.Insert ();
            result = Strategy.ContactFolderListProvider (AsStrategy.Scope.ContactEnum.None, true);
            Assert.AreEqual (0, result.Count);
            result = Strategy.ContactFolderListProvider (AsStrategy.Scope.ContactEnum.None, false);
            Assert.AreEqual (0, result.Count);
            result = Strategy.ContactFolderListProvider (AsStrategy.Scope.ContactEnum.RicInf, true);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.Ric_19, result [0].Type);
            result = Strategy.ContactFolderListProvider (AsStrategy.Scope.ContactEnum.RicInf, false);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.Ric_19, result [0].Type);
            result = Strategy.ContactFolderListProvider (AsStrategy.Scope.ContactEnum.DefRicInf, true);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.DefaultContacts_9, result [0].Type);
            result = Strategy.ContactFolderListProvider (AsStrategy.Scope.ContactEnum.DefRicInf, false);
            Assert.AreEqual (2, result.Count);
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultContacts_9 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.Ric_19 == x.Type));
            result = Strategy.ContactFolderListProvider (AsStrategy.Scope.ContactEnum.AllInf, true);
            Assert.AreEqual (1, result.Count);
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultContacts_9 == x.Type));
            result = Strategy.ContactFolderListProvider (AsStrategy.Scope.ContactEnum.AllInf, false);
            Assert.AreEqual (3, result.Count);
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultContacts_9 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.Ric_19 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14 == x.Type));
        }

        [Test]
        public void TestCanAdvance ()
        {
            bool result;
            var emailFolder = McFolder.Create (Account.Id, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            emailFolder.AsSyncMetaToClientExpected = false;
            emailFolder.Insert ();
            var calFolder = McFolder.Create (Account.Id, false, false, true, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            calFolder.AsSyncMetaToClientExpected = true;
            calFolder.Insert ();
            // Create folders.
            var conFolder = McFolder.Create (Account.Id, false, false, true, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
            conFolder.AsSyncMetaToClientExpected = false;
            conFolder.Insert ();
            var ricFolder = McFolder.Create (Account.Id, false, false, true, "0", "ric", "RIC", Xml.FolderHierarchy.TypeCode.Ric_19);
            ricFolder.AsSyncMetaToClientExpected = false;
            ricFolder.Insert ();
            result = Strategy.CanAdvance(5);
            Assert.False (result);
            calFolder.UpdateSet_AsSyncMetaToClientExpected (false);
            conFolder.UpdateSet_AsSyncMetaToClientExpected (true);
            result = Strategy.CanAdvance(5);
            Assert.False (result);
            conFolder.UpdateSet_AsSyncMetaToClientExpected (false);
            result = Strategy.CanAdvance(5);
            Assert.True (result);
            result = Strategy.CanAdvance(6);
            Assert.False (result);
            Account.DaysToSyncEmail = Xml.Provision.MaxAgeFilterCode.SyncAll_0;
            Account.Update ();
            result = Strategy.CanAdvance(6);
            Assert.True (result);
            Account.DaysToSyncEmail = Xml.Provision.MaxAgeFilterCode.OneMonth_5;
            Account.Update ();
            // Add a McPending.
            var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.ContactWriter) {
                Operation = McPending.Operations.ContactDelete,
                ParentId = conFolder.ServerId,
                ServerId = "bogus",
            };   
            pending.Insert ();
            result = Strategy.CanAdvance(5);
            Assert.False (result);
        }

        private static bool StatusIndCalled;
        private static NcResult.SubKindEnum StatusSubKind;

        private class MockProtoControl : AsProtoControl
        {
            public MockProtoControl (INcProtoControlOwner owner, int accountId) : base (owner, accountId)
            {
            }

            public override void StatusInd (NcResult status)
            {
                StatusIndCalled = true;
                StatusSubKind = status.SubKind;
            }


        }

        [Test]
        public void TestAdvanceIfPossible ()
        {
            // TODO: need AsSyncMetaToClientExpected setting sub-test.
            StatusIndCalled = false;
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            context.ProtoControl = new MockProtoControl (context.Owner, account.Id);
            context.ProtocolState = context.ProtocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.StrategyRung = 5;
                return true;
            });
            int result;
            var strat = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
            var emailFolder = McFolder.Create (account.Id, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            emailFolder.AsSyncMetaToClientExpected = false;
            emailFolder.Insert ();
            var calFolder = McFolder.Create (account.Id, false, false, true, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            calFolder.AsSyncMetaToClientExpected = true;
            calFolder.Insert ();
            // Create folders.
            var conFolder = McFolder.Create (account.Id, false, false, true, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
            conFolder.AsSyncMetaToClientExpected = false;
            conFolder.Insert ();
            var ricFolder = McFolder.Create (account.Id, false, false, true, "0", "ric", "RIC", Xml.FolderHierarchy.TypeCode.Ric_19);
            ricFolder.AsSyncMetaToClientExpected = false;
            ricFolder.Insert ();
            result = strat.AdvanceIfPossible (context.ProtocolState.StrategyRung);
            Assert.AreEqual (context.ProtocolState.StrategyRung, result);
            calFolder.UpdateSet_AsSyncMetaToClientExpected (false);
            var oldRung = context.ProtocolState.StrategyRung;
            result = strat.AdvanceIfPossible (context.ProtocolState.StrategyRung);
            Assert.AreEqual (oldRung + 1, result);
            Assert.AreEqual (context.ProtocolState.StrategyRung, result);
            var verify = McProtocolState.QueryById<McProtocolState> (context.ProtocolState.Id);
            Assert.AreEqual (oldRung + 1, verify.StrategyRung);
            Assert.True (StatusIndCalled);
            Assert.AreEqual (NcResult.SubKindEnum.Info_RicInitialSyncCompleted, StatusSubKind);
        }

        [Test]
        public void TestFolderListProvider ()
        {
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            McFolder folder;
            List<McFolder> result;
            var strat = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
            // Test folders missing scenario.
            result = strat.FolderListProvider (6, true);
            Assert.AreEqual (0, result.Count);
            // Normal conditions testing.
            folder = McFolder.Create (account.Id, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, false, "0", "useremail", "UserEmail", Xml.FolderHierarchy.TypeCode.UserCreatedMail_12);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, false, "0", "usercal", "UserCal", Xml.FolderHierarchy.TypeCode.UserCreatedCal_13);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, true, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, false, "0", "usercontacts", "UserContacts", Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, true, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, true, "0", "ric", "RIC", Xml.FolderHierarchy.TypeCode.Ric_19);
            folder.Insert ();
            result = strat.FolderListProvider (6, true);
            Assert.AreEqual (3, result.Count);
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultCal_8 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultContacts_9 == x.Type));
            result = strat.FolderListProvider (6, false);
            Assert.AreEqual (7, result.Count);
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultCal_8 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultContacts_9 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.UserCreatedMail_12 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.UserCreatedCal_13 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.Ric_19 == x.Type));
        }

        [Test]
        public void TestEmailParametersProvider ()
        {
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            var strat = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
            var folder = McFolder.Create (account.Id, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            folder.Insert ();
            var result = strat.EmailParametersProvider (folder, AsStrategy.Scope.EmailEnum.None, true, 50);
            var code = result.Item1;
            var winSize = result.Item2;
            Assert.AreEqual (code, Xml.Provision.MaxAgeFilterCode.OneDay_1);
            Assert.AreEqual (winSize, 50);
            result = strat.EmailParametersProvider (folder, AsStrategy.Scope.EmailEnum.AllInf, false, 54);
            code = result.Item1;
            winSize = result.Item2;
            Assert.AreEqual (code, Xml.Provision.MaxAgeFilterCode.SyncAll_0);
            Assert.AreEqual (winSize, 54);
        }

        [Test]
        public void CalEmailParametersProvider ()
        {
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            var strat = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
            var folder = McFolder.Create (account.Id, false, false, true, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            folder.Insert ();
            var result = strat.CalParametersProvider (folder, AsStrategy.Scope.CalEnum.None, true, 50);
            var code = result.Item1;
            var winSize = result.Item2;
            Assert.AreEqual (code, Xml.Provision.MaxAgeFilterCode.TwoWeeks_4);
            Assert.AreEqual (winSize, 50);
            result = strat.CalParametersProvider (folder, AsStrategy.Scope.CalEnum.All6m, false, 51);
            code = result.Item1;
            winSize = result.Item2;
            Assert.AreEqual (code, Xml.Provision.MaxAgeFilterCode.SixMonths_7);
            Assert.AreEqual (winSize, 51);
        }

        [Test]
        public void ContactEmailParametersProvider ()
        {

            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            var strat = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
            var folder = McFolder.Create (account.Id, false, false, true, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
            folder.Insert ();
            var result = strat.ContactParametersProvider (folder, AsStrategy.Scope.ContactEnum.None, true, 50);
            var code = result.Item1;
            var winSize = result.Item2;
            Assert.AreEqual (code, Xml.Provision.MaxAgeFilterCode.SyncAll_0);
            Assert.AreEqual (winSize, 50);
            result = strat.ContactParametersProvider (folder, AsStrategy.Scope.ContactEnum.RicInf, false, 50);
            code = result.Item1;
            winSize = result.Item2;
            Assert.AreEqual (code, Xml.Provision.MaxAgeFilterCode.SyncAll_0);
            Assert.AreEqual (winSize, 50);
        }

        [Test]
        public void ParametersProvider ()
        {
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            var strat = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
            var emailFolder = McFolder.Create (account.Id, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            emailFolder.Insert ();
            var calFolder = McFolder.Create (account.Id, false, false, true, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            calFolder.Insert ();
            var contactFolder = McFolder.Create (account.Id, false, false, true, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
            contactFolder.Insert ();
            var result = strat.ParametersProvider (emailFolder, 0, true);
            var code = result.Item1;
            var winSize = result.Item2;
            Assert.AreEqual (code, Xml.Provision.MaxAgeFilterCode.OneDay_1);
            Assert.AreEqual (winSize, AsStrategy.KBasePerFolderWindowSize * 3);
            result = strat.ParametersProvider (emailFolder, 2, false);
            code = result.Item1;
            winSize = result.Item2;
            Assert.AreEqual (code, Xml.Provision.MaxAgeFilterCode.ThreeDays_2);
            Assert.AreEqual (winSize, AsStrategy.KBasePerFolderWindowSize * 3);
            result = strat.ParametersProvider (calFolder, 0, true);
            code = result.Item1;
            winSize = result.Item2;
            Assert.AreEqual (code, Xml.Provision.MaxAgeFilterCode.TwoWeeks_4);
            Assert.AreEqual (winSize, AsStrategy.KBasePerFolderWindowSize * 3);
            result = strat.ParametersProvider (calFolder, 2, false);
            code = result.Item1;
            winSize = result.Item2;
            Assert.AreEqual (code, Xml.Provision.MaxAgeFilterCode.TwoWeeks_4);
            Assert.AreEqual (winSize, AsStrategy.KBasePerFolderWindowSize * 3);
            result = strat.ParametersProvider (contactFolder, 0, true);
            code = result.Item1;
            winSize = result.Item2;
            Assert.AreEqual (code, Xml.Provision.MaxAgeFilterCode.SyncAll_0);
            Assert.AreEqual (winSize, AsStrategy.KBasePerFolderWindowSize * 3);
            result = strat.ParametersProvider (contactFolder, 2, false);
            code = result.Item1;
            winSize = result.Item2;
            Assert.AreEqual (code, Xml.Provision.MaxAgeFilterCode.SyncAll_0);
            Assert.AreEqual (winSize, AsStrategy.KBasePerFolderWindowSize * 3);
        }

        [Test]
        public void TestAllSyncedFolders ()
        {
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            var strat = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
            var emailFolder = McFolder.Create (account.Id, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            emailFolder.AsSyncMetaToClientExpected = true;
            emailFolder.Insert ();
            var calFolder = McFolder.Create (account.Id, false, false, true, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            calFolder.AsSyncMetaToClientExpected = false;
            calFolder.Insert ();
            // Create folders.
            var conFolder = McFolder.Create (account.Id, false, false, true, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
            conFolder.AsSyncMetaToClientExpected = false;
            conFolder.Insert ();
            var ricFolder = McFolder.Create (account.Id, false, false, true, "0", "ric", "RIC", Xml.FolderHierarchy.TypeCode.Ric_19);
            ricFolder.AsSyncMetaToClientExpected = false;
            ricFolder.Insert ();
            var jFolder = McFolder.Create (account.Id, false, false, true, "0", "journal", "J", Xml.FolderHierarchy.TypeCode.DefaultJournal_11);
            jFolder.Insert ();
            var result = strat.AllSyncedFolders ();
            Assert.AreEqual (4, result.Count);
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultCal_8 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultContacts_9 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.Ric_19 == x.Type));
            Assert.False (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultJournal_11 == x.Type));
        }

        [Test]
        public void TestGenNarrowSyncKit ()
        {
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            var strat = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
            // Test with folders missing.
            var result = strat.GenNarrowSyncKit (new List<McFolder> (), 0, 50);
            Assert.IsNull (result);
            // Normal conditions testing.
            var folder = McFolder.Create (account.Id, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, false, "0", "useremail", "UserEmail", Xml.FolderHierarchy.TypeCode.UserCreatedMail_12);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, false, "0", "usercal", "UserCal", Xml.FolderHierarchy.TypeCode.UserCreatedCal_13);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, true, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, false, "0", "usercontacts", "UserContacts", Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, true, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, true, "0", "ric", "RIC", Xml.FolderHierarchy.TypeCode.Ric_19);
            folder.Insert ();
            result = strat.GenNarrowSyncKit (strat.FolderListProvider (0, true), 0, 50);
            Assert.AreEqual (50, result.OverallWindowSize);
            Assert.AreEqual (3, result.PerFolders.Count);
            Assert.True (result.PerFolders.Any (x => Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == x.Folder.Type));
            Assert.True (result.PerFolders.Any (x => Xml.FolderHierarchy.TypeCode.DefaultCal_8 == x.Folder.Type));
            Assert.True (result.PerFolders.Any (x => Xml.FolderHierarchy.TypeCode.Ric_19 == x.Folder.Type));
            var inboxParms = result.PerFolders.Single (x => Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == x.Folder.Type);
            var calParms = result.PerFolders.Single (x => Xml.FolderHierarchy.TypeCode.DefaultCal_8 == x.Folder.Type);
            var ricParms = result.PerFolders.Single (x => Xml.FolderHierarchy.TypeCode.Ric_19 == x.Folder.Type);
            // TODO: insert pending to prove that they are ignored.
            Assert.AreEqual (0, inboxParms.Commands.Count);
            Assert.AreEqual (0, calParms.Commands.Count);
            Assert.AreEqual (0, ricParms.Commands.Count);
            Assert.AreEqual (inboxParms.WindowSize, AsStrategy.KBasePerFolderWindowSize * 3);
            Assert.AreEqual (calParms.WindowSize, AsStrategy.KBasePerFolderWindowSize * 3);
            Assert.AreEqual (ricParms.WindowSize, AsStrategy.KBasePerFolderWindowSize * 3);
            Assert.AreEqual (inboxParms.FilterCode, Xml.Provision.MaxAgeFilterCode.OneDay_1);
            Assert.AreEqual (calParms.FilterCode, Xml.Provision.MaxAgeFilterCode.TwoWeeks_4);
            Assert.AreEqual (ricParms.FilterCode, Xml.Provision.MaxAgeFilterCode.SyncAll_0);
            Assert.True (inboxParms.GetChanges);
            Assert.True (calParms.GetChanges);
            Assert.True (ricParms.GetChanges);
            var check = McFolder.QueryById<McFolder> (inboxParms.Folder.Id);
            Assert.True (check.AsSyncMetaToClientExpected);
            check = McFolder.QueryById<McFolder> (calParms.Folder.Id);
            Assert.True (check.AsSyncMetaToClientExpected);
        }

        [Test]
        public void TestNarrowFoldersNoToClientExpected ()
        {
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            var strat = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
            var result = strat.ANarrowFolderHasToClientExpected ();
            Assert.IsFalse (result);
            var folder = McFolder.Create (account.Id, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            folder.Insert ();
            result = strat.ANarrowFolderHasToClientExpected ();
            Assert.IsFalse (result);
            folder = McFolder.Create (account.Id, false, false, true, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            folder.AsSyncMetaToClientExpected = true;
            folder.Insert ();
            result = strat.ANarrowFolderHasToClientExpected ();
            Assert.True (result);
            folder.UpdateSet_AsSyncMetaToClientExpected (false);
            result = strat.ANarrowFolderHasToClientExpected ();
            Assert.False (result);
            folder.Delete ();
            result = strat.ANarrowFolderHasToClientExpected ();
            Assert.False (result);
        }

        [Test]
        public void TestGenPingKit ()
        {
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            McFolder folder;
            var strat = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
            var result = strat.GenPingKit (context.ProtocolState, true, false, false);
            Assert.IsNull (result);
            result = strat.GenPingKit (context.ProtocolState, false, false, false);
            Assert.IsNull (result);
            var inbox = McFolder.Create (account.Id, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            inbox.AsSyncLastPing = DateTime.UtcNow;
            inbox.Insert ();
            folder = McFolder.Create (account.Id, false, false, false, "0", "useremail", "UserEmail", Xml.FolderHierarchy.TypeCode.UserCreatedMail_12);
            folder.AsSyncLastPing = DateTime.UtcNow.AddDays (-7);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, false, "0", "usercal", "UserCal", Xml.FolderHierarchy.TypeCode.UserCreatedCal_13);
            folder.AsSyncLastPing = DateTime.UtcNow.AddDays (-3);
            folder.Insert ();
            var cal = McFolder.Create (account.Id, false, false, true, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            cal.AsSyncLastPing = DateTime.UtcNow;
            cal.Insert ();
            folder = McFolder.Create (account.Id, false, false, false, "0", "usercontacts", "UserContacts", Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14);
            folder.AsSyncLastPing = DateTime.UtcNow.AddDays (-3);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, true, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
            folder.AsSyncLastPing = DateTime.UtcNow.AddDays (-3);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, true, "0", "ric", "RIC", Xml.FolderHierarchy.TypeCode.Ric_19);
            folder.AsSyncLastPing = DateTime.UtcNow.AddDays (-3);
            folder.Insert ();
            context.ProtocolState = context.ProtocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.StrategyRung = 6;
                target.MaxFolders = 7;
                return true;
            });
            result = strat.GenPingKit (context.ProtocolState, true, false, false);
            Assert.AreEqual (3, result.Folders.Count);
            Assert.True (result.Folders.Any (x => Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == x.Type));
            Assert.True (result.Folders.Any (x => Xml.FolderHierarchy.TypeCode.DefaultCal_8 == x.Type));
            inbox.UpdateSet_AsSyncMetaToClientExpected (true);
            result = strat.GenPingKit (context.ProtocolState, true, false, false);
            Assert.IsNull (result);
            result = strat.GenPingKit (context.ProtocolState, false, false, false);
            Assert.IsNull (result);
            inbox.UpdateSet_AsSyncMetaToClientExpected (false);
            result = strat.GenPingKit (context.ProtocolState, false, false, false);
            Assert.AreEqual (7, result.Folders.Count);
            context.ProtocolState = context.ProtocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.MaxFolders = 3;
                return true;
            });
            result = strat.GenPingKit (context.ProtocolState, false, false, false);
            Assert.AreEqual (3, result.Folders.Count);
            Assert.True (result.Folders.Any (x => Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == x.Type));
            Assert.True (result.Folders.Any (x => Xml.FolderHierarchy.TypeCode.DefaultCal_8 == x.Type));
            Assert.True (result.Folders.Any (x => Xml.FolderHierarchy.TypeCode.UserCreatedMail_12 == x.Type));
        }

        private List<McEmailMessage> Fetch_Emails;
        private List<McAttachment> Fetch_Atts;
        private McFolder Fetch_Folder;

        private void Fetch_InjectEmails (int accountId, int count)
        {
            if (null == Fetch_Folder) {
                Fetch_Folder = McFolder.Create (accountId, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
                Fetch_Folder.Insert ();
            }
            Fetch_Emails = new List<McEmailMessage> ();
            for (int i = 0; i < count; i++) {
                var body = new McBody () {
                    AccountId = accountId,
                    FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
                };
                body.Insert ();
                var email = new McEmailMessage () {
                    AccountId = accountId,
                    ServerId = string.Format ("email{0}", i),
                    IsAwaitingDelete = false,
                    Score = 0.71,
                    BodyId = body.Id,
                    DateReceived = DateTime.UtcNow.AddDays (-2),
                };
                email.Insert ();
                Fetch_Folder.Link (email);
                Fetch_Emails.Add (email);
            }
        }

        private void Fetch_DeleteEmails (int accountId)
        {
            foreach (var email in Fetch_Emails) {
                Fetch_Folder.Unlink (email);
                email.Delete ();
            }
            Fetch_Emails = null;
            Fetch_Folder.Delete ();
            Fetch_Folder = null;
        }

        private void Fetch_InjectAtts (int accountId, int count)
        {
            if (null == Fetch_Folder) {
                Fetch_Folder = McFolder.Create (accountId, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
                Fetch_Folder.Insert ();
            }
            if (null == Fetch_Emails) {
                Fetch_Emails = new List<McEmailMessage> ();
            }
            Fetch_Atts = new List<McAttachment> ();
            for (int i = 0; i < count; i++) {
                var body = McBody.InsertFile (accountId, McAbstrFileDesc.BodyTypeEnum.PlainText_1, "foo");
                var email = new McEmailMessage () {
                    AccountId = accountId,
                    ServerId = string.Format ("dummy{0}", i),
                    IsAwaitingDelete = false,
                    Score = 0.91,
                    DateReceived = DateTime.UtcNow.AddDays (-2),
                    BodyId = body.Id,
                };
                email.Insert ();
                var att = new McAttachment () {
                    AccountId = email.AccountId,
                    FilePresenceFraction = 0,
                    FileSize = 50000,
                    FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                    FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
                };
                att.Insert ();
                att.Link (email);
                Fetch_Atts.Add (att);
                Fetch_Folder.Link (email);
                Fetch_Emails.Add (email);
            }
        }

        private void Fetch_DeleteAtts (int accountId)
        {
            foreach (var att in Fetch_Atts) {
                if (null != Fetch_Emails) {
                    var attemails = McAttachment.QueryItems (accountId, att.Id).Where (x => x is McEmailMessage);
                    var email = (from fetch_e in Fetch_Emails
                        join att_e in attemails
                        on fetch_e.Id equals att_e.Id
                        select fetch_e).SingleOrDefault ();
                    if (null != email) {
                        Fetch_Folder.Unlink (email);
                        email.Delete ();
                        Fetch_Emails.Remove (email);
                    }
                }
                att.Delete ();
            }
            Fetch_Atts = null;
        }

        [Test]
        public void TestGenFetchKit ()
        {
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            var strat = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
            var result = strat.GenFetchKit ();
            Assert.IsNull (result);

            Fetch_InjectEmails (account.Id, 11);
            result = strat.GenFetchKit ();
            Assert.AreEqual (0, result.FetchAttachments.Count);
            Assert.AreEqual (AsStrategy.KBaseFetchSize, result.FetchBodies.Count);
            Assert.NotNull (result.Pendings);
            Assert.AreEqual (0, result.Pendings.Count);
            Fetch_DeleteEmails (account.Id);

            Fetch_InjectAtts (account.Id, 11);
            result = strat.GenFetchKit ();
            Assert.AreEqual (AsStrategy.KBaseFetchSize, result.FetchAttachments.Count);
            Assert.AreEqual (0, result.FetchBodies.Count);
            Assert.AreEqual (0, result.Pendings.Count);
            Fetch_DeleteAtts (account.Id);

            Fetch_InjectEmails (account.Id, AsStrategy.KBaseFetchSize / 2);
            Fetch_InjectAtts (account.Id, AsStrategy.KBaseFetchSize / 2);
            result = strat.GenFetchKit ();
            Assert.AreEqual (AsStrategy.KBaseFetchSize / 2, result.FetchAttachments.Count);
            Assert.AreEqual (AsStrategy.KBaseFetchSize / 2, result.FetchBodies.Count);
            Assert.AreEqual (0, result.Pendings.Count);
            Fetch_DeleteEmails (account.Id);
            Fetch_DeleteAtts (account.Id);
        }

        [Test]
        public void TestGenSyncKit ()
        {
            var serverIdGen = 1; // Mock server id to make pending insert happy.
            var folders = new List<McFolder> ();
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            context.ProtocolState = context.ProtocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.StrategyRung = 6;
                target.AsSyncLimit = 5;
                return true;
            });
            var dummy = new McPending (account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
                Operation = McPending.Operations.Sync,
                ServerId = "bogus",
            };
            dummy.Insert ();
            var strat = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
            var result = strat.GenSyncKit (context.ProtocolState, AsStrategy.SyncMode.Directed, dummy);
            Assert.IsNull (result);
            result = strat.GenSyncKit (context.ProtocolState, AsStrategy.SyncMode.Narrow);
            Assert.IsNull (result);
            result = strat.GenSyncKit (context.ProtocolState, AsStrategy.SyncMode.Wide);
            Assert.IsNull (result);
            var inbox = McFolder.Create (account.Id, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            inbox.AsSyncKey = "1";
            inbox.Insert ();
            folders.Add (inbox);
            var cal = McFolder.Create (account.Id, false, false, true, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            cal.AsSyncKey = "1";
            cal.Insert ();
            folders.Add (cal);
            var useremail = McFolder.Create (account.Id, false, false, false, "0", "useremail", "UserEmail", Xml.FolderHierarchy.TypeCode.UserCreatedMail_12);
            useremail.AsSyncKey = "1";
            useremail.Insert ();
            folders.Add (useremail);
            var contact = McFolder.Create (account.Id, false, false, true, "0", "contact", "Contact", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
            contact.AsSyncKey = "1";
            contact.Insert ();
            folders.Add (contact);
            var folder = McFolder.Create (account.Id, false, false, true, "0", "ric", "RIC", Xml.FolderHierarchy.TypeCode.Ric_19);
            folder.AsSyncKey = "1";
            folder.Insert ();
            folders.Add (folder);

            // Test of null result.
            result = strat.GenSyncKit (context.ProtocolState, AsStrategy.SyncMode.Wide);
            Assert.IsNull (result);

            // Test of narrow.
            result = strat.GenSyncKit (context.ProtocolState, AsStrategy.SyncMode.Narrow);
            Assert.AreEqual (3, result.PerFolders.Count);
            Assert.True (result.PerFolders.Any (x => Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == x.Folder.Type));
            Assert.True (result.PerFolders.Any (x => Xml.FolderHierarchy.TypeCode.DefaultCal_8 == x.Folder.Type));
            Assert.True (result.PerFolders.Any (x => Xml.FolderHierarchy.TypeCode.DefaultContacts_9 == x.Folder.Type));
            foreach (var rst in folders) {
                rst.UpdateSet_AsSyncMetaToClientExpected (false);
            }

            // Test simple more-available case.
            folder.UpdateSet_AsSyncMetaToClientExpected (true);
            result = strat.GenSyncKit (context.ProtocolState, AsStrategy.SyncMode.Wide);
            Assert.AreEqual (1, result.PerFolders.Count);
            Assert.True (result.PerFolders.Any (x => Xml.FolderHierarchy.TypeCode.Ric_19 == x.Folder.Type));

            // Broad test.
            foreach (var rst in folders) {
                rst.UpdateSet_AsSyncMetaToClientExpected (true);
            }
            context.ProtocolState = context.ProtocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.AsSyncLimit = 4;
                return true;
            });
            // Verify inbox does not have getChanges or pending attached.
            inbox = inbox.UpdateResetSyncState ();
            var pending = new McPending () {
                Operation = McPending.Operations.EmailMarkRead,
                AccountId = account.Id,
                State = McPending.StateEnum.Eligible,
                ParentId = inbox.ServerId,
                ServerId = serverIdGen++.ToString (),
            };
            pending.Insert ();
            // Verify cal has no pending.
            cal.UpdateSet_AsSyncMetaToClientExpected (true);
            // Verify usermail has 2 pendings and no getChanges.
            useremail.UpdateSet_AsSyncMetaToClientExpected (false);
            pending = new McPending () {
                Operation = McPending.Operations.EmailMarkRead,
                AccountId = account.Id,
                State = McPending.StateEnum.Eligible,
                ParentId = useremail.ServerId,
                ServerId = serverIdGen++.ToString (),
            };
            pending.Insert ();
            pending = new McPending () {
                Operation = McPending.Operations.EmailMarkRead,
                AccountId = account.Id,
                State = McPending.StateEnum.Eligible,
                ParentId = useremail.ServerId,
                ServerId = serverIdGen++.ToString (),
            };
            pending.Insert ();
            pending = new McPending () {
                Operation = McPending.Operations.EmailBodyDownload,
                AccountId = account.Id,
                State = McPending.StateEnum.Eligible,
                ParentId = useremail.ServerId,
                ServerId = serverIdGen++.ToString (),
            };
            pending.Insert ();
            // Verify contact has 1 pending.
            pending = new McPending () {
                Operation = McPending.Operations.ContactDelete,
                AccountId = account.Id,
                State = McPending.StateEnum.Eligible,
                ParentId = contact.ServerId,
                ServerId = serverIdGen++.ToString (),
            };
            pending.Insert ();
            result = strat.GenSyncKit (context.ProtocolState, AsStrategy.SyncMode.Wide);
            Assert.AreEqual (4, result.PerFolders.Count);
            var pfInbox = result.PerFolders.Single (x => "inbox" == x.Folder.ServerId);
            Assert.False (pfInbox.GetChanges);
            Assert.AreEqual (0, pfInbox.Commands.Count);
            var pfCal = result.PerFolders.Single (x => "cal" == x.Folder.ServerId);
            Assert.True (pfCal.GetChanges);
            Assert.True (AsStrategy.KBasePerFolderWindowSize <= pfCal.WindowSize);
            Assert.AreEqual (Xml.Provision.MaxAgeFilterCode.ThreeMonths_6, pfCal.FilterCode);
            var pfUsermail = result.PerFolders.Single (x => "useremail" == x.Folder.ServerId);
            Assert.False (pfUsermail.GetChanges);
            Assert.AreEqual (2, pfUsermail.Commands.Count);
            Assert.AreEqual (2, pfUsermail.Commands.Count (
                x => useremail.ServerId == x.ParentId &&
                McPending.Operations.EmailMarkRead == x.Operation));
            var pfContact = result.PerFolders.Single (x => "contact" == x.Folder.ServerId);
            Assert.AreEqual (0, pfContact.Commands.Count);
            contact.UpdateSet_AsSyncMetaToClientExpected (false);
            result = strat.GenSyncKit (context.ProtocolState, AsStrategy.SyncMode.Wide);
            pfContact = result.PerFolders.Single (x => "contact" == x.Folder.ServerId);
            Assert.AreEqual (1, pfContact.Commands.Count);
            Assert.AreEqual (1, pfContact.Commands.Count (
                x => contact.ServerId == x.ParentId &&
                McPending.Operations.ContactDelete == x.Operation));
            foreach (var pend in McPending.Query (account.Id)) {
                pend.Delete ();
            }

            // normal, single-issue, normal => 2 pendings off useremail.
            pending = new McPending () {
                Operation = McPending.Operations.EmailMarkRead,
                AccountId = account.Id,
                State = McPending.StateEnum.Eligible,
                ParentId = useremail.ServerId,
                ServerId = serverIdGen++.ToString (),
            };
            pending.Insert ();
            pending = new McPending () {
                Operation = McPending.Operations.EmailMarkRead,
                AccountId = account.Id,
                State = McPending.StateEnum.Eligible,
                ParentId = useremail.ServerId,
                ServerId = serverIdGen++.ToString (),
                DeferredSerialIssueOnly = true,
            };
            pending.Insert ();
            pending = new McPending () {
                Operation = McPending.Operations.EmailMarkRead,
                AccountId = account.Id,
                State = McPending.StateEnum.Eligible,
                ParentId = useremail.ServerId,
                ServerId = serverIdGen++.ToString (),
            };
            pending.Insert ();
            result = strat.GenSyncKit (context.ProtocolState, AsStrategy.SyncMode.Wide);
            pfUsermail = result.PerFolders.Single (x => "useremail" == x.Folder.ServerId);
            Assert.False (pfUsermail.GetChanges);
            Assert.AreEqual (2, pfUsermail.Commands.Count);
            Assert.AreEqual (2, pfUsermail.Commands.Count (
                x => useremail.ServerId == x.ParentId &&
                McPending.Operations.EmailMarkRead == x.Operation &&
                false == x.DeferredSerialIssueOnly));
            foreach (var pend in McPending.Query (account.Id)) {
                pend.Delete ();
            }

            // single-issue, normal => just single-issue pending off useremail.
            pending = new McPending () {
                Operation = McPending.Operations.EmailMarkRead,
                AccountId = account.Id,
                State = McPending.StateEnum.Eligible,
                ParentId = useremail.ServerId,
                ServerId = serverIdGen++.ToString (),
                DeferredSerialIssueOnly = true,
            };
            pending.Insert ();
            pending = new McPending () {
                Operation = McPending.Operations.EmailMarkRead,
                AccountId = account.Id,
                State = McPending.StateEnum.Eligible,
                ParentId = useremail.ServerId,
                ServerId = serverIdGen++.ToString (),
            };
            pending.Insert ();
            result = strat.GenSyncKit (context.ProtocolState, AsStrategy.SyncMode.Wide);
            pfUsermail = result.PerFolders.Single (x => "useremail" == x.Folder.ServerId);
            Assert.False (pfUsermail.GetChanges);
            Assert.AreEqual (1, pfUsermail.Commands.Count);
            Assert.AreEqual (1, pfUsermail.Commands.Count (
                x => useremail.ServerId == x.ParentId &&
                McPending.Operations.EmailMarkRead == x.Operation &&
                true == x.DeferredSerialIssueOnly));
            foreach (var pend in McPending.Query (account.Id)) {
                pend.Delete ();
            }
        }

        [Test]
        public void TestPick ()
        {
            /*
             * create conditions, get expected result.
             * test1: Search, user-fetch, SendMail, 
             *        long time since any search/ping, 
             *        narrow not allowed,
             *        we are not rate-limited, other ops in Q.
             *        we have conditions for spec dnlds.
             *        conditions are set to advance scope.
             */
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            context.ProtocolState = context.ProtocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.StrategyRung = 3;
                return true;
            });
            var folders = new List<McFolder> ();
            var inbox = McFolder.Create (account.Id, false, false, true, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            inbox.AsSyncKey = "1";
            inbox.Insert ();
            folders.Add (inbox);
            // NYI.
            //var strat = new AsStrategy (context);
            //var result = strat.Pick ();
            /*
             * a) in BG, get SendMail.
             * b) in FG, get Search. Delete Search.
             * b1) in QS, get narrow-sync. set recent narrow-sync.
             * b2) in QS, get wait. clear recent narrow-sync.
             * c) in FG, get user-fetch. Delete user-fetch.
             * d) in FG get SendMail. Delete SendMail.
             * e) in FG & BG get oldest op. enable narrow sync (new rung).
             * f) in FG & BG get narrow sync. set recent narrow sync.
             * g) in FG & BG get oldest op. clear recent narrow sync, set recent narrow ping.
             * h) in FG & BG get oldest op. enable rate-limited and narrow-ping.
             * i) in FG & BG get narrow ping. disable narrow ping.
             * j) in BG, get wait. turn off rate limiting and clear pending Q. P1 condition.
             * k) control coin, in FG/BG see spec-dnld and broad sync. disable both.
             * l) in FG/BG see broad ping. disable broad ping.
             * m) in FG/BG see narrow ping. disable narrow ping.
             * n) in FG/BG see wait.
             */
        }

        [Test]
        public void TestUploadTimeoutSecs ()
        {
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            var strat = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
            var secs = strat.UploadTimeoutSecs (-1);
            Assert.IsTrue (secs > AsStrategy.KMinTimeout);
            secs = strat.UploadTimeoutSecs (0);
            Assert.IsTrue (secs > AsStrategy.KMinTimeout);
            secs = strat.UploadTimeoutSecs (1);
            Assert.AreEqual (secs, AsStrategy.KMinTimeout);
            secs = strat.UploadTimeoutSecs (10000000);
            Assert.IsTrue (secs > AsStrategy.KMinTimeout);
            Assert.IsTrue (secs < 999);
        }

        [Test]
        public void TestDownloadTimeoutSecs ()
        {
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var context = new MockContext (account);
            var strat = new AsStrategy (context, AsStrategy.LadderChoiceEnum.Test);
            var secs = strat.UploadTimeoutSecs (-1);
            Assert.IsTrue (secs > AsStrategy.KMinTimeout);
            secs = strat.DownloadTimeoutSecs (0);
            Assert.IsTrue (secs > AsStrategy.KMinTimeout);
            secs = strat.DownloadTimeoutSecs (1);
            Assert.AreEqual (secs, AsStrategy.KMinTimeout);
            secs = strat.DownloadTimeoutSecs (10000000);
            Assert.IsTrue (secs > AsStrategy.KMinTimeout);
            Assert.IsTrue (secs < 999);
        }
    }
}

