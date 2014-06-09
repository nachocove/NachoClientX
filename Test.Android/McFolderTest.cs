//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.ActiveSync;
using NachoCore.Utils;
using System.Collections.Generic;
using System.Linq;
using TypeCode = NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode;
using ClassCode = NachoCore.Model.McFolderEntry.ClassCodeEnum;

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
                McFolder expectedFolder = CreateFolder (accountId, serverId: serverId, isClientOwned: true);

                var actualFolder = McFolder.GetClientOwnedFolder (accountId, serverId);

                FoldersAreEqual (expectedFolder, actualFolder, "Should be able to do basic query of client-owned folder");
            }

            [Test]
            public void CanQueryClientOwnedDistFolders ()
            {
                int accountId = 1;

                // Outbox
                McFolder expectedOutbox = CreateFolder (accountId, serverId: McFolder.ClientOwned_Outbox, isClientOwned: true);

                // GalCache
                McFolder expectedGalCache = CreateFolder (accountId, serverId: McFolder.ClientOwned_GalCache, isClientOwned: true);

                // Gleaned
                McFolder expectedGleaned = CreateFolder (accountId, serverId: McFolder.ClientOwned_Gleaned, isClientOwned: true);

                // Lost and Found
                McFolder expectedLostFound = CreateFolder (accountId, serverId: McFolder.ClientOwned_LostAndFound, isClientOwned: true);

                McFolder actualFolder1 = McFolder.GetOutboxFolder (accountId);
                FoldersAreEqual (expectedOutbox, actualFolder1, "Should be able to query for distinguished folder (Outbox)");

                McFolder galCache = McFolder.GetGalCacheFolder (accountId);
                FoldersAreEqual (expectedGalCache, galCache, "Should be able to query for distinguished folder (GalCache)");

                McFolder gleaned = McFolder.GetGleanedFolder (accountId);
                FoldersAreEqual (expectedGleaned, gleaned, "Should be able to query for distinguished folder (Gleaned)");

                McFolder lostAndFound = McFolder.GetLostAndFoundFolder (accountId);
                FoldersAreEqual (expectedLostFound, lostAndFound, "Should be able to query for distinguished folder (Lost And Found)");
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

                TypeCode typeCode1 = TypeCode.UserCreatedCal_13;
                TypeCode typeCode2 = TypeCode.DefaultContacts_9;

                CreateFolder (accountId, typeCode: typeCode1, parentId: parentId, name: name);
                CreateFolder (accountId, typeCode: typeCode2, parentId: parentId, name: name);

                McFolder expected1 = McFolder.GetUserFolder (accountId, typeCode1, parentId.ToInt (), name);
                McFolder expected2 = McFolder.GetUserFolder (accountId, typeCode2, parentId.ToInt (), name);

                Assert.AreNotEqual (expected1.Type, expected2.Type, "Folders should be able to have the same name and parent as long as their typecodes are different");
            }

            [Test]
            public void TestParentIdVariance ()
            {
                // Same name and typecodes, different parent id
                int accountId = 1;
                TypeCode typeCode = TypeCode.UserCreatedCal_13;
                string name = "Name";

                string parentId1 = "1";
                string parentId2 = "5";

                CreateFolder (accountId, typeCode: typeCode, parentId: parentId1, name: name);
                CreateFolder (accountId, typeCode: typeCode, parentId: parentId2, name: name);

                McFolder expected1 = McFolder.GetUserFolder (accountId, typeCode, parentId1.ToInt (), name);
                McFolder expected2 = McFolder.GetUserFolder (accountId, typeCode, parentId2.ToInt (), name);

                Assert.AreNotEqual (expected1.ParentId, expected2.ParentId, "Folders with identical properties should be able to reside under different parents"); 
            }

            [Test]
            public void TestNameVariance ()
            {
                // Same parent id and typecodes, different name
                int accountId = 1;
                TypeCode typeCode = TypeCode.UserCreatedCal_13;
                string parentId = "1";

                string name1 = "First Name";
                string name2 = "Second Name";

                CreateFolder (accountId, typeCode: typeCode, parentId: parentId, name: name1);
                CreateFolder (accountId, typeCode: typeCode, parentId: parentId, name: name2);

                McFolder expected1 = McFolder.GetUserFolder (accountId, typeCode: typeCode, parentId: parentId.ToInt (), name: name1);
                McFolder expected2 = McFolder.GetUserFolder (accountId, typeCode: typeCode, parentId: parentId.ToInt (), name: name2);

                Assert.AreNotEqual (expected1.DisplayName, expected2.DisplayName, "Folders with different names should be considered separate folders");
            }
        }

        [TestFixture]
        public class TestDistFolders : BaseMcFolderTest
        {
            [Test]
            public void TestGetRicContactFolder ()
            {
                TypeCode typeCode = TypeCode.Ric_19;
                TestFolderWithType (1, typeCode, "Should be able to retrieve Ric Contact distinguished folder");
            }

            [Test]
            public void TestGetDefaultInboxFolder ()
            {
                // can get default inbox folder
                TypeCode inboxType = TypeCode.DefaultInbox_2;
                TestFolderWithType (1, inboxType, "Should be able to retrieve Default Inbox distinguished folder");
                TestFolderWithType (2, inboxType, "Folders with the same type but different accountId's should be retrieved separately");
                McFolder expected1 = McFolder.GetDefaultInboxFolder (1);
                McFolder expected2 = McFolder.GetDefaultInboxFolder (2);
                Assert.AreNotEqual (expected1.AccountId, expected2.AccountId);

                // can get default calendar folder
                TypeCode calType = TypeCode.DefaultCal_8;
                TestFolderWithType (1, calType, "Should be able to retrieve Default Calendar distinguished folder");

                // can get default contact folder
                TypeCode contactsType = TypeCode.DefaultContacts_9;
                TestFolderWithType (1, contactsType, "Should be able to retrieve Default Contact distinguished folder");

                // can get default task folder
                TypeCode tasksType = TypeCode.DefaultTasks_7;
                TestFolderWithType (1, tasksType, "Should be able to retrieve Default Task distinguished folder");
            }

            private void TestFolderWithType (int accountId, TypeCode typeCode, string message)
            {
                McFolder folder1 = CreateFolder (accountId, typeCode: typeCode);

                McFolder expected1;
                switch (typeCode) {
                case TypeCode.Ric_19:
                    expected1 = McFolder.GetRicContactFolder (accountId);
                    break;
                case TypeCode.DefaultInbox_2:
                    expected1 = McFolder.GetDefaultInboxFolder (accountId);
                    break;
                case TypeCode.DefaultCal_8:
                    expected1 = McFolder.GetDefaultCalendarFolder (accountId);
                    break;
                case TypeCode.DefaultContacts_9:
                    expected1 = McFolder.GetDefaultContactFolder (accountId);
                    break;
                case TypeCode.DefaultTasks_7:
                    expected1 = McFolder.GetDefaultTaskFolder (accountId);
                    break;
                default:
                    expected1 = null;
                    break;
                }
                Assert.NotNull (expected1, "Folders count was 0; should retrieve folder with name: '{0}'", folder1.DisplayName);
                FoldersAreEqual (expected1, folder1, message);
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

                CreateFolder (1, parentId: "0");
                CreateFolder (1, parentId: "1");

                List<McFolder> retrieved2 = McFolder.QueryByParentId (1, "5");
                Assert.AreEqual (0, retrieved2.Count, "Should return empty list of folders if none were found");
            }

            [Test]
            public void TestSingleFolderRetrieved ()
            {
                CreateFolder (1, parentId: "0");
                McFolder folder2 = CreateFolder (1, parentId: "1");
                CreateFolder (1, parentId: "2");

                List<McFolder> retrieved = McFolder.QueryByParentId (1, parentId: "1");
                Assert.AreEqual (1, retrieved.Count, "Should return a single folder if only one folder has a parent id");
                FoldersAreEqual (folder2, retrieved.FirstOrDefault (), "Returned folder should match created folder");
            }

            [Test]
            public void TestMultipleFoldersRetrieved ()
            {
                CreateFolder (1, parentId: "0");
                CreateFolder (1, parentId: "1");
                CreateFolder (1, parentId: "1");
                CreateFolder (1, parentId: "1");
                CreateFolder (1, parentId: "2");
                CreateFolder (5, parentId: "1");  // different account id; therefore should not show up in query

                List<McFolder> retrieved = McFolder.QueryByParentId (1, "1");
                Assert.AreEqual (3, retrieved.Count, "Should return correct number of folders with matching parent id");
                foreach (McFolder folder in retrieved) {
                    Assert.AreEqual (1, folder.AccountId, "Account id's should match expected");
                    Assert.AreEqual ("1", folder.ParentId, "Parent id's should match expected");
                }
            }
        }

        [TestFixture]
        public class TestQueryByFolderEntryId : BaseMcFolderTest
        {
            [Test]
            public void ShouldQueryByEntryId ()
            {
                int accountId = 1;

                McFolder folder1 = CreateFolder (accountId);
                McEmailMessage email = CreateUniqueItem<McEmailMessage> (accountId);
                folder1.Link (email);

                List<McFolder> retrieved1 = McFolder.QueryByFolderEntryId<McEmailMessage> (accountId, email.Id);
                Assert.AreEqual (1, retrieved1.Count, "Should only find one matching folder");
                FoldersAreEqual (folder1, retrieved1.FirstOrDefault (), "Retrieved folder should be unchanged");
            }

            [Test]
            public void ShouldNotDisplayFoldersAwaitingDelete ()
            {
                // should not display folders awaiting delete
                int accountId = 1;

                McFolder folder1 = CreateFolder (accountId, isAwaitingDelete: true);
                McEmailMessage email = CreateUniqueItem<McEmailMessage> (accountId);
                folder1.Link (email);

                List<McFolder> retrieved1 = McFolder.QueryByFolderEntryId<McEmailMessage> (accountId, email.Id);
                Assert.AreEqual (0, retrieved1.Count, "Should not return folders awaiting delete");
            }

            [Test]
            public void ShouldNotReturnFoldersWithoutFolderEntry ()
            {
                // should only return McFolders that have a corresponding entry in McMapFolderFolderEntry
                int accountId = 1;

                McFolder folder1 = CreateFolder (accountId, autoInsert: false);
                McEmailMessage firstEmail = CreateUniqueItem<McEmailMessage> (accountId);
                McEmailMessage secondEmail = CreateUniqueItem<McEmailMessage> (accountId);
                folder1.Link (secondEmail); // insert second email, but query for first email

                List <McFolder> retrieved1 = McFolder.QueryByFolderEntryId<McEmailMessage> (accountId, firstEmail.Id);
                Assert.AreEqual (0, retrieved1.Count, "Should not find folders if folder ID and folderEntry ID differ");
            }

            [Test]
            public void ShouldQueryByFolderEntryAccountId ()
            {
                // should query folders by McMapFolderFolderEntry accountId
                int accountId = 1;
                int accountIdOther = 2;

                McFolder folder1 = CreateFolder (accountId);
                McEmailMessage email = CreateUniqueItem<McEmailMessage> (accountId);
                folder1.Link (email);

                List <McFolder> retrieved1 = McFolder.QueryByFolderEntryId<McEmailMessage> (accountIdOther, email.Id);
                List <McFolder> retrieved2 = McFolder.QueryByFolderEntryId<McEmailMessage> (accountId, email.Id);
                Assert.AreEqual (0, retrieved1.Count, "Should not retrieve folder with wrong accountId");
                Assert.AreEqual (1, retrieved2.Count, "Should be able to retrieve folder by correct accountId");
            }

            [Test]
            public void ShouldReturnFoldersWithFolderCode ()
            {
                // don't return folders that don't have the Folder class code
                int accountId = 1;

                McFolder folder1 = CreateFolder (accountId);
                McEmailMessage email = CreateUniqueItem<McEmailMessage> (accountId);
                folder1.Link (email);

                List<McFolder> retrieved1 = McFolder.QueryByFolderEntryId<McCalendar> (accountId, email.Id);
                Assert.AreEqual (0, retrieved1.Count, "Should not retrieve emails that don't have the email class code");

                List <McFolder> retrieved2 = McFolder.QueryByFolderEntryId<McEmailMessage> (accountId, email.Id);
                Assert.AreEqual (1, retrieved2.Count, "Should retrieve matching folder once it has the correct class code");
                FoldersAreEqual (folder1, retrieved2.FirstOrDefault (), "Retrieved folder should match inserted (and updated) folder");
            }

            [Test]
            public void QueryByMcItemType ()
            {
                QueryFolderOfGenericType<McEmailMessage> (ClassCode.Email, "Should be able to query email messages by folderEntryId");
                QueryFolderOfGenericType<McCalendar> (ClassCode.Calendar, "Should be able to query all calendars by folderEntryId");
                QueryFolderOfGenericType<McContact> (ClassCode.Contact, "Should be able to query all contacts by folderEntryId");
                QueryFolderOfGenericType<McTask> (ClassCode.Tasks, "Should be able to query all tasks by folderEntryId");
            }

            private void QueryFolderOfGenericType<T> (ClassCode classCode, string message) where T : McItem, new()
            {
                int accountId = 1;

                T newItem = CreateUniqueItem<T> (accountId);

                McFolder folder = CreateFolder (accountId, isAwaitingDelete: false);
                folder.Link (newItem);

                List<McFolder> retrieved1 = McFolder.QueryByFolderEntryId<T> (accountId, newItem.Id);
                Assert.AreEqual (1, retrieved1.Count, message);
            }
        }

        [TestFixture]
        public class TestQueryClientOwned : BaseMcFolderTest
        {
            [Test]
            public void TestQueryingClientOwnedFolders ()
            {
                CreateFolder (1, isClientOwned: true);
                CreateFolder (1, isClientOwned: true);
                CreateFolder (1, isClientOwned: false);
                McFolder folder4 = CreateFolder (2, isClientOwned: false);

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
                CreateFolder (1, isAwaitingDelete: true, isClientOwned: true);

                List<McFolder> retrieved1 = McFolder.QueryClientOwned (1, true);
                Assert.AreEqual (0, retrieved1.Count, "Should not retrieve any folders if only folder inserted is awaiting delete");

                McFolder folder1 = CreateFolder (1, isAwaitingDelete: false, isClientOwned: true);

                List <McFolder> retrieved2 = McFolder.QueryClientOwned (1, true);
                Assert.AreEqual (1, retrieved2.Count, "Should only retrieve client-owned folders that are not awaiting delete");
                FoldersAreEqual (folder1, retrieved2.FirstOrDefault (), "Retrieved folder should match inserted folder");

                McFolder folder2 = CreateFolder (1, isAwaitingDelete: false, isClientOwned: false);

                List <McFolder> retrieved3 = McFolder.QueryClientOwned (1, false);
                Assert.AreEqual (1, retrieved3.Count, "Should only retrieve non-client-owned folders that are not awaiting delete");
                FoldersAreEqual (folder2, retrieved3.FirstOrDefault (), "Retrieved folder should match inserted folder");
            }
        }

        [TestFixture]
        public class TestServerEndQuery : BaseMcFolderTest
        {
            [Test]
            public void CanQueryFoldersAwaitingDelete ()
            {
                // server-end should be able to process commands against folder until folder delete is complete
                McFolder folder1 = CreateFolder (1, isAwaitingDelete: true);
                CreateFolder (2);

                List<McFolder> retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (1, retrieved1.Count, "Server end query should return only 1 folder if only one has been inserted");
                FoldersAreEqual (folder1, retrieved1.FirstOrDefault (), "Server end query should return folder where isAwaitingDelete == true");

                CreateFolder (1, isAwaitingDelete: false);

                List<McFolder> retrieved2 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (2, retrieved2.Count, "Server end query should return both folders awaiting deletion and those that are not");
            }

            [Test]
            public void CannotQueryFoldersAwaitingCreate ()
            {
                // should not return folders that are awaiting creation on the server
                McFolder folder1 = CreateFolder (1, isAwaitingCreate: true);

                List<McFolder> retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (0, retrieved1.Count, "Server should not return a folder awaiting creation on the server");

                folder1.IsAwaitingCreate = false;
                folder1.Update ();

                retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (1, retrieved1.Count, "Server should return a folder once it is no longer awaiting creation");
                FoldersAreEqual (folder1, retrieved1.FirstOrDefault (), "Retrieved folder should be valid once it is created on the client");

                // should not return folders awaiting creation on server and awaiting deletion on client
                CreateFolder (2, isAwaitingDelete: true, isAwaitingCreate: true);

                List <McFolder> retrieved2 = McFolder.ServerEndQueryAll (2);
                Assert.AreEqual (0, retrieved2.Count, "Should not return folders awaiting creation on server and awaiting deletion on client");
            }

            [Test]
            public void ShouldNotReturnClientOwnedFolders ()
            {
                // should not return folders that are client owned
                McFolder folder1 = CreateFolder (1, isClientOwned: true);

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
        }

        [TestFixture]
        public class TestResetState : BaseMcFolderTest
        {
            private const string serverId = "Server Id";

            [Test]
            public void ShouldNotBreakDefaultFolder ()
            {
                McFolder folder1 = CreateFolder (1, isClientOwned: true);
                McFolder.AsResetState (1);

                McFolder retrieved1 = McFolder.GetClientOwnedFolder (1, defaultServerId);
                FoldersAreEqual (folder1, retrieved1, "Folder should be the same after resetting state");
                FlagsAreReset (retrieved1, "Folder flags should be correct when reset");
            }

            [Test]
            public void ShouldResetFolderSyncKey ()
            {
                McFolder folder1 = CreateFolder (1, asSyncKey: "10", isClientOwned: true);
                McFolder retrieved1 = McFolder.GetClientOwnedFolder (1, defaultServerId);
                Assert.AreEqual ("10", retrieved1.AsSyncKey, "AsSyncKey should be set correctly before reset event");
                McFolder.AsResetState (1);

                McFolder retrieved2 = McFolder.GetClientOwnedFolder (1, defaultServerId);
                FoldersAreEqual (folder1, retrieved2, "Folder core values should not be modified by AsResetState");
                FlagsAreReset (retrieved2, "Folder flags should have been reset correctly");
            }

            [Test]
            public void ShouldResetSyncMetaFlag ()
            {
                McFolder folder1 = CreateFolder (1, syncMetaToClient: false, isClientOwned: true);
                McFolder retrieved1 = McFolder.GetClientOwnedFolder (1, defaultServerId);
                Assert.AreEqual (false, retrieved1.AsSyncMetaToClientExpected, "AsSyncMeta... flag should be set correctly");
                McFolder.AsResetState (1);

                McFolder retrieved2 = McFolder.GetClientOwnedFolder (1, defaultServerId);
                FoldersAreEqual (folder1, retrieved1, "Folder core values should not be modified by AsResetState");
                FlagsAreReset (retrieved2, "Folder flags should have been reset correctly");

                // set both at the same time
                folder1.AsSyncMetaToClientExpected = false;
                folder1.AsSyncKey = "10";
                folder1.Update ();
                McFolder.AsResetState (1);
                McFolder retrieved3 = McFolder.GetClientOwnedFolder (1, defaultServerId);
                FlagsAreReset (retrieved3, "Both folder flags should have been reset correctly");
            }

            [Test]
            public void ShouldOnlyResetStateForAccountId ()
            {
                // only reset state for the specified account id
                CreateFolder (2, asSyncKey: "10", syncMetaToClient: false, isClientOwned: true);
                CreateFolder (1, asSyncKey: "10", syncMetaToClient: false, isClientOwned: true);  // only this folder should be retrieved

                McFolder.AsResetState (1);
                McFolder retrieved1 = McFolder.GetClientOwnedFolder (1, defaultServerId);
                FlagsAreReset (retrieved1, "Both folder flags should have been reset correctly");

                // check that folder1's flags _were not_ reset
                McFolder retrieved2 = McFolder.GetClientOwnedFolder (2, defaultServerId);
                Assert.AreEqual ("10", retrieved2.AsSyncKey, "Sync key should not be reset");
                Assert.AreEqual (false, retrieved2.AsSyncMetaToClientExpected, "AsSyncMetaToClientExpected should not be reset");
            }

            private void FlagsAreReset (McFolder actual, string message)
            {
                Assert.AreEqual ("0", actual.AsSyncKey, message);
                Assert.AreEqual (true, actual.AsSyncMetaToClientExpected, message);
            }
        }

        [TestFixture]
        public class TestDelete : BaseMcFolderTest
        {
            [Test]
            public void CanDeleteItemOfEachType ()
            {
                // delete emails
                TestDeletingItemOfType<McEmailMessage> ();

                // delete calendars
                TestDeletingItemOfType<McCalendar> ();

                // delete contacts
                TestDeletingItemOfType<McContact> ();

                // delete tasks
                TestDeletingItemOfType<McTask> ();
            }

            private void TestDeletingItemOfType<T> () where T : McItem, new() {
                int accountId = 1;

                T item = CreateUniqueItem<T> (accountId);

                McFolder folder1 = CreateFolder (accountId, serverId: defaultServerId);
                folder1.Link (item);

                // sanity checks
                T foundItem = McFolderEntry.QueryByServerId<T> (accountId, defaultServerId);
                Assert.AreEqual (item.Id, foundItem.Id, "Email insertion and linking sanity check");

                // deletion of folder should remove item too
                folder1.Delete ();
                McFolder retrieved2 = McFolder.GetClientOwnedFolder (accountId, defaultServerId);
                Assert.AreEqual (null, retrieved2, "No user folder should be found if it is deleted");
                T notFoundItem = McFolderEntry.QueryByServerId<T> (accountId, defaultServerId);
                Assert.AreEqual (null, notFoundItem, "Deleting a folder should remove any emails contained in that folder");
            }

            [Test]
            public void ShouldDeleteFoldersRecursively ()
            {
                int accountId = 1;
                string serverId = "My custom server";
                TypeCode typeCode = TypeCode.UserCreatedGeneric_1;

                // when deleting folders, should remove all contained folders
                McFolder parentFolder = CreateFolder (accountId, parentId: "0", typeCode: typeCode, serverId: serverId);
                McFolder childFolder = CreateFolder (accountId, parentId: parentFolder.Id.ToString (), typeCode: typeCode, serverId: serverId);
                McFolder subChildFolder = CreateFolder (accountId, parentId: childFolder.Id.ToString (), typeCode: typeCode, serverId: serverId);

                parentFolder.Link (childFolder);
                childFolder.Link (subChildFolder);

                McFolder foundFolder = McFolder.GetUserFolder (accountId, typeCode, childFolder.Id, subChildFolder.DisplayName);
                Assert.AreNotEqual (null, foundFolder, "Sanity test: Should retrieve a folder from query");
                FoldersAreEqual (subChildFolder, foundFolder, "Sanity check that subChild folder was added correctly");

                parentFolder.Delete ();

                McFolder notFoundFolder = McFolder.GetUserFolder (accountId, typeCode, childFolder.Id, subChildFolder.DisplayName);
                Assert.AreEqual (null, notFoundFolder, "McFolder should delete sub-folders recursively");
            }
        }
    }

    public class BaseMcFolderTest
    {
        public const string defaultServerId = "Default Server Id";

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

        public T CreateUniqueItem<T> (int accountId, string serverId = defaultServerId) where T : McItem, new ()
        {
            T newItem = new T ();
            newItem.AccountId = accountId;
            newItem.ServerId = serverId;
            newItem.Insert ();
            return newItem;
        }

        public McFolder CreateFolder (int accountId, bool isClientOwned = false, string parentId = "0", 
            string serverId = defaultServerId, string name = "Default name", TypeCode typeCode = TypeCode.UserCreatedGeneric_1,
            bool isAwaitingDelete = false, bool isAwaitingCreate = false, bool autoInsert = true, string asSyncKey = "0", 
            bool syncMetaToClient = true)
        {
            McFolder folder = McFolder.Create (accountId, isClientOwned, false, parentId, serverId, name, typeCode);

            folder.IsAwaitingDelete = isAwaitingDelete;
            folder.IsAwaitingCreate = isAwaitingCreate;
            folder.AsSyncKey = asSyncKey;
            folder.AsSyncMetaToClientExpected = syncMetaToClient;

            if (autoInsert) { folder.Insert (); }
            return folder;
        }
    }
}
