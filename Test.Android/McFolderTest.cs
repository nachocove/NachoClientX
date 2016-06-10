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
using ClassCode = NachoCore.Model.McAbstrFolderEntry.ClassCodeEnum;
using NachoAssertionFailure = NachoCore.Utils.NcAssert.NachoAssertionFailure;
using NachoCore;


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
                McFolder expectedFolder = FolderOps.CreateFolder (accountId, serverId: serverId, isClientOwned: true);
                McFolder expectedFolder2 = FolderOps.CreateFolder (accountId + 1, serverId: serverId, isClientOwned: true);

                var actualFolder = McFolder.GetClientOwnedFolder (accountId, serverId);

                FoldersAreEqual (expectedFolder, actualFolder, "Should be able to do basic query of client-owned folder");

                var actualFolders = McFolder.GetClientOwnedFolders (serverId);
                Assert.AreEqual (2, actualFolders.Count);
                if (actualFolders [0].Id == expectedFolder.Id) {
                    FoldersAreEqual (expectedFolder, actualFolders [0], "1st folder should be equal to expected folder");
                    FoldersAreEqual (expectedFolder2, actualFolders [1], "2nd folder should be equal to expected folder #2");
                } else {
                    FoldersAreEqual (expectedFolder, actualFolders [1], "2nd folder should be equal to expected folder");
                    FoldersAreEqual (expectedFolder2, actualFolders [0], "1st folder should be equal to expected folder #2");
                }
            }

            [Test]
            public void CanQueryClientOwnedDistFolders ()
            {
                int accountId = 1;

                // Outbox
                McFolder expectedOutbox = FolderOps.CreateFolder (accountId, serverId: McFolder.ClientOwned_Outbox, isClientOwned: true);

                // CalDrafts
                McFolder expectedCalDrafts = FolderOps.CreateFolder (accountId, serverId: McFolder.ClientOwned_CalDrafts, isClientOwned: true);

                // GalCache
                McFolder expectedGalCache = FolderOps.CreateFolder (accountId, serverId: McFolder.ClientOwned_GalCache, isClientOwned: true);

                // Gleaned
                McFolder expectedGleaned = FolderOps.CreateFolder (accountId, serverId: McFolder.ClientOwned_Gleaned, isClientOwned: true);

                // Lost and Found
                McFolder expectedLostFound = FolderOps.CreateFolder (accountId, serverId: McFolder.ClientOwned_LostAndFound, isClientOwned: true);

                // EmailDrafts
                McFolder expectedEmailDrafts = FolderOps.CreateFolder (accountId, serverId: McFolder.ClientOwned_EmailDrafts, isClientOwned: true);
                McFolder expectedEmailDrafts2 = FolderOps.CreateFolder (accountId + 1, serverId: McFolder.ClientOwned_EmailDrafts, isClientOwned: true);

                McFolder actualFolder1 = McFolder.GetClientOwnedOutboxFolder (accountId);
                FoldersAreEqual (expectedOutbox, actualFolder1, "Should be able to query for distinguished folder (Outbox)");

                McFolder actualFolder2 = McFolder.GetCalDraftsFolder (accountId);
                FoldersAreEqual (expectedCalDrafts, actualFolder2, "Should be able to query for distinguished folder (Calendar Drafts)");

                McFolder galCache = McFolder.GetGalCacheFolder (accountId);
                FoldersAreEqual (expectedGalCache, galCache, "Should be able to query for distinguished folder (GalCache)");

                McFolder gleaned = McFolder.GetGleanedFolder (accountId);
                FoldersAreEqual (expectedGleaned, gleaned, "Should be able to query for distinguished folder (Gleaned)");

                McFolder lostAndFound = McFolder.GetLostAndFoundFolder (accountId);
                FoldersAreEqual (expectedLostFound, lostAndFound, "Should be able to query for distinguished folder (Lost And Found)");

                McFolder emailDrafts = McFolder.GetClientOwnedDraftsFolder (accountId);
                FoldersAreEqual (expectedEmailDrafts, emailDrafts, "Should be able to query for distinguished folder (Email Drafts)");

                var emailDraftsFolders = McFolder.GetClientOwnedDraftsFolders ();
                Assert.AreEqual (2, emailDraftsFolders.Count);
                if (emailDraftsFolders [0].Id == expectedEmailDrafts.Id) {
                    FoldersAreEqual (expectedEmailDrafts, emailDraftsFolders [0], "1st folder should be equal to expected folder");
                    FoldersAreEqual (expectedEmailDrafts2, emailDraftsFolders [1], "2nd fodler should be equal to expected folder #2");
                } else {
                    FoldersAreEqual (expectedEmailDrafts, emailDraftsFolders [1], "2nd folder should be equal to expected folder");
                    FoldersAreEqual (expectedEmailDrafts2, emailDraftsFolders [0], "1st fodler should be equal to expected folder #2");
                }
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

                FolderOps.CreateFolder (accountId, typeCode: typeCode1, parentId: parentId, name: name);
                FolderOps.CreateFolder (accountId, typeCode: typeCode2, parentId: parentId, name: name);

                McFolder expected1 = McFolder.GetUserFolders (accountId, typeCode1, parentId, name).First ();
                McFolder expected2 = McFolder.GetUserFolders (accountId, typeCode2, parentId, name).First ();

                Assert.AreNotEqual (expected1.Type, expected2.Type, "Folders should be able to have the same name and parent as long as their typecodes are different");
            }

            [Test]
            public void TestParentIdVariance ()
            {
                // Same name and typecodes, different parent id
                int accountId = 1;
                TypeCode typeCode = TypeCode.UserCreatedCal_13;
                string name = "Name";

                string serverId1 = "1";
                string serverId2 = "2";

                var parent1 = FolderOps.CreateFolder (accountId, serverId: serverId1);
                var parent2 = FolderOps.CreateFolder (accountId, serverId: serverId2);

                FolderOps.CreateFolder (accountId, typeCode: typeCode, parentId: parent1.ServerId, name: name);
                FolderOps.CreateFolder (accountId, typeCode: typeCode, parentId: parent2.ServerId, name: name);

                McFolder expected1 = McFolder.GetUserFolders (accountId, typeCode, serverId1, name).First ();
                McFolder expected2 = McFolder.GetUserFolders (accountId, typeCode, serverId2, name).First ();

                Assert.AreNotEqual (expected1.ParentId, expected2.ParentId, "Folders with identical properties should be able to reside under different parents"); 
            }

            [Test]
            public void TestNameVariance ()
            {
                // Same parent id and typecodes, different name
                int accountId = 1;
                TypeCode typeCode = TypeCode.UserCreatedCal_13;
                string parentId = "0";

                string name1 = "First Name";
                string name2 = "Second Name";

                FolderOps.CreateFolder (accountId, typeCode: typeCode, parentId: parentId, name: name1);
                FolderOps.CreateFolder (accountId, typeCode: typeCode, parentId: parentId, name: name2);

                McFolder expected1 = McFolder.GetUserFolders (accountId, typeCode: typeCode, parentId: parentId, name: name1).First ();
                McFolder expected2 = McFolder.GetUserFolders (accountId, typeCode: typeCode, parentId: parentId, name: name2).First ();

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
                McFolder folder1 = FolderOps.CreateFolder (accountId, typeCode: typeCode);

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
        public class TestArchiveFolder : BaseMcFolderTest
        {
            const string Email = "bob@company.net";
            const string Password = "Password";

            private int accountId { get; set; }

            [SetUp]
            public new void SetUp ()
            {
                base.SetUp ();
                Assert.IsTrue (NcApplication.Instance.TelemetryService is Telemetry_NOOP, "Telemetry needs to be disabled");
                NcTask.StartService ();

                // Set up credential
                var account = new McAccount () {
                    AccountType = McAccount.AccountTypeEnum.Exchange,
                    AccountService = McAccount.AccountServiceEnum.Exchange,
                    EmailAddr = Email,
                };
                account.Insert ();
                var cred = new McCred () {
                    CredType = McCred.CredTypeEnum.Password,
                    AccountId = account.Id,
                    Username = Email,
                };
                accountId = account.Id;

                cred.Insert ();
                cred.UpdatePassword (Password);
                NcApplication.Instance.TestOnlyInvokeUseCurrentThread = true;
            }

            [TearDown]
            public void TearDown ()
            {
                NcApplication.Instance.TestOnlyInvokeUseCurrentThread = false;
            }

            [Test]
            public void TestGetArchiveFolder ()
            {
                var archiveFolder = McFolder.GetUserFolders (accountId, Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, "0", McFolder.ARCHIVE_DISPLAY_NAME).FirstOrDefault ();
                Assert.IsNull (archiveFolder);

                archiveFolder = McFolder.GetOrCreateArchiveFolder (accountId);
                Assert.NotNull (archiveFolder);

                // Do it again to test the code-path where the folder exists.
                archiveFolder = McFolder.GetOrCreateArchiveFolder (accountId);
                Assert.NotNull (archiveFolder);
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

                var folder1 = FolderOps.CreateFolder (1, parentId: "0", serverId: "2");
                FolderOps.CreateFolder (1, parentId: folder1.ServerId);

                List<McFolder> retrieved2 = McFolder.QueryByParentId (1, "5");
                Assert.AreEqual (0, retrieved2.Count, "Should return empty list of folders if none were found");
            }

            [Test]
            public void TestSingleFolderRetrieved ()
            {
                var folder1 = FolderOps.CreateFolder (1, parentId: "0", serverId: "5");
                var folder2 = FolderOps.CreateFolder (1, parentId: folder1.ServerId, serverId: "6");
                FolderOps.CreateFolder (1, parentId: folder2.ServerId);

                List<McFolder> retrieved = McFolder.QueryByParentId (1, parentId: folder1.ServerId);
                Assert.AreEqual (1, retrieved.Count, "Should return a single folder if only one folder has a parent id");
                FoldersAreEqual (folder2, retrieved.FirstOrDefault (), "Returned folder should match created folder");
            }

            [Test]
            public void TestMultipleFoldersRetrieved ()
            {
                var parent = FolderOps.CreateFolder (1, parentId: "0", serverId: "5");
                var folder2 = FolderOps.CreateFolder (1, parentId: parent.ServerId, serverId: "6");
                FolderOps.CreateFolder (1, parentId: folder2.ServerId, serverId: "7");
                FolderOps.CreateFolder (1, parentId: parent.ServerId, serverId: "8");
                FolderOps.CreateFolder (1, parentId: parent.ServerId, serverId: "9");

                List<McFolder> retrieved = McFolder.QueryByParentId (1, parent.ServerId);
                Assert.AreEqual (3, retrieved.Count, "Should return correct number of folders with matching parent id");
                foreach (McFolder folder in retrieved) {
                    Assert.AreEqual (1, folder.AccountId, "Account id's should match expected");
                    Assert.AreEqual (parent.ServerId, folder.ParentId, "Parent id's should match expected");
                }
            }
        }

        [TestFixture]
        public class TestQueryByServerId : BaseMcFolderTest
        {
            [Test]
            public void TestNoFolderRetrieved ()
            {
                McFolder retrieved1 = McFolder.QueryByServerId (1, "1");
                Assert.IsNull (retrieved1, "Should not retrieve any folders if none have been added");

                var folder1 = FolderOps.CreateFolder (1, parentId: "0", serverId: "2");
                FolderOps.CreateFolder (1, parentId: folder1.ServerId);

                McFolder retrieved2 = McFolder.QueryByServerId (1, "55");
                Assert.IsNull (retrieved2, "Should return empty list of folders if none were found");
            }

            [Test]
            public void TestSingleFolderRetrieved ()
            {
                var folder1 = FolderOps.CreateFolder (1, parentId: "0", serverId: "65");
                var folder2 = FolderOps.CreateFolder (1, parentId: folder1.ServerId, serverId: "66");
                FolderOps.CreateFolder (1, parentId: folder2.ServerId);

                McFolder retrieved = McFolder.QueryByServerId (1, serverId: folder1.ServerId);
                Assert.IsNotNull (retrieved, "Should return a single folder if only one folder has a parent id");
                FoldersAreEqual (folder1, retrieved, "Returned folder should match created folder");
            }

        }

        [TestFixture]
        public class QueryByMostRecentlyAccessedFolders : BaseMcFolderTest
        {
            [Test]
            public void ShouldReturnOrderedListOfMostRecentlyAccessedFolders ()
            {
                int accountId = 1;

                McFolder folder1 = FolderOps.CreateFolder (accountId, serverId: "1");
                McFolder folder2 = FolderOps.CreateFolder (accountId, serverId: "2");
                McFolder folder3 = FolderOps.CreateFolder (accountId, serverId: "3");
                DateTime now1 = DateTime.UtcNow;
                DateTime now2 = DateTime.UtcNow.AddHours (-2);
                DateTime now3 = DateTime.UtcNow.AddDays (-3);

                folder1 = folder1.UpdateSet_LastAccessed (now1);
                folder2 = folder2.UpdateSet_LastAccessed (now2);
                folder3 = folder3.UpdateSet_LastAccessed (now3);

                List<McFolder> recentlyAccessed = McFolder.QueryByMostRecentlyAccessedVisibleFolders (accountId);
                FoldersAreEqual (folder1, recentlyAccessed [0], "folder1 should be the first folder in the list");
                FoldersAreEqual (folder2, recentlyAccessed [1], "folder2 should be the second folder in the list");
                FoldersAreEqual (folder3, recentlyAccessed [2], "folder3 should be the last folder in the list");
            }
        }

        [TestFixture]
        public class QueryNonHiddenFoldersOfType : BaseMcFolderTest
        {
            [Test]
            public void TestQueryNonHiddenFoldersOfType ()
            {
                int accountId = 1;

                McFolder differentAccountId = FolderOps.CreateFolder (2, typeCode: Xml.FolderHierarchy.TypeCode.DefaultDrafts_3, name: "apple");
                McFolder waitingToDelete = FolderOps.CreateFolder (accountId, isAwaitingDelete: true, typeCode: Xml.FolderHierarchy.TypeCode.DefaultDrafts_3, name: "banana");
                McFolder defaultEmailDrafts = FolderOps.CreateFolder (accountId, typeCode: Xml.FolderHierarchy.TypeCode.DefaultDrafts_3, name: "carrot");
                McFolder deviceCalendarDrafts = FolderOps.CreateFolder (accountId, isClientOwned: true, typeCode: Xml.FolderHierarchy.TypeCode.UserCreatedCal_13, name: "date");
                McFolder defaultInbox = FolderOps.CreateFolder (accountId, typeCode: Xml.FolderHierarchy.TypeCode.DefaultInbox_2, name: "elderberry");
                McFolder hiddenDraftFolder = FolderOps.CreateFolder (accountId, isClientOwned: true, isHidden: true, typeCode: Xml.FolderHierarchy.TypeCode.UserCreatedCal_13, name: "fennel");

                Xml.FolderHierarchy.TypeCode[] draftTypes = {
                    Xml.FolderHierarchy.TypeCode.DefaultDrafts_3,
                    Xml.FolderHierarchy.TypeCode.UserCreatedCal_13,
                };

                List<McFolder> nonHiddenDraftFolders = McFolder.QueryNonHiddenFoldersOfType (accountId, draftTypes);
                List<int> nonHiddenDraftFoldersIds = new List<int> ();
                foreach (McFolder f in nonHiddenDraftFolders) {
                    nonHiddenDraftFoldersIds.Add (f.Id);
                }

                Assert.False (nonHiddenDraftFoldersIds.Contains (differentAccountId.Id)); //AccountId different than the rest / not querying by it
                Assert.False (nonHiddenDraftFoldersIds.Contains (waitingToDelete.Id)); //matches query criteria except that it is awaiting delete
                Assert.False (nonHiddenDraftFoldersIds.Contains (defaultInbox.Id)); //matches criteria except that it isn't in the listed type []
                Assert.False (nonHiddenDraftFoldersIds.Contains (hiddenDraftFolder.Id)); //matches criteria except it's hidden
                Assert.True (nonHiddenDraftFoldersIds.Contains (defaultEmailDrafts.Id)); //match
                Assert.True (nonHiddenDraftFoldersIds.Contains (deviceCalendarDrafts.Id)); //match

                List<McFolder> emptyFoldersList = McFolder.QueryNonHiddenFoldersOfType (accountId, new Xml.FolderHierarchy.TypeCode[0]); //empty list
                Assert.True (0 == emptyFoldersList.Count);
                #pragma warning disable 0219
                McFolder defaultEmailDraftsTwo = FolderOps.CreateFolder (accountId, typeCode: Xml.FolderHierarchy.TypeCode.DefaultDrafts_3, name: "honeydew-melon"); //flip-flop alphabetical order of insertions
                McFolder defaultEmailDraftsThree = FolderOps.CreateFolder (accountId, typeCode: Xml.FolderHierarchy.TypeCode.DefaultDrafts_3, name: "guava");
                #pragma warning restore 0219
                nonHiddenDraftFolders = McFolder.QueryNonHiddenFoldersOfType (accountId, draftTypes);

                Assert.True (4 == nonHiddenDraftFolders.Count);
                Assert.True (nonHiddenDraftFolders [0].DisplayName.StartsWith ("c"));
                Assert.True (nonHiddenDraftFolders [1].DisplayName.StartsWith ("d"));
                Assert.True (nonHiddenDraftFolders [2].DisplayName.StartsWith ("g"));
                Assert.True (nonHiddenDraftFolders [3].DisplayName.StartsWith ("h"));
            }
        }

        [Test]
        public void TestTypesToCommaDelimitedString ()
        {
            Xml.FolderHierarchy.TypeCode[] typeArray = {
                Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1,
                Xml.FolderHierarchy.TypeCode.DefaultInbox_2,
                Xml.FolderHierarchy.TypeCode.DefaultDrafts_3,
            };

            string typesArrayAsString = Folder_Helpers.TypesToCommaDelimitedString (typeArray);
            Assert.True (typesArrayAsString.Equals ("(1,2,3)"));

            Xml.FolderHierarchy.TypeCode[] emptyTypeArray = new TypeCode[0];
            typesArrayAsString = Folder_Helpers.TypesToCommaDelimitedString (emptyTypeArray);
            Assert.True (typesArrayAsString.Equals ("()"));
        }

        [TestFixture]
        public class TestQueryByFolderEntryId : BaseMcFolderTest
        {
            [Test]
            public void ShouldQueryByEntryId ()
            {
                int accountId = 1;

                McFolder folder1 = FolderOps.CreateFolder (accountId);
                McEmailMessage email = FolderOps.CreateUniqueItem<McEmailMessage> (accountId);
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

                McFolder folder1 = FolderOps.CreateFolder (accountId, isAwaitingDelete: true);
                McEmailMessage email = FolderOps.CreateUniqueItem<McEmailMessage> (accountId);
                folder1.Link (email);

                List<McFolder> retrieved1 = McFolder.QueryByFolderEntryId<McEmailMessage> (accountId, email.Id);
                Assert.AreEqual (0, retrieved1.Count, "Should not return folders awaiting delete");
            }

            [Test]
            public void ShouldNotReturnFoldersWithoutFolderEntry ()
            {
                // should only return McFolders that have a corresponding entry in McMapFolderFolderEntry
                int accountId = 1;

                McFolder folder1 = FolderOps.CreateFolder (accountId, autoInsert: false);
                McEmailMessage firstEmail = FolderOps.CreateUniqueItem<McEmailMessage> (accountId);
                McEmailMessage secondEmail = FolderOps.CreateUniqueItem<McEmailMessage> (accountId, FolderOps.defaultServerId + "1");
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

                McFolder folder1 = FolderOps.CreateFolder (accountId);
                McEmailMessage email = FolderOps.CreateUniqueItem<McEmailMessage> (accountId);
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

                McFolder folder1 = FolderOps.CreateFolder (accountId);
                McEmailMessage email = FolderOps.CreateUniqueItem<McEmailMessage> (accountId);
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

            private void QueryFolderOfGenericType<T> (ClassCode classCode, string message) where T : McAbstrItem, new()
            {
                int accountId = 1;

                T newItem = FolderOps.CreateUniqueItem<T> (accountId);

                McFolder folder = FolderOps.CreateFolder (accountId, isAwaitingDelete: false);
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
                FolderOps.CreateFolder (1, isClientOwned: true);
                FolderOps.CreateFolder (1, isClientOwned: true);
                FolderOps.CreateFolder (1, isClientOwned: false);
                McFolder folder4 = FolderOps.CreateFolder (2, isClientOwned: false);

                // should return folders that are client owned if asked
                List<McFolder> retrieved1 = McFolder.QueryByIsClientOwned (1, isClientOwned: true);
                Assert.AreEqual (2, retrieved1.Count, "Querying client-owned folders not awaiting delete should return correct number of folders");

                // should return folders that are not client-owned if asked
                List<McFolder> retrieved2 = McFolder.QueryByIsClientOwned (2, isClientOwned: false);
                Assert.AreEqual (1, retrieved2.Count, "Querying non-client-owned folders awaiting delete should return correct number of folders");
                FoldersAreEqual (folder4, retrieved2.FirstOrDefault (), "Retrieved folder awaiting delete should match inserted folder");
            }

            [Test]
            public void TestFoldersAwaitingDelete ()
            {
                // should not return folders that are awaiting delete
                FolderOps.CreateFolder (1, isAwaitingDelete: true, isClientOwned: true);

                List<McFolder> retrieved1 = McFolder.QueryByIsClientOwned (1, true);
                Assert.AreEqual (0, retrieved1.Count, "Should not retrieve any folders if only folder inserted is awaiting delete");

                McFolder folder1 = FolderOps.CreateFolder (1, isAwaitingDelete: false, isClientOwned: true);

                List <McFolder> retrieved2 = McFolder.QueryByIsClientOwned (1, true);
                Assert.AreEqual (1, retrieved2.Count, "Should only retrieve client-owned folders that are not awaiting delete");
                FoldersAreEqual (folder1, retrieved2.FirstOrDefault (), "Retrieved folder should match inserted folder");

                McFolder folder2 = FolderOps.CreateFolder (1, isAwaitingDelete: false, isClientOwned: false);

                List <McFolder> retrieved3 = McFolder.QueryByIsClientOwned (1, false);
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
                McFolder folder1 = FolderOps.CreateFolder (1, isAwaitingDelete: true);
                FolderOps.CreateFolder (2);

                List<McFolder> retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (1, retrieved1.Count, "Server end query should return only 1 folder if only one has been inserted");
                FoldersAreEqual (folder1, retrieved1.FirstOrDefault (), "Server end query should return folder where isAwaitingDelete == true");

                FolderOps.CreateFolder (1, isAwaitingDelete: false);

                List<McFolder> retrieved2 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (2, retrieved2.Count, "Server end query should return both folders awaiting deletion and those that are not");
            }

            [Test]
            public void CannotQueryFoldersAwaitingCreate ()
            {
                // should not return folders that are awaiting creation on the server
                McFolder folder1 = FolderOps.CreateFolder (1, isAwaitingCreate: true);

                List<McFolder> retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (0, retrieved1.Count, "Server should not return a folder awaiting creation on the server");

                folder1 = folder1.UpdateSet_IsAwaitingCreate (false);

                retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (1, retrieved1.Count, "Server should return a folder once it is no longer awaiting creation");
                FoldersAreEqual (folder1, retrieved1.FirstOrDefault (), "Retrieved folder should be valid once it is created on the client");

                // should not return folders awaiting creation on server and awaiting deletion on client
                FolderOps.CreateFolder (2, isAwaitingDelete: true, isAwaitingCreate: true);

                List <McFolder> retrieved2 = McFolder.ServerEndQueryAll (2);
                Assert.AreEqual (0, retrieved2.Count, "Should not return folders awaiting creation on server and awaiting deletion on client");
            }

            [Test]
            public void ShouldNotReturnClientOwnedFolders ()
            {
                // should not return folders that are client owned
                McFolder folder1 = FolderOps.CreateFolder (1, isClientOwned: true);

                List<McFolder> retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (0, retrieved1.Count, "Should not return folders that are client owned");

                // add isAwaitingDelete flag
                folder1 = folder1.UpdateSet_IsAwaitingDelete (true);

                retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (0, retrieved1.Count, "Should not return folders that are client owned, even if they are awaiting delete");

                // folder is no longer client owned
                folder1 = folder1.UpdateSet_IsClientOwned (false);

                retrieved1 = McFolder.ServerEndQueryAll (1);
                Assert.AreEqual (1, retrieved1.Count, "Should return folders that are client owned, but previously were not");
                FoldersAreEqual (folder1, retrieved1.FirstOrDefault (), "Non-client owned folder returned should match inserted (and updated) folder");
            }
        }

        [TestFixture]
        public class TestAsSetExpected : BaseMcFolderTest
        {
            [Test]
            public void ShouldNotSetClientFolder ()
            {
                int accountId = 1;
                var folder = FolderOps.CreateFolder (accountId, isClientOwned: true);
                McFolder.UpdateSet_AsSyncMetaToClientExpected (accountId, true);
                var received = McFolder.QueryById<McFolder> (folder.Id);
                Assert.AreEqual (false, received.AsSyncMetaToClientExpected, "Should not set meta to true on client-owned folder");
                FoldersAreEqual (folder, received, "Should not modify client-owned folder");
            }

            [Test]
            public void ShouldSetSyncedFolder ()
            {
                int accountId = 1;
                var folder = FolderOps.CreateFolder (accountId, isClientOwned: false, syncMetaToClient: false);
                McFolder.UpdateSet_AsSyncMetaToClientExpected (accountId, true);
                var received = McFolder.QueryById<McFolder> (folder.Id);
                Assert.AreEqual (true, received.AsSyncMetaToClientExpected, "Should set meta to true on synced folder");
                FoldersAreEqual (folder, received, "Should not modify non-meta fields");
            }
        }

        [TestFixture]
        public class TestResetState : BaseMcFolderTest
        {
            private const string serverId = "Server Id";

            [Test]
            public void ShouldNotBreakDefaultFolder ()
            {
                McFolder folder1 = FolderOps.CreateFolder (1, isClientOwned: false);
                McFolder.UpdateResetSyncState (1);

                McFolder retrieved1 = McFolder.QueryById<McFolder> (folder1.Id);
                FoldersAreEqual (folder1, retrieved1, "Folder should be the same after resetting state");
                FlagsAreReset (retrieved1, "Folder flags should be correct when reset");
            }

            [Test]
            public void ShouldResetFolderSyncKey ()
            {
                McFolder folder1 = FolderOps.CreateFolder (1, asSyncKey: "10", isClientOwned: false);
                McFolder retrieved1 = McFolder.QueryById<McFolder> (folder1.Id);
                Assert.AreEqual ("10", retrieved1.AsSyncKey, "AsSyncKey should be set correctly before reset event");
                McFolder.UpdateResetSyncState (1);

                McFolder retrieved2 = McFolder.QueryById<McFolder> (folder1.Id);
                FoldersAreEqual (folder1, retrieved2, "Folder core values should not be modified by AsResetState");
                FlagsAreReset (retrieved2, "Folder flags should have been reset correctly");
            }

            [Test]
            public void TestDeleteInUpdateWithOCApply ()
            {
                var folder1 = FolderOps.CreateFolder (1, syncMetaToClient: false, isClientOwned: false);
                var folder2 = McFolder.QueryById<McFolder> (folder1.Id);
                folder2 = folder2.UpdateSet_AsSyncMetaToClientExpected (true);
                var folder3 = folder1.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.Delete ();
                    return true;
                });
                Assert.IsNull (folder3);
                Assert.AreEqual (folder1.Id, folder2.Id);
            }

            [Test]
            public void ShouldResetSyncMetaFlag ()
            {
                McFolder folder1 = FolderOps.CreateFolder (1, syncMetaToClient: false, isClientOwned: false);
                McFolder retrieved1 = McFolder.QueryById<McFolder> (folder1.Id);
                Assert.AreEqual (false, retrieved1.AsSyncMetaToClientExpected, "AsSyncMeta... flag should be set correctly");
                McFolder.UpdateResetSyncState (1);

                McFolder retrieved2 = McFolder.QueryById<McFolder> (folder1.Id);
                FoldersAreEqual (folder1, retrieved2, "Folder core values should not be modified by AsResetState");
                FlagsAreReset (retrieved2, "Folder flags should have been reset correctly");

                // set both at the same time
                folder1 = folder1.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.AsSyncMetaToClientExpected = false;
                    target.AsSyncKey = "10";
                    return true;
                });
                McFolder.UpdateResetSyncState (1);
                McFolder retrieved3 = McFolder.QueryById<McFolder> (folder1.Id);
                FlagsAreReset (retrieved3, "Both folder flags should have been reset correctly");
            }

            [Test]
            public void ShouldOnlyResetStateForAccountId ()
            {
                // only reset state for the specified account id
                var folder1 = FolderOps.CreateFolder (2, asSyncKey: "10", syncMetaToClient: false, isClientOwned: false);
                var folder2 = FolderOps.CreateFolder (1, asSyncKey: "10", syncMetaToClient: false, isClientOwned: false);  // only this folder should be retrieved

                McFolder.UpdateResetSyncState (1);
                McFolder retrieved1 = McFolder.QueryById<McFolder> (folder2.Id);
                FlagsAreReset (retrieved1, "Both folder flags should have been reset correctly");

                // check that folder1's flags _were not_ reset
                McFolder retrieved2 = McFolder.QueryById<McFolder> (folder1.Id);
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
        public class TestMovingItems : BaseMcFolderTest
        {
            private string TestServerEndMoveFolderToClientOwned (Action<string> makeSubEntry)
            {
                // Set up item and destination folder
                string itemServerId = "11";
                string destServerId = "12";

                var destFolder = FolderOps.CreateFolder (accountId: defaultAccountId, serverId: destServerId, isClientOwned: true);

                // Allow caller to create folders or items inside the owner folder
                makeSubEntry (itemServerId);

                McFolder.ServerEndMoveToClientOwned (defaultAccountId, itemServerId, destServerId);
                return destFolder.ServerId;
            }

            private McFolder TestFolderMovedCorrectly (string destServerId, McFolder folder)
            {
                // Test that folder was moved correctly
                var foundFolders = McFolder.QueryByParentId (defaultAccountId, destServerId);
                Assert.AreEqual (1, foundFolders.Count, "Should move folder with subItems to client-owned folder with matching destFolderId");
                var foundFolder = foundFolders.FirstOrDefault ();
                Assert.AreEqual (folder.Id, foundFolder.Id, "Should move correct item into client-owned folder");
                Assert.AreEqual (true, foundFolder.IsClientOwned, "Should set moved item to client-owned");

                return foundFolder;
            }

            [Test]
            public void TestMovingEmptyFolderToClientOwned ()
            {
                // should move empty folder with matching ServerId to client-owned folder with matching destFolderId
                McFolder folder = new McFolder (); // must assign default value

                var destServerId = TestServerEndMoveFolderToClientOwned ((itemServerId) => {
                    folder = FolderOps.CreateFolder (accountId: defaultAccountId, serverId: itemServerId, isClientOwned: false);
                });

                TestFolderMovedCorrectly (destServerId, folder);
            }

            [Test]
            public void TestMovingFolderWithItemsToClientOwned ()
            {
                // should move folder with items and matching ServerId to client-owned folder
                McFolder folder = new McFolder (); // must assign default value
                McAbstrItem subItem1;
                McAbstrItem subItem2;

                var destServerId = TestServerEndMoveFolderToClientOwned ((itemServerId) => {
                    folder = FolderOps.CreateFolder (accountId: defaultAccountId, serverId: itemServerId, isClientOwned: false);
                    subItem1 = FolderOps.CreateUniqueItem<McEmailMessage> (accountId: defaultAccountId, serverId: "50");
                    subItem2 = FolderOps.CreateUniqueItem<McEmailMessage> (accountId: defaultAccountId, serverId: "51");
                    folder.Link (subItem1);
                    folder.Link (subItem2);
                });

                var foundFolder = TestFolderMovedCorrectly (destServerId, folder);

                // Test that the folder's sub-items got moved correctly
                var foundItems = McAbstrItem.QueryByFolderId<McEmailMessage> (defaultAccountId, foundFolder.Id);
                Assert.AreEqual (2, foundItems.Count, "Should move subItems to client-owned folder with matching destFolderId");
            }

            [Test]
            public void TestMovingFolderWithSubFoldersToClientOwned ()
            {
                // should move folder with subfolders to client-owned folder recursively
                McFolder folder = new McFolder ();

                var destServerId = TestServerEndMoveFolderToClientOwned ((itemServerId) => {
                    folder = FolderOps.CreateFolder (accountId: defaultAccountId, serverId: itemServerId, isClientOwned: false);
                    FolderOps.CreateFolder (accountId: defaultAccountId, parentId: itemServerId);
                    FolderOps.CreateFolder (accountId: defaultAccountId, parentId: itemServerId);
                });

                var found = TestFolderMovedCorrectly (destServerId, folder);
                var foundSubFolders = McFolder.QueryByParentId (defaultAccountId, found.ServerId);
                Assert.AreEqual (2, foundSubFolders.Count, "Should move subFolders recursively to client-owned folder with matching destFolderId");
            }

            private void TestMovedToClientOwned (McAbstrFolderEntry item, McFolder folder)
            {
                // Test that everything has been moved to the destination folder and retains it's correct structure
                var foundItems = McAbstrItem.QueryByFolderId<McEmailMessage> (defaultAccountId, folder.Id);
                Assert.AreEqual (1, foundItems.Count, "Should move item with matching ServerId to client-owned folder with matching destFolderId");
                var foundItem = foundItems.FirstOrDefault ();
                Assert.AreEqual (item.Id, foundItem.Id, "Should move correct item into client-owned folder");
            }

            [Test]
            public void TestNcAsserts ()
            {
                // should NcAssert if no folder matches destFolderId
                string goodItemServerId = "50";
                string badDestFolderId = "51";
                FolderOps.CreateUniqueItem<McEmailMessage> (accountId: defaultAccountId, serverId: goodItemServerId);

                TestForNachoExceptionFailure (() => {
                    McFolder.ServerEndMoveToClientOwned (defaultAccountId, goodItemServerId, badDestFolderId);
                }, "Should throw NcAssert if moving synced item to destFolder with bad Id");

                // should NcAssert if no folder entry matches serverId
                string badItemServerId = "60";
                string goodDestFolderId = "61";
                FolderOps.CreateFolder (accountId: defaultAccountId, serverId: goodDestFolderId);

                TestForNachoExceptionFailure (() => {
                    McFolder.ServerEndMoveToClientOwned (defaultAccountId, badItemServerId, goodDestFolderId);
                }, "Should throw NcAssert if trying to move a synced item but none matches the ServerId param");

                // should NcAssert if the folder entry you are trying to move is already client-owned
                string alreadyClientOwned = "70";
                string ownerFolder = "71";
                string destFolderId = "72";

                var owner = FolderOps.CreateFolder (accountId: defaultAccountId, serverId: ownerFolder);
                var item = FolderOps.CreateUniqueItem<McEmailMessage> (accountId: defaultAccountId, serverId: alreadyClientOwned);
                owner.Link (item);

                FolderOps.CreateFolder (accountId: defaultAccountId, serverId: destFolderId);

                TestForNachoExceptionFailure (() => {
                    McFolder.ServerEndMoveToClientOwned (defaultAccountId, alreadyClientOwned, destFolderId);
                }, "Should throw NcAssert if trying to move an item that is already client-owned");
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

            private void TestDeletingItemOfType<T> () where T : McAbstrItem, new()
            {
                int accountId = 1;

                T item = FolderOps.CreateUniqueItem<T> (accountId);

                McFolder folder1 = FolderOps.CreateFolder (accountId, serverId: FolderOps.defaultServerId);
                folder1.Link (item);

                // sanity checks
                T foundItem = McAbstrFolderEntry.QueryByServerId<T> (accountId, FolderOps.defaultServerId);
                Assert.AreEqual (item.Id, foundItem.Id, "Email insertion and linking sanity check");

                // deletion of folder should remove item too
                folder1.Delete ();
                McFolder retrieved2 = McFolder.GetClientOwnedFolder (accountId, FolderOps.defaultServerId);
                Assert.AreEqual (null, retrieved2, "No user folder should be found if it is deleted");
                T notFoundItem = McAbstrFolderEntry.QueryByServerId<T> (accountId, FolderOps.defaultServerId);
                Assert.AreEqual (null, notFoundItem, "Deleting a folder should remove any emails contained in that folder");
            }

            [Test]
            public void ShouldDeleteFoldersRecursively ()
            {
                int accountId = 1;
                string parentServerId = "1";
                string childServerId = "2";
                string subChildServerId = "3";

                // when deleting folders, should remove all contained folders
                McFolder parentFolder = FolderOps.CreateFolder (accountId, parentId: "0", serverId: parentServerId);
                McFolder childFolder = FolderOps.CreateFolder (accountId, parentId: parentServerId, serverId: childServerId);
                McFolder subChildFolder = FolderOps.CreateFolder (accountId, parentId: childServerId, serverId: subChildServerId);

                var foundFolder = McFolder.QueryById<McFolder> (subChildFolder.Id);
                Assert.AreNotEqual (null, foundFolder, "Sanity test: Should retrieve a folder from query");
                FoldersAreEqual (subChildFolder, foundFolder, "Sanity check that subChild folder was added correctly");

                parentFolder.Delete ();

                var notFoundFolder = McFolder.QueryById<McFolder> (subChildFolder.Id);
                Assert.AreEqual (null, notFoundFolder, "McFolder should delete sub-folders recursively");
                var notFoundChild = McFolder.QueryById<McFolder> (childFolder.Id);
                Assert.AreEqual (null, notFoundChild);
            }
        }

        [TestFixture]
        public class TestLinking : BaseMcFolderTest
        {
            [Test]
            public void TestLink ()
            {
                int accountId = 1;
                McFolder folder = FolderOps.CreateFolder (accountId);
                McEmailMessage email = FolderOps.CreateUniqueItem<McEmailMessage> (accountId);

                var result = folder.Link (email);
                Assert.AreEqual (result.Kind, NcResult.KindEnum.OK);

                List<McMapFolderFolderEntry> folderEntries = McMapFolderFolderEntry.QueryByFolderId (accountId, folder.Id);
                var folderEntry = folderEntries.FirstOrDefault ();
                if (folderEntry == null) {
                    Assert.Fail ("No matching folder entries were found");  // sanity check
                }
                Assert.AreEqual (folder.AccountId, folderEntry.AccountId, "Account ID should be set correctly");
                Assert.AreEqual (email.Id, folderEntry.FolderEntryId, "ID of object (folder entry) should be set correctly");

                // error should be thrown if object already exists in folder
                result = folder.Link (email);
                Assert.AreEqual (result.SubKind, NcResult.SubKindEnum.Error_AlreadyInFolder, "Should return error result if object already exists in folder");
            }

            [Test]
            public void TestUnlink ()
            {
                int accountId = 1;
                McFolder folder = FolderOps.CreateFolder (accountId);
                McEmailMessage email = FolderOps.CreateUniqueItem<McEmailMessage> (accountId);

                var result = folder.Unlink (email);
                Assert.AreEqual (NcResult.SubKindEnum.Error_NotInFolder, result.SubKind, "Should return error if unlinking nonexistent object");

                folder.Link (email);

                result = folder.Unlink (email);
                Assert.AreEqual (NcResult.KindEnum.OK, result.Kind, "Result should be okay when unlink succeeds");
                List<McMapFolderFolderEntry> folderEntries = McMapFolderFolderEntry.QueryByFolderId (accountId, folder.Id);
                var folderEntry = folderEntries.FirstOrDefault ();
                Assert.AreEqual (folderEntry, null, "Folder was not unlinked correctly");
            }

            [Test]
            public void TestUnlinkAll ()
            {
                int accountId = 1;
                string customServerId = "Custom Server ID";

                McFolder folder1 = FolderOps.CreateFolder (accountId);
                McFolder folder2 = FolderOps.CreateFolder (accountId);
                McEmailMessage email = FolderOps.CreateUniqueItem<McEmailMessage> (accountId);
                McEmailMessage otherEmail = FolderOps.CreateUniqueItem<McEmailMessage> (accountId, serverId: customServerId);

                var result = McFolder.UnlinkAll (email);
                Assert.AreEqual (NcResult.KindEnum.OK, result.Kind, "Should be okay to delete non-existent object");

                folder1.Link (email);
                folder2.Link (email);
                folder1.Link (otherEmail);
                folder2.Link (otherEmail);

                result = McFolder.UnlinkAll (email);
                Assert.AreEqual (NcResult.KindEnum.OK, result.Kind, "UnlinkAll should result in OK NcResult");

                // Should unlink email but not unlink otherEmail
                var folderEntries = McMapFolderFolderEntry.QueryByFolderId (accountId, folder1.Id);
                Assert.AreEqual (1, folderEntries.Count, "Email should have been unlinked while otherEmail should remain linked");
                var folderEntry = folderEntries.FirstOrDefault ();
                Assert.AreNotEqual (null, folderEntry, "otherEmail should not have been unlinked");

                Assert.AreEqual (otherEmail.Id, folderEntry.FolderEntryId, "Retrieved object should match otherEmail, not email");
            }
        }

        [TestFixture]
        public class FolderConstraints : BaseMcFolderTest
        {
            // A client-owned folder can’t be created inside a synced folder.
            // Exception should be thrown if parent is wrong kind
            [Test]
            public void TestClientOwnedInsideSynced ()
            {
                int accountId = 1;
                McFolder syncedFolder = FolderOps.CreateFolder (accountId, isClientOwned: false);

                TestForNachoExceptionFailure (() => {
                    FolderOps.CreateFolder (accountId, isClientOwned: true, parentId: syncedFolder.Id.ToString ());
                }, "Should throw NachoExceptionFailure when creating client folder with synced parent");
            }

            // A synced folder can’t be created inside a client-owned folder.
            // Exception should be thrown if parent is wrong kind
            [Test]
            public void TestSyncedInsideClientOwned ()
            {
                int accountId = 1;
                McFolder clientFolder = FolderOps.CreateFolder (accountId, isClientOwned: true);

                TestForNachoExceptionFailure (() => {
                    FolderOps.CreateFolder (accountId, isClientOwned: false, parentId: clientFolder.Id.ToString ());
                }, "Should throw NachoExceptionFailure when creating synced folder with client parent");
            }

            // A synced folder can’t be hidden. --> !isClientOwned ? !isHidden : (isHidden || !isHidden)
            [Test]
            public void TestHiddenSynced ()
            {
                int accountId = 1;

                // try creating a synced folder with hidden == true
                TestForNachoExceptionFailure (() => {
                    FolderOps.CreateFolder (accountId, isClientOwned: false, isHidden: true);
                }, "Should throw NachoExceptionFailure when creating a synced folder with isHidden set to true");

                // try creating a synced folder, then setting hidden to true
                McFolder syncedFolder = FolderOps.CreateFolder (accountId, isClientOwned: false, isHidden: false);
                TestForNachoExceptionFailure (() => {
                    syncedFolder.UpdateSet_IsHidden (true);
                }, "Should throw NachoExceptionFailure when updating a synced folder after setting isHidden to true");

                // try creating a hidden folder, then setting it to synced
                var hiddenFolder = FolderOps.CreateFolder (accountId, isClientOwned: true, isHidden: true);
                TestForNachoExceptionFailure (() => {
                    hiddenFolder.UpdateSet_IsClientOwned (false);
                }, "Should throw NachoExceptionFailure when changing a client-owned folder to synced after isHidden is set to true");
            }

            // A folder for one account can’t be created inside a folder for another account.
            [Test]
            public void TestFolderForMultipleAccounts ()
            {
                int firstAccount = 1;
                int secondAccount = 2;

                var folder1 = FolderOps.CreateFolder (firstAccount);

                TestForNachoExceptionFailure (() => {
                    FolderOps.CreateFolder (secondAccount, parentId: folder1.Id.ToString ());
                }, "Should throw NachoExceptionFailure when creating a folder whose parent has a different accountId");
            }

            // An item for one account can’t be Linked inside a folder for another account.
            [Test]
            public void TestLinkingItemsDiffAccounts ()
            {
                int firstAccount = 1;
                int secondAccount = 2;

                var folder = FolderOps.CreateFolder (firstAccount);
                var email = FolderOps.CreateUniqueItem<McEmailMessage> (secondAccount);

                TestForNachoExceptionFailure (() => {
                    folder.Link (email);
                }, "Should not be able to link item to folder of different accountId");
            }
        }
    }

    [TestFixture]
    public class FolderOC : BaseMcFolderTest
    {
        [Test]
        public void NoClobber ()
        {
            var displayName = "updated";
            int count = -1;
            var folder1 = FolderOps.CreateFolder (accountId: 1, serverId: "1", name: "initial");
            var folder1b = McFolder.QueryById<McFolder> (folder1.Id);
            Assert.AreEqual (folder1.ServerId, folder1b.ServerId);
            folder1b = folder1b.UpdateSet_AsSyncMetaToClientExpected (true);
            Assert.False (folder1.AsSyncMetaToClientExpected);
            Assert.AreEqual (0, folder1.RowVersion);
            Assert.AreEqual (1, folder1b.RowVersion);
            var folder1r = folder1.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.DisplayName = displayName;
                return false;
            }, out count, 1);
            Assert.AreEqual (0, count);
            count = -1;
            Assert.AreEqual (folder1r.RowVersion, folder1.RowVersion);
            var check = McFolder.QueryById<McFolder> (folder1.Id);
            Assert.AreEqual ("initial", check.DisplayName);
            folder1r = folder1.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.DisplayName = displayName;
                return true;
            }, out count, 1);
            Assert.AreEqual (0, count);
            count = -1;
            Assert.AreEqual (folder1r.RowVersion, folder1.RowVersion);
            check = McFolder.QueryById<McFolder> (folder1.Id);
            Assert.AreEqual ("initial", check.DisplayName);
            folder1r = folder1.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.DisplayName = displayName;
                return true;
            }, out count, 2);
            Assert.AreEqual (folder1r.DisplayName, displayName);
            Assert.AreEqual (folder1r.RowVersion, 2);
            Assert.AreEqual (1, count);
            Assert.AreEqual (folder1r.Id, folder1.Id);
            folder1 = McFolder.QueryById<McFolder> (folder1r.Id);
            Assert.AreEqual (folder1.RowVersion, folder1r.RowVersion);
            Assert.AreEqual (folder1.DisplayName, folder1r.DisplayName);
            check = McFolder.QueryById<McFolder> (folder1.Id);
            Assert.AreEqual (displayName, check.DisplayName);
        }

        [Test]
        public void BadRowVersion ()
        {
            int count = -1;
            var folder2 = FolderOps.CreateFolder (accountId: 1, serverId: "2", name: "initial");
            folder2.RowVersion = 99;
            var folder2r = folder2.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.DisplayName = "FUBAR";
                return true;
            }, out count, 1);
            Assert.AreEqual (0, count);
            Assert.AreEqual (0, folder2r.RowVersion);
            folder2.RowVersion = 99;
            folder2r = folder2.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.DisplayName = "FUBAR";
                return true;
            }, out count, 2);
            Assert.AreEqual (1, count);
            Assert.AreEqual (1, folder2r.RowVersion);
            Assert.AreEqual ("FUBAR", folder2r.DisplayName);
            var check = McFolder.QueryById<McFolder> (folder2.Id);
            Assert.AreEqual (folder2r.DisplayName, check.DisplayName);
        }
    }

    public class BaseMcFolderTest : CommonTestOps
    {

        [SetUp]
        public new void SetUp ()
        {
            base.SetUp ();
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

    [TestFixture]
    public class EpochScrub : BaseMcFolderTest
    {
        const int AccountId = 1;

        [Test]
        public void TestUpdateResetSyncState ()
        {
            McFolder folder1 = FolderOps.CreateFolder (AccountId);
            folder1 = folder1.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.AsSyncKey = "55";
                target.AsSyncMetaToClientExpected = false;
                return true;
            });
            folder1 = McFolder.QueryById<McFolder> (folder1.Id);
            Assert.AreEqual ("55", folder1.AsSyncKey);
            Assert.IsFalse (folder1.AsSyncMetaToClientExpected);
            Assert.IsFalse (folder1.AsSyncEpochScrubNeeded);
            Assert.AreEqual (0, folder1.AsSyncEpoch);

            folder1.UpdateResetSyncState ();
            Assert.AreEqual (McFolder.AsSyncKey_Initial, folder1.AsSyncKey);
            Assert.IsTrue (folder1.AsSyncMetaToClientExpected);
            Assert.IsTrue (folder1.AsSyncEpochScrubNeeded);
            Assert.AreEqual (1, folder1.AsSyncEpoch);
        }

        [Test]
        public void TestLinkAndLinkUpdate ()
        {
            McFolder folder1 = FolderOps.CreateFolder (AccountId);
            folder1 = folder1.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.AsSyncEpoch = 66;
                return true;
            });
            McEmailMessage email = FolderOps.CreateUniqueItem<McEmailMessage> (AccountId);
            folder1.Link (email);
            var map = McMapFolderFolderEntry.QueryByFolderIdFolderEntryIdClassCode (AccountId,
                          folder1.Id, email.Id, McAbstrFolderEntry.ClassCodeEnum.Email);
            Assert.AreEqual (66, folder1.AsSyncEpoch);
            Assert.AreEqual (66, map.AsSyncEpoch);
            folder1 = folder1.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.AsSyncEpoch++;
                return true;
            });
            folder1.UpdateLink (email);
            map = McMapFolderFolderEntry.QueryByFolderIdFolderEntryIdClassCode (AccountId,
                folder1.Id, email.Id, McAbstrFolderEntry.ClassCodeEnum.Email);
            Assert.AreEqual (67, map.AsSyncEpoch);
        }

        [Test]
        public void TestPerformSyncEpochScrub ()
        {
            // Setup pre-existing folder + email.
            McFolder folder1 = FolderOps.CreateFolder (AccountId);
            folder1 = folder1.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.AsSyncEpoch = 77;
                return true;
            });
            McEmailMessage email1 = FolderOps.CreateUniqueItem<McEmailMessage> (AccountId);
            folder1.Link (email1);
            // Bump to next epoch, and add 2nd email.
            folder1 = folder1.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.AsSyncEpoch++;
                return true;
            });
            McEmailMessage email2 = FolderOps.CreateUniqueItem<McEmailMessage> (AccountId, serverId: "6");
            folder1.Link (email2);
            // Do the scrub.
            folder1.PerformSyncEpochScrub (testRunSync: true);
            // 1st email is gone.
            var dead = McEmailMessage.QueryById<McEmailMessage> (email1.Id);
            Assert.IsNull (dead);
            var mapMissing = McMapFolderFolderEntry.QueryByFolderIdFolderEntryIdClassCode (AccountId,
                                 folder1.Id, email1.Id, McAbstrFolderEntry.ClassCodeEnum.Email);
            Assert.IsNull (mapMissing);
            // 2nd email is still there.
            var live = McEmailMessage.QueryById<McEmailMessage> (email2.Id);
            Assert.IsNotNull (live);
            Assert.AreEqual (live.Id, email2.Id);
            var mapFound = McMapFolderFolderEntry.QueryByFolderIdFolderEntryIdClassCode (AccountId,
                               folder1.Id, email2.Id, McAbstrFolderEntry.ClassCodeEnum.Email);
            Assert.IsNotNull (mapFound);
            Assert.AreEqual (email2.Id, mapFound.FolderEntryId);
            Assert.AreEqual (folder1.Id, mapFound.FolderId);
        }

        [Test]
        public void TestQueryOldEpochByFolderId ()
        {
            // excluded items: wrong account, IsAwaitingDelete, wrong folder, current epoch.
            McFolder folder1 = FolderOps.CreateFolder (AccountId);
            folder1 = folder1.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.AsSyncEpoch = 99;
                return true;
            });
            // email1 & email2 are included.
            McEmailMessage email1 = FolderOps.CreateUniqueItem<McEmailMessage> (AccountId);
            folder1.Link (email1);
            McEmailMessage email2 = FolderOps.CreateUniqueItem<McEmailMessage> (AccountId, serverId: "6");
            folder1.Link (email2);
            // bump to next epoch.
            folder1 = folder1.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.AsSyncEpoch++;
                return true;
            });
            // email3 excluded (epoch).
            McEmailMessage email3 = FolderOps.CreateUniqueItem<McEmailMessage> (AccountId, serverId: "7");
            folder1.Link (email3);
            // email4 excluded (account).
            FolderOps.CreateUniqueItem<McEmailMessage> (AccountId, serverId: "8");
            McFolder folder2 = FolderOps.CreateFolder (AccountId);
            folder2 = folder2.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.AsSyncEpoch = 99;
                return true;
            });
            // email5 excluded (folder).
            McEmailMessage email5 = FolderOps.CreateUniqueItem<McEmailMessage> (AccountId, serverId: "9");
            folder2.Link (email5);
            // email6 excluded (IsAwaitingDelete).
            McEmailMessage email6 = FolderOps.CreateUniqueItem<McEmailMessage> (AccountId, serverId: "10");
            email6.UpdateWithOCApply<McEmailMessage> ((record) => {
                var target = (McEmailMessage)record;
                target.IsAwaitingDelete = true;
                return true;
            });
            folder1.Link (email6);

            var oldies = McEmailMessage.QueryOldEpochByFolderId<McEmailMessage> 
                (AccountId, folder1.Id, folder1.AsSyncEpoch, 100);
            Assert.AreEqual (2, oldies.Count);
            var arr = oldies.ToArray ();
            Assert.IsTrue (email1.Id == arr [0].Id || email1.Id == arr [1].Id);
            Assert.IsTrue (email2.Id == arr [0].Id || email2.Id == arr [1].Id);
            Assert.AreNotEqual (arr [0].Id, arr [1].Id);
            oldies = McEmailMessage.QueryOldEpochByFolderId<McEmailMessage> 
                (AccountId, folder1.Id, folder1.AsSyncEpoch, 1);
            Assert.AreEqual (1, oldies.Count);
            arr = oldies.ToArray ();
            Assert.IsTrue (email1.Id == arr [0].Id || email2.Id == arr [0].Id);
        }
    }

    [TestFixture]
    public class JunkFoldersTest : BaseMcFolderTest
    {
        [Test]
        public void TestJunkFolder ()
        {
            Assert.IsNull (McFolder.JunkFolderListSqlString ());

            var junk = FolderOps.CreateFolder (accountId: defaultAccountId, name: "Junk emails");
            Assert.AreNotEqual (0, junk.Id);
            Assert.True (junk.IsJunkFolder ());

            Assert.AreEqual (String.Format ("({0})", junk.Id), McFolder.JunkFolderListSqlString ());

            var spam = FolderOps.CreateFolder (accountId: defaultAccountId, name: "Spam");
            Assert.AreNotEqual (0, spam.Id);
            Assert.True (spam.IsJunkFolder ());

            var stuff = FolderOps.CreateFolder (accountId: defaultAccountId, name: "Stuff");
            Assert.AreNotEqual (0, stuff.Id);
            Assert.False (stuff.IsJunkFolder ());

            Assert.True (
                (String.Format ("({0},{1})", junk.Id, spam.Id) == McFolder.JunkFolderListSqlString ()) ||
                (String.Format ("({0},{1})", spam.Id, junk.Id) == McFolder.JunkFolderListSqlString ()));

            spam.Delete ();
            Assert.AreEqual (String.Format ("({0})", junk.Id), McFolder.JunkFolderListSqlString ());

            junk.Delete ();
            Assert.IsNull (McFolder.JunkFolderListSqlString ()); 
        }
    }
}
