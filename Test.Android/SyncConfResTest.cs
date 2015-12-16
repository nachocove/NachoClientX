//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.ActiveSync;
using System.Linq;
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
    public class SyncConfResTest
    {
        public partial class BaseSyncConfResTest : CommonTestOps
        {
            public MockContext Context;
            public AsSyncCommand SyncCmd;

            [SetUp]
            public new void SetUp ()
            {
                base.SetUp ();

                var server = McServer.Create (defaultAccountId, McAccount.ActiveSyncCapabilities, CommonMockData.MockUri);
                server.Insert ();
                Context = new MockContext (null, null, server);
            }

            public class Inbox
            {
                public McFolder Folder;
                public McFolder DestFolder;
                public McAbstrItem Item;
                public string Token;

                public Inbox (Func<McAbstrItem> makeItem)
                {
                    Folder = ProtoOps.CreateTopFolder (withPath: true);
                    Item = makeItem ();
                    Folder.Link (Item);
                    PathOps.CreatePath (defaultAccountId, Item.ServerId, Folder.ServerId);

                    Token = null;
                }

                public void CreateDestFolder ()
                {
                    DestFolder = ProtoOps.CreateDestFolder (withPath: true);
                }
            }

            public Inbox SetState<T> (string code, NcResult.SubKindEnum opSubKind, Func<int, string> clientOp) where T : McAbstrItem, new()
            {
                Inbox inbox = new Inbox (() => {
                    return FolderOps.CreateUniqueItem<T> ();
                });

                SetSyncStrategy (inbox.Folder);

                bool[] statusIndsPosted = {false};
                MockOwner.StatusIndCallback += (result) => {
                    if (result.SubKind == opSubKind) {
                        statusIndsPosted[0] = true;
                    }
                };

                ProtoOps.DoClientSideCmds (Context, () => {
                    inbox.Token = clientOp (inbox.Item.Id); 
                });

                if (opSubKind != NcResult.SubKindEnum.NotSpecified) {
                    foreach (bool item in statusIndsPosted) { Assert.True (item, "A StatusInd was not set correctly"); }
                }

                SyncCmd = CreateSyncCmd (Context);

                return inbox;
            }

            public static AsSyncCommand CreateSyncCmd (MockContext context)
            {
                var syncCmd = new AsSyncCommand (context, ((AsProtoControl)context.ProtoControl).Strategy.GenSyncKit (context.ProtocolState));
                NcProtoControl.TestHttpClient = MockHttpClient.Instance;
                return syncCmd;
            }

            public void SetSyncStrategy (McFolder folder)
            {
                var strategy = new MockStrategy (folder);
                ((AsProtoControl)Context.ProtoControl).Strategy = strategy;
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

            // item-specific update code goes in uniqueData; must prefix with XNamespace of item
            public static string SyncAddItemCmdXml (string serverId, string parentId, XElement uniqueData)
            {
                return AirSyncCmdHierarchyRoot (parentId, (ns) => {
                    return new XElement (ns + "Add",
                        new XElement (ns + "ServerId", serverId),
                        new XElement (ns + "ApplicationData",
                            uniqueData));
                });
            }

            // item-specific update code goes in uniqueData; must prefix with XNamespace of item
            public static string SyncUpdateCmdItemXml (string serverId, string parentId, string classCode, XElement uniqueData)
            {
                return AirSyncCmdHierarchyRoot (parentId, (ns) => {
                    return new XElement (ns + "Change",
                        new XElement (ns + "ServerId", serverId),
                        new XElement (ns + "Class", classCode),
                        new XElement (ns + "ApplicationData",
                            uniqueData));
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

        [TestFixture]
        public class FolderDeleteTests : BaseSyncConfResTest
        {
            public void SyncGenericOp (TypeCode topFolderType, NcResult.SubKindEnum opSubKind, Func<string, string, string> makeItemOpXml)
            {

                // If the pending's ServerId dominates the command's ServerId, then drop the command.
                var itemServerId = "5";
                var topFolder = ProtoOps.CreateTopFolder (withPath: true, type: topFolderType);

                bool[] statusIndsPosted = {false, false};

                SetSyncStrategy (topFolder);

                MockOwner.StatusIndCallback += (result) => {
                    if (result.SubKind == NcResult.SubKindEnum.Info_FolderSetChanged) {
                        statusIndsPosted[0] = true;
                    }
                    if (result.SubKind == opSubKind) {
                        statusIndsPosted[1] = true;
                    }
                };

                string token = null;
                ProtoOps.DoClientSideCmds (Context, () => {
                    token = Context.ProtoControl.DeleteFolderCmd (topFolder.Id).GetValue<string> ();
                });

                SyncCmd = CreateSyncCmd (Context);

                var itemOpXml = makeItemOpXml (itemServerId, topFolder.ServerId);

                ProtoOps.ExecuteConflictTest (SyncCmd, itemOpXml);

                var foundPending = McPending.QueryByToken (defaultAccountId, token).FirstOrDefault ();
                Assert.NotNull (foundPending, "Pending should not be deleted by client");

                var foundFolder = McFolder.QueryByServerId<McFolder> (defaultAccountId, topFolder.ServerId);
                Assert.Null (foundFolder, "Folder should be deleted by the client");

                var foundItem = foundPending.GetItem ();
                Assert.Null (foundItem, "Command to create item should be dropped by the server");

                // make sure all StatusInd calls were made correctly
                foreach (bool item in statusIndsPosted) { Assert.True (item, "A StatusInd was not set correctly"); }
            }

            // create cal, contact, and task
            [Test]
            public void SyncAdd ()
            {
                XNamespace calNs = ClassCode.Calendar;
                SyncGenericOp (TypeCode.DefaultCal_8, NcResult.SubKindEnum.Info_CalendarSetChanged,
                    (serverId, parentId) => SyncAddItemCmdXml (serverId, parentId, 
                        new XElement (calNs + "Subject", "(SERVER)"))
                );

                SetUp ();

                XNamespace contactNs = ClassCode.Contacts;
                SyncGenericOp (TypeCode.DefaultContacts_9, NcResult.SubKindEnum.Info_ContactSetChanged, 
                    (serverId, parentId) => SyncAddItemCmdXml (serverId, parentId, 
                        new XElement (contactNs + "FirstName", "(SERVER)"))
                );

                SetUp ();

                XNamespace taskNs = ClassCode.Tasks;
                SyncGenericOp (TypeCode.DefaultTasks_7, NcResult.SubKindEnum.Info_TaskSetChanged, 
                    (serverId, parentId) => SyncAddItemCmdXml (serverId, parentId, 
                        new XElement (taskNs + "Subject", "(SERVER)"))
                );
            }

            [Test]
            public void SyncChange ()
            {
                var calCode = ClassCode.Calendar;
                XNamespace calNs = calCode;
                SyncGenericOp (TypeCode.DefaultCal_8, NcResult.SubKindEnum.Info_CalendarChanged,
                    (serverId, parentId) => SyncUpdateCmdItemXml (serverId, parentId, calCode, new XElement (calNs + "Subject", "(SERVER)"))
                );

                SetUp ();

                var contactCode = ClassCode.Contacts;
                XNamespace contactNs = contactCode;
                SyncGenericOp (TypeCode.DefaultContacts_9, NcResult.SubKindEnum.Info_ContactChanged,
                    (serverId, parentId) => SyncUpdateCmdItemXml (serverId, parentId, contactCode, new XElement (contactNs + "FirstName", "(SERVER)"))
                );

                SetUp ();

                var taskCode = ClassCode.Tasks;
                XNamespace taskNs = taskCode;
                SyncGenericOp (TypeCode.DefaultTasks_7, NcResult.SubKindEnum.Info_TaskChanged,
                    (serverId, parentId) => SyncUpdateCmdItemXml (serverId, parentId, taskCode, new XElement (taskNs + "Subject", "(SERVER)"))
                );
            }

            [Test]
            public void SyncDelete ()
            {
                SyncGenericOp (TypeCode.DefaultCal_8, NcResult.SubKindEnum.Info_CalendarSetChanged,
                    (serverId, parentId) => {
                        var cal = FolderOps.CreateUniqueItem<McCalendar> (defaultAccountId, serverId: serverId);
                        PathOps.CreatePath (defaultAccountId, serverId: cal.ServerId, parentId: parentId);
                        return SyncDeleteCmdItemXml (serverId, parentId, ClassCode.Calendar);
                    }
                );

                SetUp ();

                SyncGenericOp (TypeCode.DefaultContacts_9, NcResult.SubKindEnum.Info_ContactSetChanged,
                    (serverId, parentId) => {
                        var cont = FolderOps.CreateUniqueItem<McContact> (defaultAccountId, serverId: serverId);
                        PathOps.CreatePath (defaultAccountId, serverId: cont.ServerId, parentId: parentId);
                        return SyncDeleteCmdItemXml (serverId, parentId, ClassCode.Contacts);
                    }
                );

                SetUp ();

                SyncGenericOp (TypeCode.DefaultTasks_7, NcResult.SubKindEnum.Info_TaskSetChanged,
                    (serverId, parentId) => {
                        var task = FolderOps.CreateUniqueItem<McTask> (defaultAccountId, serverId: serverId);
                        PathOps.CreatePath (defaultAccountId, serverId: task.ServerId, parentId: parentId);
                        return SyncDeleteCmdItemXml (serverId, parentId, ClassCode.Tasks);
                    }
                );

                SetUp ();

                SyncGenericOp (TypeCode.DefaultInbox_2, NcResult.SubKindEnum.Info_EmailMessageSetChanged,
                    (serverId, parentId) => {
                        var email = FolderOps.CreateUniqueItem<McEmailMessage> (defaultAccountId, serverId: serverId);
                        PathOps.CreatePath (defaultAccountId, serverId: email.ServerId, parentId: parentId);
                        return SyncDeleteCmdItemXml (serverId, parentId, ClassCode.Email);
                    }
                );
            }
        }

        [TestFixture]
        public class FetchAttachmentTests : BaseSyncConfResTest
        {
            [Test]
            public void SyncDelete ()
            {
                // If the pending's ServerId dominates the command's ServerId, then drop the command.
                var inbox = new Inbox (() => FolderOps.CreateUniqueItem<McEmailMessage> ());
                var att = FolderOps.CreateAttachment (item: inbox.Item, displayName: "My-Attachment");
                att.Link (inbox.Item);

                bool[] statusIndsPosted = {false};
                MockOwner.StatusIndCallback += (result) => {
                    if (result.SubKind == NcResult.SubKindEnum.Info_EmailMessageSetChanged) {
                        statusIndsPosted[0] = true;
                    }
                };

                SetSyncStrategy (inbox.Folder);

                string token = null;
                ProtoOps.DoClientSideCmds (Context, () => {
                    // make pending download
                    token = Context.ProtoControl.DnldAttCmd (att.Id).GetValue<string> ();
                });

                SyncCmd = CreateSyncCmd (Context);

                var itemOpXml = SyncDeleteCmdItemXml (inbox.Item.ServerId, inbox.Folder.ServerId, ClassCode.Email);
                ProtoOps.ExecuteConflictTest (SyncCmd, itemOpXml);

                var foundPending = McPending.QueryByToken (defaultAccountId, token).FirstOrDefault ();
                Assert.Null (foundPending, "Pending should be deleted by client");

                var foundItem = McEmailMessage.QueryByServerId<McEmailMessage> (defaultAccountId, inbox.Item.ServerId);
                Assert.Null (foundItem, "Item should be deleted by the server");

                var foundFolder = McFolder.QueryByServerId<McFolder> (defaultAccountId, inbox.Folder.ServerId);
                Assert.NotNull (foundFolder, "Folder should not be deleted by the server");

                foreach (bool item in statusIndsPosted) { Assert.True (item, "A StatusInd was not set correctly"); }
            }
        }

        [TestFixture]
        public class MeetingResponseTests : BaseSyncConfResTest
        {
            [Test]
            public void TestSyncDelete ()
            {
                // If the pending's ServerId matches the command's ServerId, then delete the pending MeetingResponse.
                var inbox = new Inbox (() => FolderOps.CreateUniqueItem<McCalendar> ());
                var response = NcResponseType.Accepted;

                bool[] statusIndsPosted = {false};
                MockOwner.StatusIndCallback += (result) => {
                    if (result.SubKind == NcResult.SubKindEnum.Info_CalendarSetChanged) {
                        statusIndsPosted[0] = true;
                    }
                };

                SetSyncStrategy (inbox.Folder);

                string token = null;
                ProtoOps.DoClientSideCmds (Context, () => {
                    // TODO - there should not be 2 ProtocolState records.
                    // if !Inbox && version < 14.1 then we'll get a CalUpdate, which
                    // will result in the item getting moved to LAF (and still findable.
                    // TODO - add test cases: !Inbox, < 14.1, email item.
                    var protocolState = Context.ProtoControl.ProtocolState;
                    protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.AsProtocolVersion = "14.1";
                        return true;
                    });
                    token = Context.ProtoControl.RespondCalCmd (inbox.Item.Id, response).GetValue<string> ();
                });

                SyncCmd = CreateSyncCmd (Context);

                var itemOpXml = SyncDeleteCmdItemXml (inbox.Item.ServerId, inbox.Folder.ServerId, ClassCode.Calendar);
                ProtoOps.ExecuteConflictTest (SyncCmd, itemOpXml);

                var foundPending = McPending.QueryByToken (defaultAccountId, token).FirstOrDefault ();
                Assert.Null (foundPending, "Pending should be deleted by client");

                var foundItem = McCalendar.QueryByServerId<McCalendar> (defaultAccountId, inbox.Item.ServerId);
                Assert.Null (foundItem, "Item should be deleted by the server");

                var foundFolder = McFolder.QueryByServerId<McFolder> (defaultAccountId, inbox.Folder.ServerId);
                Assert.NotNull (foundFolder, "Folder should not be deleted by the server");

                foreach (bool item in statusIndsPosted) { Assert.True (item, "A StatusInd was not set correctly"); }
            }
        }

        [TestFixture]
        public class MoveItemsTests : BaseSyncConfResTest
        {
            // have to make the L&F folder
            [SetUp]
            public new void SetUp ()
            {
                base.SetUp ();
                BackEnd.Instance.CreateServices (defaultAccountId);  // make L&F folder
            }

            private Inbox SetStateWithDestFolder<T> (string code, NcResult.SubKindEnum opSubKind, Func<int, int, string> clientOp) where T : McAbstrItem, new()
            {
                Inbox inbox = new Inbox (() => {
                    return FolderOps.CreateUniqueItem<T> ();
                });

                inbox.CreateDestFolder ();

                SetSyncStrategy (inbox.Folder);

                bool[] statusIndsPosted = {false};
                MockOwner.StatusIndCallback += (result) => {
                    if (result.SubKind == opSubKind) {
                        statusIndsPosted[0] = true;
                    }
                };

                ProtoOps.DoClientSideCmds (Context, () => {
                    inbox.Token = clientOp (inbox.Item.Id, inbox.DestFolder.Id); 
                });

                foreach (bool item in statusIndsPosted) { Assert.True (item, "A StatusInd was not set correctly"); }

                SyncCmd = CreateSyncCmd (Context);

                return inbox;
            }

            [Test]
            public void TestSyncDeleteForMoveCalCmd ()
            {
                TestSyncDelete<McCalendar> (ClassCode.Calendar, NcResult.SubKindEnum.Info_CalendarSetChanged, (itemId, folderServerId) => {
                    return Context.ProtoControl.MoveCalCmd (itemId, folderServerId).GetValue<string> ();
                });
            }

            [Test]
            public void TestSyncDeleteForMoveContactCmd ()
            {
                TestSyncDelete<McContact> (ClassCode.Contacts, NcResult.SubKindEnum.Info_ContactSetChanged, (itemId, folderServerId) => {
                    return Context.ProtoControl.MoveContactCmd (itemId, folderServerId).GetValue<string> ();
                });
            }

            [Test]
            public void TestSyncDeleteForMoveTaskCmd ()
            {
                TestSyncDelete<McTask> (ClassCode.Tasks, NcResult.SubKindEnum.Info_TaskSetChanged, (itemId, folderServerId) => {
                    return Context.ProtoControl.MoveTaskCmd (itemId, folderServerId).GetValue<string> ();
                });
            }

            [Test]
            public void TestSyncDeleteForMoveEmailCmd ()
            {
                TestSyncDelete<McEmailMessage> (ClassCode.Email, NcResult.SubKindEnum.Info_EmailMessageSetChanged, (itemId, folderServerId) => {
                    return Context.ProtoControl.MoveEmailCmd (itemId, folderServerId).GetValue<string> ();
                });
            }

            public void TestSyncDelete<T> (string code, NcResult.SubKindEnum opSubKind, Func<int, int, string> clientOp) where T : McAbstrItem, new()
            {
                var inbox = SetStateWithDestFolder<T> (code, opSubKind, clientOp);

                var itemOpXml = SyncDeleteCmdItemXml (inbox.Item.ServerId, inbox.Folder.ServerId, ClassCode.Calendar);
                ProtoOps.ExecuteConflictTest (SyncCmd, itemOpXml);

                var foundPending = McPending.QueryByToken (defaultAccountId, inbox.Token).FirstOrDefault ();
                Assert.Null (foundPending, "Pending should be deleted by client");

                var laf = McFolder.GetLostAndFoundFolder (defaultAccountId);
                var foundParent = McMapFolderFolderEntry.QueryByFolderId (defaultAccountId, laf.Id);
                Assert.AreEqual (inbox.Item.Id, foundParent.FirstOrDefault ().FolderEntryId, "Item should be moved into L&F");
            }
        }

        [TestFixture]
        public class DeleteItemsTests : BaseSyncConfResTest
        {
            [Test]
            public void TestSyncChangeForAllItems ()
            {
                TestSyncChange<McCalendar> (ClassCode.Calendar, NcResult.SubKindEnum.Info_CalendarSetChanged, (itemId) => {
                    return Context.ProtoControl.DeleteCalCmd (itemId).GetValue<string> ();
                });

                SetUp ();

                TestSyncChange<McContact> (ClassCode.Contacts, NcResult.SubKindEnum.Info_ContactSetChanged, (itemId) => {
                    return Context.ProtoControl.DeleteContactCmd (itemId).GetValue<string> ();
                });

                SetUp ();

                TestSyncChange<McTask> (ClassCode.Tasks, NcResult.SubKindEnum.Info_TaskSetChanged, (itemId) => {
                    return Context.ProtoControl.DeleteTaskCmd (itemId).GetValue<string> ();
                });

                SetUp ();

                TestSyncChange<McEmailMessage> (ClassCode.Email, NcResult.SubKindEnum.Info_EmailMessageSetChanged, (itemId) => {
                    return Context.ProtoControl.DeleteEmailCmd (itemId, true, false).GetValue<string> ();
                });
            }

            private void TestSyncChange<T> (string code, NcResult.SubKindEnum opSubKind, Func<int, string> clientOp) where T : McAbstrItem, new()
            {
                // If the ServerIds match, then delete the command.
                var inbox = SetState<T> (code, opSubKind, clientOp);

                var itemOpXml = SyncUpdateCmdItemXml (inbox.Item.ServerId, inbox.Folder.ServerId, code,
                    new XElement (code + "Subject", "(SERVER)")
                );
                ProtoOps.ExecuteConflictTest (SyncCmd, itemOpXml);

                var foundPending = McPending.QueryByToken (defaultAccountId, inbox.Token).FirstOrDefault ();
                Assert.NotNull (foundPending, "Should not delete pending operation");

                var foundItem = McAbstrItem.QueryByServerId<T> (defaultAccountId, inbox.Item.ServerId);
                Assert.Null (foundItem, "Item should have already been deleted by the client");
            }

            [Test]
            public void TestSyncDeleteForDeleteCalCmd ()
            {
                TestSyncDelete<McCalendar> (ClassCode.Calendar, NcResult.SubKindEnum.Info_CalendarSetChanged, (itemId) => {
                    return Context.ProtoControl.DeleteCalCmd (itemId).GetValue<string> ();
                });
            }

            [Test]
            public void TestSyncDeleteForDeleteContactCmd ()
            {
                TestSyncDelete<McContact> (ClassCode.Contacts, NcResult.SubKindEnum.Info_ContactSetChanged, (itemId) => {
                    return Context.ProtoControl.DeleteContactCmd (itemId).GetValue<string> ();
                });
            }

            [Test]
            public void TestSyncDeleteForDeleteTaskCmd ()
            {
                TestSyncDelete<McTask> (ClassCode.Tasks, NcResult.SubKindEnum.Info_TaskSetChanged, (itemId) => {
                    return Context.ProtoControl.DeleteTaskCmd (itemId).GetValue<string> ();
                });
            }

            [Test]
            public void TestSyncDeleteForDeleteEmailCmd ()
            {
                TestSyncDelete<McEmailMessage> (ClassCode.Email, NcResult.SubKindEnum.Info_EmailMessageSetChanged, (itemId) => {
                    return Context.ProtoControl.DeleteEmailCmd (itemId, true, false).GetValue<string> ();
                });
            }

            private void TestSyncDelete<T> (string code, NcResult.SubKindEnum opSubKind, Func<int, string> clientOp) where T : McAbstrItem, new()
            {
                // If ServerIds match, then the delete has already been done by the client. delete both the command and the pending.
                var inbox = SetState<T> (code, opSubKind, clientOp);

                var itemOpXml = SyncDeleteCmdItemXml (inbox.Item.ServerId, inbox.Folder.ServerId, code);
                ProtoOps.ExecuteConflictTest (SyncCmd, itemOpXml);

                var foundPending = McPending.QueryByToken (defaultAccountId, inbox.Token).FirstOrDefault ();
                Assert.Null (foundPending, "Pending should be deleted because delete has been done by the client");

                var foundItem = McAbstrItem.QueryByServerId<T> (defaultAccountId, inbox.Item.ServerId);
                Assert.Null (foundItem, "Item should have already been deleted by the client");
            }
        }

        [TestFixture]
        public class ChangeItemsTests : BaseSyncConfResTest
        {
            // have to make the L&F folder
            [SetUp]
            public new void SetUp ()
            {
                base.SetUp ();
                BackEnd.Instance.CreateServices (defaultAccountId);  // make L&F folder
            }

            [Test]
            public void TestSyncDeleteForUpdateCalCmd ()
            {
                TestSyncDelete<McCalendar> (ClassCode.Calendar, (itemId) => {
                    return Context.ProtoControl.UpdateCalCmd (itemId, false).GetValue<string> ();
                });
            }

            [Test]
            public void TestSyncDeleteForUpdateContactCmd ()
            {
                TestSyncDelete<McContact> (ClassCode.Contacts, (itemId) => {
                    return Context.ProtoControl.UpdateContactCmd (itemId).GetValue<string> ();
                });
            }

            [Test]
            public void TestSyncDeleteForUpdateTaskCmd ()
            {
                TestSyncDelete<McTask> (ClassCode.Tasks, (itemId) => {
                    return Context.ProtoControl.UpdateTaskCmd (itemId).GetValue<string> ();
                });
            }

            public void TestSyncDelete<T> (string code, Func<int, string> clientOp) where T : McAbstrItem, new()
            {
                // If the ServerIds match, then move the item to lost+found and delete the pending.

                // testing StatusInd does not apply in this case
                var inbox = SetState<T> (code, NcResult.SubKindEnum.NotSpecified, clientOp);

                var itemOpXml = SyncDeleteCmdItemXml (inbox.Item.ServerId, inbox.Folder.ServerId, ClassCode.Calendar);
                ProtoOps.ExecuteConflictTest (SyncCmd, itemOpXml);

                var foundPending = McPending.QueryByToken (defaultAccountId, inbox.Token).FirstOrDefault ();
                Assert.Null (foundPending, "Pending should be deleted by client");

                var laf = McFolder.GetLostAndFoundFolder (defaultAccountId);
                var foundParent = McMapFolderFolderEntry.QueryByFolderId (defaultAccountId, laf.Id);
                Assert.AreEqual (inbox.Item.Id, foundParent.FirstOrDefault ().FolderEntryId, "Item should be moved into L&F");
            }
        }
    }
}

