//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using NachoCore.Model;
using System.Threading;
using System;

namespace NachoCore
{
    public class SalesForceProtoControl : NcProtoControl
    {
        public static McAccount CreateAccount ()
        {
            var fsAccount = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.SalesForce,
                AccountCapability = SalesForceProtoControl.SalesForceCapabilities,
                AccountService = McAccount.AccountServiceEnum.SalesForce,
                DisplayName = "SalesForce",
            };
            return fsAccount;
        }

        public enum Lst : uint
        {
            // Waiting for a SalesForce DB => Nacho DB sync to complete.
            SyncW = (St.Last + 1),
            // We've been told to chill, and are waiting for the current sync to notice the cancel request.
            Cancelling,
            // We've been told to chill, and the current sync has stopped.
            Abated,
            // Same as SyncW, but we just got a request to sync during the sync, so we need to do it again.
            SyncWQd,
            // Same as Cancelling, but there is a pending sync request after the current one completes.
            CancellingQd,
            // Same as Abate, but when we are allowed to work again, we need to go to SyncWQd.
            AbatedQd,
            // There is nothing to do.
            Idle,
            // There is nothing to do, and "Abate".
            AbateIdle,
            Parked,
        };

        public override BackEndStateEnum BackEndState {
            get {
                return BackEndStateEnum.PostAutoDPostInboxSync;
            }
        }

        public class SfdcEvt : PcEvt
        {
            new public enum E : uint
            {
                SyncStart = (PcEvt.E.Last + 1),
                SyncCancelled,
                SyncStopped,
                SyncDone,
                AbateOn,
                AbateOff,
                Last = AbateOff,
            };
        }

        public const McAccount.AccountCapabilityEnum SalesForceCapabilities = (
            McAccount.AccountCapabilityEnum.ContactReader |
            McAccount.AccountCapabilityEnum.ContactWriter);

        public SalesForceProtoControl (INcProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            Capabilities = SalesForceCapabilities;
            Sm = new NcStateMachine ("SFDC") { 
                Name = string.Format ("SFDC({0})", AccountId),
                LocalEventType = typeof(SfdcEvt),
                LocalStateType = typeof(Lst),
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Drop = new [] {
                            (uint)SfdcEvt.E.AbateOff,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.SyncStart, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SfdcEvt.E.AbateOn, Act = DoAbate, State = (uint)Lst.AbateIdle },
                        }
                    },          
                    new Node {
                        State = (uint)Lst.SyncW,
                        Drop = new [] {
                            (uint)SfdcEvt.E.AbateOff,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SfdcEvt.E.SyncCancelled,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.SyncStart, Act = DoNop, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)SfdcEvt.E.SyncStopped, Act = DoNop, State = (uint)Lst.Abated },
                            new Trans { Event = (uint)SfdcEvt.E.SyncDone, Act = DoNop, State = (uint)Lst.Idle },
                            new Trans { Event = (uint)SfdcEvt.E.AbateOn, Act = DoAbate, State = (uint)Lst.Cancelling },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Cancelling,
                        Drop = new [] {
                            (uint)SfdcEvt.E.AbateOn,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.Cancelling },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.Cancelling },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.SyncStart, Act = DoNop, State = (uint)Lst.CancellingQd },
                            new Trans { Event = (uint)SfdcEvt.E.SyncCancelled, Act = DoNop, State = (uint)Lst.Abated },
                            new Trans { Event = (uint)SfdcEvt.E.SyncStopped, Act = DoNop, State = (uint)Lst.Abated },
                            new Trans { Event = (uint)SfdcEvt.E.SyncDone, Act = DoNop, State = (uint)Lst.AbateIdle },
                            new Trans { Event = (uint)SfdcEvt.E.AbateOff, Act = DoNop, State = (uint)Lst.SyncWQd },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Abated,
                        Drop = new [] {
                            (uint)SfdcEvt.E.AbateOn,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SfdcEvt.E.SyncCancelled,
                            (uint)SfdcEvt.E.SyncStopped,
                            (uint)SfdcEvt.E.SyncDone,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.Abated },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.Abated },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.SyncStart, Act = DoNop, State = (uint)Lst.AbatedQd },
                            new Trans { Event = (uint)SfdcEvt.E.AbateOff, Act = DoSync, State = (uint)Lst.SyncW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.SyncWQd,
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                            (uint)SfdcEvt.E.SyncStart,
                            (uint)SfdcEvt.E.AbateOff,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.SyncCancelled, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SfdcEvt.E.SyncStopped, Act = DoNop, State = (uint)Lst.AbatedQd },
                            new Trans { Event = (uint)SfdcEvt.E.SyncDone, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SfdcEvt.E.AbateOn, Act = DoAbate, State = (uint)Lst.CancellingQd },
                        }
                    },
                    new Node {
                        State = (uint)Lst.CancellingQd,
                        Drop = new [] {
                            (uint)SfdcEvt.E.AbateOn,
                            (uint)SfdcEvt.E.SyncStart,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.CancellingQd },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.CancellingQd },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.SyncCancelled, Act = DoNop, State = (uint)Lst.AbatedQd },
                            new Trans { Event = (uint)SfdcEvt.E.SyncStopped, Act = DoNop, State = (uint)Lst.AbatedQd },
                            new Trans { Event = (uint)SfdcEvt.E.SyncDone, Act = DoNop, State = (uint)Lst.Abated },
                            new Trans { Event = (uint)SfdcEvt.E.AbateOff, Act = DoNop, State = (uint)Lst.SyncWQd },
                        }
                    },
                    new Node {
                        State = (uint)Lst.AbatedQd,
                        Drop = new [] {
                            (uint)SfdcEvt.E.SyncStart,
                            (uint)SfdcEvt.E.AbateOn,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SfdcEvt.E.SyncCancelled,
                            (uint)SfdcEvt.E.SyncStopped,
                            (uint)SfdcEvt.E.SyncDone,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.AbatedQd },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.AbatedQd },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.AbateOff, Act = DoSync, State = (uint)Lst.SyncWQd },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Idle,
                        Drop = new [] {
                            (uint)SfdcEvt.E.AbateOff,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SfdcEvt.E.SyncDone,
                            (uint)SfdcEvt.E.SyncCancelled,
                            (uint)SfdcEvt.E.SyncStopped,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.Idle },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.Idle },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.SyncStart, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SfdcEvt.E.AbateOn, Act = DoNop, State = (uint)Lst.AbateIdle },
                        }
                    },
                    new Node {
                        State = (uint)Lst.AbateIdle,
                        Drop = new [] {
                            (uint)SfdcEvt.E.AbateOn,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SfdcEvt.E.SyncDone,
                            (uint)SfdcEvt.E.SyncCancelled,
                            (uint)SfdcEvt.E.SyncStopped,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.AbateIdle },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.AbateIdle },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.SyncStart, Act = DoNop, State = (uint)Lst.Abated },
                            new Trans { Event = (uint)SfdcEvt.E.AbateOff, Act = DoNop, State = (uint)Lst.Idle },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Parked,
                        Drop = new [] {
                            (uint)SfdcEvt.E.AbateOn,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SfdcEvt.E.SyncDone,
                            (uint)SfdcEvt.E.SyncCancelled,
                            (uint)SfdcEvt.E.SyncStopped,
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                            (uint)PcEvt.E.Park,
                            (uint)SfdcEvt.E.SyncStart,
                            (uint)SfdcEvt.E.AbateOff,
                        },
                        Invalid = new uint[] {
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                        }
                    }         
                }
            };
            Sm.Validate ();
            NcApplication.Instance.StatusIndEvent += AbateChange;
        }

        public override void Remove ()
        {
            NcAssert.True ((uint)Lst.Parked == Sm.State || (uint)St.Start == Sm.State || (uint)St.Stop == Sm.State);
            NcApplication.Instance.StatusIndEvent -= AbateChange;
            base.Remove ();
        }

        private void AbateChange (object sender, EventArgs ea)
        {
            var siea = (StatusIndEventArgs)ea;
            if (NcResult.SubKindEnum.Info_BackgroundAbateStarted == siea.Status.SubKind) {
                Sm.PostEvent ((uint)SfdcEvt.E.AbateOn, "SFCABATEON");
            } else if (NcResult.SubKindEnum.Info_BackgroundAbateStopped == siea.Status.SubKind) {
                Sm.PostEvent ((uint)SfdcEvt.E.AbateOff, "SFCABATEOFF");
            }
        }


        private void DoSync ()
        {
            
        }

        private CancellationTokenSource DPCts = null;
        private object CtsLock = new object ();

        private void DoAbate ()
        {
            // If the state machine in state SyncW gets AbateOn, AbateOff, AbateOn events before it gets
            // the SyncCancelled event, then Cts will be null.  This is not an error, since the in-progress
            // sync has already been notified and there is nothing else that needs to be cancelled.
            lock (CtsLock) {
                if (null != DPCts) {
                    DPCts.Cancel ();
                    DPCts = null;
                }
            }
        }

        private void DoPark ()
        {
            DoAbate ();
        }

        private void DoProcQ ()
        {
            // Process the pending Q until empty. Can't be that many, because it is human generated.
            var pendings = McPending.QueryEligible (AccountId, SalesForceCapabilities);
            McContact contact = null;
            McCalendar cal = null;
            foreach (var pending in pendings) {
                pending.MarkDispached ();
                switch (pending.Operation) {
                case McPending.Operations.ContactBodyDownload:
                case McPending.Operations.ContactMove:
                case McPending.Operations.ContactSearch:
                    pending.ResolveAsHardFail (this, NcResult.WhyEnum.Unsupported);
                    break;

                case McPending.Operations.ContactCreate:
                    contact = McContact.QueryById<McContact> (pending.ItemId);
                    if (NachoPlatform.Contacts.Instance.Add (contact).isOK ()) {
                        pending.ResolveAsSuccess (this);
                    } else {
                        pending.ResolveAsHardFail (this, NcResult.WhyEnum.Unknown);
                    }
                    break;

                case McPending.Operations.ContactDelete:
                    if (NachoPlatform.Contacts.Instance.Delete (pending.ServerId).isOK ()) {
                        pending.ResolveAsSuccess (this);
                    } else {
                        pending.ResolveAsHardFail (this, NcResult.WhyEnum.Unknown);
                    }
                    break;

                case McPending.Operations.ContactUpdate:
                    contact = McContact.QueryById<McContact> (pending.ItemId);
                    if (NachoPlatform.Contacts.Instance.Change (contact).isOK ()) {
                        pending.ResolveAsSuccess (this);
                    } else {
                        pending.ResolveAsHardFail (this, NcResult.WhyEnum.Unknown);
                    }
                    break;

                default:
                    Log.Error (Log.LOG_SYS, "SalesForceProtoContol.DoProcQ: inappropriate operation: {0}", pending.Operation);
                    pending.ResolveAsHardFail (this, NcResult.WhyEnum.WrongController);
                    break;
                }
            }
        }

        protected override bool Execute ()
        {
            // Ignore base.Execute() we don't care about the network.
            // We're letting the app use Start() to trigger a re-sync. TODO - consider using Sync command.
            Sm.PostEvent ((uint)SmEvt.E.Launch, "SFDCLAUNCH");
            return true;
        }
    }
}

