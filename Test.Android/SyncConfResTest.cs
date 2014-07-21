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
                var protoControl = ProtoOps.CreateProtoControl (accountId: defaultAccountId);

                var server = McServer.Create (CommonMockData.MockUri);
                Context = new MockContext (protoControl, server);
            }

            public static AsSyncCommand CreateSyncCmd (MockContext context)
            {
                var syncCmd = new AsSyncCommand (context);
                syncCmd.HttpClientType = typeof(MockHttpClient);
                syncCmd.DnsQueryRequestType = typeof(MockDnsQueryRequest);
                return syncCmd;
            }

            public void SetSyncStrategy (McFolder folder)
            {
                var strategy = new MockStrategy (folder);
                Context.ProtoControl.SyncStrategy = strategy;
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
            private void SyncGenericOp (TypeCode topFolderType, Func<string, string, string> makeItemOpXml)
            {
                // If the pending's ServerId dominates the command's ServerId, then drop the command.
                var itemServerId = "5";
                var topFolder = ProtoOps.CreateTopFolder (withPath: true, type: topFolderType);

                SetSyncStrategy (topFolder);

                string token = null;
                ProtoOps.DoClientSideCmds (Context, () => {
                    token = Context.ProtoControl.DeleteFolderCmd (topFolder.Id);
                });

                SyncCmd = CreateSyncCmd (Context);

                var itemOpXml = makeItemOpXml (itemServerId, topFolder.ServerId);

                ProtoOps.ExecuteConflictTest (SyncCmd, itemOpXml);

                var foundPending = McPending.QueryByToken (defaultAccountId, token);
                Assert.NotNull (foundPending, "Pending should not be deleted by client");

                var foundFolder = McFolder.QueryByServerId<McFolder> (defaultAccountId, topFolder.ServerId);
                Assert.Null (foundFolder, "Folder should be deleted by the client");

                var foundItem = foundPending.GetItem ();
                Assert.Null (foundItem, "Command to create item should be dropped by the server");
            }

            // create cal, contact, and task
            [Test]
            public void SyncAdd ()
            {
                XNamespace calNs = ClassCode.Calendar;
                SyncGenericOp (TypeCode.DefaultCal_8,
                    (serverId, parentId) => SyncAddItemCmdXml (serverId, parentId, new XElement (calNs + "Subject", "(SERVER)"))
                );

                SetUp ();

                XNamespace contactNs = ClassCode.Contacts;
                SyncGenericOp (TypeCode.DefaultContacts_9,
                    (serverId, parentId) => SyncAddItemCmdXml (serverId, parentId, new XElement (contactNs + "FirstName", "(SERVER)"))
                );

                SetUp ();

                XNamespace taskNs = ClassCode.Tasks;
                SyncGenericOp (TypeCode.DefaultTasks_7,
                    (serverId, parentId) => SyncAddItemCmdXml (serverId, parentId, new XElement (taskNs + "Subject", "(SERVER)"))
                );
            }

            [Test]
            public void SyncChange ()
            {
                var calCode = ClassCode.Calendar;
                XNamespace calNs = calCode;
                SyncGenericOp (TypeCode.DefaultCal_8,
                    (serverId, parentId) => SyncUpdateCmdItemXml (serverId, parentId, calCode, new XElement (calNs + "Subject", "(SERVER)"))
                );

                SetUp ();

                var contactCode = ClassCode.Contacts;
                XNamespace contactNs = contactCode;
                SyncGenericOp (TypeCode.DefaultContacts_9,
                    (serverId, parentId) => SyncUpdateCmdItemXml (serverId, parentId, contactCode, new XElement (contactNs + "FirstName", "(SERVER)"))
                );

                SetUp ();

                var taskCode = ClassCode.Tasks;
                XNamespace taskNs = taskCode;
                SyncGenericOp (TypeCode.DefaultTasks_7,
                    (serverId, parentId) => SyncUpdateCmdItemXml (serverId, parentId, taskCode, new XElement (taskNs + "Subject", "(SERVER)"))
                );
            }

            [Test]
            public void SyncDelete ()
            {
                SyncGenericOp (TypeCode.DefaultCal_8,
                    (serverId, parentId) => {
                        var cal = FolderOps.CreateUniqueItem<McCalendar> (defaultAccountId, serverId: serverId);
                        PathOps.CreatePath (defaultAccountId, serverId: cal.ServerId, parentId: parentId);
                        return SyncDeleteCmdItemXml (serverId, parentId, ClassCode.Calendar);
                    }
                );

                SetUp ();

                SyncGenericOp (TypeCode.DefaultContacts_9,
                    (serverId, parentId) => {
                        var cont = FolderOps.CreateUniqueItem<McContact> (defaultAccountId, serverId: serverId);
                        PathOps.CreatePath (defaultAccountId, serverId: cont.ServerId, parentId: parentId);
                        return SyncDeleteCmdItemXml (serverId, parentId, ClassCode.Contacts);
                    }
                );

                SetUp ();

                SyncGenericOp (TypeCode.DefaultTasks_7,
                    (serverId, parentId) => {
                        var task = FolderOps.CreateUniqueItem<McTask> (defaultAccountId, serverId: serverId);
                        PathOps.CreatePath (defaultAccountId, serverId: task.ServerId, parentId: parentId);
                        return SyncDeleteCmdItemXml (serverId, parentId, ClassCode.Tasks);
                    }
                );

                SetUp ();

                SyncGenericOp (TypeCode.DefaultInbox_2,
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
                var itemServerId = "5";
                var topFolder = ProtoOps.CreateTopFolder (withPath: true, type: TypeCode.DefaultInbox_2);
                var email = FolderOps.CreateUniqueItem<McEmailMessage> (serverId: itemServerId);
                topFolder.Link (email);
                PathOps.CreatePath (defaultAccountId, email.ServerId, topFolder.ServerId);
                var att = FolderOps.CreateAttachment (item: email, displayName: "My-Attachment");

                SetSyncStrategy (topFolder);

                string token = null;
                ProtoOps.DoClientSideCmds (Context, () => {
                    // make pending download
                    token = Context.ProtoControl.DnldAttCmd (att.Id);
                });

                SyncCmd = CreateSyncCmd (Context);

                var itemOpXml = SyncDeleteCmdItemXml (itemServerId, topFolder.ServerId, ClassCode.Email);
                ProtoOps.ExecuteConflictTest (SyncCmd, itemOpXml);

                var foundPending = McPending.QueryByToken (defaultAccountId, token);
                Assert.Null (foundPending, "Pending should be deleted by client");

                var foundItem = McEmailMessage.QueryByServerId<McEmailMessage> (defaultAccountId, email.ServerId);
                Assert.Null (foundItem, "Item should be deleted by the server");

                var foundFolder = McFolder.QueryByServerId<McFolder> (defaultAccountId, topFolder.ServerId);
                Assert.NotNull (foundFolder, "Folder should not be deleted by the server");
            }
        }

        [TestFixture]
        public class MeetingResponseTests : BaseSyncConfResTest
        {
            [Test]
            public void TestSyncDelete ()
            {
                // If the pending's ServerId matches the command's ServerId, then delete the pending MeetingResponse.
                var topFolder = ProtoOps.CreateTopFolder (withPath: true);
                var cal = FolderOps.CreateUniqueItem<McCalendar> ();
                topFolder.Link (cal);
                PathOps.CreatePath (defaultAccountId, cal.ServerId, topFolder.ServerId);
                var response = NcResponseType.Accepted;

                SetSyncStrategy (topFolder);

                string token = null;
                ProtoOps.DoClientSideCmds (Context, () => {
                    token = Context.ProtoControl.RespondCalCmd (cal.Id, response);
                });

                SyncCmd = CreateSyncCmd (Context);

                var itemOpXml = SyncDeleteCmdItemXml (cal.ServerId, topFolder.ServerId, ClassCode.Calendar);
                ProtoOps.ExecuteConflictTest (SyncCmd, itemOpXml);

                var foundPending = McPending.QueryByToken (defaultAccountId, token);
                Assert.Null (foundPending, "Pending should be deleted by client");

                var foundItem = McCalendar.QueryByServerId<McCalendar> (defaultAccountId, cal.ServerId);
                Assert.Null (foundItem, "Item should be deleted by the server");

                var foundFolder = McFolder.QueryByServerId<McFolder> (defaultAccountId, topFolder.ServerId);
                Assert.NotNull (foundFolder, "Folder should not be deleted by the server");
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
                BackEnd.Instance.EstablishService (defaultAccountId);  // make L&F folder
            }

            [Test]
            public void TestSyncDelete ()
            {
                // If the pending's ServerId matches the command's ServerId, then delete the pending MeetingResponse.
                var topFolder = ProtoOps.CreateTopFolder (withPath: true);
                var cal = FolderOps.CreateUniqueItem<McCalendar> ();
                topFolder.Link (cal);
                PathOps.CreatePath (defaultAccountId, cal.ServerId, topFolder.ServerId);

                var destFolder = ProtoOps.CreateDestFolder (withPath: true);

                SetSyncStrategy (topFolder);

                string token = null;
                ProtoOps.DoClientSideCmds (Context, () => {
                    token = Context.ProtoControl.MoveCalCmd (cal.Id, destFolder.Id);
                });

                SyncCmd = CreateSyncCmd (Context);

                var itemOpXml = SyncDeleteCmdItemXml (cal.ServerId, topFolder.ServerId, ClassCode.Calendar);
                ProtoOps.ExecuteConflictTest (SyncCmd, itemOpXml);

                var foundPending = McPending.QueryByToken (defaultAccountId, token);
                Assert.Null (foundPending, "Pending should be deleted by client");

                var laf = McFolder.GetLostAndFoundFolder (defaultAccountId);
                var foundParent = McMapFolderFolderEntry.QueryByFolderId (defaultAccountId, laf.Id);
                Assert.AreEqual (cal.Id, foundParent.FirstOrDefault ().FolderEntryId, "Item should be moved into L&F");
            }
        }

        [TestFixture]
        public class DeleteItemsTests : BaseSyncConfResTest
        {
            private class Inbox
            {
                public McFolder folder;
                public McAbstrItem item;
                public string token;

                public Inbox (Func<McAbstrItem> makeItem)
                {
                    folder = ProtoOps.CreateTopFolder (withPath: true);
                    item = makeItem ();
                    folder.Link (item);
                    PathOps.CreatePath (defaultAccountId, item.ServerId, folder.ServerId);

                    token = null;
                }
            }

            private Inbox SetState<T> (string code, Func<int, string> clientOp) where T : McAbstrItem, new()
            {
                Inbox inbox = new Inbox (() => {
                    return FolderOps.CreateUniqueItem<T> ();
                });

                SetSyncStrategy (inbox.folder);

                ProtoOps.DoClientSideCmds (Context, () => {
                    inbox.token = clientOp (inbox.item.Id); 
                });

                SyncCmd = CreateSyncCmd (Context);

                return inbox;
            }

            [Test]
            public void TestSyncChangeForAllItems ()
            {
                TestSyncChange<McCalendar> (ClassCode.Calendar, (itemId) => {
                    return Context.ProtoControl.DeleteCalCmd (itemId);
                });

                SetUp ();

                TestSyncChange<McContact> (ClassCode.Contacts, (itemId) => {
                    return Context.ProtoControl.DeleteContactCmd (itemId);
                });

                SetUp ();

                TestSyncChange<McTask> (ClassCode.Tasks, (itemId) => {
                    return Context.ProtoControl.DeleteTaskCmd (itemId);
                });

                SetUp ();

                TestSyncChange<McEmailMessage> (ClassCode.Email, (itemId) => {
                    return Context.ProtoControl.DeleteEmailCmd (itemId);
                });
            }

            private void TestSyncChange<T> (string code, Func<int, string> clientOp) where T : McAbstrItem, new()
            {
                // If the ServerIds match, then delete the command.
                var inbox = SetState<T> (code, clientOp);

                var itemOpXml = SyncUpdateCmdItemXml (inbox.item.ServerId, inbox.folder.ServerId, code,
                    new XElement (code + "Subject", "(SERVER)")
                );
                ProtoOps.ExecuteConflictTest (SyncCmd, itemOpXml);

                var foundPending = McPending.QueryByToken (defaultAccountId, inbox.token);
                Assert.NotNull (foundPending, "Should not delete pending operation");

                var foundItem = McAbstrItem.QueryByServerId<T> (defaultAccountId, inbox.item.ServerId);
                Assert.Null (foundItem, "Item should have already been deleted by the client");
            }

            [Test]
            public void TestSyncDeleteForAllItems ()
            {
                TestSyncDelete<McCalendar> (ClassCode.Calendar, (itemId) => {
                    return Context.ProtoControl.DeleteCalCmd (itemId);
                });

                SetUp ();

                TestSyncDelete<McContact> (ClassCode.Contacts, (itemId) => {
                    return Context.ProtoControl.DeleteContactCmd (itemId);
                });

                SetUp ();

                TestSyncDelete<McTask> (ClassCode.Tasks, (itemId) => {
                    return Context.ProtoControl.DeleteTaskCmd (itemId);
                });

                SetUp ();

                TestSyncDelete<McEmailMessage> (ClassCode.Email, (itemId) => {
                    return Context.ProtoControl.DeleteEmailCmd (itemId);
                });
            }

            private void TestSyncDelete<T> (string code, Func<int, string> clientOp) where T : McAbstrItem, new()
            {
                // If ServerIds match, then the delete has already been done by the client. delete both the command and the pending.
                var inbox = SetState<T> (code, clientOp);

                var itemOpXml = SyncDeleteCmdItemXml (inbox.item.ServerId, inbox.folder.ServerId, code);
                ProtoOps.ExecuteConflictTest (SyncCmd, itemOpXml);

                var foundPending = McPending.QueryByToken (defaultAccountId, inbox.token);
                Assert.Null (foundPending, "Pending should be deleted because delete has been done by the client");

                var foundItem = McAbstrItem.QueryByServerId<T> (defaultAccountId, inbox.item.ServerId);
                Assert.Null (foundItem, "Item should have already been deleted by the client");
            }
        }
    }
}

