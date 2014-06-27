//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;


namespace Test.iOS
{
    public class BaseMcPathTest : CommonTestOps
    {
        [SetUp]
        public new void SetUp ()
        {
            base.SetUp ();
        }

        public McPath CreatePath (int accountId, string serverId = "", string parentId = "")
        {
            var path = new McPath (accountId);
            path.ServerId = serverId;
            path.ParentId = parentId;
            path.Insert ();
            return path;
        }
    }

    public class McPathTree
    {
        public McPathTree (McPath root, List<McPath> children, List<McPath> grandChildren)
        {
            Root = root;
            Children = children;
            GrandChildren = grandChildren;
        }

        public McPath Root;
        public List<McPath> Children;
        public List<McPath> GrandChildren;
    }

    public class McPathNode
    {
        public McPathNode (McPath root)
        {
            Root = root;
            Children = new List<McPathNode> ();
        }
            
        public McPathNode (McPath root, List<McPathNode> children)
        {
            Root = root;
            Children = children;
        }

        public McPath Root;
        public List<McPathNode> Children;
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

            var tree1 = CreateTree (accountId1);
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



            tree1.Root.Delete ();
            ValidateTree (accountId2, tree2);
        }

        // returns the root node
        private McPathNode CreateTree (int accountId, uint numSubChildren = 3)
        {
            var root = CreatePath (accountId, serverId: "0");
            var node = new McPathNode (root);

            // create children, each with a different serverId
            int serverId = 1;
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
                var newPath = CreatePath (accountId, parentId: parent.Root.ServerId, serverId: serverId.ToString ());
                var newNode = new McPathNode (newPath);
                parent.Children.Add (newNode);
                serverId++;
            }

            foreach (McPathNode child in parent.Children) {
                CreateChildren (accountId, child, ref serverId, numLayers - 1);
            }
        }

        private void ValidateTree (int accountId, McPathNode node, bool childrenExpected = true)
        {
            if (node.Children.Count == 0) {
                return;
            }

            // find root
            var foundRoot = McPath.QueryById<McPath> (node.Root.Id);
            Assert.NotNull (foundRoot, "Root should not have been deleted");

            // find children
            var foundChildren = McPath.QueryByParentId (accountId, node.Root.ServerId);
            Assert.AreEqual (3, foundChildren.ToList ().Count, "Should have correct number of children");

            var sortedChildren = foundChildren.OrderBy (c => c.ServerId).ToList ();

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
}
