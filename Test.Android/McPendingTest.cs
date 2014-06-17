//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.ActiveSync;
using BlockReasonEnum = NachoCore.Model.McPending.BlockReasonEnum;
using ProtoOps = Test.iOS.CommonProtoControlOps;
using StateEnum = NachoCore.Model.McPending.StateEnum;
using WhyEnum = NachoCore.Utils.NcResult.WhyEnum;
using Operations = NachoCore.Model.McPending.Operations;
using SubKindEnum = NachoCore.Utils.NcResult.SubKindEnum;


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

        /* Resolve As Success */
        [Test]
        public void ResolveAsSuccessNotDispatched ()
        {
            int accountId = 1;
            var pending = CreatePending (accountId);
            var protoControl = ProtoOps.CreateProtoControl (accountId);
            pending.Operation = Operations.EmailDelete;
            TestForNachoExceptionFailure (() => {
                pending.ResolveAsSuccess (protoControl);
            }, "Should throw NachoExceptionFailure when attempting to ResolveAsSuccess a pending object that is not dispatched");

            NcResult result = NcResult.Info (SubKindEnum.Error_AlreadyInFolder);
            TestForNachoExceptionFailure (() => {
                pending.ResolveAsSuccess (protoControl, result);
            }, "Should throw NachoExceptionFailure when attempting to ResolveAsSuccess a pending object that is not dispatched");
        }

        [Test]
        public void ResolveAsSuccessOnDispatched ()
        {
            // Operation CalCreate
            TestResolveAsSuccessWithOperation (Operations.CalCreate, SubKindEnum.Info_CalendarCreateSucceeded, "CalCreate", (pending, control) => {
                pending.ResolveAsSuccess (control);
            });

            SetUp (); // refresh database, or else new account creation fails on duplicate account email

            // Operation EmailSend
            TestResolveAsSuccessWithOperation (Operations.EmailSend, SubKindEnum.Info_EmailMessageMarkedReadSucceeded, "Email", (pending, control) => {
                var result = NcResult.Info (SubKindEnum.Info_EmailMessageMarkedReadSucceeded);
                pending.ResolveAsSuccess (control, result);
            });
        }

        private void TestResolveAsSuccessWithOperation (McPending.Operations operation, SubKindEnum subKind, string operationName,
            Action<McPending, AsProtoControl> doResolve)
        {
            int accountId = 1;
            var pending = CreatePending (accountId);
            pending.Operation = operation;
            pending.MarkDispached ();

            var protoControl = ProtoOps.CreateProtoControl (accountId);
            doResolve (pending, protoControl);

            Assert.AreEqual (subKind, MockOwner.Status.SubKind, "ResolveAsSuccess should set correct Status for: {0}", operationName);

            var retrieved = McPending.QueryById<McPending> (pending.Id);
            Assert.Null (retrieved, "Should delete pending from DB after it is resolved as success");
        }

        [Test]
        public void ResolveAsSuccessWithSuccessor ()
        {
            int accountId = 1;

            // Create an Eligible state McPending and an Eligible McPending successor.
            var pending = CreatePending (accountId);
            pending.Operation = Operations.EmailMarkRead;

            var successor = CreatePending (accountId);
            successor.Operation = Operations.EmailSetFlag;

            // Create dependency using MarkPredBlocked().
            successor.MarkPredBlocked (pending.Id);

            // Verify dependencies (McPendDep).
            var pendDeps = McPendDep.QueryByPredId (pending.Id);
            Assert.AreEqual (1, pendDeps.Count, "Sanity check: Should have one pend dep after MarkPredBlocked");

            // Verify Eligible/PredBlocked states in DB.
            var retPending = McPending.QueryById<McPending> (pending.Id);
            Assert.AreEqual (StateEnum.Eligible, retPending.State, "Pending item should have state Eligible");

            var retSuccessor = McPending.QueryById<McPending> (successor.Id);
            Assert.AreEqual (StateEnum.PredBlocked, retSuccessor.State, "Successor should have state PredBlocked");

            // MarkDispatched ()
            pending.MarkDispached ();

            // ResolveAsSuccess (ProtoControl control) on predecessor.
            var protoControl = ProtoOps.CreateProtoControl (accountId);
            pending.ResolveAsSuccess (protoControl);

            // Verify Info_EmailMessageMarkedReadSucceeded StatusInd.
            Assert.AreEqual (SubKindEnum.Info_EmailMessageMarkedReadSucceeded, MockOwner.Status.SubKind, "ResolveAsSuccess on predecessor should set StatusInd correctly");

            // Verify predecessor deleted from DB.
            retPending = McPending.QueryById<McPending> (pending.Id);
            Assert.Null (retPending, "ResolveAsSuccess should delete predecessor from the database");

            // Verify no dependencies in DB (McPendDep).
            pendDeps = McPendDep.QueryByPredId (pending.Id);
            Assert.AreEqual (0, pendDeps.Count, "ResolveAsSuccess should remove dependencies in DB");

            // Verify successor now Eligible in DB.
            retSuccessor = McPending.QueryById<McPending> (successor.Id);
            Assert.AreEqual (StateEnum.Eligible, retSuccessor.State, "ResolveAsSuccess should set successor to Eligible in DB");
        }

        [Test]
        public void ResolveAsSuccessWithBadResult ()
        {
            // ResolveAsSuccess with non-Info NcResult.
            int accountId = 1;
            var pending = CreatePending (accountId);
            var protoControl = ProtoOps.CreateProtoControl (accountId);

            pending.MarkDispached ();

            TestForNachoExceptionFailure (() => {
                var result = NcResult.OK ();
                pending.ResolveAsSuccess (protoControl, result);
            }, "Should throw NachoExceptionFailure if ResolveAsSuccess is called with a non-Info result");
        }

        /* Resolve as Cancelled */
        [Test]
        public void ResolveAsCancelledNotDispatched ()
        {
            int accountId = 1;
            var pending = CreatePending (accountId);

            TestForNachoExceptionFailure (() => {
                pending.ResolveAsCancelled ();
            }, "Should throw NachoExceptionFailure when ResolveAsCancelled is called on a non-dispatched pending object");
        }

        [Test]
        public void TestResolveAsCancelled ()
        {
            int accountId = 1;
            var pending = CreatePending (accountId);
            pending.MarkDispached ();
            var retrievedSanity = McPending.QueryById<McPending> (pending.Id);
            Assert.NotNull (retrievedSanity);

            pending.ResolveAsCancelled ();

            var retrieved = McPending.QueryById<McPending> (pending.Id);
            Assert.Null (retrieved, "Resolved as cancelled should delete pending object fromd DB");
        }

        /* Resolve As Hard Fail */
        [Test]
        public void ResolveAsHardFailNotDispatched ()
        {
            int accountId = 1;
            var pending = CreatePending (accountId);
            var protoControl = ProtoOps.CreateProtoControl (accountId);

            TestForNachoExceptionFailure (() => {
                pending.ResolveAsHardFail (protoControl, WhyEnum.AccessDeniedOrBlocked);
            }, "Should not allow ResolveAsHardFail to be called on non-dispatched methods");
        }

        [Test]
        public void TestResolveAsHardFailForResult ()
        {
            var subKind = SubKindEnum.Error_FolderCreateFailed;
            var why = WhyEnum.AccessDeniedOrBlocked;
            var result = NcResult.Error (subKind, why);

            TestResolveAsHardFail (subKind, why, (pending, protoControl) => {
                pending.ResolveAsHardFail (protoControl, result);
            });
        }

        [Test]
        public void TestResolveAsHardFailForWhy ()
        {
            var subKind = SubKindEnum.Error_FolderCreateFailed;
            var why = WhyEnum.AccessDeniedOrBlocked;

            TestResolveAsHardFail (subKind, why, (pending, protoControl) => {
                pending.ResolveAsHardFail (protoControl, why);
            });
        }

        private void TestResolveAsHardFail (SubKindEnum subKind, WhyEnum why, Action<McPending, AsProtoControl> action)
        {
            int accountId = 1;
            var pending = CreatePending (accountId);
            pending.Operation = Operations.FolderCreate;
            pending.MarkDispached ();

            var protoControl = ProtoOps.CreateProtoControl (accountId);
            action (pending, protoControl);

            // Verify ResultKind, ResultSubKind and ResultWhy stored in DB.
            var retrieved = McPending.QueryById<McPending> (pending.Id);
            Assert.AreEqual (NcResult.KindEnum.Error, retrieved.ResultKind, "ResolveAsHardFail should set Kind correctly in the DB");
            Assert.AreEqual (subKind, retrieved.ResultSubKind, "ResolveAsHardFail should set SubKind correctly in the DB");
            Assert.AreEqual (why, retrieved.ResultWhy, "ResolveAsHardFail should set Why correctly in the DB");

            // Verify failed state in the DB
            Assert.AreEqual (StateEnum.Failed, retrieved.State, "ResolveAsHardFail should set State to Failed");

            Assert.AreEqual (subKind, MockOwner.Status.SubKind, "ResolveAsHardFail should set StatusInd to subKind");
        }

        [Test]
        public void ResolveAsHardFailNonErrorResult ()
        {
            int accountId = 1;
            var pending = CreatePending (accountId);
            pending.MarkDispached ();

            var protoControl = ProtoOps.CreateProtoControl (accountId);

            var result = NcResult.OK ();
            TestForNachoExceptionFailure (() => {
                pending.ResolveAsHardFail (protoControl, result);
            }, "Should throw NachoExceptionFailure if ResolveAsHardFail is called with a non-error result");
        }
    }
}

