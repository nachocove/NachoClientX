//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.ActiveSync;
using System.Linq;
using BlockReasonEnum = NachoCore.Model.McPending.BlockReasonEnum;
using ProtoOps = Test.iOS.CommonProtoControlOps;
using StateEnum = NachoCore.Model.McPending.StateEnum;
using WhyEnum = NachoCore.Utils.NcResult.WhyEnum;
using Operations = NachoCore.Model.McPending.Operations;
using SubKindEnum = NachoCore.Utils.NcResult.SubKindEnum;
using DeferredEnum = NachoCore.Model.McPending.DeferredEnum;
using System.Collections.Generic;


namespace Test.iOS
{
    public class BaseMcPendingTest : CommonTestOps
    {

        [SetUp]
        public new void SetUp ()
        {
            base.SetUp ();
        }

        public McPending CreatePending (int accountId = defaultAccountId, string serverId = "PhonyServer", Operations operation = Operations.FolderDelete)
        {
            var pending = new McPending (accountId);
            pending.ServerId = serverId;
            pending.Operation = operation;
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
            int firstAccount = 1;
            int secondAccount = 2;

            var pendA = CreatePending (accountId: firstAccount);
            var succA1 = CreatePending (accountId: firstAccount);
            var succA2 = CreatePending (accountId: firstAccount);
            var succA3 = CreatePending (accountId: firstAccount);

            McPending[] groupA = { pendA, succA1, succA2, succA3 };

            var pendB = CreatePending (accountId: secondAccount);
            var succB1 = CreatePending (accountId: secondAccount);
            var succB2 = CreatePending (accountId: secondAccount);
            var succB3 = CreatePending (accountId: secondAccount);

            McPending[] groupB = { pendB, succB1, succB2, succB3 };

            // start from second item in each group and mark the predecessor of each as blocked
            foreach (McPending[] group in new[] {groupA, groupB}) {
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

            for (int i = 1; i < groupA.Length; ++i) {
                groupA [i - 1].UnblockSuccessors (); // unblock successors for groupA
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

            for (int i = 1; i < groupB.Length; ++i) {
                groupB [i - 1].UnblockSuccessors (); // unblock successors for groupB
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

        [Test]
        public void SingleSuccessor ()
        {
            var pending1 = CreatePending (serverId: "FirstPending", operation: Operations.CalCreate);
            var pending2 = CreatePending (serverId: "SecondPending", operation: Operations.CalCreate);

            // Insert a successor McPending that is dependent upon BOTH predecessors.
            var successor = CreatePending (serverId: "Successor", operation: Operations.FolderDelete); // delete always blocks

            // UnblockSuccessors() on the 1st predecessor.
            pending1.UnblockSuccessors ();

            // Verify that the successor is still blocked.
            var retrieved = McPending.QueryById<McPending> (successor.Id);
            Assert.AreEqual (StateEnum.PredBlocked, retrieved.State, "Successor should still be blocked if only one pred has unblocked");

            // Verify that there is only one McPendDep remaining between the second predecessor and the successor.
            var pendDeps = McPendDep.QueryBySuccId (successor.Id);
            Assert.AreEqual (1, pendDeps.Count (), "Should only have one remaining McPendDep");

            // UnblockSuccessors() on the 2nd predecessor.
            pending2.UnblockSuccessors ();

            // Verify that the successor is now marked Eligible.
            retrieved = McPending.QueryById<McPending> (successor.Id);
            Assert.AreEqual (StateEnum.Eligible, retrieved.State, "Unblocking second pred should set state of pending to eligible");

            // Verify that all McPendDep associated with the test case are deleted.
            pendDeps = McPendDep.QueryBySuccId (successor.Id);
            Assert.AreEqual (0, pendDeps.Count (), "All pend deps should have been deleted");
        }

        /* Dispatched */

        [Test]
        public void DispatchedTest ()
        {
            var pending = CreatePending ();
            pending.MarkDispached ();
            var retrieved = McObject.QueryById<McPending> (pending.Id);
            Assert.AreEqual (StateEnum.Dispatched, retrieved.State, "MarkDispatched () should set state to Dispatched");
        }

        /* Resolve as user blocked */

        [Test]
        public void ResolveBlockedEligiblePending ()
        {
            // resolve with eligible pending
            var pending = CreatePending ();
            var protoControl = ProtoOps.CreateProtoControl ();
            TestForNachoExceptionFailure (() => {
                pending.ResolveAsUserBlocked (protoControl, BlockReasonEnum.AdminRemediation, WhyEnum.AccessDeniedOrBlocked);
            }, "Should throw NachoExceptionFailure if ResolveAsUsertBlocked is called on eligible pending");
        }

        [Test]
        public void ResolveBlockedNonErrorResult ()
        {
            // ResolveAsUserBlocked with a non-Error NcResult.
            var pending = CreatePending ();
            pending.MarkDispached ();
            var protoControl = ProtoOps.CreateProtoControl ();
            TestForNachoExceptionFailure (() => {
                pending.ResolveAsUserBlocked (protoControl, BlockReasonEnum.AdminRemediation, NcResult.OK ());
            }, "Should throw NachoExceptionFailure if ResolveAsUserBlocked is called with a non-error result");
        }

        [Test]
        public void ResolveBlockedPending ()
        {
            // ResolveAsUserBlocked with an error NcResult and non-eligible pending should succeed
            var pending = CreatePending ();
            pending.Operation = McPending.Operations.FolderCreate;
            pending.MarkDispached ();
            var protoControl = ProtoOps.CreateProtoControl ();

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
            var pending = CreatePending ();
            var protoControl = ProtoOps.CreateProtoControl ();
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
            var pending = CreatePending ();
            pending.Operation = operation;
            pending.MarkDispached ();

            var protoControl = ProtoOps.CreateProtoControl ();
            doResolve (pending, protoControl);

            Assert.AreEqual (subKind, MockOwner.Status.SubKind, "ResolveAsSuccess should set correct Status for: {0}", operationName);

            var retrieved = McPending.QueryById<McPending> (pending.Id);
            Assert.Null (retrieved, "Should delete pending from DB after it is resolved as success");
        }

        [Test]
        public void ResolveAsSuccessWithSuccessor ()
        {
            // Create an Eligible state McPending and an Eligible McPending successor.
            var pending = CreatePending ();
            pending.Operation = Operations.EmailMarkRead;

            var successor = CreatePending ();
            successor.Operation = Operations.EmailSetFlag;

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
            var protoControl = ProtoOps.CreateProtoControl ();
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
            var pending = CreatePending ();
            var protoControl = ProtoOps.CreateProtoControl ();

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
            var pending = CreatePending ();

            TestForNachoExceptionFailure (() => {
                pending.ResolveAsCancelled ();
            }, "Should throw NachoExceptionFailure when ResolveAsCancelled is called on a non-dispatched pending object");
        }

        [Test]
        public void TestResolveAsCancelled ()
        {
            var pending = CreatePending ();
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
            var pending = CreatePending ();
            var protoControl = ProtoOps.CreateProtoControl ();

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
            var pending = CreatePending ();
            pending.Operation = Operations.FolderCreate;
            pending.MarkDispached ();

            var protoControl = ProtoOps.CreateProtoControl ();
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
            var pending = CreatePending ();
            pending.MarkDispached ();

            var protoControl = ProtoOps.CreateProtoControl ();

            var result = NcResult.OK ();
            TestForNachoExceptionFailure (() => {
                pending.ResolveAsHardFail (protoControl, result);
            }, "Should throw NachoExceptionFailure if ResolveAsHardFail is called with a non-error result");
        }

        [Test]
        public void ResolveAsDeferredNonDispatched ()
        {
            // Test ResolveAsDeferredForce
            var pending1 = CreatePending ();
            TestForNachoExceptionFailure (() => {
                pending1.ResolveAsDeferredForce ();
            }, "Should throw NachoExceptionFailure if ResolveAsDeferredForce is called on a non-dispatched pending object");

            // Test ResolveAsDeferred
            var pending2 = CreatePending ();
            var protoControl = ProtoOps.CreateProtoControl ();

            var reason = DeferredEnum.UntilFSync;
            var result = NcResult.Error ("There was an error");
            TestForNachoExceptionFailure (() => {
                pending2.ResolveAsDeferred (protoControl, reason, result);
            }, "Should throw NachoExceptionFailure if ResolveAsDeferred is called on a non-dispatched pending object");
        }

        [Test]
        public void TestResolveAsDeferredForce ()
        {
            var pending = CreatePending ();
            pending.MarkDispached ();
            pending.ResolveAsDeferredForce ();

            var retrieved = McPending.QueryById<McPending> (pending.Id);
            Assert.AreEqual (StateEnum.Deferred, retrieved.State, "ResolveAsDeferredForce should set state to deferred in DB");
            Assert.AreEqual (DeferredEnum.UntilTime, retrieved.DeferredReason, "Should set deferred reason to until time in DB");
            Assert.IsTrue ((retrieved.DeferredUntilTime - DateTime.UtcNow).Seconds < 10, "Deferred Until Time should be within 10 seconds of UTC now");
        }

        [Test]
        public void TestResolveAsDeferred ()
        {
            var protoControl = ProtoOps.CreateProtoControl ();
            var eligibleAfter = DateTime.UtcNow.AddSeconds (3.0);

            // ResolveAsDeferred (ProtoControl control, DateTime eligibleAfter, NcResult onFail) with eligibleAfter in the future.
            var pending = CreatePending ();
            pending.MarkDispached ();

            var onFail = NcResult.Error (SubKindEnum.Error_AlreadyInFolder);
            pending.ResolveAsDeferred (protoControl, eligibleAfter, onFail);

            ResolvedAssertions (pending.Id, eligibleAfter);

            // Repeat with ResolveAsDeferred (ProtoControl control, DateTime eligibleAfter, NcResult.WhyEnum why).
            var pending2 = CreatePending ();
            pending2.MarkDispached ();

            var why = WhyEnum.AccessDeniedOrBlocked;
            pending2.ResolveAsDeferred (protoControl, eligibleAfter, why);

            ResolvedAssertions (pending2.Id, eligibleAfter);
        }

        private void ResolvedAssertions (int pendId, DateTime eligibleAfter)
        {
            var retrieved = McPending.QueryById<McPending> (pendId);
            // Verify state is Deferred in DB.
            Assert.AreEqual (StateEnum.Deferred, retrieved.State, "Should set state to deferred in DB");

            // Verify reason is UntilTime in DB.
            Assert.AreEqual (DeferredEnum.UntilTime, retrieved.DeferredReason, "Should set deferred reason to until time in DB");

            // Verify UntilTime == eligibleAfter in the DB.
            Assert.AreEqual (eligibleAfter, retrieved.DeferredUntilTime, "Should set UntilTime in DB to eligibleAfter param");
        }

        [Test]
        public void TestMaxDefers ()
        {
            double waitSeconds = 0.5;
            var protoControl = ProtoOps.CreateProtoControl ();
            var eligibleAfter = DateTime.UtcNow.AddSeconds (waitSeconds);
            var subKind = SubKindEnum.Error_AlreadyInFolder; 
            var why = WhyEnum.AccessDeniedOrBlocked;
            var onFail = NcResult.Error (subKind, why);

            var pending = CreatePending ();

            McPending retrieved;
            for (int i = 0; i < McPending.KMaxDeferCount; ++i) {
                pending.MarkDispached ();
                pending.ResolveAsDeferred (protoControl, eligibleAfter, onFail);

                // Verify state is Deferred in DB.
                retrieved = McPending.QueryById<McPending> (pending.Id);
                Assert.AreEqual (eligibleAfter, retrieved.DeferredUntilTime, "Should set UntilTime in DB to eligibleAfter param");
                Assert.AreEqual (StateEnum.Deferred, retrieved.State, "Should set state to deferred");

                System.Threading.Thread.Sleep ((int)(waitSeconds * 1000));
                McPending.MakeEligibleOnTime (defaultAccountId);
                retrieved = McPending.QueryById<McPending> (pending.Id);
                Assert.AreEqual (StateEnum.Eligible, retrieved.State, "MakeEligibleOnTime should set state to eligible in DB");
            }

            pending.MarkDispached ();
            // resolve with UTC now
            pending.ResolveAsDeferred (protoControl, DateTime.UtcNow, onFail);

            retrieved = McPending.QueryById<McPending> (pending.Id);
            Assert.AreEqual (NcResult.KindEnum.Error, retrieved.ResultKind, "Should set result kind to error");
            Assert.AreEqual (subKind, retrieved.ResultSubKind, "Should set subKind correctly");
            Assert.AreEqual (why, retrieved.ResultWhy, "Should set Why correctly");
            // verify failed state in DB
            Assert.AreEqual (StateEnum.Failed, retrieved.State, "Should set state to failed");

            // verify StatusInd
            Assert.AreEqual (onFail.SubKind, MockOwner.Status.SubKind);
        }

        private void TestWithSyncTypeAndReason (DeferredEnum reason, Action<McPending> makeEligible, Func <List<McPending>> querySyncType)
        {
            // Create a 1nd Eligible state McPending.
            var pending = CreatePending ();
            pending.MarkDispached ();
            var protoControl = ProtoOps.CreateProtoControl ();

            // resolve deferred with reason UntilSync
            var subKind = SubKindEnum.Error_AlreadyInFolder; 
            var why = WhyEnum.AccessDeniedOrBlocked;
            var onFail = NcResult.Error (subKind, why);

            pending.ResolveAsDeferred (protoControl, reason, onFail);
            VerifyStateAndReason (pending.Id, StateEnum.Deferred, reason);

            // Create a 2nd Eligible state McPending.
            var secPending = CreatePending ();
            secPending.MarkDispached ();

            // Resolve as deferred with UTC Now
            var eligibleAfter = DateTime.UtcNow.AddSeconds (0.5);
            secPending.ResolveAsDeferred (protoControl, eligibleAfter, onFail);
            VerifyStateAndReason (secPending.Id, StateEnum.Deferred, DeferredEnum.UntilTime);

            // Find only the 1st using QueryDeferredSync (int accountId).
            var firstPend = querySyncType ();
            System.Threading.Thread.Sleep (500);
            var secondPend = McPending.QueryDeferredUntilNow (defaultAccountId);

            Assert.AreEqual (1, firstPend.Count, "Should return a single object");
            Assert.AreEqual (reason, firstPend.FirstOrDefault ().DeferredReason, "Deferred reason (UntilSync) should be set to UntilSync");

            Assert.AreEqual (1, secondPend.Count, "Should only have one UntilTime object in DB");
            Assert.AreEqual (DeferredEnum.UntilTime, secondPend.FirstOrDefault ().DeferredReason, "Deferred reason (UntilTime) should be stored in DB");

            makeEligible (pending);

            // Verify only the 1st is in Eligible state in DB.
            var firstRetr = McPending.QueryById<McPending> (pending.Id);
            Assert.AreEqual (StateEnum.Eligible, firstRetr.State, "Only the 1st should be in the eligible state in DB");

            var secRetr = McPending.QueryById<McPending> (secPending.Id);
            Assert.AreNotEqual (StateEnum.Eligible, secRetr.State);

            // ResolveAsDeferred (ProtoControl control, DeferredEnum reason, NcResult onFail) with reason UntilSync again on the 1st.
            firstRetr.MarkDispached ();
            firstRetr.ResolveAsDeferred (protoControl, reason, onFail);

            McPending.MakeEligibleOnTime (defaultAccountId);

            // Verify only the 2nd is in Eligible state in DB.
            firstRetr = McPending.QueryById<McPending> (pending.Id);
            Assert.AreNotEqual (StateEnum.Eligible, firstRetr.State, "First pending object's state should not be eligible");

            secRetr = McPending.QueryById<McPending> (secPending.Id);
            Assert.AreEqual (StateEnum.Eligible, secRetr.State, "Second object's pending state should be set to eligible");
        }

        [Test]
        public void TestDeferredForOnSync ()
        {
            var reason = DeferredEnum.UntilSync;
            Action<McPending> syncOp = (pend) => McPending.MakeEligibleOnSync (defaultAccountId);
            Func <List<McPending>> querySyncType = () => McPending.QueryDeferredSync (defaultAccountId);
            TestWithSyncTypeAndReason (reason, syncOp, querySyncType);
        }

        [Test]
        public void TestDeferredForFSync ()
        {
            var reason = DeferredEnum.UntilFSync;
            Action<McPending> syncOp = (pend) => McPending.MakeEligibleOnFSync (defaultAccountId);
            Func <List<McPending>> querySyncType = () => McPending.QueryDeferredFSync (defaultAccountId);
            TestWithSyncTypeAndReason (reason, syncOp, querySyncType);
        }

        [Test]
        public void TestDeferredForFSyncThenSync ()
        {
            var reason = DeferredEnum.UntilFSyncThenSync;
            Action<McPending> syncOp = (pend) => {
                McPending.MakeEligibleOnFSync (defaultAccountId);
                var firstPending = McPending.QueryById<McPending> (pend.Id);
                Assert.AreEqual (StateEnum.Deferred, firstPending.State, "State should still be deferred in DB");
                Assert.AreEqual (DeferredEnum.UntilSync, firstPending.DeferredReason, "Deferred reason should have chend to UntilSync");
                McPending.MakeEligibleOnSync (defaultAccountId);
            };
            Func <List<McPending>> querySyncType = () => McPending.QueryDeferredFSync (defaultAccountId);
            TestWithSyncTypeAndReason (reason, syncOp, querySyncType);
        }

        private void VerifyStateAndReason (int pendId, StateEnum state, DeferredEnum reason)
        {
            // Verify state and reason in DB.
            var retrieved = McPending.QueryById<McPending> (pendId);
            Assert.AreEqual (StateEnum.Deferred, retrieved.State, "State should be set correctly in DB");
            Assert.AreEqual (reason, retrieved.DeferredReason, "Should set deferred reason in DB");
        }
    }
}

