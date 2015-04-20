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
                    return protoControl.CreateCalCmd (itemId, folderId);
                });
                SetUp ();
                TestCreatingItem<McContact> ((protoControl, itemId, folderId) => {
                    return protoControl.CreateContactCmd (itemId, folderId);
                });
                SetUp ();
                TestCreatingItem<McTask> ((protoControl, itemId, folderId) => {
                    return protoControl.CreateTaskCmd (itemId, folderId);
                });
            }

            [Test]
            public void TestCreateFolderCmd ()
            {
                int accountId = 1;
                var protoControl = ProtoOps.CreateProtoControl (accountId);

                var destFolder = FolderOps.CreateFolder (accountId, isClientOwned: true);

                var result = protoControl.CreateFolderCmd (destFolder.Id, "New folder name", TypeCode.Unknown_18);
                Assert.AreEqual (result.Kind, NcResult.KindEnum.Error);
                Assert.AreEqual (result.SubKind, NcResult.SubKindEnum.Error_ClientOwned);
            }

            private void TestCreatingItem<T> (Func <AsProtoControl, int, int, NcResult> action) where T : McAbstrItem, new()
            {
                int accountId = 1;
                var protoControl = ProtoOps.CreateProtoControl (accountId);

                var clientFolder = FolderOps.CreateFolder (accountId, isClientOwned: true);
                var clientItem = FolderOps.CreateUniqueItem<T> (accountId);
                clientFolder.Link (clientItem);

                var result = action (protoControl, clientItem.Id, clientFolder.Id);
                Assert.AreEqual (result.Kind, NcResult.KindEnum.Error);
                Assert.AreEqual (result.SubKind, NcResult.SubKindEnum.Error_ClientOwned);
            }
        }

        [TestFixture]
        public class TestUpdateAssertions : BaseProtoApisTest
        {
            [Test]
            public void TestUpdateItemCmds ()
            {
                TestUpdatingItem<McCalendar> ((protoControl, itemId) => {
                    return protoControl.UpdateCalCmd (itemId, false);
                });
                SetUp ();
                TestUpdatingItem<McContact> ((protoControl, itemId) => {
                    return protoControl.UpdateContactCmd (itemId);
                });
                SetUp ();
                TestUpdatingItem<McTask> ((protoControl, itemId) => {
                    return protoControl.UpdateTaskCmd (itemId);
                });
            }

            private void TestUpdatingItem<T> (Func <AsProtoControl, int, NcResult> action) where T : McAbstrItem, new()
            {
                int accountId = 1;
                var protoControl = ProtoOps.CreateProtoControl (accountId);

                var clientFolder = FolderOps.CreateFolder (accountId, isClientOwned: true);
                var clientItem = FolderOps.CreateUniqueItem<T> (accountId);
                clientFolder.Link (clientItem);

                var result = action (protoControl, clientItem.Id);
                Assert.AreEqual (result.Kind, NcResult.KindEnum.Error);
                Assert.AreEqual (result.SubKind, NcResult.SubKindEnum.Error_ClientOwned); 
            }
        }

        // Should not be able to delete client-owned items with api
        [TestFixture]
        public class TestDeleteAssertions : BaseProtoApisTest
        {
            [Test]
            public void TestDeleteItemCmds ()
            {
                TestDeletingItem<McEmailMessage> ((protoControl, itemId) => {
                    return protoControl.DeleteEmailCmd (itemId);
                });
                SetUp ();
                TestDeletingItem<McCalendar> ((protoControl, itemId) => {
                    return protoControl.DeleteCalCmd (itemId);
                });
                SetUp ();
                TestDeletingItem<McContact> ((protoControl, itemId) => {
                    return protoControl.DeleteContactCmd (itemId);
                });
                SetUp ();
                TestDeletingItem<McTask> ((protoControl, itemId) => {
                    return protoControl.DeleteTaskCmd (itemId);
                });
            }

            [Test]
            public void TestDeleteFolderCmd ()
            {
                int accountId = 1;
                var protoControl = ProtoOps.CreateProtoControl (accountId);

                var clientFolder = FolderOps.CreateFolder (accountId, isClientOwned: true);
                var otherFolder = FolderOps.CreateFolder (accountId, isClientOwned: true, parentId: clientFolder.ServerId);

                var result = protoControl.DeleteFolderCmd (otherFolder.Id);
                Assert.AreEqual (result.Kind, NcResult.KindEnum.Error);
                Assert.AreEqual (result.SubKind, NcResult.SubKindEnum.Error_ClientOwned);
            }

            private void TestDeletingItem<T> (Func <AsProtoControl, int, NcResult> action) where T : McAbstrItem, new()
            {
                int accountId = 1;
                var protoControl = ProtoOps.CreateProtoControl (accountId);

                var clientFolder = FolderOps.CreateFolder (accountId, isClientOwned: true);
                var clientItem = FolderOps.CreateUniqueItem<T> (accountId);
                clientFolder.Link (clientItem);

                var result = action (protoControl, clientItem.Id);
                Assert.AreEqual (result.Kind, NcResult.KindEnum.Error);
                Assert.AreEqual (result.SubKind, NcResult.SubKindEnum.Error_ClientOwned);
            }
        }

        [TestFixture]
        public class TestMoveAssertions : BaseProtoApisTest
        {
            [Test]
            public void TestMoveItemCmds ()
            {
                TestMovingItem<McEmailMessage> ((protoControl, item, destFolder) => {
                    return protoControl.MoveEmailCmd (item.Id, destFolder.Id);
                });
                SetUp ();
                TestMovingItem<McCalendar> ((protoControl, item, destFolder) => {
                    return protoControl.MoveCalCmd (item.Id, destFolder.Id);
                });
                SetUp ();
                TestMovingItem<McContact> ((protoControl, item, destFolder) => {
                    return protoControl.MoveContactCmd (item.Id, destFolder.Id);
                });
                SetUp ();
                TestMovingItem<McTask> ((protoControl, item, destFolder) => {
                    return protoControl.MoveTaskCmd (item.Id, destFolder.Id);
                });
            }

            [Test]
            public void TestMoveFolderCmd ()
            {
                int accountId = 1;
                var protoControl = ProtoOps.CreateProtoControl (accountId);

                // cannot move client-owned folder to non-client-owned folder
                var folder1 = FolderOps.CreateFolder (accountId, isClientOwned: true);
                var destFolder1 = FolderOps.CreateFolder (accountId, isClientOwned: false);

                var result = protoControl.MoveFolderCmd (folder1.Id, destFolder1.Id);
                Assert.AreEqual (result.Kind, NcResult.KindEnum.Error);
                Assert.AreEqual (result.SubKind, NcResult.SubKindEnum.Error_ClientOwned);
                // cannot move non-client-owned fodler to client-owned folder
                var folder2 = FolderOps.CreateFolder (accountId, isClientOwned: false);
                var destFolder2 = FolderOps.CreateFolder (accountId, isClientOwned: true);

                result = protoControl.MoveFolderCmd (folder2.Id, destFolder2.Id);
                Assert.AreEqual (result.Kind, NcResult.KindEnum.Error);
                Assert.AreEqual (result.SubKind, NcResult.SubKindEnum.Error_ClientOwned);                    
            }

            private void TestMovingItem<T> (Func<AsProtoControl, T, McFolder, NcResult> action) where T : McAbstrItem, new()
            {
                int accountId = 1;
                var protoControl = ProtoOps.CreateProtoControl (accountId);

                // cannot move item from client-owned to non-client-owned folder
                var folder1 = FolderOps.CreateFolder (accountId, isClientOwned: true);
                var item = FolderOps.CreateUniqueItem<T> (accountId);
                folder1.Link (item);

                var destFolder1 = FolderOps.CreateFolder (accountId, isClientOwned: false);

                var result = action (protoControl, item, destFolder1);
                Assert.AreEqual (result.Kind, NcResult.KindEnum.Error);
                Assert.AreEqual (result.SubKind, NcResult.SubKindEnum.Error_ClientOwned); 

                // cannot move item from non-client-owned to client-owned folder
                var folder2 = FolderOps.CreateFolder (accountId, isClientOwned: false);
                folder2.Link (item);

                var destFolder2 = FolderOps.CreateFolder (accountId, isClientOwned: true);

                result = action (protoControl, item, destFolder2);
                Assert.AreEqual (result.Kind, NcResult.KindEnum.Error);
                Assert.AreEqual (result.SubKind, NcResult.SubKindEnum.Error_ClientOwned); 
            }
        }

        [TestFixture]
        public class TestRenameAssertions : BaseProtoApisTest
        {
            [Test]
            public void TestRenameFolderCmd ()
            {
                int accountId = 1;
                var protoControl = ProtoOps.CreateProtoControl (accountId);

                var clientFolder = FolderOps.CreateFolder (accountId, isClientOwned: true);

                var result = protoControl.RenameFolderCmd (clientFolder.Id, "New display name");
                Assert.AreEqual (result.Kind, NcResult.KindEnum.Error);
                Assert.AreEqual (result.SubKind, NcResult.SubKindEnum.Error_ClientOwned);                     
            }
        }
    }

    public class BaseProtoApisTest : CommonTestOps
    {
        [SetUp]
        public new void SetUp ()
        {
            base.SetUp ();
        }
    }
}

