//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using NachoCore.Model;
using System.Threading;
using System;
using System.Collections.Generic;

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
            fsAccount.Insert ();
            return fsAccount;
        }

        public enum Lst : uint
        {
            InitW = (St.Last + 1),
            // wait for the fetch of the endpoint query paths.
            ResourceW,
            ObjectsW,
            UiCrdW,
            SyncWIds,
            SyncW,
            Idle,
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
                SyncDone,
                UiSetCred,
                Last = UiSetCred,
            };
        }

        public const McAccount.AccountCapabilityEnum SalesForceCapabilities = (
            McAccount.AccountCapabilityEnum.ContactReader |
            McAccount.AccountCapabilityEnum.ContactWriter);

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
        /// <summary>
        /// The API-based query path. We store it with the controller in memory, because it may change and
        /// we should query for it each time.
        /// </summary>
        /// <value>The query path.</value>
        public Dictionary<string, string> ResourcePaths { get; set; }

        /// <summary>
        /// SObject URL's
        /// </summary>
        /// <value>The object urls.</value>
        public Dictionary<string, string> ObjectUrls { get; set; }

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
                            (uint)SfdcEvt.E.SyncDone,
                            (uint)SfdcEvt.E.AuthFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                        }
                    },     
                    new Node {
                        State = (uint)Lst.UiCrdW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                            (uint)SfdcEvt.E.SyncDone,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)SfdcEvt.E.AuthFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoDisc, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                        }
                    },
                    new Node {
                        State = (uint)Lst.InitW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                            (uint)SfdcEvt.E.SyncDone,
                        },
                        Invalid = new uint[] {
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoGetVersion, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoGetResources, State = (uint)Lst.ResourceW },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoGetVersion, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.ResourceW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                            (uint)SfdcEvt.E.SyncDone,
                        },
                        Invalid = new uint[] {
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoGetResources, State = (uint)Lst.ResourceW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoGetObjects, State = (uint)Lst.ObjectsW },
                            new Trans { Event = (uint)SfdcEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoGetResources, State = (uint)Lst.ResourceW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                        }
                    },
                    new Node {
                        State = (uint)Lst.ObjectsW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                            (uint)SfdcEvt.E.SyncDone,
                        },
                        Invalid = new uint[] {
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoGetObjects, State = (uint)Lst.ObjectsW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSyncIds, State = (uint)Lst.SyncWIds },
                            new Trans { Event = (uint)SfdcEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoGetObjects, State = (uint)Lst.ObjectsW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                        }
                    },
                    new Node {
                        State = (uint)Lst.SyncWIds,
                        Drop = new uint[] {
                        },
                        Invalid = new [] {
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSyncIds, State = (uint)Lst.SyncWIds },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.SyncDone, Act = DoNop, State = (uint)Lst.Idle },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSyncIds, State = (uint)Lst.SyncWIds },
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
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSyncIds, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.SyncDone, Act = DoNop, State = (uint)Lst.Idle },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoNop, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSyncIds, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SfdcEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Idle,
                        Drop = new uint[] {
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SfdcEvt.E.AuthFail,
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.SyncDone, Act = DoSync, State = (uint)Lst.SyncW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Parked,
                        Drop = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SfdcEvt.E.SyncDone,
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                            (uint)PcEvt.E.Park,
                            (uint)SfdcEvt.E.SyncDone,
                        },
                        Invalid = new [] {
                            (uint)SfdcEvt.E.AuthFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.InitW },
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

        SFDCCommand Cmd;
        void SetCmd (SFDCCommand cmd)
        {
            CancelCmd ();
            Cmd = cmd;
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

        private void DoDisc ()
        {
            ResourcePaths = null;
            DoGetVersion ();
        }

        private void DoGetVersion ()
        {
            SetCmd (new SFDCGetApiVersionsCommand (this));
            ExecuteCmd ();
        }

        private void DoGetResources ()
        {
            SetCmd (new SFDCGetResourcesCommand (this));
            ExecuteCmd ();
        }

        private void DoGetObjects ()
        {
            SetCmd (new SFDCGetObjectsCommand (this));
            ExecuteCmd ();
        }

        private void DoSyncIds ()
        {
            SetCmd (new SFDCGetContactIdsCommand (this));
            ExecuteCmd ();
        }

        private void DoSync ()
        {
            var nextCmd = (SFDCCommand)Sm.Arg;
            if (null != nextCmd) {
                SetCmd (nextCmd);
                ExecuteCmd ();
            } else {
                DoNop ();
            }
        }

        private void DoPark ()
        {
        }

        private void DoUiCredReq ()
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
            base.Execute ();

            // We're letting the app use Start() to trigger a re-sync. TODO - consider using Sync command.
            Sm.PostEvent ((uint)SmEvt.E.Launch, "SFDCPCLAUNCH");
            return true;
        }
    }
}

