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
        // Note: many of these tests rely on the underlying Ladder definition.
        [Test]
        public void TestScopeFlagIsSet ()
        {
            Assert.False (AsStrategy.Scope.FlagIsSet (0, AsStrategy.Scope.FlagEnum.RicSynced));
            Assert.True (AsStrategy.Scope.FlagIsSet(1, AsStrategy.Scope.FlagEnum.RicSynced));
            Assert.True (AsStrategy.Scope.FlagIsSet(2, AsStrategy.Scope.FlagEnum.RicSynced));
            for (int i = 3; i < AsStrategy.Scope.Ladder.GetLength (0); ++i) {
                Assert.True (AsStrategy.Scope.FlagIsSet(i, AsStrategy.Scope.FlagEnum.RicSynced));
                Assert.True (AsStrategy.Scope.FlagIsSet(i, AsStrategy.Scope.FlagEnum.NarrowSyncOk));
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
            Assert.AreEqual (3, val.Count);
            Assert.True (val.Contains (AsStrategy.Scope.ItemType.Email));
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
        public void TestEmailFolderListProvider ()
        {
            var context = new MockContext ();
            McFolder folder;
            List<McFolder> result;
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var strat = new AsStrategy (context);
            folder = McFolder.Create (account.Id, false, false, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, "0", "user", "User", Xml.FolderHierarchy.TypeCode.UserCreatedMail_12);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            folder.Insert ();
            result = strat.EmailFolderListProvider (account.Id, AsStrategy.Scope.EmailEnum.None, true);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.DefaultInbox_2, result [0].Type);
            result = strat.EmailFolderListProvider (account.Id, AsStrategy.Scope.EmailEnum.None, false);
            Assert.AreEqual (0, result.Count);
            result = strat.EmailFolderListProvider (account.Id, AsStrategy.Scope.EmailEnum.Def1w, false);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.DefaultInbox_2, result [0].Type);
            result = strat.EmailFolderListProvider (account.Id, AsStrategy.Scope.EmailEnum.All3m, true);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.DefaultInbox_2, result [0].Type);
            result = strat.EmailFolderListProvider (account.Id, AsStrategy.Scope.EmailEnum.All3m, false);
            Assert.AreEqual (2, result.Count);
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.UserCreatedMail_12 == x.Type));
        }

        [Test]
        public void TestCalFolderListProvider ()
        {
            var context = new MockContext ();
            McFolder folder;
            List<McFolder> result;
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var strat = new AsStrategy (context);
            folder = McFolder.Create (account.Id, false, false, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, "0", "user", "User", Xml.FolderHierarchy.TypeCode.UserCreatedCal_13);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            folder.Insert ();
            result = strat.CalFolderListProvider (account.Id, AsStrategy.Scope.CalEnum.None, true);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.DefaultCal_8, result [0].Type);
            result = strat.CalFolderListProvider (account.Id, AsStrategy.Scope.CalEnum.None, false);
            Assert.AreEqual (0, result.Count);
            result = strat.CalFolderListProvider (account.Id, AsStrategy.Scope.CalEnum.Def2w, false);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.DefaultCal_8, result [0].Type);
            result = strat.CalFolderListProvider (account.Id, AsStrategy.Scope.CalEnum.All3m, true);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.DefaultCal_8, result [0].Type);
            result = strat.CalFolderListProvider (account.Id, AsStrategy.Scope.CalEnum.All3m, false);
            Assert.AreEqual (2, result.Count);
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultCal_8 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.UserCreatedCal_13 == x.Type));
        }

        [Test]
        public void TestContactFolderListProvider ()
        {
            var context = new MockContext ();
            McFolder folder;
            List<McFolder> result;
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var strat = new AsStrategy (context);
            folder = McFolder.Create (account.Id, false, false, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, "0", "user", "User", Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, "0", "ric", "RIC", Xml.FolderHierarchy.TypeCode.Ric_19);
            folder.Insert ();
            result = strat.ContactFolderListProvider (account.Id, AsStrategy.Scope.ContactEnum.None, true);
            Assert.AreEqual (0, result.Count);
            result = strat.ContactFolderListProvider (account.Id, AsStrategy.Scope.ContactEnum.None, false);
            Assert.AreEqual (0, result.Count);
            result = strat.ContactFolderListProvider (account.Id, AsStrategy.Scope.ContactEnum.RicInf, true);
            Assert.AreEqual (0, result.Count);
            result = strat.ContactFolderListProvider (account.Id, AsStrategy.Scope.ContactEnum.RicInf, false);
            Assert.AreEqual (1, result.Count);
            Assert.AreEqual (Xml.FolderHierarchy.TypeCode.Ric_19, result [0].Type);
            result = strat.ContactFolderListProvider (account.Id, AsStrategy.Scope.ContactEnum.DefRicInf, true);
            Assert.AreEqual (0, result.Count);
            result = strat.ContactFolderListProvider (account.Id, AsStrategy.Scope.ContactEnum.DefRicInf, false);
            Assert.AreEqual (2, result.Count);
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultContacts_9 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.Ric_19 == x.Type));
            result = strat.ContactFolderListProvider (account.Id, AsStrategy.Scope.ContactEnum.AllInf, true);
            Assert.AreEqual (0, result.Count);
            result = strat.ContactFolderListProvider (account.Id, AsStrategy.Scope.ContactEnum.AllInf, false);
            Assert.AreEqual (3, result.Count);
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultContacts_9 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.Ric_19 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14 == x.Type));
        }

        [Test]
        public void TestCanAdvance ()
        {
            var context = new MockContext ();
            bool result;
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var strat = new AsStrategy (context);
            var emailFolder = McFolder.Create (account.Id, false, false, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            emailFolder.AsSyncMetaToClientExpected = true;
            emailFolder.Insert ();
            var calFolder = McFolder.Create (account.Id, false, false, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            calFolder.AsSyncMetaToClientExpected = false;
            calFolder.Insert ();
            // Create folders.
            var conFolder = McFolder.Create (account.Id, false, false, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
            conFolder.AsSyncMetaToClientExpected = false;
            conFolder.Insert ();
            var ricFolder = McFolder.Create (account.Id, false, false, "0", "ric", "RIC", Xml.FolderHierarchy.TypeCode.Ric_19);
            ricFolder.AsSyncMetaToClientExpected = false;
            ricFolder.Insert ();
            result = strat.CanAdvance (account.Id, 5);
            Assert.False (result);
            emailFolder.AsSyncMetaToClientExpected = false;
            emailFolder.Update ();
            calFolder.AsSyncMetaToClientExpected = true;
            calFolder.Update ();
            result = strat.CanAdvance (account.Id, 5);
            Assert.False (result);
            calFolder.AsSyncMetaToClientExpected = false;
            calFolder.Update ();
            conFolder.AsSyncMetaToClientExpected = true;
            conFolder.Update ();
            result = strat.CanAdvance (account.Id, 5);
            Assert.False (result);
            conFolder.AsSyncMetaToClientExpected = false;
            conFolder.Update ();
            result = strat.CanAdvance (account.Id, 5);
            Assert.True (result);
        }

        private static bool StatusIndCalled;
        private static NcResult.SubKindEnum StatusSubKind;

        private class MockProtoControl : AsProtoControl
        {
            public MockProtoControl (IProtoControlOwner owner, int accountId) : base (owner, accountId)
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
            StatusIndCalled = false;
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();var context = new MockContext ();
            context.ProtoControl = new MockProtoControl (context.Owner, account.Id);
            context.ProtocolState.StrategyRung = 5;
            context.ProtocolState.Update ();
            int result;
            var strat = new AsStrategy (context);
            var emailFolder = McFolder.Create (account.Id, false, false, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            emailFolder.AsSyncMetaToClientExpected = true;
            emailFolder.Insert ();
            var calFolder = McFolder.Create (account.Id, false, false, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            calFolder.AsSyncMetaToClientExpected = false;
            calFolder.Insert ();
            // Create folders.
            var conFolder = McFolder.Create (account.Id, false, false, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
            conFolder.AsSyncMetaToClientExpected = false;
            conFolder.Insert ();
            var ricFolder = McFolder.Create (account.Id, false, false, "0", "ric", "RIC", Xml.FolderHierarchy.TypeCode.Ric_19);
            ricFolder.AsSyncMetaToClientExpected = false;
            ricFolder.Insert ();
            result = strat.AdvanceIfPossible (account.Id, context.ProtocolState.StrategyRung);
            Assert.AreEqual (context.ProtocolState.StrategyRung, result);
            emailFolder.AsSyncMetaToClientExpected = false;
            emailFolder.Update ();
            var oldRung = context.ProtocolState.StrategyRung;
            result = strat.AdvanceIfPossible (account.Id, context.ProtocolState.StrategyRung);
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
            var context = new MockContext ();
            McFolder folder;
            List<McFolder> result;
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var strat = new AsStrategy (context);
            folder = McFolder.Create (account.Id, false, false, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, "0", "useremail", "UserEmail", Xml.FolderHierarchy.TypeCode.UserCreatedMail_12);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, "0", "usercal", "UserCal", Xml.FolderHierarchy.TypeCode.UserCreatedCal_13);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, "0", "usercontacts", "UserContacts", Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
            folder.Insert ();
            folder = McFolder.Create (account.Id, false, false, "0", "ric", "RIC", Xml.FolderHierarchy.TypeCode.Ric_19);
            folder.Insert ();
            result = strat.FolderListProvider (account.Id, 6, true);
            Assert.AreEqual (2, result.Count);
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == x.Type));
            Assert.True (result.Any (x => Xml.FolderHierarchy.TypeCode.DefaultCal_8 == x.Type));
            result = strat.FolderListProvider (account.Id, 6, false);
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
            var context = new MockContext ();
            var strat = new AsStrategy (context);
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var folder = McFolder.Create (account.Id, false, false, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
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
            var context = new MockContext ();
            var strat = new AsStrategy (context);
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var folder = McFolder.Create (account.Id, false, false, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
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

            var context = new MockContext ();
            var strat = new AsStrategy (context);
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var folder = McFolder.Create (account.Id, false, false, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
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
            var context = new MockContext ();
            var strat = new AsStrategy (context);
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
            };
            account.Insert ();
            var emailFolder = McFolder.Create (account.Id, false, false, "0", "inbox", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            emailFolder.Insert ();
            var calFolder = McFolder.Create (account.Id, false, false, "0", "cal", "Cal", Xml.FolderHierarchy.TypeCode.DefaultCal_8);
            calFolder.Insert ();
            var contactFolder = McFolder.Create (account.Id, false, false, "0", "contacts", "Contacts", Xml.FolderHierarchy.TypeCode.DefaultContacts_9);
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
    }
}

