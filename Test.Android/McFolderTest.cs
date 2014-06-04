//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.ActiveSync;
using NachoCore.Utils;
using System.Collections.Generic;
using System.Linq;

namespace Test.iOS
{
    [TestFixture]
    public class McFolderTest 
    {
        [TestFixture]
        public class TestClientOwnedDistFolders : BaseMcFolderTest
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
                int accountId = 1;

                // Outbox
                McFolder expectedOutbox = CreateDistFolder (accountId, McFolder.ClientOwned_Outbox);
                expectedOutbox.Insert ();

                // GalCache
                McFolder expectedGalCache = CreateDistFolder (accountId, McFolder.ClientOwned_GalCache);
                expectedGalCache.Insert ();

                // Gleaned
                McFolder expectedGleaned = CreateDistFolder (accountId, McFolder.ClientOwned_Gleaned);
                expectedGleaned.Insert ();

                // Lost and Found
                McFolder expectedLostFound = CreateDistFolder (accountId, McFolder.ClientOwned_LostAndFound);
                expectedLostFound.Insert ();

                McFolder actualFolder1 = McFolder.GetOutboxFolder (accountId);
                FoldersAreEqual (expectedOutbox, actualFolder1, "Should be able to query for distinguished folder (Outbox)");

                McFolder galCache = McFolder.GetGalCacheFolder (accountId);
                FoldersAreEqual (expectedGalCache, galCache, "Should be able to query for distinguished folder (GalCache)");

                McFolder gleaned = McFolder.GetGleanedFolder (accountId);
                FoldersAreEqual (expectedGleaned, gleaned, "Should be able to query for distinguished folder (Gleaned)");

                McFolder lostAndFound = McFolder.GetLostAndFoundFolder (accountId);
                FoldersAreEqual (expectedLostFound, lostAndFound, "Should be able to query for distinguished folder (Lost And Found)");
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
        public class UserFoldersTests : BaseMcFolderTest
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
        public class TestDistFolders : BaseMcFolderTest
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
                // can get default inbox folder
                Xml.FolderHierarchy.TypeCode inboxType = Xml.FolderHierarchy.TypeCode.DefaultInbox_2;
                TestFolderWithType (1, inboxType, "Should be able to retrieve Default Inbox distinguished folder");
                TestFolderWithType (2, inboxType, "Folders with the same type but different accountId's should be retrieved separately");
                McFolder expected1 = McFolder.GetDefaultInboxFolder (1);
                McFolder expected2 = McFolder.GetDefaultInboxFolder (2);
                Assert.AreNotEqual (expected1.AccountId, expected2.AccountId);

                // can get default calendar folder
                Xml.FolderHierarchy.TypeCode calType = Xml.FolderHierarchy.TypeCode.DefaultCal_8;
                TestFolderWithType (1, calType, "Should be able to retrieve Default Calendar distinguished folder");

                // can get default contact folder
                Xml.FolderHierarchy.TypeCode contactsType = Xml.FolderHierarchy.TypeCode.DefaultContacts_9;
                TestFolderWithType (1, contactsType, "Should be able to retrieve Default Contact distinguished folder");

                // can get default task folder
                Xml.FolderHierarchy.TypeCode tasksType = Xml.FolderHierarchy.TypeCode.DefaultTasks_7;
                TestFolderWithType (1, tasksType, "Should be able to retrieve Default Task distinguished folder");
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
                string name = "Folder Name";
                McFolder folder = McFolder.Create (accountId, isClientOwned, false, parentId, serverId, name, typeCode);
                return folder;
            }
        }

        [TestFixture]
        public class TestQueryByParentId : BaseMcFolderTest
        {
            [Test]
            public void TestZeroFoldersRetrieved ()
            {
                List<McFolder> retrieved1 = McFolder.QueryByParentId (1, "1");
                Assert.AreEqual (0, retrieved1.Count, "Should not retrieve any folders if none have been added");

                McFolder folder1 = CreateFolder (1, "0");
                McFolder folder2 = CreateFolder (1, "1");
                folder1.Insert ();
                folder2.Insert ();

                List<McFolder> retrieved2 = McFolder.QueryByParentId (1, "5");
                Assert.AreEqual (0, retrieved2.Count, "Should return empty list of folders if none were found");
            }

            [Test]
            public void TestSingleFolderRetrieved ()
            {
                McFolder folder1 = CreateFolder (1, "0");
                McFolder folder2 = CreateFolder (1, "1");
                McFolder folder3 = CreateFolder (1, "2");
                folder1.Insert ();
                folder2.Insert ();
                folder3.Insert ();

                List<McFolder> retrieved = McFolder.QueryByParentId (1, "1");
                Assert.AreEqual (1, retrieved.Count, "Should return a single folder if only one folder has a parent id");
                FoldersAreEqual (folder2, retrieved.FirstOrDefault (), "Returned folder should match created folder");
            }

            [Test]
            public void TestMultipleFoldersRetrieved ()
            {
                McFolder folder1 = CreateFolder (1, "0");
                McFolder folder2 = CreateFolder (1, "1");
                McFolder folder3 = CreateFolder (1, "1");
                McFolder folder4 = CreateFolder (1, "1");
                McFolder folder5 = CreateFolder (1, "2");
                McFolder folder6 = CreateFolder (5, "1");  // different account id; therefore should not show up in query
                folder1.Insert ();
                folder2.Insert ();
                folder3.Insert ();
                folder4.Insert ();
                folder5.Insert ();

                List<McFolder> retrieved = McFolder.QueryByParentId (1, "1");
                Assert.AreEqual (3, retrieved.Count, "Should return correct number of folders with matching parent id");
                foreach (McFolder folder in retrieved) {
                    Assert.AreEqual (1, folder.AccountId, "Account id's should match expected");
                    Assert.AreEqual ("1", folder.ParentId, "Parent id's should match expected");
                }
            }

            private McFolder CreateFolder (int accountId, string parentId)
            {
                bool isClientOwned = false;
                string serverId = "Server";
                string name = "Folder Name";
                Xml.FolderHierarchy.TypeCode typeCode = Xml.FolderHierarchy.TypeCode.Unknown_18;
                McFolder folder = McFolder.Create (accountId, isClientOwned, false, parentId, serverId, name, typeCode);
                return folder;
            }
        }

        [TestFixture]
        public class TestQueryByFolderEntryId
        {
//            [Test]
        }

        [TestFixture]
        public class TestQueryClientOwned : BaseMcFolderTest
        {
            [Test]
            public void TestQueryingClientOwnedFolders ()
            {
                McFolder folder1 = CreateClientFolder (1);
                McFolder folder2 = CreateClientFolder (1);
                McFolder folder3 = CreateNonClientFolder (1);
                McFolder folder4 = CreateNonClientFolder (2);

                folder1.Insert ();
                folder2.Insert ();
                folder3.Insert ();
                folder4.Insert ();

                // should return folders that are client owned if asked
                List<McFolder> retrieved1 = McFolder.QueryClientOwned (1, isClientOwned: true);
                Assert.AreEqual (2, retrieved1.Count, "Querying client-owned folders not awaiting delete should return correct number of folders");

                // should return folders that are not client-owned if asked
                List<McFolder> retrieved2 = McFolder.QueryClientOwned (2, isClientOwned: false);
                Assert.AreEqual (1, retrieved2.Count, "Querying non-client-owned folders awaiting delete should return correct number of folders");
                FoldersAreEqual (folder4, retrieved2.FirstOrDefault (), "Retrieved folder awaiting delete should match inserted folder");
            }

            [Test]
            public void TestFoldersAwaitingDelete ()
            {
                // should not return folders that are awaiting delete
                McFolder deleted1 = CreateClientFolder (1, isAwaitingDelete: true);
                deleted1.Insert ();

                List<McFolder> retrieved1 = McFolder.QueryClientOwned (1, true);
                Assert.AreEqual (0, retrieved1.Count, "Should not retrieve any folders if only folder inserted is awaiting delete");

                McFolder folder1 = CreateClientFolder (1, isAwaitingDelete: false);
                folder1.Insert ();

                List <McFolder> retrieved2 = McFolder.QueryClientOwned (1, true);
                Assert.AreEqual (1, retrieved2.Count, "Should only retrieve client-owned folders that are not awaiting delete");
                FoldersAreEqual (folder1, retrieved2.FirstOrDefault (), "Retrieved folder should match inserted folder");

                McFolder folder2 = CreateNonClientFolder (1);
                folder2.Insert ();

                List <McFolder> retrieved3 = McFolder.QueryClientOwned (1, false);
                Assert.AreEqual (1, retrieved3.Count, "Should only retrieve non-client-owned folders that are not awaiting delete");
                FoldersAreEqual (folder2, retrieved3.FirstOrDefault (), "Retrieved folder should match inserted folder");
            }

            private McFolder CreateClientFolder (int accountId, bool isAwaitingDelete = false)
            {
                bool isClientOwned = true;
                string parentId = "1";
                string serverId = "Server";
                string name = "Folder Name";
                Xml.FolderHierarchy.TypeCode typeCode = Xml.FolderHierarchy.TypeCode.Unknown_18;
                McFolder folder = McFolder.Create (accountId, isClientOwned, false, parentId, serverId, name, typeCode);

                folder.IsAwaitingDelete = isAwaitingDelete;
                return folder;
            }

            private McFolder CreateNonClientFolder(int accountId)
            {
                bool isClientOwned = false;
                string parentId = "1";
                string serverId = "Server";
                string name = "Folder Name";
                Xml.FolderHierarchy.TypeCode typeCode = Xml.FolderHierarchy.TypeCode.Unknown_18;
                McFolder folder = McFolder.Create (accountId, isClientOwned, false, parentId, serverId, name, typeCode);

                folder.IsAwaitingDelete = false;
                return folder;
            }
        }
    }

    public class BaseMcFolderTest
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

