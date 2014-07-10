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
            public AsFolderSyncCommand FolderCmd;

            [SetUp]
            public new void SetUp ()
            {
                base.SetUp ();
                var protoControl = ProtoOps.CreateProtoControl (accountId: defaultAccountId);

                var server = McServer.Create (CommonMockData.MockUri);
                Context = new MockContext (protoControl, server);

//                FolderCmd = CreateFolderSyncCmd (Context);
            }

            public void SetSyncStrategy (McFolder folder)
            {
                var strategy = new MockStrategy (folder);
                Context.ProtoControl.SyncStrategy = strategy;
            }
        }

        [TestFixture]
        public class FirstSyncTest : BaseSyncConfResTest
        {
            // create cal, contact, and task
            [Test]
            public void TestSyncAddMatch ()
            {
//                string subject = "(UPDATED BY SERVER)";
//                string calId = "15";
//
//                // If pending's ParentId matches the ServerId of the command, then move to lost+found and delete pending.
//                var topFolder = CreateTopFolder (withPath: true, type: TypeCode.DefaultCal_8);
//
//                SetSyncStrategy (topFolder);
//
//                string token = null;
//
//                var syncResponseXml = SyncAddItemCmdXml (calId, topFolder.ServerId, (ns) => {
//                    return new XElement (ns + "Subject", subject);
//                });
//
//                // syncCmd must be created here; must come after setting sync strategy
//                var syncCmd = CreateSyncCmd (Context);
//
//                ExecuteSyncConflictTest (FolderCmd, syncCmd, SyncResponseDeleteTop, syncResponseXml);
//
//                var foundItem = McItem.QueryByServerId<McCalendar> (defaultAccountId, calId);
//                Assert.NotNull (foundItem, "Item should not be deleted");
//                Assert.AreEqual (subject, foundItem.Subject, "Item should have been added by the server");
//
//                var foundFolder = McFolder.QueryByServerId<McFolder> (defaultAccountId, topFolder.ServerId);
//                Assert.NotNull (foundFolder, "Folder should not be deleted");
//                Assert.AreEqual (McFolder.ClientOwned_LostAndFound, foundFolder.ParentId, "Folder should have been moved to lost and found");
//
//                var pendResponseOp = McPending.QueryByToken (defaultAccountId, token);
//                Assert.Null (pendResponseOp, "Pending operation should be deleted by SyncCommand");
            }
        }
    }
}

