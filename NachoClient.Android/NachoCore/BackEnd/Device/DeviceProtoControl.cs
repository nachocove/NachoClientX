//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore
{
    public class DeviceProtoControl : NcProtoControl
    {
        public enum Lst : uint
        {
            // Waiting for a device DB => Nacho DB sync to complete.
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
                
        public class DevEvt : PcEvt
        {
            new public enum E : uint
            {
                SyncStart = (PcEvt.E.Last + 1),
                SyncCancelled,
                SyncDone,
                AbateOn,
                AbateOff,
                Last = AbateOff,
            };
        }

        private NcDeviceContacts DeviceContacts = null;
        private NcDeviceCalendars DeviceCalendars = null;
        private CancellationTokenSource Cts = null;

        public DeviceProtoControl (INcProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            Capabilities = McAccount.DeviceCapabilities;
            // FIXME JEFF
            // We use a single SM for both cal & contacts. This causes a change in one DB to trigger a 
            // reload of both DBs. A small change is needed to the generic SM code to fix this.
            Sm = new NcStateMachine ("DEV") { 
                Name = string.Format ("DEV({0})", AccountId),
                LocalEventType = typeof(DevEvt),
                LocalStateType = typeof(Lst),
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Drop = new uint[] {
                            (uint)DevEvt.E.AbateOff,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)DevEvt.E.SyncCancelled,
                            (uint)DevEvt.E.SyncDone,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)DevEvt.E.SyncStart, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)DevEvt.E.AbateOn, Act = DoAbate, State = (uint)Lst.AbateIdle },
                        }
                    },
                    new Node {
                        State = (uint)Lst.SyncW,
                        Drop = new uint[] {
                            (uint)DevEvt.E.AbateOff,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)DevEvt.E.SyncCancelled,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoProcQ, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)DevEvt.E.SyncStart, Act = DoNop, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)DevEvt.E.SyncDone, Act = DoNop, State = (uint)Lst.Idle },
                            new Trans { Event = (uint)DevEvt.E.AbateOn, Act = DoAbate, State = (uint)Lst.Cancelling },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Cancelling,
                        Drop = new uint[] {
                            (uint)DevEvt.E.AbateOn,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoProcQ, State = (uint)Lst.Cancelling },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.Cancelling },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)DevEvt.E.SyncStart, Act = DoNop, State = (uint)Lst.CancellingQd },
                            new Trans { Event = (uint)DevEvt.E.SyncCancelled, Act = DoNop, State = (uint)Lst.Abated },
                            new Trans { Event = (uint)DevEvt.E.SyncDone, Act = DoNop, State = (uint)Lst.AbateIdle },
                            new Trans { Event = (uint)DevEvt.E.AbateOff, Act = DoNop, State = (uint)Lst.SyncWQd },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Abated,
                        Drop = new uint[] {
                            (uint)DevEvt.E.AbateOn,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)DevEvt.E.SyncCancelled,
                            (uint)DevEvt.E.SyncDone,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoProcQ, State = (uint)Lst.Abated },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.Abated },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)DevEvt.E.SyncStart, Act = DoNop, State = (uint)Lst.AbatedQd },
                            new Trans { Event = (uint)DevEvt.E.AbateOff, Act = DoSync, State = (uint)Lst.SyncW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.SyncWQd,
                        Drop = new uint[] {
                            (uint)SmEvt.E.Launch,
                            (uint)DevEvt.E.SyncStart,
                            (uint)DevEvt.E.AbateOff,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoProcQ, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)DevEvt.E.SyncCancelled, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)DevEvt.E.SyncDone, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)DevEvt.E.AbateOn, Act = DoAbate, State = (uint)Lst.CancellingQd },
                        }
                    },
                    new Node {
                        State = (uint)Lst.CancellingQd,
                        Drop = new uint[] {
                            (uint)DevEvt.E.AbateOn,
                            (uint)DevEvt.E.SyncStart,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoProcQ, State = (uint)Lst.CancellingQd },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.CancellingQd },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)DevEvt.E.SyncCancelled, Act = DoNop, State = (uint)Lst.AbatedQd },
                            new Trans { Event = (uint)DevEvt.E.SyncDone, Act = DoNop, State = (uint)Lst.Abated },
                            new Trans { Event = (uint)DevEvt.E.AbateOff, Act = DoNop, State = (uint)Lst.SyncWQd },
                        }
                    },
                    new Node {
                        State = (uint)Lst.AbatedQd,
                        Drop = new uint[] {
                            (uint)DevEvt.E.SyncStart,
                            (uint)DevEvt.E.AbateOn,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)DevEvt.E.SyncCancelled,
                            (uint)DevEvt.E.SyncDone,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoProcQ, State = (uint)Lst.AbatedQd },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.AbatedQd },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)DevEvt.E.AbateOff, Act = DoSync, State = (uint)Lst.SyncWQd },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Idle,
                        Drop = new uint[] {
                            (uint)DevEvt.E.AbateOff,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)DevEvt.E.SyncDone,
                            (uint)DevEvt.E.SyncCancelled,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoProcQ, State = (uint)Lst.Idle },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.Idle },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)DevEvt.E.SyncStart, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)DevEvt.E.AbateOn, Act = DoNop, State = (uint)Lst.AbateIdle },
                        }
                    },
                    new Node {
                        State = (uint)Lst.AbateIdle,
                        Drop = new uint[] {
                            (uint)DevEvt.E.AbateOn,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)DevEvt.E.SyncDone,
                            (uint)DevEvt.E.SyncCancelled,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoProcQ, State = (uint)Lst.AbateIdle },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.AbateIdle },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)DevEvt.E.SyncStart, Act = DoNop, State = (uint)Lst.Abated },
                            new Trans { Event = (uint)DevEvt.E.AbateOff, Act = DoNop, State = (uint)Lst.Idle },
                        }
                    },
                }
            };
            Sm.Validate ();
            Contacts.Instance.ChangeIndicator += DeviceDbChange;
            Calendars.Instance.ChangeIndicator += DeviceDbChange;
            NcApplication.Instance.StatusIndEvent += AbateChange;
        }

        public override void Remove ()
        {
            NcAssert.True ((uint)Lst.Parked == Sm.State || (uint)St.Start == Sm.State || (uint)St.Stop == Sm.State);
            Contacts.Instance.ChangeIndicator -= DeviceDbChange;
            Calendars.Instance.ChangeIndicator -= DeviceDbChange;
            NcApplication.Instance.StatusIndEvent -= AbateChange;
            base.Remove ();
        }

        private void AbateChange (object sender, EventArgs ea)
        {
            var siea = (StatusIndEventArgs)ea;
            if (NcResult.SubKindEnum.Info_BackgroundAbateStarted == siea.Status.SubKind) {
                Sm.PostEvent ((uint)DevEvt.E.AbateOn, "DEVCABATEON");
            } else if (NcResult.SubKindEnum.Info_BackgroundAbateStopped == siea.Status.SubKind) {
                Sm.PostEvent ((uint)DevEvt.E.AbateOff, "DEVCABATEOFF");
            }
        }

        private void DeviceDbChange (object sender, EventArgs ea)
        {
            Sm.PostEvent ((uint)DevEvt.E.SyncStart, "DEVCISTART");
        }

        // Start a new sync if needed, or resume an old one.
        private void DoSync ()
        {
            if (null == DeviceContacts) {
                DeviceContacts = new NcDeviceContacts ();
            }
            if (null == DeviceCalendars) {
                DeviceCalendars = new NcDeviceCalendars ();
            }
            if (null == Cts) {
                Cts = new CancellationTokenSource ();
            }
            var cToken = Cts.Token;
            NcTask.Run (() => {
                try {
                    // Protect against the situation where another DoSync background task finishes in between
                    // this call to DoSync and when this background task gets going.
                    var deviceContacts = DeviceContacts;
                    if (null != deviceContacts) {
                        lock (deviceContacts) {
                            while (!deviceContacts.ProcessNextContact ()) {
                                cToken.ThrowIfCancellationRequested ();
                            }
                            while (!deviceContacts.RemoveNextStale ()) {
                                cToken.ThrowIfCancellationRequested ();
                            }
                            deviceContacts.Report ();
                            // We completed the sync.
                            DeviceContacts = null;
                        }
                    }

                    var deviceCalendars = DeviceCalendars;
                    if (null != deviceCalendars) {
                        lock (deviceCalendars) {
                            while (!deviceCalendars.ProcessNextCal ()) {
                                cToken.ThrowIfCancellationRequested ();
                            }
                            while (!deviceCalendars.RemoveNextStale ()) {
                                cToken.ThrowIfCancellationRequested ();
                            }
                            deviceCalendars.Report ();
                            // We completed the sync.
                            DeviceCalendars = null;
                        }
                    }

                    Sm.PostEvent ((uint)DevEvt.E.SyncDone, "DEVNCCONSYNCED");
                } catch (OperationCanceledException) {
                    // Abate was signaled.
                    Sm.PostEvent ((uint)DevEvt.E.SyncCancelled, "DEVNCCONCANCEL");
                }
            }, "DeviceProtoControl:DoSync");
        }

        private void DoAbate ()
        {
            // If the state machine in state SyncW gets AbateOn, AbateOff, AbateOn events before it gets
            // the SyncCancelled event, then Cts will be null.  This is not an error, since the in-progress
            // sync has already been notified and there is nothing else that needs ot be cancelled.
            if (null != Cts) {
                Cts.Cancel ();
                Cts = null;
            }
        }

        private void DoPark ()
        {
            DoAbate ();
        }

        private void DoProcQ ()
        {
            // Process the pending Q until empty. Can't be that many, because it is human generated.
            var pendings = McPending.QueryEligible (AccountId, McAccount.DeviceCapabilities);
            McContact contact = null;
            McCalendar cal = null;
            foreach (var pending in pendings) {
                pending.MarkDispached ();
                switch (pending.Operation) {
                case McPending.Operations.CalBodyDownload:
                case McPending.Operations.CalMove:
                case McPending.Operations.ContactBodyDownload:
                case McPending.Operations.ContactMove:
                case McPending.Operations.ContactSearch:
                    pending.ResolveAsHardFail (this, NcResult.WhyEnum.Unsupported);
                    break;

                case McPending.Operations.CalCreate:
                    cal = McCalendar.QueryById<McCalendar> (pending.ItemId);
                    if (Calendars.Instance.Add (cal).isOK ()) {
                        pending.ResolveAsSuccess (this);
                    } else {
                        pending.ResolveAsHardFail (this, NcResult.WhyEnum.Unknown);
                    }
                    break;

                case McPending.Operations.CalDelete:
                    if (Calendars.Instance.Delete (pending.ServerId).isOK ()) {
                        pending.ResolveAsSuccess (this);
                    } else {
                        pending.ResolveAsHardFail (this, NcResult.WhyEnum.Unknown);
                    }
                    break;
                
                case McPending.Operations.CalUpdate:
                    cal = McCalendar.QueryById<McCalendar> (pending.ItemId);
                    if (Calendars.Instance.Change (cal).isOK ()) {
                        pending.ResolveAsSuccess (this);
                    } else {
                        pending.ResolveAsHardFail (this, NcResult.WhyEnum.Unknown);
                    }
                    break;

                case McPending.Operations.ContactCreate:
                    contact = McContact.QueryById<McContact> (pending.ItemId);
                    if (Contacts.Instance.Add (contact).isOK ()) {
                        pending.ResolveAsSuccess (this);
                    } else {
                        pending.ResolveAsHardFail (this, NcResult.WhyEnum.Unknown);
                    }
                    break;

                case McPending.Operations.ContactDelete:
                    if (Contacts.Instance.Delete (pending.ServerId).isOK ()) {
                        pending.ResolveAsSuccess (this);
                    } else {
                        pending.ResolveAsHardFail (this, NcResult.WhyEnum.Unknown);
                    }
                    break;

                case McPending.Operations.ContactUpdate:
                    contact = McContact.QueryById<McContact> (pending.ItemId);
                    if (Contacts.Instance.Change (contact).isOK ()) {
                        pending.ResolveAsSuccess (this);
                    } else {
                        pending.ResolveAsHardFail (this, NcResult.WhyEnum.Unknown);
                    }
                    break;

                default:
                    Log.Error (Log.LOG_SYS, "DeviceProtoControl.DoProcQ: inappropriate operation: {0}", pending.Operation);
                    pending.ResolveAsHardFail (this, NcResult.WhyEnum.WrongController);
                    break;
                }
            }
        }

        public override bool Execute ()
        {
            // Ignore base.Execute() we don't care about the nextwork.
            // We're letting the app use Start() to trigger a re-sync. TODO - consider using Sync command.
            Sm.PostEvent ((uint)SmEvt.E.Launch, "DEVICELAUNCH");
            return true;
        }
    }
}
