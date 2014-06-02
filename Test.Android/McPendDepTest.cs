﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.ActiveSync;

namespace Test.Common
{
    public class McPendDepTest : NcTestBase
    {
        [Test]
        public void DepThenIndep ()
        {
            List<int> depPendIdList = new List<int> ();
            var createFolder = new McPending (1) {
                Operation = McPending.Operations.FolderCreate,
                ServerId = "parent",
                ParentId = "0",
                DisplayName = "Folder",
                FolderCreate_Type = Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14,
            };
            createFolder.Insert ();
            for (int iter = 0; iter < 10; ++iter) {
                var pending = new McPending (1) {
                    Operation = McPending.Operations.ContactCreate,
                    ItemId = iter,
                    ParentId = "guid",
                    ClientId = iter.ToString(),
                };
                pending.Insert ();
                pending.MarkPredBlocked (createFolder.Id);
                depPendIdList.Add (pending.Id);
            }
            // Verify all are blocked.
            var suc = McPending.QuerySuccessors (1, createFolder.Id);
            Assert.IsNotNull (suc);
            Assert.True (10 == suc.Count);
            foreach (var pid in depPendIdList) {
                var pending = NcModel.Instance.Db.Get<McPending> (pid);
                Assert.True (pending.State == McPending.StateEnum.PredBlocked);
                var dep = McPending.QueryPredecessors (1, pending.Id);
                Assert.IsNotNull (dep);
                Assert.True (1 == dep.Count);
                var pred = dep.First ();
                Assert.True (createFolder.Id == pred.Id);
            }
            createFolder.UnblockSuccessors ();
            // Verify all aren't blocked.
            suc = McPending.QuerySuccessors (1, createFolder.Id);
            Assert.IsNotNull (suc);
            Assert.True (0 == suc.Count);
            foreach (var pid in depPendIdList) {
                var pending = NcModel.Instance.Db.Get<McPending> (pid);
                Assert.True (pending.State == McPending.StateEnum.Eligible);
                var dep = McPending.QueryPredecessors (1, pending.Id);
                Assert.IsNotNull (dep);
                Assert.True (0 == dep.Count);
            }
        }
    }
}

