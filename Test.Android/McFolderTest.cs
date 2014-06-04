//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.ActiveSync;
using NachoCore.Utils;

namespace Test.iOS
{
    [TestFixture]
    public class McFolderTest 
    {
        [TestFixture]
        public class TestClientOwnedDistFolders : McFolderTestBase
        {
            [Test]
            public void CanQueryClientOwnedFolder ()
            {
                int accountId = 1;
                string serverId = "TestServer";
                McFolder expectedFolder = CreateDistFolder (accountId, serverId);

                expectedFolder.Insert ();

                var actualFolder = McFolder.GetClientOwnedFolder (accountId, serverId);

                FoldersAreEqual (expectedFolder, actualFolder, "Should be able to do basic query of client-owned folder");
            }

            [Test]
            public void CanQueryClientOwnedDistFolders ()
            {
                // Outbox
                int accountId = 1;
                McFolder expectedOutbox = CreateDistFolder (accountId, McFolder.ClientOwned_Outbox);
                expectedOutbox.Insert ();

                McFolder actualFolder1 = McFolder.GetOutboxFolder (accountId);
                FoldersAreEqual (expectedOutbox, actualFolder1, "Should be able to query for distinguished folder (Outbox)");

                // GalCache
                McFolder expectedGalCache = CreateDistFolder (accountId, McFolder.ClientOwned_GalCache);
                expectedGalCache.Insert ();

                McFolder actualFolder2 = McFolder.GetGalCacheFolder (accountId);
                FoldersAreEqual (expectedGalCache, actualFolder2, "Should be able to query for distinguished folder (GalCache)");

                // Gleaned
                McFolder expectedGleaned = CreateDistFolder (accountId, McFolder.ClientOwned_Gleaned);
                expectedGleaned.Insert ();

                McFolder actualFolder3 = McFolder.GetGleanedFolder (accountId);
                FoldersAreEqual (expectedGleaned, actualFolder3, "Should be able to query for distinguished folder (Gleaned)");

                // Lost and Found
                McFolder expectedLostFound = CreateDistFolder (accountId, McFolder.ClientOwned_LostAndFound);
                expectedGleaned.Insert ();

                McFolder actualFolder4 = McFolder.GetGleanedFolder (accountId);
                FoldersAreEqual (expectedLostFound, actualFolder4, "Should be able to query for distinguished folder (Lost And Found)");
            }

            private McFolder CreateDistFolder (int accountId, string serverId)
            {
                bool isClientOwned = true;
                string parentId = "ParentFolder";
                var folderType = Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1;
                McFolder folder = McFolder.Create (accountId, isClientOwned, false, parentId, serverId, "DisplayName", folderType);
                return folder;
            }
        }

        [TestFixture]
        public class UserFoldersTests : McFolderTestBase
        {
            [Test]
            public void TestTypecodeVariance ()
            {
                // Same name, parent id; different typecodes
                int accountId = 1;
                string parentId = "0";
                string name = "Name";

                Xml.FolderHierarchy.TypeCode typeCode1 = Xml.FolderHierarchy.TypeCode.UserCreatedCal_13;
                Xml.FolderHierarchy.TypeCode typeCode2 = Xml.FolderHierarchy.TypeCode.DefaultContacts_9;

                McFolder folder1 = CreateUserFolder (accountId, typeCode1, parentId, name);
                McFolder folder2 = CreateUserFolder (accountId, typeCode2, parentId, name);

                folder1.Insert ();
                folder2.Insert ();

                McFolder expected1 = McFolder.GetUserFolder (accountId, typeCode1, parentId.ToInt (), name);
                McFolder expected2 = McFolder.GetUserFolder (accountId, typeCode2, parentId.ToInt (), name);

                Assert.AreNotEqual (expected1.Type, expected2.Type, "Folders should be able to have the same name and parent as long as their typecodes are different");
            }

            [Test]
            public void TestParentIdVariance ()
            {
                // Same name and typecodes, different parent id
                int accountId = 1;
                Xml.FolderHierarchy.TypeCode typeCode = Xml.FolderHierarchy.TypeCode.UserCreatedCal_13;
                string name = "Name";

                string parentId1 = "1";
                string parentId2 = "5";

                McFolder folder1 = CreateUserFolder (accountId, typeCode, parentId1, name);
                McFolder folder2 = CreateUserFolder (accountId, typeCode, parentId2, name);

                folder1.Insert ();
                folder2.Insert ();

                McFolder expected1 = McFolder.GetUserFolder (accountId, typeCode, parentId1.ToInt (), name);
                McFolder expected2 = McFolder.GetUserFolder (accountId, typeCode, parentId2.ToInt (), name);

                Assert.AreNotEqual (expected1.ParentId, expected2.ParentId, "Folders with identical properties should be able to reside under different parents"); 
            }

            [Test]
            public void TestNameVariance ()
            {
                // Same parent id and typecodes, different name
                int accountId = 1;
                Xml.FolderHierarchy.TypeCode typeCode = Xml.FolderHierarchy.TypeCode.UserCreatedCal_13;
                string parentId = "1";

                string name1 = "First Name";
                string name2 = "Second Name";

                McFolder folder1 = CreateUserFolder (accountId, typeCode, parentId, name1);
                McFolder folder2 = CreateUserFolder (accountId, typeCode, parentId, name2);

                folder1.Insert ();
                folder2.Insert ();

                McFolder expected1 = McFolder.GetUserFolder (accountId, typeCode, parentId.ToInt (), name1);
                McFolder expected2 = McFolder.GetUserFolder (accountId, typeCode, parentId.ToInt (), name2);

                Assert.AreNotEqual (expected1.DisplayName, expected2.DisplayName, "Folders with different names should be considered separate folders");
            }

            private McFolder CreateUserFolder (int accountId, Xml.FolderHierarchy.TypeCode typeCode, string parentId, string name)
            {
                bool isClientOwned = false;
                string serverId = "My Server";
                McFolder folder = McFolder.Create (accountId, isClientOwned, false, parentId, serverId, name, typeCode);
                return folder;
            }
        }

        [TestFixture]
        public class TestDistFolders : McFolderTestBase
        {
            [Test]
            public void TestGetRicContactFolder ()
            {
                Xml.FolderHierarchy.TypeCode typeCode = Xml.FolderHierarchy.TypeCode.Ric_19;
                TestFolderWithType (1, typeCode, "Should be able to retrieve Ric Contact distinguished folder");
            }

            [Test]
            public void TestGetDefaultInboxFolder ()
            {
                Xml.FolderHierarchy.TypeCode typeCode = Xml.FolderHierarchy.TypeCode.DefaultInbox_2;
                TestFolderWithType (1, typeCode, "Should be able to retrieve Default Inbox distinguished folder");
                TestFolderWithType (2, typeCode, "Folders with the same type but different accountId's should be retrieved separately");
                McFolder expected1 = McFolder.GetDefaultInboxFolder (1);
                McFolder expected2 = McFolder.GetDefaultInboxFolder (2);
                Assert.AreNotEqual (expected1.AccountId, expected2.AccountId);
            }

            [Test]
            public void TestGetDefaultCalendarFolder ()
            {
                Xml.FolderHierarchy.TypeCode typeCode = Xml.FolderHierarchy.TypeCode.DefaultCal_8;
                TestFolderWithType (1, typeCode, "Should be able to retrieve Default Calendar distinguished folder");
            }

            [Test]
            public void TestGetDefaultContactFolder ()
            {
                Xml.FolderHierarchy.TypeCode typeCode = Xml.FolderHierarchy.TypeCode.DefaultContacts_9;
                TestFolderWithType (1, typeCode, "Should be able to retrieve Default Contact distinguished folder");
            }

            [Test]
            public void TestGetDefaultTaskFolder ()
            {
                Xml.FolderHierarchy.TypeCode typeCode = Xml.FolderHierarchy.TypeCode.DefaultTasks_7;
                TestFolderWithType (1, typeCode, "Should be able to retrieve Default Task distinguished folder");
            }

            private void TestFolderWithType (int accountId, Xml.FolderHierarchy.TypeCode typeCode, string message)
            {
                McFolder folder1 = CreateDistFolder (accountId, typeCode);
                folder1.Insert ();

                McFolder expected1;
                switch (typeCode) {
                case Xml.FolderHierarchy.TypeCode.Ric_19:
                    expected1 = McFolder.GetRicContactFolder (accountId);
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultInbox_2:
                    expected1 = McFolder.GetDefaultInboxFolder (accountId);
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultCal_8:
                    expected1 = McFolder.GetDefaultCalendarFolder (accountId);
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultContacts_9:
                    expected1 = McFolder.GetDefaultContactFolder (accountId);
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultTasks_7:
                    expected1 = McFolder.GetDefaultTaskFolder (accountId);
                    break;
                default:
                    expected1 = null;
                    break;
                }
                Assert.NotNull (expected1, "Folders count was 0; should retrieve folder with name: '{0}'", folder1.DisplayName);
                FoldersAreEqual (expected1, folder1, message);
            }

            private McFolder CreateDistFolder (int accountId, Xml.FolderHierarchy.TypeCode typeCode)
            {
                bool isClientOwned = false;
                string parentId = "2";
                string serverId = "Server";
                string name = "Server Name";
                McFolder folder = McFolder.Create (accountId, isClientOwned, false, parentId, serverId, name, typeCode);
                return folder;
            }
        }
    }

    public class McFolderTestBase
    {
        [SetUp]
        public void SetUp ()
        {
            Log.Info (Log.LOG_TEST, "Setup started");
            NcModel.Instance.Reset (System.IO.Path.GetTempFileName ());
        }

        public void FoldersAreEqual (McFolder expected, McFolder actual, string testDesc)
        {
            Assert.AreEqual (expected.AccountId, actual.AccountId, testDesc);
            Assert.AreEqual (expected.IsClientOwned, actual.IsClientOwned, testDesc);
            Assert.AreEqual (expected.IsHidden, actual.IsHidden, testDesc);
            Assert.AreEqual (expected.ParentId, actual.ParentId, testDesc);
            Assert.AreEqual (expected.ServerId, actual.ServerId, testDesc);
            Assert.AreEqual (expected.DisplayName, actual.DisplayName, testDesc);
            Assert.AreEqual (expected.Type, actual.Type, testDesc);
        }
    }
}

