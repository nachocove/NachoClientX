//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.ActiveSync;
using System.Linq;
using ProtoOps = Test.iOS.CommonProtoControlOps;
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
    public class ConflictResolutionTest
    {
        public partial class BaseConflictResTest : CommonTestOps
        {
            public AsProtoControl ProtoControl;
            public AsFolderSyncCommand FolderCmd;

            public string SyncResponseXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<FolderSync xmlns=\"FolderHierarchy\">\n  <Status>1</Status>\n  <SyncKey>1</SyncKey>\n  <Changes>\n    <Count>3</Count>\n    <Add>\n      <ServerId>0</ServerId>\n      <ParentId>0</ParentId>\n      <DisplayName>Top-Level-Folder</DisplayName>\n      <Type>1</Type>\n    </Add>\n    <Add>\n      <ServerId>2</ServerId>\n      <ParentId>0</ParentId>\n      <DisplayName>Sub-Cal-Folder</DisplayName>\n      <Type>13</Type>\n    </Add>\n    <Add>\n      <ServerId>3</ServerId>\n      <ParentId>0</ParentId>\n      <DisplayName>Sub-Task-Folder</DisplayName>\n      <Type>15</Type>\n    </Add>\n  </Changes>\n</FolderSync>";

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

            public McFolder CreateFolder (int destId, string name, TypeCode type, string parentId)
            {
                ProtoControl.CreateFolderCmd (destId, name, type);
                var found = GetCreatedFolder (defaultAccountId, type, parentId, name);
                return found;
            }

            private McFolder GetCreatedFolder (int accountId, TypeCode type, string parentId, string name)
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

        public class BasicConflictResTest : BaseConflictResTest
        {
            [Test]
            public void TestFolderCreateConflict ()
            {
                var folderCreateEvent = new AutoResetEvent(false);
                ProtoControl.Sm = CreatePhonyProtoSm (() => {
                    // Gets set when CreateFolderCmd completes
                    folderCreateEvent.Set ();
                });

                var topFolderName = "Top-Level-Folder";
                var topFolderType = TypeCode.UserCreatedGeneric_1;
                var pendTopFolder = CreateFolder (-1, topFolderName, topFolderType, "0");

                var subCalFolder = CreateFolder (pendTopFolder.Id, "Sub-Cal-Folder", TypeCode.UserCreatedCal_13, pendTopFolder.ServerId);
                var subTaskFolder = CreateFolder (pendTopFolder.Id, "Sub-Task-Folder", TypeCode.UserCreatedTasks_15, pendTopFolder.ServerId);

                bool didFinish = folderCreateEvent.WaitOne (1000);
                Assert.IsTrue (didFinish, "Folder creation did not finish");

                ExecuteConflictTest ();

                // Assert that the correct changes to the Q and to the Model DB happen because of the FolderSync.
                var foundTopFolder = McFolder.QueryById<McFolder> (pendTopFolder.Id);
                var foundCalFolder = McFolder.QueryById<McFolder> (subCalFolder.Id);
                var foundTaskFolder = McFolder.QueryById<McFolder> (subTaskFolder.Id);

                string expectedServerId = "0";  // Should match ServerId in SyncResponseXml
                Assert.AreEqual (expectedServerId, foundTopFolder.ServerId, "Top level folder's ServerId should match ServerId sent from server");
                Assert.AreEqual (expectedServerId, foundCalFolder.ParentId, "Sub folders' ParentId's should match ServerId sent from server");
                Assert.AreEqual (expectedServerId, foundTaskFolder.ParentId, "Sub folders' ParentId's should match ServerId sent from server");
            }
        }

        // State machine part of class
        public partial class BaseConflictResTest : CommonTestOps
        {
            public void ExecuteConflictTest ()
            {
                var autoResetEvent = new AutoResetEvent(false);

                NcStateMachine sm = CreatePhonySM (() => {
                    autoResetEvent.Set ();
                });

                MockHttpClient.ProvideHttpResponseMessage = (request) => {
                    var mockResponse = new HttpResponseMessage () {
                        Content = new StringContent (SyncResponseXml, Encoding.UTF8, "text/xml"),
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

