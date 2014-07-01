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
using NachoCore.Model;
using System.Text;
using NachoCore.Utils;
using System.Net.Http;
using System.Threading;


/* Response code document: http://msdn.microsoft.com/en-us/library/ff631512(v=exchg.80).aspx */
namespace Test.iOS
{
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
        public partial class BaseConflictResTest : CommonTestOps
        {
            public AsProtoControl ProtoControl;
            public AsFolderSyncCommand FolderCmd;

            public string SyncResponseXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<FolderSync xmlns=\"FolderHierarchy\">\n  <Status>1</Status>\n  <SyncKey>1</SyncKey>\n  <Changes>\n    <Count>3</Count>\n    <Add>\n      <ServerId>0</ServerId>\n      <ParentId>0</ParentId>\n      <DisplayName>Top-Level-Folder</DisplayName>\n      <Type>1</Type>\n    </Add>\n    <Add>\n      <ServerId>2</ServerId>\n      <ParentId>0</ParentId>\n      <DisplayName>Sub-Cal-Folder</DisplayName>\n      <Type>13</Type>\n    </Add>\n    <Add>\n      <ServerId>3</ServerId>\n      <ParentId>0</ParentId>\n      <DisplayName>Sub-Task-Folder</DisplayName>\n      <Type>15</Type>\n    </Add>\n  </Changes>\n</FolderSync>";
            public string SyncResponseDelete = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<FolderSync xmlns=\"FolderHierarchy\">\n  <Status>1</Status>\n  <SyncKey>1</SyncKey>\n  <Changes>\n    <Count>2</Count>\n    <Delete>\n      <ServerId>1</ServerId>\n      <ParentId>0</ParentId>\n      <DisplayName>Top-Level-Folder</DisplayName>\n      <Type>1</Type>\n    </Delete>\n  </Changes>\n</FolderSync>";

            [SetUp]
            public new void SetUp ()
            {
                base.SetUp ();
                ProtoControl = ProtoOps.CreateProtoControl (accountId: defaultAccountId);
                FolderCmd = CreateFolderSyncCmd ();
            }

            public AsFolderSyncCommand CreateFolderSyncCmd ()
            {
                var context = new MockContext (ProtoControl);
                context.Server = CreateServer ();
                var folderCmd = new AsFolderSyncCommand (context);
                folderCmd.HttpClientType = typeof(MockHttpClient);
                folderCmd.DnsQueryRequestType = typeof(MockDnsQueryRequest);
                return folderCmd;
            }

            private McServer CreateServer ()
            {
                var phonyServer = McServer.Create (CommonMockData.MockUri);
                return phonyServer;
            }

            public CreatedFolder CreateFolder (int destId, string name, TypeCode type, string parentId)
            {
                var token = ProtoControl.CreateFolderCmd (destId, name, type);
                var found = GetCreatedFolder (defaultAccountId, type, parentId, name);
                return new CreatedFolder (found, token);
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
        }

        public class FolderCreateConfResTest : BaseConflictResTest
        {
            [Test]
            public void TestAddConflict ()
            {
                // make dummy placeholder folders because C# does not have the equivalent of Obj-C __block modifier for vars
                var pendTopFolder = new McFolder ();
                var subCalFolder = new McFolder ();
                var subTaskFolder = new McFolder ();

                DoClientSideCmds (() => {
                    var topFolderName = "Top-Level-Folder";
                    var topFolderType = TypeCode.UserCreatedGeneric_1;
                    pendTopFolder = CreateFolder (-1, topFolderName, topFolderType, "0").Folder;

                    subCalFolder = CreateFolder (pendTopFolder.Id, "Sub-Cal-Folder", TypeCode.UserCreatedCal_13, pendTopFolder.ServerId).Folder;
                    subTaskFolder = CreateFolder (pendTopFolder.Id, "Sub-Task-Folder", TypeCode.UserCreatedTasks_15, pendTopFolder.ServerId).Folder;
                });

                ExecuteConflictTest (SyncResponseXml);

                // Assert that the correct changes to the Q and to the Model DB happen because of the FolderSync.
                var foundTopFolder = McFolder.QueryById<McFolder> (pendTopFolder.Id);
                var foundCalFolder = McFolder.QueryById<McFolder> (subCalFolder.Id);
                var foundTaskFolder = McFolder.QueryById<McFolder> (subTaskFolder.Id);

                string expectedServerId = "0";  // Should match ServerId in SyncResponseXml
                Assert.AreEqual (expectedServerId, foundTopFolder.ServerId, "Top level folder's ServerId should match ServerId sent from server");
                Assert.AreEqual (expectedServerId, foundCalFolder.ParentId, "Sub folders' ParentId's should match ServerId sent from server");
                Assert.AreEqual (expectedServerId, foundTaskFolder.ParentId, "Sub folders' ParentId's should match ServerId sent from server");

                Assert.AreEqual ("2", foundCalFolder.ServerId, "Sub-folder ServerId should match ServerId in response XML");
                Assert.AreEqual ("3", foundTaskFolder.ServerId, "Sub-folder ServerId should match ServerId in response XML");
            }

            [Test]
            public void TestAddTypeConflict ()
            {
                // If DisplayName and ParentId match but Type does not match, then alter 
                // DisplayName of the pending so the user sees the potential conflict
                var pendTopFolder = new CreatedFolder ();
                var badType = TypeCode.UserCreatedCal_13; // does not match type in xml response
                var folderName = "Top-Level-Folder";

                DoClientSideCmds (() => {
                    pendTopFolder = CreateFolder (-1, folderName, badType, "0");
                });

                ExecuteConflictTest (SyncResponseXml);

                var foundTopFolder = GetCreatedFolder (defaultAccountId, TypeCode.UserCreatedGeneric_1, "0", folderName);
                var pending = McPending.QueryByToken (defaultAccountId, pendTopFolder.Token);

                Assert.AreEqual ("0", foundTopFolder.ServerId, "Sanity check: ServerId of top folder should match expected");
                Assert.AreEqual (TypeCode.UserCreatedGeneric_1, foundTopFolder.Type, "Type should be changed to match server's type when name and parentId match but type does not");
                Assert.AreNotEqual (folderName, pending.DisplayName, "DisplayName should be changed when server's display name for the item does not match client's name");
            }

            [Test]
            public void TestAddSameFolder ()
            {
                // If DisplayName and ParentId and Type match, then both server and client are trying to create the same folder - do: 
                var pendTopFolder = new CreatedFolder ();
                var type = TypeCode.UserCreatedGeneric_1;
                var folderName = "Top-Level-Folder";

                DoClientSideCmds (() => {
                    pendTopFolder = CreateFolder (-1, folderName, type, "0");
                });

                ExecuteConflictTest (SyncResponseXml);

                var foundTopFolder = GetCreatedFolder (defaultAccountId, TypeCode.UserCreatedGeneric_1, "0", folderName);
                var pending = McPending.QueryByToken (defaultAccountId, pendTopFolder.Token);

                // (1) Delete pending FolderCreate, but don't perform the failure-action, and don't notify via StatusInd. 
                Assert.Null (pending, "Pending FolderCreate should be deleted");
                Assert.AreEqual (NcResult.SubKindEnum.Info_FolderSyncSucceeded, MockOwner.Status.SubKind, "Should not notify StatusInd");

                // (2) Add a re-write from pending's ServerId (the guid) to command's ServerId.
                Assert.AreEqual ("0", foundTopFolder.ServerId, "Add a re-write from pending's ServerId (the guid) to command's ServerId");
            }

            [Test]
            public void TestDeleteCommand ()
            {
                // If the command's ServerId dominates the pending's ParentId, then the destination for the 
                // new folder is being deleted by the server. 

                // top-folder
                var folderName = "Top-Level-Folder";
                var type = TypeCode.UserCreatedGeneric_1;

                var topFolder = FolderOps.CreateFolder (defaultAccountId, parentId: "0", serverId: "1", name: folderName, typeCode: type);

                var subFolder = new CreatedFolder ();

                // Set up path: This is the client's "best understanding" of the servers point of view the last time they talked
                PathOps.CreatePath (defaultAccountId, "1", "0");
              
                DoClientSideCmds (() => {
                    subFolder = CreateFolder (topFolder.Id, "Sub-Cal-Folder", TypeCode.UserCreatedCal_13, topFolder.ServerId);
                });

                ExecuteConflictTest (SyncResponseDelete);

                var subFolderPending = McPending.QueryByToken (defaultAccountId, subFolder.Token);
                Assert.Null (subFolderPending, "Should delete pending if the destination for the new folder is being deleted by the server");
            }
        }

        // State machine part of class
        public partial class BaseConflictResTest : CommonTestOps
        {
            /* Execute any commands that rely on the proto control state machine within the lambda of this function */
            public void DoClientSideCmds (Action doCmds)
            {
                var folderCreateEvent = new AutoResetEvent(false);
                ProtoControl.Sm = CreatePhonyProtoSm (() => {
                    // Gets set when CreateFolderCmd completes
                    folderCreateEvent.Set ();
                });

                doCmds ();

                bool didFinish = folderCreateEvent.WaitOne (1000);
                Assert.IsTrue (didFinish, "Folder creation did not finish");
            }

            public void ExecuteConflictTest (string responseXml)
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

                FolderCmd.Execute (sm);

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

