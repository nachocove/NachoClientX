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

                SyncCmd = CreateSyncCmd (Context);
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

        [TestFixture]
        public class FolderDeleteTests : BaseSyncConfResTest
        {
            // create cal, contact, and task
            [Test]
            public void SyncAdd ()
            {
                // If the pending's ServerId dominates the command's ServerId, then drop the command.
                var itemServerId = "5";
                var topFolder = ProtoOps.CreateTopFolder (withPath: true, type: TypeCode.DefaultInbox_2);

                string addItemXml = SyncAddItemCmdXml (itemServerId, topFolder.ServerId,
                                        (ns) => new XElement (ns + "Subject", "(SERVER)"));

                string token = null;
                ProtoOps.DoClientSideCmds (Context, () => {
                    token = Context.ProtoControl.DeleteFolderCmd (topFolder.Id);
                });

                ProtoOps.ExecuteConflictTest (SyncCmd, addItemXml);

                var foundPending = McPending.QueryByToken (defaultAccountId, token);
                Assert.NotNull (foundPending, "Pending should not be deleted by client");

                var foundFolder = McFolder.QueryByServerId<McFolder> (defaultAccountId, topFolder.ServerId);
                Assert.Null (foundFolder, "Folder should be deleted by the client");

                var foundItem = foundPending.GetItem ();
                Assert.Null (foundItem, "Command to create item should be dropped by the server");
            }

            [Test]
            public void SyncChange ()
            {

            }

            [Test]
            public void SyncDelete ()
            {

            }
        }
    }
}

