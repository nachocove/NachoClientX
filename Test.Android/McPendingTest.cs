//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.ActiveSync;
using BlockReasonEnum = NachoCore.Model.McPending.BlockReasonEnum;
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
        public AsProtoControl protoControl;

        [SetUp]
        public new void SetUp ()
        {
            base.SetUp ();
            protoControl = ProtoOps.CreateProtoControl (accountId: defaultAccountId);
        }

        public static McPending CreatePending (int accountId = defaultAccountId, string serverId = "PhonyServer", Operations operation = Operations.FolderDelete,
            McAccount.AccountCapabilityEnum capability = McAccount.AccountCapabilityEnum.EmailReaderWriter,
            string token = "", string clientId = "", string parentId = "", string destId = "", McAbstrItem item = null, StateEnum state = StateEnum.Eligible, bool doNotDelay = false)
        {
            McPending pending;
            if (item != null) {
                pending = new McPending (accountId, capability, item);
            } else {
                pending = new McPending (accountId, capability);
                pending.ServerId = serverId;
            }
            pending.ServerId = serverId;
            pending.Operation = operation;
            pending.Token = token;
            pending.ClientId = clientId;
            pending.ParentId = parentId;
            pending.DestParentId = destId;
            pending.State = state;
            pending.DelayNotAllowed = doNotDelay;
            pending.Insert ();
            return pending;
        }

        public static McPending CreateDeferredPending (AsProtoControl pctrl, DeferredEnum reason, int accountId = defaultAccountId)
        {
            var onFail = NcResult.Error ("There was an error");

            // create pending
            var pending = CreatePending (accountId: accountId);
            pending.MarkDispatched ();
            pending.ResolveAsDeferred (pctrl, reason, onFail);
            return pending;
        }

        public static McPending CreateDeferredWithSeconds (AsProtoControl pctrl, double seconds, int accountId = defaultAccountId)
        {
            var onFail = NcResult.Error ("There was an error");

            var pending = CreatePending ();
            pending.MarkDispatched ();

            // Resolve as deferred with UTC Now
            var eligibleAfter = DateTime.UtcNow.AddSeconds (seconds);
            pending.ResolveAsDeferred (pctrl, eligibleAfter, onFail);
            return pending;
        }

        public static void PendingsAreEqual (McPending pend1, McPending pend2)
        {
            Assert.AreEqual (pend1.State, pend2.State, "Pending objects should have the same State");
            Assert.AreEqual (pend1.DeferredReason, pend2.DeferredReason, "Pending objects should have the same deferred reason");
            Assert.AreEqual (pend1.Id, pend2.Id, "Pending objects should have the same Id");
            Assert.AreEqual (pend1.AccountId, pend2.AccountId, "Pending objects should have the same AccountId");
        }
    }

    public class McPendingTest 
    {
        public class TestDependencies : BaseMcPendingTest
        {
            [SetUp]
            public new void SetUp ()
            {
                base.SetUp ();
                protoControl = ProtoOps.CreateProtoControl (accountId: defaultAccountId);
            }

            [Test]
            public void BasicDependencyTest ()
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
                    groupA [i - 1].UnblockSuccessors (null, StateEnum.Eligible); // unblock successors for groupA
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
                    groupB [i - 1].UnblockSuccessors (null, StateEnum.Eligible); // unblock successors for groupB
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
                        var item = McAbstrObject.QueryById<McPending> (group [i].Id);
                        Assert.AreEqual (StateEnum.Eligible, item.State, "Everything should be Eligible");
                    }
                }
            }

            [Test]
            public void FailedPredecessor ()
            {
                var pending1 = CreatePending (serverId: "Pending1", operation: Operations.CalCreate);
                var successor1 = CreatePending (serverId: "Successor1", operation: Operations.FolderDelete); // delete always blocks
                pending1.UnblockSuccessors (protoControl, StateEnum.Failed);
                var retrieved = McPending.QueryById<McPending> (successor1.Id);
                Assert.NotNull (retrieved);
                Assert.AreEqual (StateEnum.Failed, retrieved.State);
            }

            [Test]
            public void SingleSuccessorDependency ()
            {
                var pending1 = CreatePending (serverId: "FirstPending", operation: Operations.CalCreate);
                var pending2 = CreatePending (serverId: "SecondPending", operation: Operations.CalCreate);

                // Insert a successor McPending that is dependent upon BOTH predecessors.
                var successor = CreatePending (serverId: "Successor", operation: Operations.FolderDelete); // delete always blocks

                // UnblockSuccessors() on the 1st predecessor.
                pending1.UnblockSuccessors (null, StateEnum.Eligible);

                // Verify that the successor is still blocked.
                var retrieved = McPending.QueryById<McPending> (successor.Id);
                Assert.AreEqual (StateEnum.PredBlocked, retrieved.State, "Successor should still be blocked if only one pred has unblocked");

                // Verify that there is only one McPendDep remaining between the second predecessor and the successor.
                var pendDeps = McPendDep.QueryBySuccId (successor.Id);
                Assert.AreEqual (1, pendDeps.Count (), "Should only have one remaining McPendDep");

                // UnblockSuccessors() on the 2nd predecessor.
                pending2.UnblockSuccessors (null, StateEnum.Eligible);

                // Verify that the successor is now marked Eligible.
                retrieved = McPending.QueryById<McPending> (successor.Id);
                Assert.AreEqual (StateEnum.Eligible, retrieved.State, "Unblocking second pred should set state of pending to eligible");

                // Verify that all McPendDep associated with the test case are deleted.
                pendDeps = McPendDep.QueryBySuccId (successor.Id);
                Assert.AreEqual (0, pendDeps.Count (), "All pend deps should have been deleted");
            }

        }

        public class TestDispatched : BaseMcPendingTest
        {
            [Test]
            public void BasicDispatchedTest ()
            {
                var pending = CreatePending ();
                pending.MarkDispatched ();
                var retrieved = McAbstrObject.QueryById<McPending> (pending.Id);
                Assert.AreEqual (StateEnum.Dispatched, retrieved.State, "MarkDispatched () should set state to Dispatched");
            }
        }

        public class TestResolveAsUserBlocked : BaseMcPendingTest
        {

            [Test]
            public void ResolveBlockedEligiblePending ()
            {
                // resolve with eligible pending
                var pending = CreatePending ();
                TestForNachoExceptionFailure (() => {
                    pending.ResolveAsUserBlocked (protoControl, BlockReasonEnum.AdminRemediation, WhyEnum.AccessDeniedOrBlocked);
                }, "Should throw NachoExceptionFailure if ResolveAsUsertBlocked is called on eligible pending");
            }

            [Test]
            public void ResolveBlockedNonErrorResult ()
            {
                // ResolveAsUserBlocked with a non-Error NcResult.
                var pending = CreatePending ();
                pending.MarkDispatched ();
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
                pending.MarkDispatched ();

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

        public class TestResolveAsSuccess : BaseMcPendingTest
        {
            [Test]
            public void ResolveAsSuccessNotDispatched ()
            {
                var pending = CreatePending ();
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
                TestResolveAsSuccessWithOperation (Operations.CalCreate, SubKindEnum.Info_CalendarCreateSucceeded, "CalCreate", (pending) => {
                    pending.ResolveAsSuccess (protoControl);
                });

                SetUp (); // refresh database, or else new account creation fails on duplicate account email

                // Operation EmailSend
                TestResolveAsSuccessWithOperation (Operations.EmailSend, SubKindEnum.Info_EmailMessageMarkedReadSucceeded, "Email", (pending) => {
                    var result = NcResult.Info (SubKindEnum.Info_EmailMessageMarkedReadSucceeded);
                    pending.ResolveAsSuccess (protoControl, result);
                });
            }

            private void TestResolveAsSuccessWithOperation (McPending.Operations operation, SubKindEnum subKind, string operationName,
                Action<McPending> doResolve)
            {
                var pending = CreatePending ();
                pending.Operation = operation;
                pending.MarkDispatched ();

                doResolve (pending);

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
                pending.EmailSetFlag_FlagType = McPending.MarkReadFlag;

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
                pending.MarkDispatched ();

                // ResolveAsSuccess (ProtoControl control) on predecessor.
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

                pending.MarkDispatched ();

                TestForNachoExceptionFailure (() => {
                    var result = NcResult.OK ();
                    pending.ResolveAsSuccess (protoControl, result);
                }, "Should throw NachoExceptionFailure if ResolveAsSuccess is called with a non-Info result");
            }
        }

        public class TestDelayNotAllowed : BaseMcPendingTest
        {
            [Test]
            public void ResolveAllDoNotDelayAsFailed ()
            {
                // rejected because DoNotDelay is false.
                var pending1r = CreatePending (accountId:5, operation:Operations.EmailBodyDownload, serverId:"1:1");
                var pending2 = CreatePending (accountId:5, operation:Operations.EmailBodyDownload, serverId:"1:2");
                // rejected because accountId is 6.
                var pending3r = CreatePending (accountId:6, operation:Operations.EmailBodyDownload, serverId:"1:3");
                // rejected because state is Failed.
                var pending4r = CreatePending (accountId:5, operation:Operations.EmailBodyDownload, serverId:"1:4");
                pending2 = pending2.UpdateWithOCApply<McPending> ((record) => {
                    var target = (McPending)record;
                    target.DelayNotAllowed = true;
                    return true;
                });
                pending3r = pending3r.UpdateWithOCApply<McPending> ((record) => {
                    var target = (McPending)record;
                    target.DelayNotAllowed = true;
                    return true;
                });
                pending4r = pending4r.UpdateWithOCApply<McPending> ((record) => {
                    var target = (McPending)record;
                    target.State = StateEnum.Failed;
                    return true;
                });
                McPending.ResolveAllDelayNotAllowedAsFailed (protoControl, 5);
                var search = McPending.QueryById<McPending> (pending1r.Id);
                Assert.True (null != search && pending1r.Id == search.Id);
                Assert.True (StateEnum.Eligible == search.State);
                search = McPending.QueryById<McPending> (pending2.Id);
                Assert.True (null != search && pending2.Id == search.Id);
                Assert.True (StateEnum.Failed == search.State);
                search = McPending.QueryById<McPending> (pending3r.Id);
                Assert.True (null != search && pending3r.Id == search.Id);
                Assert.True (StateEnum.Eligible == search.State);
                search = McPending.QueryById<McPending> (pending4r.Id);
                Assert.True (null != search && pending4r.Id == search.Id);
                Assert.True (StateEnum.Failed == search.State);
            }
        }

        public class TestResolveAsCancelled : BaseMcPendingTest
        {
            [Test]
            public void ResolveAsCancelledNotDispatched ()
            {
                var pending = CreatePending ();

                TestForNachoExceptionFailure (() => {
                    pending.ResolveAsCancelled ();
                }, "Should throw NachoExceptionFailure when ResolveAsCancelled is called on a non-dispatched pending object");
            }

            [Test]
            public void ResolveAsCancelledNotDispatchedOk ()
            {
                var pending = CreatePending ();
                var id = pending.Id;
                var pendingr = McPending.QueryById<McPending> (id);
                Assert.NotNull (pendingr);
                pending.ResolveAsCancelled (false);
                pendingr = McPending.QueryById<McPending> (id);
                Assert.Null (pendingr);
            }

            [Test]
            public void BasicResolveAsCancelledTest ()
            {
                var pending = CreatePending ();
                pending.MarkDispatched ();
                var retrievedSanity = McPending.QueryById<McPending> (pending.Id);
                Assert.NotNull (retrievedSanity);

                pending.ResolveAsCancelled ();

                var retrieved = McPending.QueryById<McPending> (pending.Id);
                Assert.Null (retrieved, "Resolved as cancelled should delete pending object fromd DB");
            }
        }

        public class TestResolveAsHardFail : BaseMcPendingTest
        {
            [Test]
            public void TestResolveAsHardFailForResult ()
            {
                var subKind = SubKindEnum.Error_FolderCreateFailed;
                var why = WhyEnum.AccessDeniedOrBlocked;
                var result = NcResult.Error (subKind, why);

                TestBasicResolveAsHardFail (subKind, why, (pending) => {
                    pending.ResolveAsHardFail (protoControl, result);
                });
            }

            [Test]
            public void TestResolveAsHardFailForWhy ()
            {
                var subKind = SubKindEnum.Error_FolderCreateFailed;
                var why = WhyEnum.AccessDeniedOrBlocked;

                TestBasicResolveAsHardFail (subKind, why, (pending) => {
                    pending.ResolveAsHardFail (protoControl, why);
                });
            }

            private void TestBasicResolveAsHardFail (SubKindEnum subKind, WhyEnum why, Action<McPending> action)
            {
                var pending = CreatePending ();
                pending.Operation = Operations.FolderCreate;
                pending.MarkDispatched ();

                action (pending);

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
                pending.MarkDispatched ();

                var result = NcResult.OK ();
                TestForNachoExceptionFailure (() => {
                    pending.ResolveAsHardFail (protoControl, result);
                }, "Should throw NachoExceptionFailure if ResolveAsHardFail is called with a non-error result");
            }
        }

        public class TestResolveAsDeferred : BaseMcPendingTest
        {
            McFolder TestFolder;

            [SetUp]
            public new void SetUp ()
            {
                base.SetUp ();
                TestFolder = McFolder.Create (defaultAccountId, true, false, false, McFolder.AsRootServerId, "TestFoo", "TestFoo", Xml.FolderHierarchy.TypeCode.UserCreatedMail_12);
                TestFolder.Insert ();
            }

            [TearDown]
            public void TearDown ()
            {
                if (null != TestFolder) {
                    TestFolder.Delete ();
                }
            }

            [Test]
            public void ResolveAsDeferredNonDispatched ()
            {
                // Test ResolveAsDeferredForce
                var pending1 = CreatePending ();
                TestForNachoExceptionFailure (() => {
                    pending1.ResolveAsDeferredForce (protoControl);
                }, "Should throw NachoExceptionFailure if ResolveAsDeferredForce is called on a non-dispatched pending object");

                // Test ResolveAsDeferred
                var pending2 = CreatePending ();

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
                pending.MarkDispatched ();
                pending.ResolveAsDeferredForce (protoControl);

                var retrieved = McPending.QueryById<McPending> (pending.Id);
                Assert.AreEqual (StateEnum.Deferred, retrieved.State, "ResolveAsDeferredForce should set state to deferred in DB");
                Assert.AreEqual (DeferredEnum.UntilTime, retrieved.DeferredReason, "Should set deferred reason to until time in DB");
                Assert.IsTrue ((retrieved.DeferredUntilTime - DateTime.UtcNow).Seconds < 10, "Deferred Until Time should be within 10 seconds of UTC now");
            }

            [Test]
            public void BasicResolveAsDeferredTest ()
            {
                var eligibleAfter = DateTime.UtcNow.AddSeconds (0.5);

                // ResolveAsDeferred (ProtoControl control, DateTime eligibleAfter, NcResult onFail) with eligibleAfter in the future.
                var pending = CreatePending ();
                pending.MarkDispatched ();

                var onFail = NcResult.Error (SubKindEnum.Error_AlreadyInFolder);
                pending.ResolveAsDeferred (protoControl, eligibleAfter, onFail);

                ResolvedAssertions (pending.Id, eligibleAfter);

                // Repeat with ResolveAsDeferred (ProtoControl control, DateTime eligibleAfter, NcResult.WhyEnum why).
                var pending2 = CreatePending ();
                pending2.MarkDispatched ();

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
                var eligibleAfter = DateTime.UtcNow.AddSeconds (waitSeconds);
                var subKind = SubKindEnum.Error_AlreadyInFolder; 
                var why = WhyEnum.AccessDeniedOrBlocked;
                var onFail = NcResult.Error (subKind, why);

                var pending = CreatePending ();

                McPending retrieved;
                for (int i = 0; i < McPending.KMaxDeferCount; ++i) {
                    pending.MarkDispatched ();
                    pending.ResolveAsDeferred (protoControl, eligibleAfter, onFail);

                    // Verify state is Deferred in DB.
                    retrieved = McPending.QueryById<McPending> (pending.Id);
                    Assert.AreEqual (eligibleAfter, retrieved.DeferredUntilTime, "Should set UntilTime in DB to eligibleAfter param");
                    Assert.AreEqual (StateEnum.Deferred, retrieved.State, "Should set state to deferred");

                    System.Threading.Thread.Sleep ((int)(waitSeconds * 1000));
                    McPending.MakeEligibleOnTime ();
                    retrieved = McPending.QueryById<McPending> (pending.Id);
                    Assert.AreEqual (StateEnum.Eligible, retrieved.State, "MakeEligibleOnTime should set state to eligible in DB");
                }

                pending.MarkDispatched ();
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
                double waitTime = 0.5;

                // Create a 1nd Eligible state McPending and resolve deferred with reason UntilSync
                var pending = CreateDeferredPending (protoControl, reason);
                VerifyStateAndReason (pending.Id, StateEnum.Deferred, reason);

                // Create a 2nd Eligible state McPending and resolve as deferred with UTC Now
                var secPending = CreateDeferredWithSeconds (protoControl, waitTime);
                VerifyStateAndReason (secPending.Id, StateEnum.Deferred, DeferredEnum.UntilTime);

                // Find only the 1st using QueryDeferredSync (int accountId).
                var firstPend = querySyncType ();
                System.Threading.Thread.Sleep ((int)(waitTime * 1000));
                var secondPend = McPending.QueryDeferredUntilNow ();

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
                var onFail = NcResult.Error ("There was an error");
                firstRetr.MarkDispatched ();
                firstRetr.ResolveAsDeferred (protoControl, reason, onFail);

                McPending.MakeEligibleOnTime ();

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

            [Test]
            public void TestDeferredFMetaData ()
            {
                var reason = DeferredEnum.UntilFMetaData;
                // Create a 1nd Eligible state McPending and resolve deferred with reason UntilSync
                var pending = CreateDeferredPending (protoControl, reason);
                pending = pending.UpdateWithOCApply<McPending> ((record) => {
                    var target = (McPending)record;
                    target.ServerId = TestFolder.ServerId;
                    return true;
                });
                VerifyStateAndReason (pending.Id, StateEnum.Deferred, reason);

                // Find only the 1st using QueryDeferredSync (int accountId).
                var firstPend = McPending.QueryDeferredFMetaData (TestFolder);
                Assert.AreEqual (1, firstPend.Count, "Should return a single object");
                Assert.AreEqual (reason, firstPend.FirstOrDefault ().DeferredReason, "Deferred reason should be set to UntilFMetaData");

                McPending.MakeEligibleOnFMetaData (TestFolder);

                // Verify only the 1st is in Eligible state in DB.
                var firstRetr = McPending.QueryById<McPending> (pending.Id);
                Assert.AreEqual (StateEnum.Eligible, firstRetr.State, "should be in the eligible state in DB");
            }

            private void VerifyStateAndReason (int pendId, StateEnum state, DeferredEnum reason)
            {
                // Verify state and reason in DB.
                var retrieved = McPending.QueryById<McPending> (pendId);
                Assert.AreEqual (StateEnum.Deferred, retrieved.State, "State should be set correctly in DB");
                Assert.AreEqual (reason, retrieved.DeferredReason, "Should set deferred reason in DB");
            }
        }

        public class TestQuery : BaseMcPendingTest
        {
            [Test]
            public void TestBasicMcPendingQuery ()
            {
                var pending = CreatePending (accountId: 1);
                CreatePending (accountId: 2); // other pending
                var retrieved = McPending.Query (1);
                Assert.AreEqual (1, retrieved.Count, "Should retrieve pending from correct account");
                PendingsAreEqual (pending, retrieved.FirstOrDefault ());
            }

            [Test]
            public void TestQueryNonFailedNonDeleted ()
            {
                CreatePending (accountId: 1);
                var matcher = CreatePending (accountId: 2); // other pending
                var isDel = CreatePending (accountId: 2);
                isDel = isDel.UpdateWithOCApply<McPending> ((record) => {
                    var target = (McPending)record;
                    target.State = McPending.StateEnum.Deleted;
                    return true;
                });
                var isFailed = CreatePending (accountId: 2);
                isFailed = isFailed.UpdateWithOCApply<McPending> ((record) => {
                    var target = (McPending)record;
                    target.State = McPending.StateEnum.Failed;
                    return true;
                });
                var retrieved = McPending.QueryNonFailedNonDeleted (2);
                Assert.AreEqual (1, retrieved.Count, "Should retrieve pending from correct account");
                PendingsAreEqual (matcher, retrieved.FirstOrDefault ());
            }

            [Test]
            public void TestQueryEligible ()
            {
                var pendElig = CreatePending ();
                CreatePending (); // pending but not eligible
                CreatePending (accountId: 5); // pending in other account
                var retrieved = McPending.QueryEligible (defaultAccountId, McAccount.ActiveSyncCapabilities);
                PendingsAreEqual (pendElig, retrieved.FirstOrDefault ());
            }

            [Test]
            public void TestQueryAllNonDispatchedNonFailedDoNotDelay ()
            {
                CreatePending (operation: Operations.TaskCreate, doNotDelay: true, state: StateEnum.Dispatched); // dispatched, otherwise matching.
                CreatePending (operation: Operations.TaskCreate, doNotDelay: true, accountId: 5); // in other account, otherwise matching.
                CreatePending (); // do not delay false, otherwise matching.
                CreatePending (operation: Operations.TaskCreate, doNotDelay: true, capability: McAccount.AccountCapabilityEnum.EmailSender); // other capability, otherwise matching.
                CreatePending (operation: Operations.TaskCreate, doNotDelay: true, state: StateEnum.Failed); // failed, otherwise matching.
                CreatePending (operation: Operations.TaskCreate, doNotDelay: true, state: StateEnum.Deleted); // deleted, otherwise matching.
                var gonner = CreatePending (operation: Operations.TaskCreate, doNotDelay: true); // matching.
                var retrieved = McPending.QueryAllNonDispatchedNonFailedDoNotDelay (gonner.AccountId, McAccount.ImapCapabilities).ToList ();
                Assert.AreEqual (1, retrieved.Count);
                Assert.AreEqual (gonner.Id, retrieved.First ().Id);
            }

            [Test]
            public void TestQueryPredecessors ()
            {
                CreatePending (); // first
                CreatePending (); // second
                var third = CreatePending ();
                CreatePending (accountId: 5); // pending object in another account
                var retrieved = McPending.QueryPredecessors (defaultAccountId, third.Id);
                Assert.AreEqual (2, retrieved.Count);
            }

            [Test]
            public void TestQuerySuccessors ()
            {
                var first = CreatePending ();
                CreatePending (); // second
                CreatePending (); // third
                var retrieved = McPending.QuerySuccessors (first.Id);
                Assert.AreEqual (2, retrieved.Count);
            }

            [Test]
            public void TestQueryDeferredFSync ()
            {
                var fsyncReason = DeferredEnum.UntilFSync;
                var fsyncsyncReason = DeferredEnum.UntilFSyncThenSync;

                CreateDeferredPending (protoControl, fsyncReason); // first
                CreateDeferredPending (protoControl, fsyncsyncReason); // second
                var third = CreateDeferredPending (protoControl, fsyncReason, accountId: 5);

                var retrieved = McPending.QueryDeferredFSync (defaultAccountId);
                Assert.AreEqual (2, retrieved.Count, "Should only retrieve folders with correct accountId");
                for (int i = 0; i < retrieved.Count; ++i) {
                    Assert.AreNotEqual (third.Id, retrieved [i].Id);
                }
            }

            [Test]
            public void TestQueryDeferredSync ()
            {
                var pending = CreateDeferredPending (protoControl, DeferredEnum.UntilSync);
                CreateDeferredPending (protoControl, DeferredEnum.UntilSync, accountId: 5);
                var retrieved = McPending.QueryDeferredSync (defaultAccountId);
                Assert.AreEqual (1, retrieved.Count, "Query should only return object in the specified account");
                PendingsAreEqual (pending, retrieved.FirstOrDefault ());
            }

            [Test]
            public void TestQueryDeferredUntilNow ()
            {
                double waitSeconds = 0.5;

                CreateDeferredWithSeconds (protoControl, waitSeconds); // pendFuture
                var pendPast = CreateDeferredWithSeconds (protoControl, -waitSeconds);
                CreateDeferredWithSeconds (protoControl, waitSeconds, accountId: 5); // pendOtherAccount 

                var retrieved = McPending.QueryDeferredUntilNow ();
                Assert.AreEqual (1, retrieved.Count);
                PendingsAreEqual (pendPast, retrieved.FirstOrDefault ());
            }

            [Test]
            public void TestQueryByToken ()
            {
                string token = "5";
                string other = "10";
                TestQueryBySomeValue (token, other, () => McPending.QueryByToken (defaultAccountId, token).FirstOrDefault ());
            }

            [Test]
            public void TestQueryByClientId ()
            {
                string clientId = "5";
                string other = "10";
                TestQueryBySomeValue (clientId, other, () => McPending.QueryByClientId (defaultAccountId, clientId));
            }

            [Test]
            public void TestQueryByServerId ()
            {
                string serverId = "5";
                string other = "10";
                TestQueryBySomeValue (serverId, other, () => McPending.QueryByServerId (defaultAccountId, serverId).FirstOrDefault ());
            }

            // this function executes queries that return a single object
            private void TestQueryBySomeValue (string firstVal, string secVal, Func<McPending> doQuery)
            {
                var firstPending = CreatePending (serverId: firstVal, token: firstVal, clientId: firstVal);
                CreatePending (serverId: secVal, token: secVal, clientId: secVal);
                CreatePending (accountId: 5, serverId: firstVal, token: firstVal, clientId: firstVal);

                var retrieved = doQuery ();

                Assert.NotNull (retrieved);
                PendingsAreEqual (firstPending, retrieved);
            }

            [Test]
            public void TestQueryByOperation ()
            {
                Operations operation = Operations.CalUpdate;
                TestQueryBySomeValueForList (() => McPending.QueryByOperation (defaultAccountId, operation), operation: operation);
            }

            [Test]
            public void TestQueryEligiblebyServerId ()
            {
                string serverId = "1";
                string otherVal = "2";
                TestQueryBySomeValueForList (() => McPending.QueryEligibleByFolderServerId (defaultAccountId, serverId), serverId, otherVal);
            }

            // this function executes queries that return lists
            private void TestQueryBySomeValueForList (Func<List<McPending>> doQuery, string firstVal = "1", string secVal = "2",
                Operations operation = Operations.CalCreate)
            {
                var firstPending = CreatePending (operation: operation, parentId: firstVal, serverId: "1");
                CreatePending (operation: operation, parentId: secVal, serverId: "2");
                CreatePending (accountId: 5, operation: operation, parentId: firstVal, serverId: "3");

                var retrieved = doQuery ();

                PendingsAreEqual (firstPending, retrieved.FirstOrDefault ());
            }

            [Test]
            public void TestQueryFirstEligibleByOperation ()
            {
                var firstPend = CreatePending (operation: Operations.CalUpdate, capability: McAccount.AccountCapabilityEnum.CalWriter, serverId:"a");
                CreatePending (operation: Operations.CalUpdate, capability: McAccount.AccountCapabilityEnum.CalWriter, serverId:"b"); // second pending object
                CreatePending (accountId:3, operation: Operations.CalUpdate, capability: McAccount.AccountCapabilityEnum.CalWriter, serverId:"c"); // pending object in another account

                var retrieved = McPending.QueryFirstEligibleByOperation (defaultAccountId, Operations.CalUpdate);
                PendingsAreEqual (firstPend, retrieved);
            }

            [Test]
            public void TestQueryFirstEligibleByOperation2 ()
            {
                var found = new List<McPending> ();
                found.Add (CreatePending (operation: Operations.EmailDelete, capability: McAccount.AccountCapabilityEnum.EmailReaderWriter, serverId:"a"));
                found.Add (CreatePending (operation: Operations.CalDelete, capability: McAccount.AccountCapabilityEnum.CalWriter, serverId:"b"));
                found.Add (CreatePending (operation: Operations.ContactDelete,capability:  McAccount.AccountCapabilityEnum.ContactWriter, serverId:"c")); 
                found.Add (CreatePending (operation: Operations.TaskDelete, capability: McAccount.AccountCapabilityEnum.TaskWriter, serverId:"d")); 
                CreatePending (accountId:3, operation: Operations.CalDelete, capability: McAccount.AccountCapabilityEnum.CalWriter, serverId:"e"); // pending object in another account
                CreatePending (operation: Operations.TaskDelete, capability: McAccount.AccountCapabilityEnum.TaskWriter, serverId:"f"); // excluded by limit.
                CreatePending (operation: Operations.CalUpdate, capability: McAccount.AccountCapabilityEnum.CalWriter, serverId:"g"); // excluded by op.

                var retrieved = McPending.QueryFirstEligibleByOperation (defaultAccountId, 
                                    Operations.EmailDelete, Operations.CalDelete, Operations.ContactDelete, Operations.TaskDelete,
                                    4);
                Assert.AreEqual (4, retrieved.Count);
                foreach (var pending in retrieved) {
                    var match = found.Where (x => x.Operation == pending.Operation).FirstOrDefault ();
                    Assert.IsNotNull (match);
                    found.Remove (match);
                }
                Assert.AreEqual (0, found.Count);
            }

            [Test]
            public void TestQueryItemUsingServerId ()
            {
                string serverId = "3";

                Action<McAbstrItem, Operations> testQuery = (item, op) => {
                    var itemPend = CreatePending (serverId: serverId, operation: Operations.EmailMove, item: item);
                    var foundItem = itemPend.QueryItemUsingServerId ();
                    FolderOps.ItemsAreEqual (item, foundItem);
                };

                var email = FolderOps.CreateUniqueItem<McEmailMessage> (serverId: serverId);
                testQuery (email, Operations.EmailMove);

                var cal = FolderOps.CreateUniqueItem<McCalendar> (serverId: serverId);
                testQuery (cal, Operations.CalMove);

                var contact = FolderOps.CreateUniqueItem<McContact> (serverId: serverId);
                testQuery (contact, Operations.ContactMove);

                var task = FolderOps.CreateUniqueItem<McTask> (serverId: serverId);
                testQuery (task, Operations.TaskMove);

                var badPend = CreatePending (serverId: serverId, operation: Operations.AttachmentDownload);
                var noItem = badPend.QueryItemUsingServerId ();
                Assert.Null (noItem, "Should return null if a non-move operation is queried");
            }

            [Test]
            public void TestQueryByEmailMessageId ()
            {
                var email1 = FolderOps.CreateUniqueItem<McEmailMessage> (serverId: "e1");
                var cal = FolderOps.CreateUniqueItem<McCalendar> (serverId: "c1");
                var email2 = FolderOps.CreateUniqueItem<McEmailMessage> (accountId: 3, serverId: "e2");
                var pend1 = CreatePending (operation: Operations.EmailSend, capability: McAccount.AccountCapabilityEnum.EmailSender, item: email1, serverId: "e1");
                CreatePending (operation: Operations.EmailBodyDownload, item: email1);
                CreatePending (operation: Operations.EmailSend, item: email2, serverId: "e2", accountId: 3);
                CreatePending (operation: Operations.CalDelete, capability: McAccount.AccountCapabilityEnum.CalWriter, item: cal);

                var found = McPending.QueryByEmailMessageId (email1.AccountId, email1.Id);
                PendingsAreEqual (found, pend1);
                pend1 = pend1.UpdateWithOCApply<McPending> ((record) => {
                    var target = (McPending)record;
                    target.Operation = Operations.EmailForward;
                    return true;
                });
                found = McPending.QueryByEmailMessageId (email1.AccountId, email1.Id);
                PendingsAreEqual (found, pend1);
                pend1 = pend1.UpdateWithOCApply<McPending> ((record) => {
                    var target = (McPending)record;
                    target.Operation = Operations.EmailReply;
                    return true;
                });
                found = McPending.QueryByEmailMessageId (email1.AccountId, email1.Id);
                PendingsAreEqual (found, pend1);
            }
        }

        public class TestDependencyCreation : BaseMcPendingTest
        {
            private void TestItemCreateDep (Operations op)
            {
                var serverId = "1";
                var blocker = CreatePending (operation: Operations.FolderCreate, serverId: serverId);
                var secondPend = CreatePending (operation: op, parentId: blocker.ServerId); // gets blocked

                var retrieved = McPending.QueryById<McPending> (secondPend.Id);
                PendingsAreEqual (secondPend, retrieved);
                Assert.AreEqual (StateEnum.PredBlocked, retrieved.State, "Creating a folder whose parent is still awaiting creation should set folder state to PredBlocked");

                var otherServerId = "2";
                var thirdPend = CreatePending (operation: op, serverId: otherServerId);

                retrieved = McPending.QueryById<McPending> (thirdPend.Id);
                Assert.AreEqual (StateEnum.Eligible, retrieved.State, "State should not be PredBlocked if second folder added has a unique serverId");
            }

            [Test]
            public void TestFolderCreate ()
            {
                var op = Operations.FolderCreate;
                TestItemCreateDep (op);
            }

            [Test]
            public void TestCalCreate ()
            {
                var op = Operations.CalCreate;
                TestItemCreateDep (op);
            }

            [Test]
            public void TestContactCreate ()
            {
                var op = Operations.ContactCreate;
                TestItemCreateDep (op);
            }

            [Test]
            public void TestTaskCreate ()
            {
                var op = Operations.TaskCreate;
                TestItemCreateDep (op);
            }

            [Test]
            public void TestFolderDeleteDep ()
            {
                var op = Operations.FolderDelete;
                var firstPend = CreatePending (operation: op);

                var retrieved = McPending.QueryById<McPending> (firstPend.Id);
                Assert.AreEqual (StateEnum.Eligible, retrieved.State, "First FolderDelete pending object should be eligible");

                var secPend = CreatePending (operation: op);

                retrieved = McPending.QueryById<McPending> (secPend.Id);
                Assert.AreEqual (StateEnum.PredBlocked, retrieved.State, "Second FolderDelete pending object that is inserted should have State PredBlocked");
            }

            [Test]
            public void TestFolderUpdateDep ()
            {
                var op = Operations.FolderUpdate;
                DoFolderUpdateDep (1, op);

                op = Operations.FolderCreate;
                DoFolderUpdateDep (2, op);
            }

            private void DoFolderUpdateDep (int accountId, Operations op)
            {
                var serverId = "1";
                var firstPend = CreatePending (accountId: accountId, operation: op, serverId: serverId);
                var secPend = CreatePending (accountId: accountId, operation: Operations.FolderUpdate, serverId: firstPend.ServerId);

                var retrieved = McPending.QueryById<McPending> (secPend.Id);
                Assert.AreEqual (StateEnum.PredBlocked, retrieved.State, "Second FolderUpdate command on one folder should have PredBlocked state");
            }

            [Test]
            public void TestMeetingResponseDep ()
            {
                var op = Operations.CalRespond;
                var serverId = "1";
                var firstPend = CreatePending (operation: op, capability: McAccount.AccountCapabilityEnum.CalWriter, serverId: serverId);
                var secPend = CreatePending (operation: op, capability: McAccount.AccountCapabilityEnum.CalWriter, serverId: firstPend.ServerId);

                var retrieved = McPending.QueryById<McPending> (secPend.Id);
                Assert.AreEqual (StateEnum.PredBlocked, retrieved.State);
            }

            [Test]
            public void TestCalMoveDep ()
            {
                var op = Operations.CalMove;
                var serverId = "5";

                TestItemMoveDep (op, McAccount.AccountCapabilityEnum.CalWriter, serverId);
            }

            [Test]
            public void TestContactMoveDep ()
            {
                var op = Operations.CalMove;
                var serverId = "5";

                TestItemMoveDep (op, McAccount.AccountCapabilityEnum.CalWriter, serverId);
            }

            [Test]
            public void TestEmailMoveDep ()
            {
                var op = Operations.CalMove;
                var serverId = "5";

                TestItemMoveDep (op, McAccount.AccountCapabilityEnum.CalWriter, serverId);
            }

            [Test]
            public void TestTaskMoveDep ()
            {
                var op = Operations.CalMove;
                var serverId = "5";

                TestItemMoveDep (op, McAccount.AccountCapabilityEnum.CalWriter, serverId);
            }

            private void TestItemMoveDep (Operations op, McAccount.AccountCapabilityEnum capability, string serverId)
            {
                NcResult result = NcResult.Info (SubKindEnum.Info_ContactMoveSucceeded);

                // Create pending object to block other pending operations
                var blocker = CreatePending (serverId: serverId, operation: Operations.FolderCreate);

                TestMoveAfterFolderCreate (blocker, op, capability, result);
                TestMoveIntoOtherPending (blocker, op, result, shouldBlock: true);

                // remove FolderCreate blocker
                blocker.MarkDispatched ();
                blocker.ResolveAsSuccess (protoControl, result);

                // make a CalCreate blocker
                var calBlocker = CreatePending (serverId: serverId, operation: Operations.CalCreate);

                TestMoveAfterCalCreate (blocker, op, capability, result);
                TestMoveIntoOtherPending (blocker, op, result, shouldBlock: false);

                // remove CalCreate blocker
                calBlocker.MarkDispatched ();
                calBlocker.ResolveAsSuccess (protoControl, result);
            }

            private void TestMoveAfterFolderCreate (McPending blocker, Operations op, McAccount.AccountCapabilityEnum capability, NcResult result)
            {
                // Test that Move Op is blocked when pred has Op FolderCreate
                var parIdPend = CreatePending (parentId: blocker.ServerId, operation: op, capability: capability);
                var retrieved = McPending.QueryById<McPending> (parIdPend.Id);

                Assert.AreEqual (StateEnum.PredBlocked, retrieved.State, "ItemMove operation should be blocked by FolderCreate if serverId's are equal");

                parIdPend.MarkDispatched ();
                parIdPend.ResolveAsSuccess (protoControl, result);
            }

            private void TestMoveIntoOtherPending (McPending blocker, Operations op, NcResult result, bool shouldBlock)
            {
                // Test that Move op is blocked when dest folder has Op FolderCreate
                var destIdPend = CreatePending (destId: blocker.ServerId, operation: op);
                var retrieved = McPending.QueryById<McPending> (destIdPend.Id);

                if (shouldBlock) {
                    Assert.AreEqual (StateEnum.PredBlocked, retrieved.State, "ItemMove operation should be blocked by FolderCreate if destParentId == pred.ServerId");
                } else {
                    Assert.AreNotEqual (StateEnum.PredBlocked, retrieved.State, "ItemMove operation should not be blocked by CalCreate if destParentId == pred.ServerId");
                }

                destIdPend.MarkDispatched ();
                destIdPend.ResolveAsSuccess (protoControl, result);
            }

            private void TestMoveAfterCalCreate (McPending blocker, Operations op, McAccount.AccountCapabilityEnum capability, NcResult result)
            {
                // Test that Move Op is blocked when pred has Op Cal Create
                var pend = CreatePending (serverId: blocker.ServerId, operation: op, capability: capability);
                var retrieved = McPending.QueryById<McPending> (pend.Id);

                Assert.AreEqual (StateEnum.PredBlocked, retrieved.State, "ItemMove op should be blocked when pred has Op CalCreate");
                pend.MarkDispatched ();
                pend.ResolveAsSuccess (protoControl, result);
            }
        }

        public class Misc : BaseMcPendingTest
        {
            const string firstSId = "first";
            const string secondSId = "second";

            [Test]
            public void TestPriority ()
            {
                var first = CreatePending (item: null, operation: Operations.EmailBodyDownload, serverId: firstSId);
                var second = CreatePending (item: null, operation: Operations.EmailBodyDownload, serverId: secondSId);
                second.Prioritize ();
                var shouldBeNone = McPending.QueryEligibleOrderByPriorityStamp (defaultAccountId, McAccount.AccountCapabilityEnum.EmailSender);
                Assert.AreEqual (0, shouldBeNone.Count ());
                shouldBeNone = McPending.QueryEligibleOrderByPriorityStamp (defaultAccountId, McAccount.AccountCapabilityEnum.CalReader);
                Assert.AreEqual (0, shouldBeNone.Count ());
                shouldBeNone = McPending.QueryEligibleOrderByPriorityStamp (defaultAccountId, McAccount.AccountCapabilityEnum.EmailReaderWriter);
                Assert.AreEqual (2, shouldBeNone.Count ());
                shouldBeNone = McPending.QueryEligible (defaultAccountId, McAccount.AccountCapabilityEnum.EmailSender);
                Assert.AreEqual (0, shouldBeNone.Count ());
                shouldBeNone = McPending.QueryEligible (defaultAccountId, McAccount.AccountCapabilityEnum.CalReader);
                Assert.AreEqual (0, shouldBeNone.Count ());
                shouldBeNone = McPending.QueryEligible (defaultAccountId, McAccount.AccountCapabilityEnum.EmailReaderWriter);
                Assert.AreEqual (2, shouldBeNone.Count ());
                var seq = McPending.QueryEligibleOrderByPriorityStamp (defaultAccountId, McAccount.ActiveSyncCapabilities);
                Assert.AreEqual (2, seq.Count ());
                Assert.AreEqual (secondSId, seq.First ().ServerId);
                first.Prioritize ();
                seq = McPending.QueryEligibleOrderByPriorityStamp (defaultAccountId, McAccount.ActiveSyncCapabilities);
                Assert.AreEqual (firstSId, seq.First ().ServerId);
                Assert.AreEqual (2, seq.Count ());
                second.Prioritize ();
                seq = McPending.QueryEligibleOrderByPriorityStamp (defaultAccountId, McAccount.ActiveSyncCapabilities);
                Assert.AreEqual (2, seq.Count ());
                Assert.AreEqual (secondSId, seq.First ().ServerId);
            }

            [Test]
            public void TestIsDuplicate ()
            {
                var first = CreatePending (item: null, operation: Operations.EmailBodyDownload, serverId: firstSId);
                first = first.UpdateWithOCApply<McPending> ((record) => {
                    var target = (McPending)record;
                    target.ParentId = "dog";
                    return true;
                });
                var second = new McPending (defaultAccountId, first.Capability) {
                    ServerId = first.ServerId,
                    ParentId = first.ParentId,
                    Operation = first.Operation,
                };
                Assert.True (second.IsDuplicate ());
                McPending found;
                Assert.True (second.IsDuplicate (out found));
                Assert.NotNull (found);
                Assert.AreEqual (first.ServerId, found.ServerId);
                second.ServerId = secondSId;
                Assert.False (second.IsDuplicate ());
                Assert.False (second.IsDuplicate (out found));
                Assert.Null (found);
                second.ServerId = first.ServerId;
                Assert.True (second.IsDuplicate ());
                second.ParentId = "cat";
                Assert.False (second.IsDuplicate ());
                second.ParentId = first.ParentId;
                Assert.True (second.IsDuplicate ());
                second.Operation = Operations.CalBodyDownload;
                second.Capability = McAccount.AccountCapabilityEnum.CalReader;
                try {
                    second.IsDuplicate ();
                    Assert.True(false);
                } catch (NcAssert.NachoAssertionFailure) {
                }
                second.Operation = first.Operation;
                second.Capability = first.Capability;
                Assert.True (second.IsDuplicate ());
                second.AccountId++;
                Assert.False (second.IsDuplicate ());
            }
        }

        public class DeleteAndRefCount : BaseMcPendingTest
        {
            private McFolder commonFolder;
            private string defaultServerId;

            [SetUp]
            public new void SetUp ()
            {
                base.SetUp ();
                commonFolder = FolderOps.CreateFolder (accountId: defaultAccountId);
                defaultServerId = "3";
            }

            [Test]
            public void TestPendingDelete ()
            {
                // Item should not be deleted if McPending.Delete () is called when refCount > 0 AND isAwaitingDelete == True
                var item = CreateAndLinkItem ();

                var firstPend = CreatePending (item: item, 
                    operation: Operations.CalCreate, 
                    capability: McAccount.AccountCapabilityEnum.CalWriter,
                    serverId: defaultServerId);
                var secPend = CreatePending (item: item, 
                    operation: Operations.CalCreate, 
                    capability: McAccount.AccountCapabilityEnum.CalWriter,
                    serverId: defaultServerId);
                firstPend.MarkDispatched ();
                secPend.MarkDispatched ();

                Assert.AreEqual (2, item.PendingRefCount, "PendingRefCount should be 2 after 2 pending items are added");
                TestItemExistsWithQueries (item);

                item.Delete ();

                firstPend.ResolveAsSuccess (protoControl, NcResult.Info (SubKindEnum.Info_CalendarChanged));

                var retrievedItem = McAbstrObject.QueryById<McCalendar> (item.Id);
                Assert.AreEqual (1, retrievedItem.PendingRefCount, "PendingRefCount should be 1 after 1 pending item is resolved successfully");

                // Item should be deleted if McPending.Delete () is called when the refCount is 0 AND isAwaitingDelete == True
                secPend.ResolveAsSuccess (protoControl, NcResult.Info (SubKindEnum.Info_CalendarChanged));
                retrievedItem = McAbstrObject.QueryById<McCalendar> (item.Id);
                Assert.IsNull (retrievedItem, "Item that isAwaitingDelete should be deleted when refCount reaches 0");
            }

            [Test]
            public void TestNotPendingDelete ()
            {
                // Item should not be deleted when refCount reaches 0 if isAwaitingDelete is false
                var item = CreateAndLinkItem ();

                var firstPend = CreatePending (item: item, 
                    operation: Operations.CalCreate, 
                    capability: McAccount.AccountCapabilityEnum.CalWriter,
                    serverId: defaultServerId);
                var secPend = CreatePending (item: item, 
                    operation: Operations.CalCreate, 
                    capability: McAccount.AccountCapabilityEnum.CalWriter,
                    serverId: defaultServerId);
                firstPend.MarkDispatched ();
                secPend.MarkDispatched ();

                firstPend.ResolveAsSuccess (protoControl, NcResult.Info (SubKindEnum.Info_CalendarChanged));
                secPend.ResolveAsSuccess (protoControl, NcResult.Info (SubKindEnum.Info_CalendarChanged));

                var retrieved = TestItemExistsWithQueries (item);
                Assert.AreEqual (0, retrieved.PendingRefCount, "Ref Count should be 0 after all pendings have been resolved on an item not awaiting delete");
            }

            [Test]
            public void TestDeletingItemWhenRefCountZero ()
            {
                // Item should be deleted by McItem.Delete () if refCount == 0
                var item = CreateAndLinkItem ();

                var firstPend = CreatePending (item: item, operation: Operations.CalCreate, 
                    capability: McAccount.AccountCapabilityEnum.CalWriter, 
                    serverId: defaultServerId);
                firstPend.MarkDispatched ();

                firstPend.ResolveAsSuccess (protoControl, NcResult.Info (SubKindEnum.Info_CalendarChanged));

                // Verify the item has not been deleted; use McItem queries
                var retrieved = TestItemExistsWithQueries (item);

                retrieved.Delete ();

                // Verify the item has been deleted
                retrieved = McAbstrObject.QueryById<McCalendar> (item.Id);
                Assert.Null (retrieved, "Should be able to fully delete an item if its PendingRefCount == 0");
            }

            [Test]
            public void TestNcAsserts ()
            {
                var item = FolderOps.CreateUniqueItem<McCalendar> (defaultAccountId);
                commonFolder.Link (item);

                item.PendingRefCount = 100001;
                item.Update ();

                TestForNachoExceptionFailure (() => {
                    item.Delete ();
                }, "Should throw an exception when trying to delete McItem if PendingRefCount > 100000");
            }

            private McAbstrItem CreateAndLinkItem ()
            {
                var item = FolderOps.CreateUniqueItem<McCalendar> (defaultAccountId, defaultServerId);
                commonFolder.Link (item);
                return item;
            }

            private McAbstrItem TestItemExistsWithQueries (McAbstrItem item)
            {
                var foundItem = McAbstrItem.QueryByClientId<McCalendar> (defaultAccountId, item.ClientId);
                Assert.NotNull (foundItem, "Item should not be null and should be able to be retrieved by ClientId");
                var foundItems = McAbstrItem.QueryByFolderId<McCalendar> (defaultAccountId, commonFolder.Id);
                Assert.AreEqual (1, foundItems.Count, "Item should exist in and be the only item in the folder");
                Assert.AreEqual (item.Id, foundItems.FirstOrDefault ().Id, "Item in folder should match expected");
                return foundItem;
            }
        }

        public class TestCommandDomination : BaseMcPendingTest
        {
            [Test]
            public void TestDomination ()
            {
                int accountId = 1;

                // make McPending with different ServerId, ParentId, and DestId vals
                var pend = CreatePending (accountId: accountId, serverId: "4", parentId: "2", destId: "3");

                var top = new PathOps.McPathNode (PathOps.CreatePath (accountId, "1", "0"));
                var parent = new PathOps.McPathNode (PathOps.CreatePath (accountId, "2", "1"));
                var dest = new PathOps.McPathNode (PathOps.CreatePath (accountId, "3", "1"));
                var item = new PathOps.McPathNode (PathOps.CreatePath (accountId, "4", "2"));
                var bottom = new PathOps.McPathNode (PathOps.CreatePath (accountId, "5", "4"));

                // add nodes to their parents
                top.Children.Add (parent);
                top.Children.Add (dest);
                parent.Children.Add (item);
                item.Children.Add (bottom);

                // CommandDominatesParentId
                bool domParentId = pend.CommandDominatesParentId (top.Root.ServerId);
                Assert.IsTrue (domParentId, "CommandDominatesParentId should return true if param is parent of parent");
                domParentId = pend.CommandDominatesParentId (dest.Root.ServerId);
                Assert.IsFalse (domParentId, "Sibling of parent should not dominate parent id");
                domParentId = pend.CommandDominatesParentId (bottom.Root.ServerId);
                Assert.IsFalse (domParentId, "Child of item should not dominate parent Id");

                // CommandDominatesServerId
                bool domServerId = pend.CommandDominatesServerId (top.Root.ServerId);
                Assert.IsTrue (domServerId, "Parent of parent should dominate item");
                domServerId = pend.CommandDominatesServerId (dest.Root.ServerId);
                Assert.IsFalse (domServerId, "Sibling of parent should not dominate item");
                domServerId = pend.CommandDominatesServerId (bottom.Root.ServerId);
                Assert.IsFalse (domServerId, "Child of item does not dominate serverId");

                // CommandDominatesDestParentId
                bool domDestParentId = pend.CommandDominatesDestParentId (top.Root.ServerId);
                Assert.IsTrue (domDestParentId, "Parent of destParent should dominate");
                domDestParentId = pend.CommandDominatesDestParentId (parent.Root.ServerId);
                Assert.IsFalse (domDestParentId, "Sibling of destParent should not dominate it");
                domDestParentId = pend.CommandDominatesDestParentId (item.Root.ServerId);
                Assert.IsFalse (domDestParentId, "Item should not dominate destParent");
            }

            [Test]
            public void TestPendingItemNotDom ()
            {
                var parent = new PathOps.McPathNode (PathOps.CreatePath (defaultAccountId, "1", "0"));
                var item = new PathOps.McPathNode (PathOps.CreatePath (defaultAccountId, "2", "1"));
                var badItem = new PathOps.McPathNode (PathOps.CreatePath (defaultAccountId, "3", "1"));

                var folder = FolderOps.CreateFolder (accountId: defaultAccountId, parentId: parent.Root.ParentId, serverId: parent.Root.ServerId);
                var email = FolderOps.CreateUniqueItem<McEmailMessage> (accountId: defaultAccountId, serverId: item.Root.ServerId);
                folder.Link (email);
                var emailPend = CreatePending (accountId: defaultAccountId, parentId: item.Root.ParentId, serverId: item.Root.ServerId, operation: Operations.EmailSend, item: email);
                var badEmail = FolderOps.CreateUniqueItem<McEmailMessage> (accountId: defaultAccountId, serverId: badItem.Root.ServerId);
                folder.Link (badEmail);

                var domItem = emailPend.CommandDominatesItem (badEmail.ServerId);
                Assert.IsFalse (domItem, "CommandDominatesItem should return false when the argument does not match or dominate");
            }

            [Test]
            public void TestPendingItemInSyncedFolder ()
            {
                // create McPath tree
                int accountId = 1;

                var parent = new PathOps.McPathNode (PathOps.CreatePath (accountId, "1", "0"));
                var item = new PathOps.McPathNode (PathOps.CreatePath (accountId, "2", "1"));
                var bottom = new PathOps.McPathNode (PathOps.CreatePath (accountId, "3", "2"));

                // add nodes to their parents
                parent.Children.Add (item);
                item.Children.Add (bottom);

                // create folder, item, and pending
                var folder = FolderOps.CreateFolder (accountId: accountId, serverId: parent.Root.ServerId);
                var email = FolderOps.CreateUniqueItem<McEmailMessage> (accountId: accountId, serverId: item.Root.ServerId);
                folder.Link (email);
                var emailPend = CreatePending (accountId: accountId, parentId: folder.ServerId, operation: Operations.EmailReply, serverId: email.ServerId, item: email);

                var domItemServerId = emailPend.CommandDominatesItem (email.ServerId);
                Assert.IsTrue (domItemServerId, "CommandDominatesItem should return true when the item ServerId matches the argument");

                var domfolderServerId = emailPend.CommandDominatesItem (folder.ServerId);
                Assert.IsTrue (domfolderServerId, "CommandDominatesItem should return true when the argument dominates the item ServerId");

                var domNotDom = emailPend.CommandDominatesItem (bottom.Root.ServerId);
                Assert.IsFalse (domNotDom, "CommandDominatesItem should not return true when the argument does not dominate the item");
            }
        }
    }
}

