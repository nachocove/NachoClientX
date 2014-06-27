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
            var root1 = tree1.Root;
            root1.Delete ();

            var root2 = tree2.Root;
            ValidateTree (accountId2, root2, tree2.Children, tree2.GrandChildren);
        }

        // returns the root node
        private McPathTree CreateTree (int accountId)
        {
            List<McPath> firstLayer = new List<McPath> ();
            List<McPath> secondLayer = new List<McPath> ();

            var root = CreatePath (accountId, serverId: "0");

            // create children, each with a different serverId
            int serverId = 1;
            var children = CreateChildPaths (accountId, root, ref serverId);
            firstLayer.AddRange (children);

            foreach (McPath child in children) {
                var grandChildren = CreateChildPaths (accountId, child, ref serverId);
                secondLayer.AddRange (grandChildren);
            }

            ValidateTree (accountId, root, firstLayer, secondLayer);

            return new McPathTree (root, firstLayer, secondLayer);
        }

        private void ValidateTree (int accountId, McPath root, List<McPath> children, List<McPath> grandChildren)
        {
            if (children.Count == 0) {
                return;
            }

            // find root
            var foundRoot = McPath.QueryById<McPath> (root.Id);
            Assert.NotNull (foundRoot, "Root should not have been deleted");

            // find children
            var foundChildren = McPath.QueryByParentId (accountId, root.ServerId);
            Assert.AreEqual (3, foundChildren.ToList ().Count, "Should have correct number of children");
            var sortedChildren = foundChildren.OrderBy (c => c.ServerId).ToList ();

            for (int i = 0; i < sortedChildren.Count; ++i) {
                Assert.AreEqual (children [i].Id, sortedChildren [i].Id, "Children should be inserted correctly");
                if (grandChildren.Count != 0) {
                    ValidateTree (accountId, children [i], grandChildren.GetRange (3 * i, 3), new List<McPath> ());
                }
            }
        }

        private List<McPath> CreateChildPaths (int accountId, McPath path, ref int serverId)
        {
            List<McPath> tree = new List<McPath> ();
            for (int i = 0; i < 3; ++i) {
                var newPath = CreatePath (accountId, parentId: path.ServerId, serverId: serverId.ToString ());
                tree.Add (newPath);
                serverId++;
            }
            return tree;
        }
    }
}
