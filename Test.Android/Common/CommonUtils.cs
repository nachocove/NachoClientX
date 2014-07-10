//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;
using NUnit.Framework;
using System.Collections.Generic;
using TypeCode = NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode;
using System.Threading;
using System.Net.Http;
using System.Text;

namespace Test.iOS
{
    public class ProtoOps : CommonTestOps
    {
        public const string TopFolderName = "Top-Level-Folder";

        public static McAccount CreateAccount (int pcsId = 5)
        {
            var account = new McAccount () {
                EmailAddr = "johnd@foo.utopiasystems.net",
                ServerId = 1,
                ProtocolStateId = pcsId,
            };
            account.Insert ();

            return account;
        }

        public static McProtocolState CreateProtocolState ()
        {
            McProtocolState pcs = new McProtocolState ();
            pcs.Insert ();

            return pcs;
        }

        public static AsProtoControl CreateProtoControl (int accountId = defaultAccountId)
        {
            // clean static property
            MockOwner.Status = null;

            var pcs = CreateProtocolState ();
            CreateAccount (pcs.Id);
            NcTask.StartService ();

            MockOwner mockOwner = new MockOwner ();
            AsProtoControl protoControl = new AsProtoControl (mockOwner, accountId);

            return protoControl;
        }

        // Parent & Server Id's correspond to response XML
        public static McFolder CreateTopFolder (bool withPath = false, TypeCode type = TypeCode.UserCreatedGeneric_1)
        {
            var folder = FolderOps.CreateFolder (defaultAccountId, parentId: "0", serverId: "1", name: TopFolderName, typeCode: type);
            if (withPath) {
                // Set up path: This is the client's "best understanding" of the servers point of view the last time they talked
                PathOps.CreatePath (defaultAccountId, folder.ServerId, folder.ParentId);
            }
            return folder;
        }

        /* Execute any commands that rely on the proto control state machine within the lambda of this function */
        public static void DoClientSideCmds (MockContext context, Action doCmds)
        {
            var syncEvent = new AutoResetEvent(false);
            context.ProtoControl.Sm = CreatePhonyProtoSm (() => {
                // Gets set when CreateFolderCmd completes
                syncEvent.Set ();
            });

            doCmds ();

            bool didFinish = syncEvent.WaitOne (1000);
            Assert.IsTrue (didFinish, "Folder creation did not finish");
        }


        public static NcStateMachine CreatePhonyProtoSm (Action action)
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

        public static void ExecuteConflictTest (AsCommand cmd, string responseXml)
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
            
        // state machine for http op
        public static NcStateMachine CreatePhonySM (Action action)
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

    public class FolderOps : CommonTestOps
    {
        public const string defaultServerId = "5";

        public static T CreateUniqueItem<T> (int accountId = defaultAccountId, string serverId = defaultServerId) where T : McItem, new ()
        {
            T newItem = new T {
                AccountId = accountId,
                ServerId = serverId,
            };
            newItem.Insert ();
            return newItem;
        }

        public static McAttachment CreateAttachment (McItem item, int accountId = defaultAccountId, string displayName = "")
        {
            var att =  new McAttachment {
                AccountId = accountId,
                DisplayName = displayName,
                EmailMessageId = item.Id,
            };
            att.Insert ();
            return att;
        }

        public static void ItemsAreEqual (McItem item1, McItem item2)
        {
            Assert.AreEqual (item1.Id, item2.Id, "Equivalent items should have the same Id");
            Assert.AreEqual (item1.AccountId, item2.AccountId, "Equivalent items should have the same AccountId");
            Assert.AreEqual (item2.ServerId, item2.ServerId, "Equivalent items should have the same ServerId");
        }

        public static McFolder CreateFolder (int accountId, bool isClientOwned = false, bool isHidden = false, string parentId = "0", 
            string serverId = defaultServerId, string name = "Default name", NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode typeCode = NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1,
            bool isAwaitingDelete = false, bool isAwaitingCreate = false, bool autoInsert = true, string asSyncKey = "-1", 
            bool syncMetaToClient = false)
        {
            McFolder folder = McFolder.Create (accountId, isClientOwned, isHidden, parentId, serverId, name, typeCode);

            folder.IsAwaitingDelete = isAwaitingDelete;
            folder.IsAwaitingCreate = isAwaitingCreate;

            if (asSyncKey == "-1") {
                asSyncKey = null;
            } else {
                folder.AsSyncKey = asSyncKey;
            }
            folder.AsSyncMetaToClientExpected = syncMetaToClient;

            if (autoInsert) { folder.Insert (); }
            return folder;
        }
    }

    public class PathOps : CommonTestOps
    {
        public static McPath CreatePath (int accountId, string serverId = "", string parentId = "")
        {
            var path = new McPath (accountId);
            path.ServerId = serverId;
            path.ParentId = parentId;
            path.Insert ();
            return path;
        }

        public class McPathNode
        {
            public McPathNode (McPath root)
            {
                Root = root;
                Children = new List<McPathNode> ();
            }

            public McPath Root;
            public List<McPathNode> Children;
        }
    }

    public class CommonTestOps
    {
        public const int defaultAccountId = 1;

        public void SetUp ()
        {
            NcModel.Instance.Reset (System.IO.Path.GetTempFileName ());
           
            // turn off telemetry logging for tests
            LogSettings settings = Log.SharedInstance.Settings;
            settings.Error.DisableTelemetry ();
            settings.Warn.DisableTelemetry ();
            settings.Info.DisableTelemetry ();
            settings.Debug.DisableTelemetry ();
        }

        public void TestForNachoExceptionFailure (Action action, string message)
        {
            try {
                action ();
                Assert.Fail (message);
            } catch (NachoCore.Utils.NcAssert.NachoAssertionFailure e) {
                Log.Info (Log.LOG_TEST, "NachoAssertFailure message: {0}", e.Message);
            }
        }
    }
}

