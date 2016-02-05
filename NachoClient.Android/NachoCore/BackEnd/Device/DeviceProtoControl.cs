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
                SyncStopped,
                SyncDone,
                AbateOn,
                AbateOff,
                Last = AbateOff,
            };
        }

        private NcDeviceContacts DeviceContacts = null;
        private NcDeviceCalendars DeviceCalendars = null;
        private CancellationTokenSource DPCts = null;
        private object CtsLock = new object ();

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
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                            (uint)DevEvt.E.SyncCancelled,
                            (uint)DevEvt.E.SyncStopped,
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
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)DevEvt.E.SyncStart, Act = DoNop, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)DevEvt.E.SyncStopped, Act = DoNop, State = (uint)Lst.Abated },
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
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.Cancelling },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.Cancelling },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)DevEvt.E.SyncStart, Act = DoNop, State = (uint)Lst.CancellingQd },
                            new Trans { Event = (uint)DevEvt.E.SyncCancelled, Act = DoNop, State = (uint)Lst.Abated },
                            new Trans { Event = (uint)DevEvt.E.SyncStopped, Act = DoNop, State = (uint)Lst.Abated },
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
                            (uint)DevEvt.E.SyncStopped,
                            (uint)DevEvt.E.SyncDone,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.Abated },
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
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)DevEvt.E.SyncCancelled, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)DevEvt.E.SyncStopped, Act = DoNop, State = (uint)Lst.AbatedQd },
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
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.CancellingQd },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.CancellingQd },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)DevEvt.E.SyncCancelled, Act = DoNop, State = (uint)Lst.AbatedQd },
                            new Trans { Event = (uint)DevEvt.E.SyncStopped, Act = DoNop, State = (uint)Lst.AbatedQd },
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
                            (uint)DevEvt.E.SyncStopped,
                            (uint)DevEvt.E.SyncDone,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncWQd },
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.AbatedQd },
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
                            (uint)DevEvt.E.SyncStopped,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.Idle },
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
                            (uint)DevEvt.E.SyncStopped,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoProcQ, State = (uint)Lst.AbateIdle },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoProcQ, State = (uint)Lst.AbateIdle },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)DevEvt.E.SyncStart, Act = DoNop, State = (uint)Lst.Abated },
                            new Trans { Event = (uint)DevEvt.E.AbateOff, Act = DoNop, State = (uint)Lst.Idle },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Parked,
                        Drop = new uint[] {
                            (uint)DevEvt.E.AbateOn,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)DevEvt.E.SyncDone,
                            (uint)DevEvt.E.SyncCancelled,
                            (uint)DevEvt.E.SyncStopped,
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                            (uint)PcEvt.E.Park,
                            (uint)DevEvt.E.SyncStart,
                            (uint)DevEvt.E.AbateOff,
                        },
                        Invalid = new uint[] {
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                        }
                    } 
                }
            };
            Sm.Validate ();
            NachoPlatform.Contacts.Instance.ChangeIndicator += DeviceDbChange;
            NachoPlatform.Calendars.Instance.ChangeIndicator += DeviceDbChange;
            NcApplication.Instance.StatusIndEvent += AbateChange;
        }

        public override void Remove ()
        {
            NcAssert.True ((uint)Lst.Parked == Sm.State || (uint)St.Start == Sm.State || (uint)St.Stop == Sm.State);
            NachoPlatform.Contacts.Instance.ChangeIndicator -= DeviceDbChange;
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
            // This method is always called on the UI thread.  The SyncStart event can result in a call
            // to DoSync(), which gathers all the device contacts and calendar events before kicking off
            // its own background thread.  Gathering the contacts and events can take a while, long enough
            // that it shouldn't happen on the UI thread.  So kick off a task to post the event.
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)DevEvt.E.SyncStart, "DEVCISTART");
            }, "DeviceProtoControl:DeviceDbChange");
        }

        // Start a new sync if needed, or resume an old one.
        private void DoSync ()
        {
            try {
                if (null == DeviceContacts) {
                    DeviceContacts = new NcDeviceContacts ();
                }
                if (null == DeviceCalendars) {
                    DeviceCalendars = new NcDeviceCalendars ();
                }
            } catch (OperationCanceledException) {
                // The app is shutting down.  Cancel the sync.
                Sm.PostEvent ((uint)DevEvt.E.SyncDone, "DEVPCABORTED");
                return;
            }
            var abateTokenSource = new CancellationTokenSource ();
            lock (CtsLock) {
                DPCts = abateTokenSource;
            }
            var abateToken = abateTokenSource.Token;
            NcTask.Run (() => {
                var linkedCancel = CancellationTokenSource.CreateLinkedTokenSource (abateToken, NcTask.Cts.Token);
                var cToken = linkedCancel.Token;
                try {
                    // Protect against the situation where another DoSync background task finishes in between
                    // this call to DoSync and when this background task gets going.
                    var deviceContacts = DeviceContacts;
                    if (null != deviceContacts) {
                        lock (deviceContacts) {
                            bool somethingHappened = false;
                            try {
                                while (!deviceContacts.ProcessNextContact ()) {
                                    somethingHappened = true;
                                    cToken.ThrowIfCancellationRequested ();
                                }
                                while (!deviceContacts.RemoveNextStale ()) {
                                    somethingHappened = true;
                                    cToken.ThrowIfCancellationRequested ();
                                }
                            } finally {
                                // Trigger status events and report progress both when the sync is complete
                                // and when the sync has been interrupted.
                                if (somethingHappened) {
                                    deviceContacts.Report ();
                                }
                            }
                        }
                    }

                    var deviceCalendars = DeviceCalendars;
                    if (null != deviceCalendars) {
                        lock (deviceCalendars) {
                            bool somethingHappened = false;
                            try {
                                while (!deviceCalendars.ProcessNextCalendarFolder ()) {
                                    somethingHappened = true;
                                    cToken.ThrowIfCancellationRequested ();
                                }
                                while (!deviceCalendars.ProcessNextCalendarEvent ()) {
                                    somethingHappened = true;
                                    cToken.ThrowIfCancellationRequested ();
                                }
                                while (!deviceCalendars.RemoveNextStaleEvent ()) {
                                    somethingHappened = true;
                                    cToken.ThrowIfCancellationRequested ();
                                }
                                while (!deviceCalendars.RemoveNextStaleFolder ()) {
                                    somethingHappened = true;
                                    cToken.ThrowIfCancellationRequested ();
                                }
                            } finally {
                                // Trigger status events and report progress both when the sync is complete
                                // and when the sync has been interrupted.
                                if (somethingHappened) {
                                    deviceCalendars.Report ();
                                }
                            }
                        }
                    }

                    // The sync has completed.  Reset things so the next DoSync will start over.
                    DeviceContacts = null;
                    DeviceCalendars = null;

                    Sm.PostEvent ((uint)DevEvt.E.SyncDone, "DEVPCSYNCED");

                } catch (OperationCanceledException) {
                    if (abateToken.IsCancellationRequested) {
                        // Abate was signaled
                        Sm.PostEvent ((uint)DevEvt.E.SyncCancelled, "DEVPCCANCEL");
                    } else if (NcTask.Cts.Token.IsCancellationRequested) {
                        // The app is shutting down
                        Sm.PostEvent ((uint)DevEvt.E.SyncStopped, "DEVPCSTOPPED");
                    } else {
                        Log.Error (Log.LOG_SYS,
                            "DeviceProtoControl:DoSync caught an OperationCanceledException, but neither of the cancellation tokens have been cancelled.");
                        throw;
                    }
                } finally {
                    linkedCancel.Dispose ();
                    lock (CtsLock) {
                        if (DPCts == abateTokenSource) {
                            DPCts = null;
                        }
                    }
                    abateTokenSource.Dispose ();
                }
            }, "DeviceProtoControl:DoSync");
        }

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
            var pendings = McPending.QueryEligible (AccountId, McAccount.DeviceCapabilities);
            McContact contact = null;
            McCalendar cal = null;
            foreach (var pending in pendings) {
                pending.MarkDispatched ();
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
                    Log.Error (Log.LOG_SYS, "DeviceProtoControl.DoProcQ: inappropriate operation: {0}", pending.Operation);
                    pending.ResolveAsHardFail (this, NcResult.WhyEnum.WrongController);
                    break;
                }
            }
        }

        protected override bool Execute ()
        {
            // Ignore base.Execute() we don't care about the network.
            // We're letting the app use Start() to trigger a re-sync. TODO - consider using Sync command.
            Sm.PostEvent ((uint)SmEvt.E.Launch, "DEVICELAUNCH");
            return true;
        }
    }
}
