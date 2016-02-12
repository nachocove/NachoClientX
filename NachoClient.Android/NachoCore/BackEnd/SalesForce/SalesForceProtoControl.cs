//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using NachoCore.Model;
using System;

namespace NachoCore.SFDC
{
    public class SalesForceProtoControl : NcProtoControl
    {
        public const int KDefaultResyncSeconds = 60*30;

        public const McAccount.AccountCapabilityEnum SalesForceCapabilities = (
            McAccount.AccountCapabilityEnum.ContactReader |
            McAccount.AccountCapabilityEnum.ContactWriter);

        public static McAccount CreateAccount ()
        {
            var fsAccount = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.SalesForce,
                AccountCapability = SalesForceProtoControl.SalesForceCapabilities,
                AccountService = McAccount.AccountServiceEnum.SalesForce,
                DisplayName = "SalesForce",
            };
            fsAccount.Insert ();
            return fsAccount;
        }

        public enum Lst : uint
        {
            DiscW = (St.Last + 1),
            // wait for the fetch of the endpoint query paths.
            UiCrdW,
            SyncW,
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
                AuthFail = (PcEvt.E.Last + 1),
                UiSetCred,
                Last = UiSetCred,
            };
        }

        public static void PopulateServer (int accountId, Uri serverUri)
        {
            var server = McServer.QueryByAccountIdAndCapabilities (accountId, SalesForceProtoControl.SalesForceCapabilities);
            if (server == null) {
                server = new McServer () {
                    AccountId = accountId,
                    Capabilities = SalesForceProtoControl.SalesForceCapabilities,
                };
                server.Insert ();
            }
            server.UpdateWithOCApply<McServer> (((record) => {
                var target = (McServer)record;
                target.Host = serverUri.Host;
                target.Port = serverUri.Port >= 0 ? serverUri.Port : 443;
                target.Scheme = serverUri.Scheme;
                target.Path = "";
                return true;
            }));
        }

        public SalesForceSetup SFDCSetup { get; protected set; }

        protected SFDCCommand Cmd;

        protected bool DiscoveryDone { get; set; }

        public SalesForceProtoControl (INcProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            Capabilities = SalesForceCapabilities;
            SetupAccount ();
            Sm = new NcStateMachine ("SFDCPC") { 
                Name = string.Format ("SFDCPC({0})", AccountId),
                LocalEventType = typeof(SfdcEvt),
                LocalStateType = typeof(Lst),
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Drop = new uint[] {
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                            (uint)SfdcEvt.E.AuthFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                        }
                    },     
                    new Node {
                        State = (uint)Lst.UiCrdW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)SfdcEvt.E.AuthFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                        }
                    },
                    new Node {
                        State = (uint)Lst.DiscW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                        },
                        Invalid = new uint[] {
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.SyncW,
                        Drop = new uint[] {
                        },
                        Invalid = new [] {
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, ActSetsState = true },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSyncSuccess, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SfdcEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Parked,
                        Drop = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                            (uint)PcEvt.E.Park,
                        },
                        Invalid = new [] {
                            (uint)SfdcEvt.E.AuthFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDrive, ActSetsState = true },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    }         
                }
            };
            Sm.Validate ();
        }

        public override void Remove ()
        {
            NcAssert.True ((uint)Lst.Parked == Sm.State || (uint)St.Start == Sm.State || (uint)St.Stop == Sm.State);
            base.Remove ();
        }

        void DoDisc ()
        {
            if (SFDCSetup == null) {
                SFDCSetup = new SalesForceSetup (this, AccountId);
            } else {
                Log.Warn (Log.LOG_SFDC, "Starting a discovery on top of a discovery!");
            }
            SFDCSetup.Execute ();
        }

        SalesForceContactSync Sync;

        void DoSync ()
        {
            if (null != ReSyncTimer) {
                ReSyncTimer.Dispose ();
                ReSyncTimer = null;
            }
            DiscoveryDone = true;
            if (null != Sync) {
                Sync.Cancel ();
                Sync = null;
            }
            Sync = new SalesForceContactSync (this, AccountId);
            Sync.Execute ();
        }

        NcTimer ReSyncTimer;

        void DoSyncSuccess ()
        {
            Sync.Cancel ();
            Sync = null;
            if (null != ReSyncTimer) {
                ReSyncTimer.Dispose ();
            }
            ReSyncTimer = new NcTimer ("SFDCResyncTimer", (state) => Execute (), null, new TimeSpan (0, 0, KDefaultResyncSeconds), TimeSpan.Zero);
        }

        void DoPark ()
        {
            CancelCmd ();
        }

        void DoDrive ()
        {
            if (DiscoveryDone) {
                DoSync ();
                Sm.State = (uint)Lst.SyncW;
            } else {
                DoDisc ();
                Sm.State = (uint)Lst.DiscW;
            }
        }

        void DoUiCredReq ()
        {
            CancelCmd ();
            BackEndStatePreset = BackEndStateEnum.CredWait;
            // Send the request toward the UI.
            Owner.CredReq (this);
        }

        public override void CredResp ()
        {
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)SfdcEvt.E.UiSetCred, "SFDCPCUSC");
            }, "SfdcCredResp");
        }

        protected override bool Execute ()
        {
            if (!base.Execute ()) {
                return false;
            }
            NcTask.Run (() => Sm.PostEvent ((uint)SmEvt.E.Launch, "SFDCPCLAUNCH"), "SFDCExecute");
            return true;
        }

        protected override void ForceStop ()
        {
            if (null != ReSyncTimer) {
                ReSyncTimer.Dispose ();
                ReSyncTimer = null;
            }
            base.ForceStop ();
        }

        void CancelCmd ()
        {
            if (Cmd != null) {
                Cmd.Cancel ();
            }
            Cmd = null;
        }

        void ExecuteCmd ()
        {
            NcAssert.NotNull (Cmd);
            Cmd.Execute (Sm);
        }

        void SetCmd (SFDCCommand cmd)
        {
            if (Cmd != null &&
                cmd.GetType () == Cmd.GetType ()) {
                // this is a retry. Use the same command to keep track of number of retries.
                return;
            }
            CancelCmd ();
            Cmd = cmd;
        }
    }
}

