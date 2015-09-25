//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;


namespace Test.iOS
{
    public class BaseMcPathTest : PathOps
    {
        [SetUp]
        public new void SetUp ()
        {
            base.SetUp ();
        }

        // returns the root node
        public McPathNode CreateTree (int accountId, uint numSubChildren = 3)
        {
            var root = CreatePath (accountId, serverId: "1", parentId: "0", isFolder: true);
            var node = new McPathNode (root);

            // create children, each with a different serverId
            int serverId = 2;
            CreateChildren (accountId, node, ref serverId, 2);

            ValidateTree (accountId, node);

            return node;
        }

        private void CreateChildren (int accountId, McPathNode parent, ref int serverId, int numLayers)
        {
            if (numLayers == 0) {
                return;
            }

            for (int i = 0; i < 3; ++i) {
                var newPath = CreatePath (accountId, 
                    parentId: parent.Root.ServerId, 
                    serverId: serverId.ToString (),
                    isFolder: (numLayers - 1) > 0
                );
                var newNode = new McPathNode (newPath);
                parent.Children.Add (newNode);
                serverId++;
            }

            foreach (McPathNode child in parent.Children) {
                CreateChildren (accountId, child, ref serverId, numLayers - 1);
            }
        }

        public void ValidateTree (int accountId, McPathNode node, bool childrenExpected = true)
        {
            if (node.Children.Count == 0) {
                return;
            }

            // find root
            var foundRoot = McPath.QueryById<McPath> (node.Root.Id);
            Assert.NotNull (foundRoot, "Root should not have been deleted");
            if (childrenExpected) {
                Assert.IsTrue (foundRoot.IsFolder);
            }
            // find children
            var foundFolders = McPath.QueryByParentId (accountId, node.Root.ServerId, true);
            var foundItems = McPath.QueryByParentId (accountId, node.Root.ServerId, false);
            var foundChildren = foundFolders.ToList ();
            foundChildren.AddRange (foundItems);
            Assert.AreEqual (3, foundChildren.ToList ().Count, "Should have correct number of children");

            var sortedChildren = foundChildren.OrderBy (c => Convert.ToInt32 (c.ServerId)).ToList ();

            for (int i = 0; i < sortedChildren.Count; ++i) {
                // validate or invalidate tree based on whether the object id's match expected
                if (childrenExpected) {
                    Assert.AreEqual (node.Children [i].Root.Id, sortedChildren [i].Id, "Children should be inserted correctly");
                } else {
                    Assert.AreNotEqual (node.Children [i].Root.Id, sortedChildren [i].Id, "This tree should not have matching children");
                }

                ValidateTree (accountId, node.Children [i], childrenExpected: childrenExpected);
            }
        }
    }

    [TestFixture]
    public class McPathTest : BaseMcPathTest
    {
        [Test]
        public void TestDeleteNodes ()
        {
            int accountId1 = 1;
            int accountId2 = 2;

            var tree1 = CreateTree (accountId1);
            var tree2 = CreateTree (accountId2);

            // delete all of the account 1 nodes by deleting the root node
            tree1.Root.Delete ();

            ValidateTree (accountId2, tree2);
        }

        [Test]
        public void TestNodesAreSpecificToAccount ()
        {
            int accountId1 = 1;
            int accountId2 = 2;

            CreateTree (accountId1); // first tree
            var tree2 = CreateTree (accountId2);

            // Verify that you CAN’T find account 2 nodes using QueryByServerId w/account 1.
            ValidateTree (accountId1, tree2, childrenExpected: false);
        }

        [Test]
        public void TestDomination ()
        {
            int accountId1 = 1;
            int accountId2 = 2;

            var tree1 = CreateTree (accountId1);
            var tree2 = CreateTree (accountId2);

            bool topEqualsBottom = McPath.Dominates (accountId1, tree1.Root.ServerId, tree1.Root.ServerId);
            Assert.IsFalse (topEqualsBottom, "Dominates should return false when topId == bottomId");

            TraverseTreeWithOp (tree1, (top, bottom) => {
                bool directDescendent = McPath.Dominates (accountId1, top.Root.ServerId, bottom.Root.ServerId);
                Assert.IsTrue (directDescendent, "Dominates should return true when bottomId is a direct descendant of topId");
            });

            TraverseTreeWithOp (tree1, (top, bottom) => {
                foreach (McPathNode child in bottom.Children) {
                    bool indirectDescendent = McPath.Dominates (accountId1, top.Root.ServerId, child.Root.ServerId);
                    Assert.IsTrue (indirectDescendent, "Dominates should return true when bottomId is an indirect descendant of topId");
                }
            });

            bool topIdFound = McPath.Dominates (accountId1, "500", tree1.Children [0].Root.ServerId);
            Assert.IsFalse (topIdFound, "Dominates should be false when topId is not found");

            bool bottomIdFound = McPath.Dominates (accountId1, tree1.Root.ServerId, "500");
            Assert.IsFalse (bottomIdFound, "Dominates should be false when bottomId is not found");

            tree1.Root.Delete ();
            ValidateTree (accountId2, tree2);
        }

        // traverse the tree, comparing all child and parent elements at each level
        private void TraverseTreeWithOp (McPathNode root, Action<McPathNode, McPathNode> compare)
        {
            var top = root;

            foreach (McPathNode bottom in top.Children) {
                compare (top, bottom);
                TraverseTreeWithOp (bottom, compare);
            }
        }

        [Test]
        public void TestDeleteNonFolderByParentId ()
        {
            int accountId = 99;
            var tree1 = CreateTree (accountId);
            var tree2 = CreateTree (accountId + 1);
            NcModel.Instance.RunInTransaction (() => {
                McPath.DeleteNonFolderByParentId (accountId, tree1.Root.ServerId);
            });
            var folders = McPath.QueryByParentId (accountId, tree1.Root.ServerId, true);
            Assert.AreEqual (3, folders.ToList ().Count);
            var leaves = McPath.QueryByParentId (accountId, tree1.Root.ServerId, false);
            Assert.AreEqual (0, leaves.ToList ().Count);
            // All children of root are folders.
            ValidateTree (accountId, tree1);
            var lowestFolder0 = tree1.Children [0];
            var lowestFolder1 = tree1.Children [1];
            leaves = McPath.QueryByParentId (accountId, lowestFolder0.Root.ServerId, false);
            Assert.AreEqual (3, leaves.ToList ().Count);
            NcModel.Instance.RunInTransaction (() => {
                McPath.DeleteNonFolderByParentId (accountId, lowestFolder0.Root.ServerId);
            });
            leaves = McPath.QueryByParentId (accountId, lowestFolder0.Root.ServerId, false);
            Assert.AreEqual (0, leaves.ToList ().Count);
            ValidateTree (accountId + 1, tree2);
            leaves = McPath.QueryByParentId (accountId, lowestFolder1.Root.ServerId, false);
            Assert.AreEqual (3, leaves.ToList ().Count);
        }
    }

    [TestFixture]
    public class McPathDupTest : BaseMcPathTest
    {
        private static string dupServerId = "dup";
        private static string oldServerId = "old";
        private static string newServerId = "new";
        private static string tmpServerId = "tmp";

        [Test]
        public void TestInsertDup ()
        {
            var first = new McPath () {
                AccountId = 1,
                ServerId = dupServerId,
                ParentId = oldServerId,
            };
            first.Insert ();
            var second = new McPath () {
                AccountId = 1,
                ServerId = dupServerId,
                ParentId = newServerId,
            };
            second.Insert ();
            var found = McPath.QueryByServerId (1, dupServerId);
            Assert.NotNull (found);
            Assert.AreEqual (dupServerId, found.ServerId);
            Assert.AreEqual (newServerId, found.ParentId);
        }

        [Test]
        public void TestUpdateDup ()
        {
            var first = new McPath () {
                AccountId = 1,
                ServerId = dupServerId,
                ParentId = oldServerId,
            };
            first.Insert ();
            var second = new McPath () {
                AccountId = 1,
                ServerId = tmpServerId,
                ParentId = newServerId,
            };
            second.Insert ();
            var found = McPath.QueryByServerId (1, dupServerId);
            Assert.NotNull (found);
            Assert.AreEqual (dupServerId, found.ServerId);
            Assert.AreEqual (oldServerId, found.ParentId);
            found = McPath.QueryByServerId (1, tmpServerId);
            Assert.NotNull (found);
            Assert.AreEqual (tmpServerId, found.ServerId);
            Assert.AreEqual (newServerId, found.ParentId);
            second.ServerId = dupServerId;
            second.Update ();
            found = McPath.QueryByServerId (1, dupServerId);
            Assert.NotNull (found);
            Assert.AreEqual (dupServerId, found.ServerId);
            Assert.AreEqual (newServerId, found.ParentId);
        }

        [Test]
        public void TestQueryDup ()
        {
            var first = new McPath () {
                AccountId = 1,
                ServerId = dupServerId,
                ParentId = oldServerId,
            };
            first.Insert ();
            var found = McPath.QueryByServerId (1, dupServerId);
            Assert.AreEqual (dupServerId, found.ServerId);
            Assert.AreEqual (oldServerId, found.ParentId);
            var second = new McPath () {
                AccountId = 1,
                ServerId = dupServerId,
                ParentId = newServerId,
            };
            // Need to avoid dup-catcher in McPath.Insert().
            NcModel.Instance.Db.Insert (second);
            Assert.True (0 < second.Id);
            var count = NcModel.Instance.Db.Table<McPath> ().Count ();
            Assert.AreEqual (2, count);
            found = McPath.QueryByServerId (1, dupServerId);
            Assert.AreEqual (dupServerId, found.ServerId);
            Assert.AreEqual (oldServerId, found.ParentId);
            count = NcModel.Instance.Db.Table<McPath> ().Count ();
            Assert.AreEqual (1, count);
        }
    }
}
