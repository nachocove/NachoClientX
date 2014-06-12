//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NUnit.Framework;

namespace Test.Android
{
    public class McPendingTest
    {
        [SetUp]
        public void Setup ()
        {
            NcModel.Instance.Reset (System.IO.Path.GetTempFileName ());
        }

        private McPending CreatePending (int accountId)
        {
            var pending = new McPending (accountId);
            pending.Insert ();
            return pending;
        }

        [Test]
        public void DependencyTest ()
        {
            int accountId = 1;

            var pendA = CreatePending (accountId);
            var succA1 = CreatePending (accountId);
            var succA2 = CreatePending (accountId);
            var succA3 = CreatePending (accountId);

            McPending[] groupA = { pendA, succA1, succA2, succA3 };

            var pendB = CreatePending (accountId);
            var succB1 = CreatePending (accountId);
            var succB2 = CreatePending (accountId);
            var succB3 = CreatePending (accountId);

            McPending[] groupB = { pendB, succB1, succB2, succB3 };

            // start from second item in each group and mark the predecessor of each as blocked
            foreach (McPending[] group in new[] {groupA, groupB}) {
                for (int i = 1; i < group.Length; ++i) {
                    group [i].MarkPredBlocked (group [i - 1].Id);
                }

                for (int i = 0; i < group.Length - 1; ++i) {
                    var pend = McPendDep.QueryByPredId (group [i].Id);
                    Assert.NotNull (pend, "MarkPredBlock should create a new McPendDep object");
                }

                var notPend = McPendDep.QueryByPredId (group [group.Length].Id);
                Assert.IsNull (notPend, "The last item in each list should not be blocking anything");
            }
        }
    }
}

