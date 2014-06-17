//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.Utils;
using BlockReasonEnum = NachoCore.Model.McPending.BlockReasonEnum;
using ProtoOps = Test.iOS.CommonProtoControlOps;
using StateEnum = NachoCore.Model.McPending.StateEnum;
using WhyEnum = NachoCore.Utils.NcResult.WhyEnum;


namespace Test.iOS
{
    public class BaseMcPendingTest : CommonTestOps
    {
        [SetUp]
        public void SetUp ()
        {
            base.SetUp ();
        }

        public McPending CreatePending (int accountId)
        {
            var pending = new McPending (accountId);
            pending.Insert ();
            return pending;
        }
    }

    public class McPendingTest : BaseMcPendingTest
    {
        /* Dependencies */

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
                    group [i].MarkPredBlocked (group [0].Id);
                }

                var pendDeps = McPendDep.QueryByPredId (group [0].Id);
                Assert.AreEqual (3, pendDeps.Count, "Should create a pendDep for each successor");
                foreach (McPendDep pendDep in pendDeps) {
                    Assert.AreEqual (group[0].Id, pendDep.PredId, "MarkPredBlock should create a new McPendDep object");
                }

                // verify eligible state
                var eligibleItems = McPending.QueryById<McPending> (group [0].Id);
                Assert.AreEqual (StateEnum.Eligible, eligibleItems.State, "Items that are not blocked have state set to eligible");

                // verify blocked state
                for (int i = 1; i < group.Length; ++i) {
                    var item = McPending.QueryById<McPending> (group [i].Id);
                    Assert.AreEqual (StateEnum.PredBlocked, item.State, "Items that are blocked by a pred should have state set to blocked");
                }
            }

            pendA.UnblockSuccessors ();
            for (int i = 1; i < groupA.Length; ++i) {
                var item = McPending.QueryById<McPending> (groupA [i].Id);
                Assert.AreEqual (StateEnum.Eligible, item.State, "Unblocked group A successors should have Eligible state");
            }

            // unblocking successors for group A should not unblock successors for group B
            for (int i = 1; i < groupB.Length; ++i) {
                var item = McPending.QueryById<McPending> (groupB [i].Id);
                Assert.AreEqual (StateEnum.PredBlocked, item.State, "Unblocking successors for grouop A should not unblock successors for group B");
            }

            // McPendDeps for group B should still be intact
            var bPendDeps = McPendDep.QueryByPredId (groupB [0].Id);
            Assert.AreEqual (3, bPendDeps.Count, "PendDeps for each successor in groupB should remain intact");
            foreach (McPendDep pendDep in bPendDeps) {
                Assert.AreEqual (groupB [0].Id, pendDep.PredId, "PendDeps should keep correct id");
            }

            // unblock successors for groupB
            pendB.UnblockSuccessors ();
            for (int i = 1; i < groupB.Length; ++i) {
                var item = McPending.QueryById<McPending> (groupB [i].Id);
                Assert.AreEqual (StateEnum.Eligible, item.State, "Unblocked group B successors should have Eligible state");
            }

            // should not be any remaining deps in DB
            var aPendDeps = McPendDep.QueryByPredId (pendA.Id);
            bPendDeps = McPendDep.QueryByPredId (pendB.Id);
            Assert.AreEqual (0, aPendDeps.Count + bPendDeps.Count, "There should not be any dependencies in DB");

            // verify eligible state in DB
            foreach (McPending[] group in new[] {groupA, groupB}) {
                for (int i = 0; i < group.Length; ++i) {
                    var item = McObject.QueryById<McPending> (group [i].Id);
                    Assert.AreEqual (StateEnum.Eligible, item.State, "Everything should be Eligible");
                }
            }
        }

        /* Dispatched */

        [Test]
        public void DispatchedTest ()
        {
            int accountId = 1;
            var pending = CreatePending (accountId);
            pending.MarkDispached ();
            var retrieved = McObject.QueryById<McPending> (pending.Id);
            Assert.AreEqual (StateEnum.Dispatched, retrieved.State, "MarkDispatched () should set state to Dispatched");
        }

        /* Resolve as user blocked */

        [Test]
        public void ResolveBlockedEligiblePending ()
        {
            // resolve with eligible pending
            int accountId = 1;
            var pending = CreatePending (accountId);
            var protoControl = ProtoOps.CreateProtoControl (accountId);
            TestForNachoExceptionFailure (() => {
                pending.ResolveAsUserBlocked (protoControl, BlockReasonEnum.AdminRemediation, WhyEnum.AccessDeniedOrBlocked);
            }, "Should throw NachoExceptionFailure if ResolveAsUsertBlocked is called on eligible pending");
        }

        [Test]
        public void ResolveBlockedNonErrorResult ()
        {
            // ResolveAsUserBlocked with a non-Error NcResult.
            int accountId = 1;
            var pending = CreatePending (accountId);
            pending.MarkDispached ();
            var protoControl = ProtoOps.CreateProtoControl (accountId);
            TestForNachoExceptionFailure (() => {
                pending.ResolveAsUserBlocked (protoControl, BlockReasonEnum.AdminRemediation, NcResult.OK ());
            }, "Should throw NachoExceptionFailure if ResolveAsUserBlocked is called with a non-error result");
        }

        [Test]
        public void ResolveBlockedPending ()
        {
            // ResolveAsUserBlocked with an error NcResult and non-eligible pending should succeed
            int accountId = 1;
            var pending = CreatePending (accountId);
            pending.Operation = McPending.Operations.FolderCreate;
            pending.MarkDispached ();
            var protoControl = ProtoOps.CreateProtoControl (accountId);

            var whyReason = WhyEnum.AccessDeniedOrBlocked;
            var whyResult = NcResult.Error (NcResult.SubKindEnum.Error_FolderCreateFailed, whyReason);
            pending.ResolveAsUserBlocked (protoControl, BlockReasonEnum.AdminRemediation, whyReason);

            string resultMessage = "Should update status with error result if resolve successful"; 
            Assert.AreEqual (whyResult.Why, MockOwner.Status.Why, resultMessage);
            Assert.AreEqual (whyResult.SubKind, MockOwner.Status.SubKind, resultMessage);

            var retrieved = McPending.QueryById<McPending> (pending.Id);
            Assert.AreEqual (StateEnum.UserBlocked, retrieved.State, "State should be UserBlocked in DB after successful ResolveAsUserBlocked call");
        }
    }
}

