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
                folder6.Insert ();

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
        public class TestQueryByFolderEntryId : BaseMcFolderTest
        {
            [Test]
            public void ShouldQueryByEntryId ()
            {
                int accountId = 1;
                int folderId = 1;
                int folderEntryId = 11;
                McFolder folder1 = CreateFolder (accountId);
                McMapFolderFolderEntry folderEntry = CreateFolderEntry (accountId, folderId, folderEntryId, McFolderEntry.ClassCodeEnum.Folder);
                Console.WriteLine ("folder1 ID: {0}", folder1.Id);
                Console.WriteLine ("folderId: {0}", folderEntry.FolderId);

                List<McFolder> retrieved1 = McFolder.QueryByFolderEntryId<McFolder> (accountId, folderEntryId);
                Assert.AreEqual (1, retrieved1.Count, "Should only find one matching folder");
                FoldersAreEqual (folder1, retrieved1.FirstOrDefault (), "Retrieved folder should be unchanged");
            }

            [Test]
            public void ShouldNotDisplayFoldersAwaitingDelete ()
            {// should not display folders awaiting delete
                int accountId = 1;
                int folderId = 1;
                int folderEntryId = 11;
                CreateFolder (accountId, isAwaitingDelete: true);
                CreateFolderEntry (accountId, folderId, folderEntryId, McFolderEntry.ClassCodeEnum.Folder);

                List<McFolder> retrieved1 = McFolder.QueryByFolderEntryId<McFolder> (accountId, folderEntryId);
                Assert.AreEqual (0, retrieved1.Count, "Should not return folders awaiting delete");
            }

            [Test]
            public void ShouldNotReturnFoldersWithoutFolderEntry ()
            {
                // should only return McFolders that have a corresponding entry in McMapFolderFolderEntry
                int accountId = 1;
                int folderId = 50;
                int folderEntryId = 11;
                McFolder folder1 = CreateFolder (accountId, autoInsert: false);
                folder1.Id = 30;
                folder1.Update ();
                CreateFolderEntry (accountId, folderId, folderEntryId, McFolderEntry.ClassCodeEnum.Folder, autoInsert: false);

                List <McFolder> retrieved1 = McFolder.QueryByFolderEntryId<McFolder> (accountId, folderEntryId);
                Assert.AreEqual (0, retrieved1.Count, "Should not find folders if folder ID and folderEntry ID differ");
            }

            [Test]
            public void ShouldQueryByFolderEntryAccountId ()
            {
                // should query folders by McMapFolderFolderEntry accountId
                int accountId = 1;
                int accountIdOther = 2;
                int folderId = 1;
                int folderEntryId = 11;
                CreateFolder (accountId);
                CreateFolderEntry (accountIdOther, folderId, folderEntryId, McFolderEntry.ClassCodeEnum.Folder);

                List <McFolder> retrieved1 = McFolder.QueryByFolderEntryId<McFolder> (accountId, folderEntryId);
                List <McFolder> retrieved2 = McFolder.QueryByFolderEntryId<McFolder> (accountIdOther, folderEntryId);
                Assert.AreEqual (0, retrieved1.Count, "Should only retrieve folder by folderEntry accountId");
                Assert.AreEqual (1, retrieved2.Count, "Should be able to retrieve folder by folderEntry accountId");
            }

            [Test]
            public void ShouldReturnFoldersWithFolderCode ()
            {
                // should only return folders with “Folder” class code
                McFolderEntry.ClassCodeEnum goodClassCode = McFolderEntry.ClassCodeEnum.Folder;
                McFolderEntry.ClassCodeEnum badClassCode = McFolderEntry.ClassCodeEnum.Calendar;

                // don't return folders that don't have the Folder class code
                int accountId = 1;
                int folderId = 1;
                int folderEntryId = 11;
                McFolder folder1 = CreateFolder (accountId);
                McMapFolderFolderEntry folderEntry1 = CreateFolderEntry (accountId, folderId, folderEntryId, badClassCode);

                List<McFolder> retrieved1 = McFolder.QueryByFolderEntryId<McFolder> (accountId, folderEntryId);
                Assert.AreEqual (0, retrieved1.Count, "Should not retrieve folders that don't have the folder class code");

                // make sure that class code can be changed back and the record will be found
                folderEntry1.ClassCode = goodClassCode;
                folderEntry1.Update ();

                List <McFolder> retrieved2 = McFolder.QueryByFolderEntryId<McFolder> (accountId, folderEntryId);
                Assert.AreEqual (1, retrieved2.Count, "Should retrieve matching folder once it has the correct class code");
                FoldersAreEqual (folder1, retrieved2.FirstOrDefault (), "Retrieved folder should match inserted (and updated) folder");
            }

            [Test]
            public void QueryByMcItemType ()
            {
                QueryFolderOfGenericType<McEmailMessage> ("Should be able to query email messages by folderEntryId");
                QueryFolderOfGenericType<McCalendar> ("Should be able to query all calendars by folderEntryId");
                QueryFolderOfGenericType<McContact> ("Should be able to query all contacts by folderEntryId");
                QueryFolderOfGenericType<McTask> ("Should be able to query all tasks by folderEntryId");
            }

            private void QueryFolderOfGenericType<T> (string message) where T : McItem, new()
            {
                int accountId = 1;
                int folderId = 1;
                int folderEntryId = 11;

                T email1 = new T ();
                email1.AccountId = accountId;
                email1.Insert ();

                CreateFolderEntry (accountId, folderId, folderEntryId);

                List<McFolder> retrieved1 = McFolder.QueryByFolderEntryId<T> (accountId, folderEntryId);
                Assert.AreEqual (0, retrieved1.Count, message);
            }

            private McFolder CreateFolder (int accountId, bool isAwaitingDelete = false, bool autoInsert = true)
            {
                bool isClientOwned = false;
                string parentId = "0";
                string serverId = "Server";
                string name = "Folder Name";
                Xml.FolderHierarchy.TypeCode typeCode = Xml.FolderHierarchy.TypeCode.Unknown_18;
                McFolder folder = McFolder.Create (accountId, isClientOwned, false, parentId, serverId, name, typeCode);

                folder.IsAwaitingDelete = isAwaitingDelete;

                if (autoInsert) { folder.Insert (); }
                return folder;
            }

            private McMapFolderFolderEntry CreateFolderEntry (int accountId, int folderId, int folderEntryId, 
                McFolderEntry.ClassCodeEnum classCode = McFolderEntry.ClassCodeEnum.Folder, bool autoInsert = true)
            {
                McMapFolderFolderEntry folderEntry = new McMapFolderFolderEntry (accountId);

                folderEntry.FolderId = folderId;
                folderEntry.FolderEntryId = folderEntryId;
                folderEntry.ClassCode = classCode;

                if (autoInsert) { folderEntry.Insert (); }

                return folderEntry;
            }
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

        [TestFixture]
        public class TestServerEndQuery : BaseMcFolderTest
        {
            [Test]
            public void CanQueryFoldersAwaitingDelete ()
            {
                // server-end should be able to process commands against folder until folder delete is complete
                McFolder folder1 = CreateClientFolder (1, isAwaitingDelete: true);
                McFolder badFolder = CreateClientFolder (2);
                folder1.Insert ();
                badFolder.Insert ();

                List<McFolder> retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (1, retrieved1.Count, "Server end query should return only 1 folder if only one has been inserted");
                FoldersAreEqual (folder1, retrieved1.FirstOrDefault (), "Server end query should return folder where isAwaitingDelete == true");

                McFolder folder2 = CreateClientFolder (1, isAwaitingDelete: false);
                folder2.Insert ();

                List<McFolder> retrieved2 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (2, retrieved2.Count, "Server end query should return both folders awaiting deletion and those that are not");
            }

            [Test]
            public void CannotQueryFoldersAwaitingCreate ()
            {
                // should not return folders that are awaiting creation on the server
                McFolder folder1 = CreateClientFolder (1, isAwaitingCreate: true);
                folder1.Insert ();

                List<McFolder> retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (0, retrieved1.Count, "Server should not return a folder awaiting creation on the server");

                folder1.IsAwaitingCreate = false;
                folder1.Update ();

                retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (1, retrieved1.Count, "Server should return a folder once it is no longer awaiting creation");
                FoldersAreEqual (folder1, retrieved1.FirstOrDefault (), "Retrieved folder should be valid once it is created on the client");

                // should not return folders awaiting creation on server and awaiting deletion on client
                McFolder folder2 = CreateClientFolder (2, isAwaitingDelete: true, isAwaitingCreate: true);
                folder2.Insert ();

                List <McFolder> retrieved2 = McFolder.ServerEndQueryAll (2);
                Assert.AreEqual (0, retrieved2.Count, "Should not return folders awaiting creation on server and awaiting deletion on client");
            }

            [Test]
            public void ShouldNotReturnClientOwnedFolders ()
            {
                // should not return folders that are client owned
                McFolder folder1 = CreateClientFolder (1, isClientOwned: true);
                folder1.Insert ();

                List<McFolder> retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (0, retrieved1.Count, "Should not return folders that are client owned");

                // add isAwaitingDelete flag
                folder1.IsAwaitingDelete = true;
                folder1.Update ();

                retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (0, retrieved1.Count, "Should not return folders that are client owned, even if they are awaiting delete");

                // folder is no longer client owned
                folder1.IsClientOwned = false;
                folder1.Update ();

                retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (1, retrieved1.Count, "Should return folders that are client owned, but previously were not");
                FoldersAreEqual (folder1, retrieved1.FirstOrDefault (), "Non-client owned folder returned should match inserted (and updated) folder");
            }

            private McFolder CreateClientFolder (int accountId, bool isAwaitingDelete = false, bool isAwaitingCreate = false, 
                bool isClientOwned = false)
            {
                string parentId = "1";
                string serverId = "Server";
                string name = "Folder Name";
                Xml.FolderHierarchy.TypeCode typeCode = Xml.FolderHierarchy.TypeCode.Unknown_18;
                McFolder folder = McFolder.Create (accountId, isClientOwned, false, parentId, serverId, name, typeCode);

                folder.IsAwaitingCreate = isAwaitingCreate;
                folder.IsAwaitingDelete = isAwaitingDelete;
                return folder;
            }
        }

        [TestFixture]
        public class TestResetState : BaseMcFolderTest
        {
            private const string serverId = "Server Id";

            [Test]
            public void ShouldNotBreakDefaultFolder ()
            {
                McFolder folder1 = CreateFolder (1);
                folder1.Insert ();
                McFolder.AsResetState (1);

                McFolder retrieved1 = McFolder.GetClientOwnedFolder (1, serverId);
                FoldersAreEqual (folder1, retrieved1, "Folder should be the same after resetting state");
                FlagsAreReset (retrieved1, "Folder flags should be correct when reset");
            }

            [Test]
            public void ShouldResetFolderSyncKey ()
            {
                McFolder folder1 = CreateFolder (1, asSyncKey: "10");
                folder1.Insert ();
                McFolder retrieved1 = McFolder.GetClientOwnedFolder (1, serverId);
                Assert.AreEqual ("10", retrieved1.AsSyncKey, "AsSyncKey should be set correctly before reset event");
                McFolder.AsResetState (1);

                McFolder retrieved2 = McFolder.GetClientOwnedFolder (1, serverId);
                FoldersAreEqual (folder1, retrieved2, "Folder core values should not be modified by AsResetState");
                FlagsAreReset (retrieved2, "Folder flags should have been reset correctly");
            }

            [Test]
            public void ShouldResetSyncMetaFlag ()
            {
                McFolder folder1 = CreateFolder (1, syncMetaToClient: false);
                folder1.Insert ();
                McFolder retrieved1 = McFolder.GetClientOwnedFolder (1, serverId);
                Assert.AreEqual (false, retrieved1.AsSyncMetaToClientExpected, "AsSyncMeta... flag should be set correctly");
                McFolder.AsResetState (1);

                McFolder retrieved2 = McFolder.GetClientOwnedFolder (1, serverId);
                FoldersAreEqual (folder1, retrieved1, "Folder core values should not be modified by AsResetState");
                FlagsAreReset (retrieved2, "Folder flags should have been reset correctly");

                // set both at the same time
                folder1.AsSyncMetaToClientExpected = false;
                folder1.AsSyncKey = "10";
                folder1.Update ();
                McFolder.AsResetState (1);
                McFolder retrieved3 = McFolder.GetClientOwnedFolder (1, serverId);
                FlagsAreReset (retrieved3, "Both folder flags should have been reset correctly");
            }

            [Test]
            public void ShouldOnlyResetStateForAccountId ()
            {
                // only reset state for the specified account id
                McFolder folder1 = CreateFolder (2, asSyncKey: "10", syncMetaToClient: false);
                folder1.Insert ();

                McFolder folder2 = CreateFolder (1, asSyncKey: "10", syncMetaToClient: false);  // only this folder should be retrieved
                folder2.Insert ();

                McFolder.AsResetState (1);
                McFolder retrieved1 = McFolder.GetClientOwnedFolder (1, serverId);
                FlagsAreReset (retrieved1, "Both folder flags should have been reset correctly");

                // check that folder1's flags _were not_ reset
                McFolder retrieved2 = McFolder.GetClientOwnedFolder (2, serverId);
                Assert.AreEqual ("10", retrieved2.AsSyncKey, "Sync key should not be reset");
                Assert.AreEqual (false, retrieved2.AsSyncMetaToClientExpected, "AsSyncMetaToClientExpected should not be reset");
            }

            private void FlagsAreReset (McFolder actual, string message)
            {
                Assert.AreEqual ("0", actual.AsSyncKey, message);
                Assert.AreEqual (true, actual.AsSyncMetaToClientExpected, message);
            }

            private McFolder CreateFolder (int accountId, string asSyncKey = "0", bool syncMetaToClient = true)
            {
                bool isClientOwned = true;  // make it easy to query with client-owned
                string parentId = "1";
                string name = "Folder Name";
                Xml.FolderHierarchy.TypeCode typeCode = Xml.FolderHierarchy.TypeCode.Unknown_18;
                McFolder folder = McFolder.Create (accountId, isClientOwned, false, parentId, serverId, name, typeCode);

                folder.AsSyncKey = asSyncKey;
                folder.AsSyncMetaToClientExpected = syncMetaToClient;

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

