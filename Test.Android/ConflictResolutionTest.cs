//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.ActiveSync;
using System.Linq;
using ProtoOps = Test.iOS.CommonProtoControlOps;
using FolderOps = Test.iOS.CommonFolderOps;
using PathOps = Test.iOS.CommonPathOps;
using Operations = NachoCore.Model.McPending.Operations;
using TypeCode = NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode;
using ClassCode = NachoCore.ActiveSync.Xml.AirSync.ClassCode;
using NachoCore.Model;
using System.Text;
using NachoCore.Utils;
using System.Net.Http;
using System.Threading;
using System.Xml.Linq;
using System.Collections.Generic;
using NachoCore;


namespace Test.iOS
{
    /* Response code document: http://msdn.microsoft.com/en-us/library/ff631512(v=exchg.80).aspx */
    public class CreatedFolder
    {
        public CreatedFolder (McFolder folder, string token)
        {
            Folder = folder;
            Token = token;
        }

        public CreatedFolder ()
        {
            Folder = new McFolder ();
            Token = "";
        }

        public McFolder Folder;
        public string Token;
    }

    public class ConflictResolutionTest
    {
        public partial class BaseConfResTest : CommonTestOps
        {
            public MockContext Context;
            public AsFolderSyncCommand FolderCmd;

            public string SyncResponseAddMultiple = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<FolderSync xmlns=\"FolderHierarchy\">\n  <Status>1</Status>\n  <SyncKey>1</SyncKey>\n  <Changes>\n    <Count>3</Count>\n    <Add>\n      <ServerId>1</ServerId>\n      <ParentId>0</ParentId>\n      <DisplayName>Top-Level-Folder</DisplayName>\n      <Type>1</Type>\n    </Add>\n    <Add>\n      <ServerId>2</ServerId>\n      <ParentId>1</ParentId>\n      <DisplayName>Sub-Cal-Folder</DisplayName>\n      <Type>13</Type>\n    </Add>\n    <Add>\n      <ServerId>3</ServerId>\n      <ParentId>1</ParentId>\n      <DisplayName>Sub-Task-Folder</DisplayName>\n      <Type>15</Type>\n    </Add>\n  </Changes>\n</FolderSync>\n";
            public string SyncResponseAddSub = AddSingleFolderXml ("2", "1", "Child-Folder", TypeCode.UserCreatedGeneric_1);
            public string SyncResponseDeleteTop = DeleteSingleFolderXml ("1", "0", "Top-Level-Folder", TypeCode.UserCreatedGeneric_1);
            public string SyncResponseDeleteSub = DeleteSingleFolderXml ("2", "1", "Child-Folder", TypeCode.UserCreatedGeneric_1);
            public string SyncResponseUpdateRenameTop = UpdateSingleFolderXml ("1", "0", "Top-Level-Folder (UPDATED BY SERVER)", TypeCode.UserCreatedGeneric_1);
            public string SyncResponseUpdateMoveTop = UpdateSingleFolderXml ("1", "2", "Top-Level-Folder", TypeCode.UserCreatedGeneric_1);

            // These names correspond to the DisplayName entries in the response XML messages
            public const string TopFolderName = "Top-Level-Folder";
            public const string TopFolderServerUpdateName = "Top-Level-Folder (UPDATED BY SERVER)";
            public const string SubCalFolderName = "Sub-Cal-Folder";
            public const string SubContactFolderName = "Sub-Contact-Folder";
            public const string SubTaskFolderName = "Sub-Task-Folder";
            public const string ChildFolderName = "Child-Folder";

            [SetUp]
            public new void SetUp ()
            {
                base.SetUp ();
                var protoControl = ProtoOps.CreateProtoControl (accountId: defaultAccountId);

                var server = McServer.Create (CommonMockData.MockUri);
                Context = new MockContext (protoControl, server);

                FolderCmd = CreateFolderSyncCmd (Context);
            }

            public AsFolderSyncCommand CreateFolderSyncCmd (MockContext context)
            {
                var folderCmd = new AsFolderSyncCommand (context);
                folderCmd.HttpClientType = typeof(MockHttpClient);
                folderCmd.DnsQueryRequestType = typeof(MockDnsQueryRequest);
                return folderCmd;
            }

            public AsSyncCommand CreateSyncCmd (MockContext context)
            {
                var syncCmd = new AsSyncCommand (context);
                syncCmd.HttpClientType = typeof(MockHttpClient);
                FolderCmd.DnsQueryRequestType = typeof(MockDnsQueryRequest);
                return syncCmd;
            }

            public McFolder CreateAndGetFolderWithCmd (int destId, string name, TypeCode type, string parentId)
            {
                Context.ProtoControl.CreateFolderCmd (destId, name, type);
                var found = GetCreatedFolder (defaultAccountId, type, parentId, name);
                return found;
            }

            // Parent & Server Id's correspond to response XML
            public McFolder CreateTopFolder (bool withPath = false, TypeCode type = TypeCode.UserCreatedGeneric_1)
            {
                var folder = FolderOps.CreateFolder (defaultAccountId, parentId: "0", serverId: "1", name: TopFolderName, typeCode: type);
                if (withPath) {
                    // Set up path: This is the client's "best understanding" of the servers point of view the last time they talked
                    PathOps.CreatePath (defaultAccountId, folder.ServerId, folder.ParentId);
                }
                return folder;
            }

            // Parent & Server Id's correspond to response XML
            public McFolder CreateChildFolder (bool withPath = false)
            {
                var folder = FolderOps.CreateFolder (defaultAccountId, parentId: "1", serverId: "2", name: ChildFolderName);
                if (withPath) {
                    PathOps.CreatePath (defaultAccountId, folder.ServerId, folder.ParentId);
                }
                return folder;
            }

            // Parent & Server Id's correspond to response XML
            public McFolder CreateSubCalFolder (bool withPath = false)
            {
                var type = TypeCode.UserCreatedCal_13;
                var folder = FolderOps.CreateFolder (defaultAccountId, parentId: "1", serverId: "2", name: SubCalFolderName, typeCode: type);
                if (withPath) {
                    PathOps.CreatePath (defaultAccountId, folder.ServerId, folder.ParentId);
                }
                return folder;
            }

            public McFolder CreateSubContactFolder (bool withPath = false)
            {
                var type = TypeCode.UserCreatedContacts_14;
                var folder = FolderOps.CreateFolder (defaultAccountId, parentId: "1", serverId: "4", name: SubContactFolderName, typeCode: type);
                if (withPath) {
                    PathOps.CreatePath (defaultAccountId, folder.ServerId, folder.ParentId);
                }
                return folder;
            }

            // Parent & Server Id's correspond to response XML
            public McFolder CreateSubTaskFolder (bool withPath = false)
            {
                var type = TypeCode.UserCreatedTasks_15;
                var folder = FolderOps.CreateFolder (defaultAccountId, parentId: "1", serverId: "3", name: SubTaskFolderName, typeCode: type);
                if (withPath) {
                    PathOps.CreatePath (defaultAccountId, folder.ServerId, folder.ParentId);
                }
                return folder;
            }

            public McFolder GetCreatedFolder (int accountId, TypeCode type, string parentId, string name)
            {
                var found = McFolder.QueryByParentId (accountId, parentId).Where (folder => folder.Type == type)
                    .Where (folder => folder.DisplayName == name).ToList ();
                Assert.AreEqual (1, found.Count, "Found too many matching folders after FolderCreateCmd");
                return found.FirstOrDefault ();
            }

            public T DoCreateItemCmd<T> (McFolder folder, Func<int, int, string> createCmd) where T : McItem, new()
            {
                var item = CommonFolderOps.CreateUniqueItem<T> (serverId: folder.ServerId);
                folder.Link (item);
                createCmd (item.Id, folder.Id);
                return item;
            }
                
            public static string SingleFolderChangeXml (string changeType, string serverId, string parentId, string displayName, TypeCode typeCode)
            {
                return FolderHierarchyRoot (1, (ns) => {
                    return new XElement (ns + changeType,
                        new XElement (ns + "ServerId", serverId),
                        new XElement (ns + "ParentId", parentId),
                        new XElement (ns + "DisplayName", displayName),
                        new XElement (ns + "Type", (int)typeCode));
                });
            }

            public static string FolderHierarchyRoot (int count, Func<XNamespace, XElement> operation)
            {
                XNamespace ns = "FolderHierarchy";
                XElement tree = new XElement (ns + "FolderSync",
                                    new XElement (ns + "Status", 1),
                                    new XElement (ns + "SyncKey", 1),
                                    new XElement (ns + "Changes",
                                        new XElement (ns + "Count", count),
                        operation (ns)
                    ));
                return tree.ToString ();
            }

            public static string AddSingleFolderXml (string serverId, string parentId, string displayName, TypeCode typeCode)
            {
                return SingleFolderChangeXml ("Add", serverId, parentId, displayName, typeCode);
            }

            public static string DeleteSingleFolderXml (string serverId, string parentId, string displayName, TypeCode typeCode)
            {
                return SingleFolderChangeXml ("Delete", serverId, parentId, displayName, typeCode);
            }

            public static string UpdateSingleFolderXml (string serverId, string parentId, string displayName, TypeCode typeCode)
            {
                return SingleFolderChangeXml ("Update", serverId, parentId, displayName, typeCode);
            }

            // Generate mock AirSync response that has a "Commands" section (as opposed to a "Responses" section)
            public static string AirSyncCmdHierarchyRoot (string parentId, Func<XNamespace, XElement> operation)
            {
                XNamespace ns = "AirSync";
                XNamespace nsEmail = ClassCode.Email;
                XNamespace nsCal = ClassCode.Calendar;
                XNamespace nsCont = ClassCode.Contacts;
                XNamespace nsTask = ClassCode.Tasks;

                XElement tree = new XElement (ns + "Sync",
                    new XAttribute (XNamespace.Xmlns + "tasks", nsTask),
                    new XAttribute (XNamespace.Xmlns + "calendar", nsCal),
                    new XAttribute (XNamespace.Xmlns + "contacts", nsCont),
                    new XAttribute (XNamespace.Xmlns + "email", nsEmail),
                                    new XElement (ns + "Collections",
                                        new XElement (ns + "Collection",
                                            new XElement (ns + "SyncKey", 7),
                                            new XElement (ns + "CollectionId", parentId),
                                            new XElement (ns + "Status", 1),
                                            new XElement (ns + "Commands",
                                                operation (ns)))));
                return tree.ToString ();
            }

            // item-specific update code goes in operation lambda; must prefix with XNamespace of item
            public static string SyncAddItemCmdXml (string serverId, string parentId, Func<XNamespace, XElement> operation)
            {
                return AirSyncCmdHierarchyRoot (parentId, (ns) => {
                    return new XElement (ns + "Add",
                        new XElement (ns + "ServerId", serverId),
                        new XElement (ns + "ApplicationData",
                            operation (ns)));
                });
            }

            // item-specific update code goes in operation lambda; must prefix with XNamespace of item
            public static string SyncUpdateCmdItemXml (string clientId, string serverId, string parentId, string classCode, Func <XDocument> operation)
            {
                return AirSyncCmdHierarchyRoot (parentId, (ns) => {
                    return new XElement (ns + "Change",
                        new XElement (ns + "ServerId", serverId),
                        new XElement (ns + "Class", classCode),
                        new XElement (ns + "ApplicationData",
                            operation ()));
                });
            }

            // item-specific update code goes in operation lambda; must prefix with XNamespace of item
            public static string SyncDeleteCmdItemXml (string serverId, string parentId, string classCode)
            {
                return AirSyncCmdHierarchyRoot (parentId, (ns) => {
                    return new XElement (ns + "Delete",
                        new XElement (ns + "ServerId", serverId),
                        new XElement (ns + "Class", classCode));
                });
            }
        }

        public class FolderCreateConfResTest : BaseConfResTest
        {
            [Test]
            public void TestFolderSyncAddConflict ()
            {
                // make dummy placeholder folders because C# does not have the equivalent of Obj-C __block modifier for vars
                var topFolder = new McFolder ();

                DoClientSideCmds (() => {
                    topFolder = CreateAndGetFolderWithCmd (-1, TopFolderName, TypeCode.UserCreatedGeneric_1, "0");
                    CreateAndGetFolderWithCmd (topFolder.Id, SubCalFolderName, TypeCode.UserCreatedCal_13, topFolder.ServerId);
                    CreateAndGetFolderWithCmd (topFolder.Id, SubTaskFolderName, TypeCode.UserCreatedTasks_15, topFolder.ServerId);
                });

                ExecuteConflictTest (FolderCmd, SyncResponseAddMultiple);

                // Assert that the correct changes to the Q and to the Model DB happen because of the FolderSync.
                var foundTopFolder = McFolder.QueryByServerId<McFolder> (defaultAccountId, "1");
                var foundCalFolder = McFolder.QueryByServerId<McFolder> (defaultAccountId, "2");
                var foundTaskFolder = McFolder.QueryByServerId<McFolder> (defaultAccountId, "3");

                string expectedServerId = "1";  // Should match ServerId in SyncResponseXml
                Assert.AreEqual (expectedServerId, foundTopFolder.ServerId, "Top level folder's ServerId should match ServerId sent from server");
                Assert.AreEqual (expectedServerId, foundCalFolder.ParentId, "Sub folders' ParentId's should match ServerId sent from server");
                Assert.AreEqual (expectedServerId, foundTaskFolder.ParentId, "Sub folders' ParentId's should match ServerId sent from server");

                Assert.AreEqual ("2", foundCalFolder.ServerId, "Sub-folder ServerId should match ServerId in response XML");
                Assert.AreEqual ("3", foundTaskFolder.ServerId, "Sub-folder ServerId should match ServerId in response XML");
            }

            [Test]
            public void TestFolderSyncAddTypeConflict ()
            {
                // If DisplayName and ParentId match but Type does not match, then alter 
                // DisplayName of the pending so the user sees the potential conflict
                var badType = TypeCode.UserCreatedCal_13; // does not match type in xml response
                var folderName = TopFolderName;

                string token = null;
                DoClientSideCmds (() => {
                    token = Context.ProtoControl.CreateFolderCmd (-1, folderName, badType); 
                });

                ExecuteConflictTest (FolderCmd, SyncResponseAddMultiple);

                var foundTopFolder = GetCreatedFolder (defaultAccountId, TypeCode.UserCreatedGeneric_1, "0", folderName);
                var pending = McPending.QueryByToken (defaultAccountId, token);

                Assert.AreEqual ("1", foundTopFolder.ServerId, "Sanity check: ServerId of top folder should match expected");
                Assert.AreEqual (TypeCode.UserCreatedGeneric_1, foundTopFolder.Type, "Type should be changed to match server's type when name and parentId match but type does not");
                Assert.AreNotEqual (folderName, pending.DisplayName, "DisplayName should be changed when server's display name for the item does not match client's name");
            }

            [Test]
            public void TestFolderSyncAddSameFolder ()
            {
                // If DisplayName and ParentId and Type match, then both server and client are trying to create the same folder - do: 
                var type = TypeCode.UserCreatedGeneric_1;
                var folderName = TopFolderName;

                string token = null;
                DoClientSideCmds (() => {
                    token = Context.ProtoControl.CreateFolderCmd (-1, folderName, type);
                });

                ExecuteConflictTest (FolderCmd, SyncResponseAddMultiple);

                var foundTopFolder = GetCreatedFolder (defaultAccountId, TypeCode.UserCreatedGeneric_1, "0", folderName);
                var pending = McPending.QueryByToken (defaultAccountId, token);

                // (1) Delete pending FolderCreate, but don't perform the failure-action, and don't notify via StatusInd. 
                Assert.Null (pending, "Pending FolderCreate should be deleted");
                Assert.AreEqual (NcResult.SubKindEnum.Info_FolderSyncSucceeded, MockOwner.Status.SubKind, "Should not notify StatusInd");

                // (2) Add a re-write from pending's ServerId (the guid) to command's ServerId.
                Assert.AreEqual ("1", foundTopFolder.ServerId, "Add a re-write from pending's ServerId (the guid) to command's ServerId");
            }

            // TODO: Fix this test once code for "matching" (as opposed to dominating) has been written
            [Test]
            public void TestFolderSyncDeleteMatch ()
            {
                // If the command's ServerId matches the pending's ParentId,
                // then the destination for the new folder is being deleted by the server. 
                var topFolder = CreateTopFolder (withPath: true);

                string token = null;
                DoClientSideCmds (() => {
                    // Create a subFolder of the top folder before client receives message to delete top folder
                    token = Context.ProtoControl.CreateFolderCmd (topFolder.Id, SubCalFolderName, TypeCode.UserCreatedCal_13); 
                });

                // deletes top folder
                ExecuteConflictTest (FolderCmd, SyncResponseDeleteTop);

                var subFolderPending = McPending.QueryByToken (defaultAccountId, token);
                Assert.Null (subFolderPending, "Should delete pending if the destination for the new folder is being deleted by the server");
            }

            [Test]
            public void TestFolderSyncDeleteDominate ()
            {
                // If the command's ServerId dominates the pending's ParentId,
                // then the destination for the new folder is being deleted by the server. 
                CreateTopFolder (withPath: true);
                var childFolder = CreateChildFolder (withPath: true);
              
                string token = null;
                DoClientSideCmds (() => {
                    // Create a subFolder of the top folder before client receives message to delete top folder
                    token = Context.ProtoControl.CreateFolderCmd (childFolder.Id, "GrandChild-Folder", TypeCode.UserCreatedGeneric_1); 
                });

                // deletes top folder
                ExecuteConflictTest (FolderCmd, SyncResponseDeleteTop);

                var subFolderPending = McPending.QueryByToken (defaultAccountId, token);
                Assert.Null (subFolderPending, "Should delete pending if the destination for the new folder is being deleted by the server");
            }
        }

        public class FolderDeleteConfResTest : BaseConfResTest
        {
            [Test]
            public void TestFolderSyncAdd ()
            {
                // make a top folder and corresponding path; will call FolderDelete on this folder
                var topFolder = CreateTopFolder ();

                string token = null;
                DoClientSideCmds (() => {
                    token = Context.ProtoControl.DeleteFolderCmd (topFolder.Id);
                });

                ExecuteConflictTest (FolderCmd, SyncResponseAddSub);

                var pendDeleteOp = McPending.QueryByToken (defaultAccountId, token);
                Assert.NotNull (pendDeleteOp, "Should not delete pending delete operation");

                var foundParent = McFolder.QueryByServerId<McFolder> (defaultAccountId, topFolder.ServerId);
                Assert.Null (foundParent, "DeleteFolderCmd should delete parent folder on which the command is called");

                // find the child that is to be added in SyncResponseAddSub XML with ServerId==2
                var foundChild = McFolder.QueryByServerId<McFolder> (defaultAccountId, "2");
                Assert.Null (foundChild, "DeleteFolderCmd should cancel the server command if the pending's ServerId dominates the command's ServerId");
            }

            [Test]
            public void TestFolderSyncDeleteServerIdMatch ()
            {
                // If the ServerIds match, do: drop the command and delete the pending FolderDelete, 
                // as the client has already done the delete, do not StatusInd
                var topFolder = CreateTopFolder (withPath: true);

                string token = null;
                DoClientSideCmds (() => {
                    token = Context.ProtoControl.DeleteFolderCmd (topFolder.Id);
                });

                ExecuteConflictTest (FolderCmd, SyncResponseDeleteTop);

                var pendDeleteOp = McPending.QueryByToken (defaultAccountId, token);
                Assert.Null (pendDeleteOp, "Should delete pending FolderDelete because delete has already happened on server");

                var foundFolder = McFolder.QueryByServerId<McFolder> (defaultAccountId, topFolder.ServerId);
                Assert.Null (foundFolder, "Folder should have been deleted");

                // TODO: Test that StatusInd does not get set (what is the best way to do this?)
            }

            [Test]
            public void TestFolderSyncCommandDomPend ()
            {
                // If the command's ServerId dominates the pending's ServerId, 
                // delete pending FolderDelete (duplicate), do not StatusInd.
                var topFolder = CreateTopFolder (withPath: true);
                var childFolder = CreateChildFolder (withPath: true);

                string token = null;
                DoClientSideCmds (() => {
                    token = Context.ProtoControl.DeleteFolderCmd (childFolder.Id);
                });

                ExecuteConflictTest (FolderCmd, SyncResponseDeleteTop);

                var pendDeleteOp = McPending.QueryByToken (defaultAccountId, token);
                Assert.Null (pendDeleteOp, "Should delete pending FolderDelete because delete of parent already happened on server");

                var foundParent = McFolder.QueryByServerId<McFolder> (defaultAccountId, topFolder.ServerId);
                Assert.Null (foundParent, "Should delete parent folder");

                var foundChild = McFolder.QueryByServerId<McFolder> (defaultAccountId, childFolder.ServerId);
                Assert.Null (foundChild, "Should delete sub folder under parent folder that server deletes");
            }

            [Test]
            public void TestFolderSyncPendDomCommand ()
            {
                // If the pending FolderDelete's ServerId dominates the command, then drop the command.
                var topFolder = CreateTopFolder (withPath: true);
                var childFolder = CreateChildFolder (withPath: true);

                string token = null;
                DoClientSideCmds (() => {
                    token = Context.ProtoControl.DeleteFolderCmd (topFolder.Id);
                });

                ExecuteConflictTest (FolderCmd, SyncResponseDeleteSub);

                var pendDeleteOp = McPending.QueryByToken (defaultAccountId, token);
                Assert.NotNull (pendDeleteOp, "Should not delete pending operation if it's ServerId dominates the server command's ServerId");
                Assert.AreEqual (topFolder.ServerId, pendDeleteOp.ServerId, "Pending delete operation should match top folder");

                var foundParent = McFolder.QueryByServerId<McFolder> (defaultAccountId, topFolder.ServerId);
                Assert.Null (foundParent, "DeleteFolderCmd should delete parent folder");

                var foundChild = McFolder.QueryByServerId<McFolder> (defaultAccountId, childFolder.ServerId);
                Assert.Null (foundChild, "Delete folder command should delete sub folder of parent folder the command was called on");
            }
        }

        public class FolderUpdateRenameConfResTest : BaseConfResTest
        {
            [Test]
            public void TestFolderSyncDeleteCommand ()
            {
                // If the command's ServerId dominates the pending FolderUpdate's ServerId, 
                // then the destination for the update is being deleted by the server. 
                var topFolder = CreateTopFolder (withPath: true);
                var childFolder = CreateChildFolder (withPath: true);

                string newName = "Updated-Name";
                string token = null;
                DoClientSideCmds (() => {
                    token = Context.ProtoControl.RenameFolderCmd (childFolder.Id, newName);
                });

                ExecuteConflictTest (FolderCmd, SyncResponseDeleteTop);

                // parent and child folder should be deleted by server cmd
                var foundParent = McFolder.QueryByServerId<McFolder> (defaultAccountId, topFolder.ServerId);
                Assert.Null (foundParent, "Parent should be deleted by FolderSync:Delete command");

                var foundChild = McFolder.QueryByServerId<McFolder> (defaultAccountId, childFolder.ServerId);
                Assert.Null (foundChild, "Child folder should be deleted by FolderSync:Delete command, overriding the Update pending");

                // Pending rename should be deleted
                var pendRenameOp = McPending.QueryByToken (defaultAccountId, token);
                Assert.Null (pendRenameOp, "Should delete pending RenameOp when FolderSync:Delete command's ServerId dominates pending's ServerId");
            }

            [Test]
            public void TestFolderSyncServerUpdateRename ()
            {
                // If ServerId match, delete pending. Server wins.
                var topFolder = CreateTopFolder (withPath: true);

                string newName = "Top-Level-Folder (UPDATED BY CLIENT)";
                string token = null;
                DoClientSideCmds (() => {
                    token = Context.ProtoControl.RenameFolderCmd (topFolder.Id, newName);
                });

                ExecuteConflictTest (FolderCmd, SyncResponseUpdateRenameTop);

                // top-level folder should be updated by server, not client
                var foundParent = McFolder.QueryByServerId<McFolder> (defaultAccountId, topFolder.ServerId);
                Assert.NotNull (foundParent, "Should not delete top-level folder");
                Assert.AreEqual (TopFolderServerUpdateName, foundParent.DisplayName, "Folder name should be updated by the server, not the client");

                // pending should be deleted
                var pendRenameOp = McPending.QueryByToken (defaultAccountId, token);
                Assert.Null (pendRenameOp, "Should delete pending RenameOp when FolderSync:Update (Rename) command's ServerId matches pending's ServerId");
            }

            [Test]
            public void TestFolderSyncServerUpdateMove ()
            {
                // If ServerId match, delete pending. Server wins.
                var topFolder = CreateTopFolder (withPath: true);

                // ServerId must match the serverId in SyncResponseUpdateMove XML
                var destFolder = FolderOps.CreateFolder (defaultAccountId, parentId: "0", serverId: "2", name: "Dest-Folder", typeCode: TypeCode.UserCreatedGeneric_1);
                PathOps.CreatePath (defaultAccountId, destFolder.ServerId, destFolder.ParentId);

                // folder that the client will attempt to move the item into
                var siblingFolder = FolderOps.CreateFolder (defaultAccountId, parentId: "0", serverId: "3", name: "Sibling", typeCode: TypeCode.UserCreatedGeneric_1);
                PathOps.CreatePath (defaultAccountId, siblingFolder.ServerId, siblingFolder.ParentId);

                var renameString = "TopFolder (UPDATED BY CLIENT)";
                string token = null;
                DoClientSideCmds (() => {
                    // make pending op; this op should never be executed
                    token = Context.ProtoControl.RenameFolderCmd (topFolder.Id, renameString);
                });

                // should move top folder into siblingFolder
                ExecuteConflictTest (FolderCmd, SyncResponseUpdateMoveTop);

                // top-level folder should be updated by server, not client
                var foundParent = McFolder.QueryByServerId<McFolder> (defaultAccountId, topFolder.ServerId);
                Assert.NotNull (foundParent, "Should not delete top-level folder");
                Assert.AreEqual (destFolder.ServerId, foundParent.ParentId, "Folder should be moved to the folder the server says it should move to");
                Assert.AreNotEqual (renameString, foundParent.DisplayName, "Folder should not be renamed because server should overwrite pending rename");

                // pending should be deleted
                var pendRenameOp = McPending.QueryByToken (defaultAccountId, token);
                Assert.Null (pendRenameOp, "Should delete pending MoveOp when FolderSync:Update (Move) command's ServerId matches pending's ServerId");
            }
        }

        public class FolderUpdateMoveConfResTest : BaseConfResTest
        {
            [Test]
            public void TestFolderSyncAdd ()
            {
                var curParent = FolderOps.CreateFolder (defaultAccountId, parentId: "0", serverId: "3", name: "Cur-Parent-Folder", typeCode: TypeCode.UserCreatedGeneric_1);
                PathOps.CreatePath (defaultAccountId, curParent.ServerId, curParent.ParentId);

                var topFolder = CreateTopFolder (withPath: true);

                // name and type must match those same fields in SyncResponseAddSub
                string clientChildServerId = "15"; // the serverId of the child from the client's perspective
                var curChildOnClient = FolderOps.CreateFolder (defaultAccountId, parentId: curParent.ServerId, serverId: "15", name: ChildFolderName, typeCode: TypeCode.UserCreatedGeneric_1);
                PathOps.CreatePath (defaultAccountId, curChildOnClient.ServerId, curChildOnClient.ParentId);

                string token = null;
                DoClientSideCmds (() => {
                    token = Context.ProtoControl.MoveFolderCmd (curChildOnClient.Id, topFolder.Id);
                });

                // should add equivalent folder to childFolder to topFolder
                ExecuteConflictTest (FolderCmd, SyncResponseAddSub);

                // find child folder that existed on client and was moved
                var foundChild = McFolder.QueryByServerId<McFolder> (defaultAccountId, clientChildServerId);
                Assert.AreEqual (topFolder.ServerId, foundChild.ParentId, "Should move folder into folder defined by both client and server");

                // find child folder added by server
                var foundServerChild = McFolder.QueryByServerId<McFolder> (defaultAccountId, "2");
                Assert.AreEqual (topFolder.ServerId, foundServerChild.ParentId, "Should move folder into folder defined by both client and server");

                // should create a re-write to alter the pending's DisplayName
                var pendMoveOp = McPending.QueryByToken (defaultAccountId, token);
                Assert.NotNull (pendMoveOp, "MoveOp should not be deleted");
                Assert.AreEqual (ChildFolderName + " Client-Moved", pendMoveOp.DisplayName, "Should append suffix to note conflict");
            }
        }

        public class FolderSyncDeleteConfResTest : BaseConfResTest
        {
            [Test]
            public void TestFetchAttachment ()
            {
                // If the pending's ServerId is dominated by the command's ServerId, then delete the pending ItemOperations Fetch.
                var topFolder = CreateTopFolder (withPath: true);
                var email = FolderOps.CreateUniqueItem<McEmailMessage> ();
                topFolder.Link (email);
                PathOps.CreatePath (defaultAccountId, email.ServerId, topFolder.ServerId);
                var att = FolderOps.CreateAttachment (item: email, displayName: "My-Attachment");

                string token = null;
                DoClientSideCmds (() => {
                    token = Context.ProtoControl.DnldAttCmd (att.Id);
                });

                ExecuteConflictTest (FolderCmd, SyncResponseDeleteTop);

                var foundEmail = McEmailMessage.QueryByServerId<McEmailMessage> (defaultAccountId, email.ServerId);
                Assert.Null (foundEmail, "Server delete of parent folder should also delete email");

                var foundAtt = McAttachment.QueryById<McAttachment> (att.Id);
                Assert.Null (foundAtt, "Server delete of parent folder should also delete attachment");

                var pendDnldOp = McPending.QueryByToken (defaultAccountId, token);
                Assert.Null (pendDnldOp, "Pending fetch attachment operation should be deleted when a folder that dominates the item is deleted");
            }

            [Test]
            public void TestMeetingResponse ()
            {
                // If the pending's ServerId is dominated by the command's ServerId, then delete the pending MeetingResponse.
                var topFolder = CreateTopFolder (withPath: true);
                var cal = FolderOps.CreateUniqueItem<McCalendar> ();
                topFolder.Link (cal);
                PathOps.CreatePath (defaultAccountId, cal.ServerId, topFolder.ServerId);
                var response = NcResponseType.Accepted;

                string token = null;
                DoClientSideCmds (() => { 
                    token = Context.ProtoControl.RespondCalCmd (cal.Id, response);
                });

                ExecuteConflictTest (FolderCmd, SyncResponseDeleteTop);

                var foundCal = McCalendar.QueryByServerId<McCalendar> (defaultAccountId, cal.ServerId);
                Assert.Null (foundCal, "Server delete of parent folder should also delete sub calendar");

                var pendResponseOp = McPending.QueryByToken (defaultAccountId, token);
                Assert.Null (pendResponseOp, "Pending meeting response operation should be deleted when a folder that dominates the item is deleted");
            }
        }

        [TestFixture]
        public class TestSyncCmdAdd : BaseConfResTest
        {
            [SetUp]
            public new void SetUp ()
            {
                base.SetUp ();
                BackEnd.Instance.EstablishService (defaultAccountId);  // make L&F folder
            }

            // create cal, contact, and task
            [Test]
            public void TestSyncAddMatchAllitems ()
            {
                TestSyncMatch<McCalendar> (TypeCode.DefaultCal_8,
                    (itemId, parentId) => Context.ProtoControl.CreateCalCmd (itemId, parentId)
                );

                TestSyncMatch<McContact> (TypeCode.DefaultContacts_9,
                    (itemId, parentId) => Context.ProtoControl.CreateContactCmd (itemId, parentId)
                );

                TestSyncMatch<McTask> (TypeCode.DefaultTasks_7,
                    (itemId, parentId) => Context.ProtoControl.CreateTaskCmd (itemId, parentId)
                );
            }

            [Test]
            public void TestSyncUpdateMatchAllItems ()
            {
                TestSyncMatch <McCalendar> (TypeCode.DefaultCal_8,
                    (itemId, parentId) => Context.ProtoControl.UpdateCalCmd (itemId)
                );

                TestSyncMatch <McContact> (TypeCode.DefaultContacts_9,
                    (itemId, parentId) => Context.ProtoControl.UpdateContactCmd (itemId)
                );

                TestSyncMatch <McTask> (TypeCode.DefaultTasks_7,
                    (itemId, parentId) => Context.ProtoControl.UpdateTaskCmd (itemId)
                );
            }

            public void TestSyncMatch<T> (TypeCode topFolderType, Func<int, int, string> creationCmd) where T : McItem, new()
            {
                // If pending's ParentId matches the ServerId of the command, then move to lost+found and delete pending.
                var topFolder = CreateTopFolder (withPath: true, type: topFolderType);
                McItem item = MakeSingleLayerPath<T> (topFolder);

                string token = null;
                DoClientSideCmds (() => {
                    token = creationCmd (item.Id, topFolder.Id);
                });

                DoSyncAdd<T> (item, token);
            }
                
            [Test]
            public void TestSyncAddDomAllItems ()
            {
                TestSyncDom<McCalendar> (TypeCode.DefaultCal_8, 
                    () => CreateSubCalFolder (withPath: true), 
                    (itemId, folderId) => Context.ProtoControl.CreateCalCmd (itemId, folderId)
                );

                TestSyncDom<McContact> (TypeCode.DefaultContacts_9,
                    () => CreateSubContactFolder (withPath: true),
                    (itemId, folderId) => Context.ProtoControl.CreateContactCmd (itemId, folderId)
                );

                TestSyncDom<McTask> (TypeCode.DefaultTasks_7,
                    () => CreateSubTaskFolder (withPath: true),
                    (itemId, folderId) => Context.ProtoControl.CreateTaskCmd (itemId, folderId)
                );
            }

            [Test]
            public void TestSyncUpdateDomAllItems ()
            {
                TestSyncDom<McCalendar> (TypeCode.DefaultCal_8,
                    () => CreateSubCalFolder (withPath: true),
                    (itemId, folderId) => Context.ProtoControl.UpdateCalCmd (itemId)
                );

                TestSyncDom<McContact> (TypeCode.DefaultContacts_9,
                    () => CreateSubContactFolder (withPath: true),
                    (itemId, folderId) => Context.ProtoControl.UpdateContactCmd (itemId)
                );

                TestSyncDom<McTask> (TypeCode.DefaultTasks_7,
                    () => CreateSubTaskFolder (withPath: true),
                    (itemId, folderId) => Context.ProtoControl.UpdateTaskCmd (itemId)
                );
            }

            public void TestSyncDom<T> (TypeCode topFolderType, Func<McFolder> makeSubFolder, Func<int, int, string> makeItem) where T : McItem, new()
            {
                // If pending's ParentId is dominated by the ServerId of the command, then move to lost+found and delete pending.
                McFolder subFolder = null;
                McItem item = MakeDoubleLayerPath<T> (topFolderType, () => {
                    subFolder = makeSubFolder ();
                    return subFolder;
                });

                string token = null;
                DoClientSideCmds (() => {
                    token = makeItem (item.Id, subFolder.Id);
                });

                DoSyncAdd<T> (item, token);
            }

            private McItem MakeSingleLayerPath<T> (McFolder topFolder) where T : McItem, new()
            {
                var item = FolderOps.CreateUniqueItem<T> ();
                topFolder.Link (item);
                PathOps.CreatePath (defaultAccountId, item.ServerId, topFolder.ServerId);
                return item;
            }

            private McItem MakeDoubleLayerPath<T> (TypeCode topFolderType, Func<McFolder> makeSubFolder) where T : McItem, new()
            {
                CreateTopFolder (withPath: true, type: topFolderType);
                var subFolder = makeSubFolder ();
                var item = FolderOps.CreateUniqueItem<T> ();
                subFolder.Link (item);
                PathOps.CreatePath (defaultAccountId, item.ServerId, subFolder.ServerId);
                return item;
            }

            private void DoSyncAdd<T> (McItem item, string token) where T : McItem, new()
            {
                ExecuteConflictTest (FolderCmd, SyncResponseDeleteTop);

                // QueryByServerId asserts if more than one item is found
                var foundItem = McItem.QueryByServerId<T> (defaultAccountId, item.ServerId);
                Assert.NotNull (foundItem, "Item should not be deleted; only moved to L&F");

                var laf = McFolder.GetLostAndFoundFolder (defaultAccountId);
                var foundParent = McMapFolderFolderEntry.QueryByFolderId (defaultAccountId, laf.Id);
                Assert.AreEqual (foundItem.Id, foundParent.FirstOrDefault ().FolderEntryId, "Item should be moved into L&F");

                var foundPend = McPending.QueryByToken (defaultAccountId, token);
                Assert.Null (foundPend, "Pending should be deleted when server delete command dominates pending");
            }
        }

        // State machine part of class
        public partial class BaseConfResTest : CommonTestOps
        {
            /* Execute any commands that rely on the proto control state machine within the lambda of this function */
            public void DoClientSideCmds (Action doCmds)
            {
                var syncEvent = new AutoResetEvent(false);
                Context.ProtoControl.Sm = CreatePhonyProtoSm (() => {
                    // Gets set when CreateFolderCmd completes
                    syncEvent.Set ();
                });

                doCmds ();

                bool didFinish = syncEvent.WaitOne (1000);
                Assert.IsTrue (didFinish, "Folder creation did not finish");
            }

            public void ExecuteConflictTest (AsCommand cmd, string responseXml)
            {
                var autoResetEvent = new AutoResetEvent(false);

                NcStateMachine sm = CreatePhonySM (() => {
                    autoResetEvent.Set ();
                });

                MockHttpClient.ProvideHttpResponseMessage = (request) => {
                    var mockResponse = new HttpResponseMessage () {
                        Content = new StringContent (responseXml, Encoding.UTF8, "text/xml"),
                    };

                    return mockResponse;
                };

                cmd.Execute (sm);

                bool didFinish = autoResetEvent.WaitOne (2000);
                Assert.IsTrue (didFinish, "FolderCmd operation did not finish");
            }

            public NcStateMachine CreatePhonyProtoSm (Action action)
            {
                var sm = new NcStateMachine ("PHONY-PROTO") {
                    Name = "PhonyProtoControlSm",
                    LocalEventType = typeof(AsProtoControl.CtlEvt),
                    LocalStateType = typeof(AsProtoControl.Lst),
                    TransTable = new [] {
                        new Node {State = (uint)St.Start,
                            On = new [] {
                                new Trans { 
                                    Event = (uint)AsProtoControl.CtlEvt.E.PendQ, 
                                    Act = delegate () {
                                        // DoPick happens here in AsProtoControl
                                        // Stop the operation here: We don't need to go any further (item has already been added to pending queue)
                                        action ();
                                    },
                                    State = (uint)St.Start },
                            }
                        },
                    }
                };
                return sm;
            }

            // state machine for http op
            public NcStateMachine CreatePhonySM (Action action)
            {
                var sm = new NcStateMachine ("PHONY") {
                    Name = "BasicPhonyPing",
                    LocalEventType = typeof(AsProtoControl.CtlEvt),
                    LocalStateType = typeof(AsProtoControl.Lst),
                    TransTable = new [] {
                        new Node {State = (uint)St.Start,
                            On = new [] {
                                new Trans {
                                    Event = (uint)SmEvt.E.Launch,
                                    Act = delegate () {},
                                    State = (uint)St.Start },
                                new Trans { 
                                    Event = (uint)SmEvt.E.Success, 
                                    Act = delegate () {
                                        Log.Info (Log.LOG_TEST, "Success event was posted to Owner SM");
                                        action();
                                    },
                                    State = (uint)St.Start },
                                new Trans {
                                    Event = (uint)SmEvt.E.HardFail,
                                    Act = delegate () {
                                        Log.Info (Log.LOG_TEST, "Hard fail was posted to Owner SM");
                                    },
                                    State = (uint)St.Start },
                            }
                        },
                    }
                };
                return sm;
            }
        }
    }
}

