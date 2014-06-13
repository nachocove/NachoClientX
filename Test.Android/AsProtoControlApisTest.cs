//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.ActiveSync;
using NachoCore.Utils;
using NachoCore.Model;
using TypeCode = NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode;


namespace Test.iOS
{
    public class AsProtoControlApisTest 
    {
        [TestFixture]
        public class TestCreateAssertions : BaseProtoApisTest
        {
            [Test]
            public void TestCreateItemCmds ()
            {
                TestCreatingItem<McCalendar> ((protoControl, itemId, folderId) => {
                    protoControl.CreateCalCmd (itemId, folderId);
                });
                SetUp ();
                TestCreatingItem<McContact> ((protoControl, itemId, folderId) => {
                    protoControl.CreateContactCmd (itemId, folderId);
                });
                SetUp ();
                TestCreatingItem<McTask> ((protoControl, itemId, folderId) => {
                    protoControl.CreateTaskCmd (itemId, folderId);
                });
            }

            [Test]
            public void TestCreateFolderCmd ()
            {
                int accountId = 1;
                var protoControl = CreateProtoControl (accountId);

                var destFolder = CreateFolder (accountId, isClientOwned: true);

                TestForNachoExceptionFailure (() => {
                    protoControl.CreateFolderCmd (destFolder.Id, "New folder name", TypeCode.Unknown_18);
                }, String.Format ("Should throw exception if attempting to create folder (type: {0}) in client-owned folder", typeof(McFolder).ToString ()));
            }

            private void TestCreatingItem<T> (Action <AsProtoControl, int, int> action) where T : McItem, new()
            {
                int accountId = 1;
                var protoControl = CreateProtoControl (accountId);

                var clientFolder = CreateFolder (accountId, isClientOwned: true);
                var clientItem = CreateUniqueItem<T> (accountId);
                clientFolder.Link (clientItem);

                TestForNachoExceptionFailure (() => {
                    action (protoControl, clientItem.Id, clientFolder.Id);
                }, String.Format ("Should throw exception if attempting to create item (type: {0}) in client-owned folder", typeof(T).ToString ()));
            }
        }

        [TestFixture]
        public class TestUpdateAssertions : BaseProtoApisTest
        {

        }

        // Should not be able to delete client-owned items with api
        [TestFixture]
        public class TestDeleteAssertions : BaseProtoApisTest
        {
            [Test]
            public void TestDeleteItemCmds ()
            {
                TestDeletingItem<McEmailMessage> ((protoControl, itemId) => {
                    protoControl.DeleteEmailCmd (itemId);
                });
                SetUp ();
                TestDeletingItem<McCalendar> ((protoControl, itemId) => {
                    protoControl.DeleteCalCmd (itemId);
                });
                SetUp ();
                TestDeletingItem<McContact> ((protoControl, itemId) => {
                    protoControl.DeleteContactCmd (itemId);
                });
                SetUp ();
                TestDeletingItem<McTask> ((protoControl, itemId) => {
                    protoControl.DeleteTaskCmd (itemId);
                });
            }

            [Test]
            public void TestDeleteFolderCmd ()
            {
                int accountId = 1;
                var protoControl = CreateProtoControl (accountId);

                var clientFolder = CreateFolder (accountId, isClientOwned: true);
                var otherFolder = CreateFolder (accountId, isClientOwned: true, parentId: clientFolder.ServerId);

                TestForNachoExceptionFailure (() => {
                    protoControl.DeleteFolderCmd (otherFolder.Id);
                }, String.Format ("Should throw exception if attempting to delete folder (type: {0}) in client-owned folder", typeof(McFolder).ToString ()));
            }

            private void TestDeletingItem<T> (Action <AsProtoControl, int> action) where T : McItem, new()
            {
                int accountId = 1;
                var protoControl = CreateProtoControl (accountId);

                var clientFolder = CreateFolder (accountId, isClientOwned: true);
                var clientItem = CreateUniqueItem<T> (accountId);
                clientFolder.Link (clientItem);

                TestForNachoExceptionFailure (() => {
                    action (protoControl, clientItem.Id);
                }, String.Format ("Should throw exception if attempting to delete item (type: {0}) in client-owned folder", typeof(T).ToString ()));
            }
        }

        [TestFixture]
        public class TestMoveAssertions : BaseProtoApisTest
        {
            [Test]
            public void TestMoveItemCmds ()
            {
                TestMovingItem<McEmailMessage> ((protoControl, item, destFolder) => {
                    protoControl.MoveEmailCmd (item.Id, destFolder.Id);
                });
                SetUp ();
                TestMovingItem<McCalendar> ((protoControl, item, destFolder) => {
                    protoControl.MoveCalCmd (item.Id, destFolder.Id);
                });
                SetUp ();
                TestMovingItem<McContact> ((protoControl, item, destFolder) => {
                    protoControl.MoveContactCmd (item.Id, destFolder.Id);
                });
                SetUp ();
                TestMovingItem<McTask> ((protoControl, item, destFolder) => {
                    protoControl.MoveTaskCmd (item.Id, destFolder.Id);
                });
            }

            [Test]
            public void TestMoveFolderCmd ()
            {
                int accountId = 1;
                var protoControl = CreateProtoControl (accountId);

                // cannot move client-owned folder to non-client-owned folder
                var folder1 = CreateFolder (accountId, isClientOwned: true);
                var destFolder1 = CreateFolder (accountId, isClientOwned: false);

                TestForNachoExceptionFailure (() => {
                    protoControl.MoveFolderCmd (folder1.Id, destFolder1.Id);
                }, "Should not be able to move client-owned folder to non-client-owned folder");

                // cannot move non-client-owned fodler to client-owned folder
                var folder2 = CreateFolder (accountId, isClientOwned: false);
                var destFolder2 = CreateFolder (accountId, isClientOwned: true);

                TestForNachoExceptionFailure (() => {
                    protoControl.MoveFolderCmd (folder2.Id, destFolder2.Id);
                }, "Should not be able to move non-client-owned folder to client-owned folder");
            }

            private void TestMovingItem<T> (Action<AsProtoControl, T, McFolder> action) where T : McItem, new()
            {
                int accountId = 1;
                var protoControl = CreateProtoControl (accountId);

                // cannot move item from client-owned to non-client-owned folder
                var folder1 = CreateFolder (accountId, isClientOwned: true);
                var item = CreateUniqueItem<T> (accountId);
                folder1.Link (item);

                var destFolder1 = CreateFolder (accountId, isClientOwned: false);

                TestForNachoExceptionFailure (() => {
                    action (protoControl, item, destFolder1);
                }, "Should not be able to move item from client-owned to non-client-owned folder");

                // cannot move item from non-client-owned to client-owned folder
                var folder2 = CreateFolder (accountId, isClientOwned: false);
                folder2.Link (item);

                var destFolder2 = CreateFolder (accountId, isClientOwned: true);

                TestForNachoExceptionFailure (() => {
                    action (protoControl, item, destFolder2);
                }, "Should not be able to move item from non-client-owned to client-owned folder");
            }
        }

        [TestFixture]
        public class TestRenameAssertions : BaseProtoApisTest
        {
            [Test]
            public void TestRenameFolderCmd ()
            {
                int accountId = 1;
                var protoControl = CreateProtoControl (accountId);

                var clientFolder = CreateFolder (accountId, isClientOwned: true);

                TestForNachoExceptionFailure (() => {
                    protoControl.RenameFolderCmd (clientFolder.Id, "New display name");
                }, String.Format ("Should throw exception if attempting to delete item (type: {0}) in client-owned folder", typeof(McFolder).ToString ()));
            }
        }
    }

    public class BaseProtoApisTest : CommonFolderOps
    {
        [SetUp]
        public void SetUp ()
        {
            Log.Info (Log.LOG_TEST, "Setup started");
            NcModel.Instance.Reset (System.IO.Path.GetTempFileName ());
        }

        public McAccount CreateAccount (int pcsId = 5)
        {
            var account = new McAccount () {
                EmailAddr = "johnd@foo.utopiasystems.net",
                ServerId = 1,
                ProtocolStateId = pcsId,
            };
            account.Insert ();

            return account;
        }

        public McProtocolState CreateProtocolState ()
        {
            McProtocolState pcs = new McProtocolState ();
            pcs.Insert ();

            return pcs;
        }

        public AsProtoControl CreateProtoControl (int accountId)
        {
            MockOwner mockOwner = new MockOwner ();

            var pcs = CreateProtocolState ();
            CreateAccount (pcs.Id);
            NcTask.Start ();

            AsProtoControl protoControl = new AsProtoControl (mockOwner, accountId);

            return protoControl;
        }
    }
}

