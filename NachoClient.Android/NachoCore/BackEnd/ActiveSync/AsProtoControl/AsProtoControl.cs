// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.ActiveSync
{
    public partial class AsProtoControl : NcProtoControl, IPushAssistOwner
    {
        private INcCommand Cmd;
        private AsValidateConfig Validator;

        public enum Lst : uint
        {
            DiscW = (St.Last + 1),
            UiDCrdW,
            UiPCrdW,
            UiServConfW,
            UiCertOkW,
            OptW,
            ProvW,
            SettingsW,
            FSyncW,
            // same as FSyncW, but will try to Sync on Success.
            FSync2W,
            SyncW,
            PingW,
            QOpW,
            HotQOpW,
            FetchW,
            // we are active, but choosing not to execute.
            IdleW,
            // we are not active. when we re-activate on Launch, we pick-up at the saved state.
            // TODO: make Parked part of base SM functionality.
            Parked,
        };

        public override BackEndStateEnum BackEndState {
            get {
                if (null != BackEndStatePreset) {
                    return (BackEndStateEnum)BackEndStatePreset;
                }
                var state = Sm.State;
                if ((uint)Lst.Parked == state) {
                    state = ProtocolState.ProtoControlState;
                }
                // Every state above must be mapped here.
                switch (state) {
                case (uint)St.Start:
                    return BackEndStateEnum.NotYetStarted;

                case (uint)Lst.DiscW:
                    return BackEndStateEnum.Running;

                case (uint)Lst.UiDCrdW:
                case (uint)Lst.UiPCrdW:
                    return BackEndStateEnum.CredWait;

                case (uint)Lst.UiServConfW:
                    return BackEndStateEnum.ServerConfWait;

                case (uint)Lst.UiCertOkW:
                    return BackEndStateEnum.CertAskWait;

                case (uint)Lst.OptW:
                case (uint)Lst.ProvW:
                case (uint)Lst.SettingsW:
                case (uint)Lst.FSyncW:
                case (uint)Lst.FSync2W: 
                case (uint)Lst.SyncW:
                case (uint)Lst.PingW:
                case (uint)Lst.QOpW:
                case (uint)Lst.HotQOpW:
                case (uint)Lst.FetchW:
                case (uint)Lst.IdleW:
                    return (ProtocolState.HasSyncedInbox) ? 
                        BackEndStateEnum.PostAutoDPostInboxSync : 
                        BackEndStateEnum.PostAutoDPreInboxSync;

                default:
                    NcAssert.CaseError (string.Format ("Unhandled state {0}", Sm.State));
                    return BackEndStateEnum.PostAutoDPostInboxSync;
                }
            }
        }

        private X509Certificate2 _ServerCertToBeExamined;

        public override X509Certificate2 ServerCertToBeExamined {
            get {
                return _ServerCertToBeExamined;
            }
        }

        // If you're exposed to AsHttpOperation, you need to cover these.
        public class AsEvt : PcEvt
        {
            new public enum E : uint
            {
                ReDisc = (PcEvt.E.Last + 1),
                ReProv,
                ReSync,
                AuthFail,
                Last = AuthFail,
            };
        }
        // Events of the form UiXxYy are events coming directly from the UI/App toward the controller.
        // DB-based events (even if UI-driven) and server-based events lack the Ui prefix.
        public class CtlEvt : AsEvt
        {
            new public enum E : uint
            {
                UiSetCred = (AsEvt.E.Last + 1),
                GetServConf,
                UiSetServConf,
                GetCertOk,
                UiCertOkYes,
                UiCertOkNo,
                ReFSync,
            };
        }

        public IAsStrategy Strategy { set; get; }

        private PushAssist PushAssist { set; get; }

        private int ConcurrentExtraRequests = 0;

        public AsProtoControl (INcProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            Capabilities = McAccount.ActiveSyncCapabilities;
            SetupAccount ();
            /*
             * State Machine design:
             * * Events from the UI can come at ANY time. They are not always relevant, and should be dropped when not.
             * * ForceStop can happen at any time, and must Cancel anything that is going on immediately.
             * * ForceSync can happen at any time, and must Cancel anything that is going on immediately and initiate Sync.
             * * Objects can be added to the McPending Q at any time.
             * * All other events must come from the orderly completion of commands or internal forced transitions.
             * 
             * The SM Q is an event-Q not a work-Q. Where we need to "remember" to do more than one thing, that
             * memory must be embedded in the state machine.
             * 
             * Sync, Provision, Discovery and FolderSync can be forced by posting the appropriate event.
             * 
             * TempFail: for scenarios where a command can return TempFail, just keep re-trying:
             *  - NcCommStatus will eventually shut us down as TempFail counts against Quality. 
             *  - Max deferrals on pending will pull "bad" pendings out of the Q.
             */
            Sm = new NcStateMachine ("ASPC") { 
                Name = string.Format ("ASPC({0})", AccountId),
                LocalEventType = typeof(CtlEvt),
                LocalStateType = typeof(Lst),
                StateChangeIndication = UpdateSavedState,
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.UiCertOkNo, 
                            (uint)CtlEvt.E.UiCertOkYes,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },

                    new Node {
                        // There is no HardFail. Can't pass DiscW w/out a working server - period.
                        State = (uint)Lst.DiscW, 
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)AsEvt.E.ReProv,
                            (uint)CtlEvt.E.ReFSync,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoOpt, State = (uint)Lst.OptW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans {
                                Event = (uint)AsEvt.E.AuthFail,
                                Act = DoUiCredReq,
                                State = (uint)Lst.UiDCrdW
                            },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans {
                                Event = (uint)CtlEvt.E.GetServConf,
                                Act = DoUiServConfReq,
                                State = (uint)Lst.UiServConfW
                            },
                            new Trans {
                                Event = (uint)CtlEvt.E.GetCertOk,
                                Act = DoUiCertOkReq,
                                State = (uint)Lst.UiCertOkW
                            },
                            new Trans {
                                Event = (uint)CtlEvt.E.UiSetServConf,
                                Act = DoSetServConf,
                                State = (uint)Lst.DiscW
                            },
                            new Trans { Event = (uint)CtlEvt.E.UiSetCred, Act = DoSetCred, State = (uint)Lst.DiscW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.UiDCrdW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)CtlEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans {
                                Event = (uint)CtlEvt.E.UiSetServConf,
                                Act = DoSetServConf,
                                State = (uint)Lst.DiscW
                            },
                        }
                    },

                    new Node {
                        State = (uint)Lst.UiPCrdW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                        },
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW
                            },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)CtlEvt.E.UiSetCred, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)CtlEvt.E.UiSetServConf, Act = DoSetServConf, State = (uint)Lst.ProvW
                            },
                        }
                    },

                    new Node {
                        State = (uint)Lst.UiServConfW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans {
                                Event = (uint)CtlEvt.E.UiSetServConf,
                                Act = DoSetServConf,
                                State = (uint)Lst.DiscW
                            },
                        }
                    },

                    new Node {
                        State = (uint)Lst.UiCertOkW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.UiSetCred,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans {
                                Event = (uint)CtlEvt.E.UiCertOkYes,
                                Act = DoCertOkYes,
                                State = (uint)Lst.DiscW
                            },
                            new Trans { Event = (uint)CtlEvt.E.UiCertOkNo, Act = DoCertOkNo, State = (uint)Lst.DiscW },
                            new Trans {
                                Event = (uint)CtlEvt.E.UiSetServConf,
                                Act = DoSetServConf,
                                State = (uint)Lst.DiscW
                            },
                        }
                    },

                    new Node {
                        State = (uint)Lst.OptW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)AsEvt.E.ReProv,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoOpt, State = (uint)Lst.OptW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoOldProtoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoOpt, State = (uint)Lst.OptW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.ProvW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)AsEvt.E.ReProv,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSettings, State = (uint)Lst.SettingsW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.SettingsW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSettings, State = (uint)Lst.SettingsW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoFSync, State = (uint)Lst.FSyncW },
                            // We choose to move on and try FSync if we're stuck on Settings not working.
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSettings, State = (uint)Lst.SettingsW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.FSyncW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoNop, State = (uint)Lst.FSync2W },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.FSync2W,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoFSync, State = (uint)Lst.FSync2W },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoFSync, State = (uint)Lst.FSync2W },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoFSync, State = (uint)Lst.FSync2W },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSync2W },
                        }
                    },

                    new Node {
                        State = (uint)Lst.SyncW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoExtraOrDont, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.PingW,
                        Drop = new [] {
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.QOpW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.HotQOpW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNopOrPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoExtraOrDont, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.FetchW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.IdleW,
                        Drop = new [] {
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.Parked,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)PcEvt.E.Park,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.ReSync,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDrive, ActSetsState = true },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },
                }
            };
            Sm.Validate ();
            Sm.State = ProtocolState.ProtoControlState;
            LastBackEndState = BackEndState;
            Strategy = new AsStrategy (this);
            PushAssist = new PushAssist (this);
            NcCommStatus.Instance.CommStatusNetEvent += NetStatusEventHandler;
            NcCommStatus.Instance.CommStatusServerEvent += ServerStatusEventHandler;
            NcApplication.Instance.StatusIndEvent += StatusIndEventHandler;
        }

        public override void Remove ()
        {
            if (!((uint)Lst.Parked == Sm.State || (uint)St.Start == Sm.State || (uint)St.Stop == Sm.State)) {
                Log.Warn (Log.LOG_IMAP, "AsProtoControl.Remove called while state is {0}", Sm.State);
            }
            // TODO cleanup stuff on disk like for wipe.
            NcCommStatus.Instance.CommStatusNetEvent -= NetStatusEventHandler;
            NcCommStatus.Instance.CommStatusServerEvent -= ServerStatusEventHandler;
            NcApplication.Instance.StatusIndEvent -= StatusIndEventHandler;
            if (null != PushAssist) {
                PushAssist.Dispose ();
                PushAssist = null;
            }
            base.Remove ();
        }
        // Methods callable by the owner.
        // Keep Execute() harmless if it is called while already executing.
        public override bool Execute ()
        {
            if (!base.Execute ()) {
                return false;
            }
            if (null == Server) {
                Sm.PostEvent ((uint)AsEvt.E.ReDisc, "ASPCEXECAUTOD");
            } else {
                // All states are required to handle the Launch event gracefully.
                Sm.PostEvent ((uint)SmEvt.E.Launch, "ASPCEXE");
            }
            return true;
        }

        public override void CredResp ()
        {
            Sm.PostEvent ((uint)CtlEvt.E.UiSetCred, "ASPCUSC");
        }

        public override void ServerConfResp (bool forceAutodiscovery)
        {
            if (forceAutodiscovery) {
                Sm.PostEvent ((uint)AsEvt.E.ReDisc, "ASPCURD");
            } else {
                Sm.PostEvent ((uint)CtlEvt.E.UiSetServConf, "ASPCUSSC");
            }
        }

        public override void CertAskResp (bool isOkay)
        {
            if (isOkay) {
                Sm.PostEvent ((uint)CtlEvt.E.UiCertOkYes, "ASPCUCOY");
            } else {
                Sm.PostEvent ((uint)CtlEvt.E.UiCertOkNo, "ASPCUCON");
            }
        }
        // State-machine's state persistance callback.
        private void UpdateSavedState ()
        {
            BackEndStatePreset = null;
            var protocolState = ProtocolState;
            uint stateToSave = Sm.State;
            switch (stateToSave) {
            case (uint)Lst.UiDCrdW:
            case (uint)Lst.UiServConfW:
            case (uint)Lst.UiCertOkW:
                stateToSave = (uint)Lst.DiscW;
                break;
            case (uint)Lst.Parked:
                // We never save Parked.
                return;
            }
            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.ProtoControlState = stateToSave;
                return true;
            });
            if (LastBackEndState != BackEndState) {
                var res = NcResult.Info (NcResult.SubKindEnum.Info_BackEndStateChanged);
                res.Value = AccountId;
                StatusInd (res);
            }
            LastBackEndState = BackEndState;
        }
        // State-machine action methods.
        private void DoUiServConfReq ()
        {
            BackEndStatePreset = BackEndStateEnum.ServerConfWait;
            // Send the request toward the UI.
            Owner.ServConfReq (this, Sm.Arg);
        }

        private void DoSetServConf ()
        {
            if (CmdIs (typeof(AsAutodiscoverCommand))) {
                var autoDiscoCmd = (AsAutodiscoverCommand)Cmd;
                autoDiscoCmd.Sm.PostEvent ((uint)AsAutodiscoverCommand.TlEvt.E.ServerSet, "ASPCDSSC");
            }
        }

        private void DoUiCredReq ()
        {
            if (null != Cmd && !CmdIs (typeof(AsAutodiscoverCommand))) {
                Cmd.Cancel ();
            }
            BackEndStatePreset = BackEndStateEnum.CredWait;
            // Send the request toward the UI.
            Owner.CredReq (this);
        }

        private void DoSetCred ()
        {
            if (CmdIs (typeof(AsAutodiscoverCommand))) {
                var autoDiscoCmd = (AsAutodiscoverCommand)Cmd;
                autoDiscoCmd.Sm.PostEvent ((uint)AsAutodiscoverCommand.TlEvt.E.CredSet, "ASPCDSC");
            }
        }

        private void DoUiCertOkReq ()
        {
            BackEndStatePreset = BackEndStateEnum.CertAskWait;
            _ServerCertToBeExamined = (X509Certificate2)Sm.Arg;
            Owner.CertAskReq (this, _ServerCertToBeExamined);
        }

        private void DoCertOkNo ()
        {
            if (CmdIs (typeof(AsAutodiscoverCommand))) {
                var autoDiscoCmd = (AsAutodiscoverCommand)Cmd;
                autoDiscoCmd.Sm.PostEvent ((uint)AsAutodiscoverCommand.SharedEvt.E.SrvCertN, "ASPCDCON");
            }
        }

        private void DoCertOkYes ()
        {
            if (CmdIs (typeof(AsAutodiscoverCommand))) {
                var autoDiscoCmd = (AsAutodiscoverCommand)Cmd;
                autoDiscoCmd.Sm.PostEvent ((uint)AsAutodiscoverCommand.SharedEvt.E.SrvCertY, "ASPCDCOY");
            }
        }

        private void DoDisc ()
        {
            SetCmd (new AsAutodiscoverCommand (this));
            ExecuteCmd ();
        }

        private void DoOpt ()
        {
            SetCmd (new AsOptionsCommand (this));
            ExecuteCmd ();
        }

        private void DoProv ()
        {
            if (ProtocolState.DisableProvisionCommand) {
                Sm.PostEvent ((uint)SmEvt.E.Success, "DOPROVNOPROV");
            } else {
                SetCmd (new AsProvisionCommand (this));
                ExecuteCmd ();
            }
        }

        private void DoOldProtoProv ()
        {
            // If OPTIONS gets a hard failure, then assume oldest supported protocol version and try to keep going.
            AsOptionsCommand.SetOldestProtoVers (this);
            DoProv ();
        }

        private void DoSettings ()
        {
            SetCmd (new AsSettingsCommand (this));
            ExecuteCmd ();
        }

        private void DoFSync ()
        {
            SetCmd (new AsFolderSyncCommand (this));
            ExecuteCmd ();
        }

        private void DoNopOrPick ()
        {
            // If we are parked, the Cmd has been set to null.
            // Otherwise, it has the last command executed (or still executing).
            if (null == Cmd) {
                // We are not running, go figure out what to do.
                // DoPick will set the State.
                DoPick ();
            } else {
                // We are running, ignore the Launch, stay in the current State.
            }
        }

        private void DoExDone ()
        {
            Interlocked.Decrement (ref ConcurrentExtraRequests);
            // Send the PendQHot so that the ProtoControl SM looks to see if there is another hot op
            // to run in parallel.
            Sm.PostEvent ((uint)PcEvt.E.PendQHot, "DOEXDONE1MORE");
        }

        private void DoExtraOrDont ()
        {
            /* TODO
             * Move decision logic into strategy.
             * Evaluate server success rate based on number of outstanding requests.
             * Let those rates drive the allowed concurrency, rather than "1 + 2".
             */
            if (NcCommStatus.CommQualityEnum.OK == NcCommStatus.Instance.Quality (Server.Id) &&
                NetStatusSpeedEnum.CellSlow_2 != NcCommStatus.Instance.Speed &&
                4 > ConcurrentExtraRequests) {
                Interlocked.Increment (ref ConcurrentExtraRequests);
                var pack = Strategy.PickUserDemand ();
                if (null == pack) {
                    // If strategy could not find something to do, we won't be using the side channel.
                    Interlocked.Decrement (ref ConcurrentExtraRequests);
                } else {
                    Log.Info (Log.LOG_AS, "DoExtraOrDont: starting extra request.");
                    var dummySm = new NcStateMachine ("ASPC:EXTRA") { 
                        Name = string.Format ("ASPC:EXTRA({0})", AccountId),
                        LocalEventType = typeof(AsEvt),
                        TransTable = new[] {
                            new Node {
                                State = (uint)St.Start,
                                Invalid = new [] {
                                    (uint)NcProtoControl.PcEvt.E.PendQ,
                                    (uint)NcProtoControl.PcEvt.E.PendQHot,
                                    (uint)NcProtoControl.PcEvt.E.Park,
                                },
                                On = new Trans[] {
                                    new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)St.Start },
                                    new Trans { Event = (uint)SmEvt.E.Success, Act = DoExDone, State = (uint)St.Stop },
                                    new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoExDone, State = (uint)St.Stop },
                                    new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoExDone, State = (uint)St.Stop },
                                    new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoExDone, State = (uint)St.Stop },
                                    new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoExDone, State = (uint)St.Stop },
                                    new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoExDone, State = (uint)St.Stop },
                                    new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoExDone, State = (uint)St.Stop },
                                },
                            }
                        }
                    };
                    dummySm.Validate ();
                    var pickAction = pack.Item1;
                    var cmd = pack.Item2;
                    switch (pickAction) {
                    case PickActionEnum.Fetch:
                    case PickActionEnum.QOop:
                    case PickActionEnum.HotQOp:
                        cmd.Execute (dummySm);
                        break;

                    case PickActionEnum.Sync:
                        // TODO add support for user-initiated Sync of >= 1 folders.
                        // if current op is a sync including specified folder(s) - we must make sure we don't
                        // have 2 concurrent syncs of the same folder.
                    case PickActionEnum.Ping:
                    case PickActionEnum.Wait:
                    default:
                        NcAssert.CaseError (cmd.ToString ());
                        break;
                    }
                    // Leave State unchanged.
                    return;
                }
            }
            // If we got here, we decided that doing an extra request was a bad idea, ...
            if (0 == ConcurrentExtraRequests) {
                // ... and we are currently processing no extra requests. Only in this case will we 
                // interrupt the base request, and only then if we are not already dealing with a "hot" request.
                if ((uint)Lst.HotQOpW != Sm.State) {
                    Log.Info (Log.LOG_AS, "DoExtraOrDont: calling Pick.");
                    // DoPick sets the State.
                    DoPick ();
                } else {
                    Log.Info (Log.LOG_AS, "DoExtraOrDont: not calling Pick (HotQOpW).");
                }
            } else {
                // ... and we are capable of processing extra requests, just not now.
                Log.Info (Log.LOG_AS, "DoExtraOrDont: not starting extra request on top of {0}.", ConcurrentExtraRequests);
            }
        }

        private void DoPick ()
        {
            // Having PickCore eliminates fail-to-set-state bugs.
            Sm.State = (uint)PickCore ();
        }

        private Lst PickCore ()
        {
            /* Due to threading race condition we must clear any event possibly posted
             * by a non-cancelled-in-time await.
             * TODO: couple ClearEventQueue with PostEvent inside SM mutex, or that a cancelled op
             * cannot ever post an event after the Cancel.
             */
            if (null != Cmd) {
                Cmd.Cancel ();
            }
            Sm.ClearEventQueue ();
            var pack = Strategy.Pick ();
            var transition = pack.Item1;
            var cmd = pack.Item2;
            switch (transition) {
            case PickActionEnum.Fetch:
                SetAndExecute (cmd);
                return Lst.FetchW;

            case PickActionEnum.Ping:
                SetAndExecute (cmd);
                return Lst.PingW;

            case PickActionEnum.QOop:
                SetAndExecute (cmd);
                return Lst.QOpW;

            case PickActionEnum.HotQOp:
                SetAndExecute (cmd);
                return Lst.HotQOpW;

            case PickActionEnum.Sync:
                SetAndExecute (cmd);
                return Lst.SyncW;

            case PickActionEnum.Wait:
                SetAndExecute (cmd);
                return Lst.IdleW;

            case PickActionEnum.FSync:
                DoFSync ();
                return Lst.FSyncW;

            default:
                NcAssert.CaseError (cmd.ToString ());
                return Lst.IdleW;
            }
        }

        private void DoSync ()
        {
            AsSyncCommand cmd = null;
            var syncKit = Strategy.GenSyncKit (AccountId, ProtocolState);
            if (null != syncKit) {
                cmd = new AsSyncCommand (this, syncKit);
            } else {
                // Something is wrong. Do a FolderSync, and hope it gets better.
                Log.Error (Log.LOG_AS, "DoSync: got a null SyncKit.");
                Sm.PostEvent ((uint)CtlEvt.E.ReFSync, "PCFSYNCNULL");
                return;
            }
            SetCmd (cmd);
            ExecuteCmd ();
        }

        private void SetAndExecute (AsCommand cmd)
        {
            if (null != cmd as AsPingCommand && null != PushAssist) {
                PushAssist.Execute ();
            }
            SetCmd (cmd);
            ExecuteCmd ();
        }

        private void DoPark ()
        {
            if (null != PushAssist) {
                PushAssist.Park ();
            }
            SetCmd (null);
            // Because we are going to stop for a while, we need to fail any
            // pending that aren't allowed to be delayed.
            McPending.ResolveAllDelayNotAllowedAsFailed (ProtoControl, Account.Id);
        }

        private void DoDrive ()
        {
            if (null != PushAssist) {
                if (PushAssist.IsStartOrParked ()) {
                    PushAssist.Execute ();
                }
            }
            switch (ProtocolState.ProtoControlState) {
            case (uint)Lst.UiCertOkW:
            case (uint)Lst.UiDCrdW:
            case (uint)Lst.UiServConfW:
                Sm.State = (uint)Lst.DiscW;
                break;
            default:
                Sm.State = ProtocolState.ProtoControlState;
                break;
            }
            Sm.PostEvent ((uint)SmEvt.E.Launch, "DRIVE");
        }

        private bool CmdIs (Type cmdType)
        {
            return (null != Cmd && Cmd.GetType () == cmdType);
        }

        private void SetCmd (INcCommand nextCmd)
        {
            if (null != Cmd) {
                Cmd.Cancel ();
            }
            Cmd = nextCmd;
        }

        private void ExecuteCmd ()
        {
            if (null != PushAssist) {
                if (PushAssist.IsStartOrParked ()) {
                    PushAssist.Execute ();
                }
            }
            Cmd.Execute (Sm);
        }

        public override void ForceStop ()
        {
            if (null != PushAssist) {
                PushAssist.Park ();
            }
            Sm.PostEvent ((uint)PcEvt.E.Park, "FORCESTOP");
        }

        public override void ValidateConfig (McServer server, McCred cred)
        {
            CancelValidateConfig ();
            Validator = new AsValidateConfig (this);
            Validator.Execute (server, cred);
        }

        public override void CancelValidateConfig ()
        {
            if (null != Validator) {
                Validator.Cancel ();
                Validator = null;
            }
        }

        public void ServerStatusEventHandler (Object sender, NcCommStatusServerEventArgs e)
        {
            if (e.ServerId == Server.Id) {
                switch (e.Quality) {
                case NcCommStatus.CommQualityEnum.OK:
                    Log.Info (Log.LOG_AS, "Server {0} communication quality OK.", Server.Host);
                    Execute ();
                    break;

                default:
                case NcCommStatus.CommQualityEnum.Degraded:
                    Log.Info (Log.LOG_AS, "Server {0} communication quality degraded.", Server.Host);
                    break;

                case NcCommStatus.CommQualityEnum.Unusable:
                    Log.Info (Log.LOG_AS, "Server {0} communication quality unusable.", Server.Host);
                    Sm.PostEvent ((uint)PcEvt.E.Park, "SSEHPARK");
                    break;
                }
            }
        }

        public void NetStatusEventHandler (Object sender, NetStatusEventArgs e)
        {
            if (NachoPlatform.NetStatusStatusEnum.Up == e.Status) {
                Execute ();
            } else {
                // The "Down" case.
                Sm.PostEvent ((uint)PcEvt.E.Park, "NSEHPARK");
            }
        }

        public void StatusIndEventHandler (Object sender, EventArgs ea)
        {
            var siea = (StatusIndEventArgs)ea;
            if (null == siea.Account || siea.Account.Id != AccountId) {
                return;
            }
            switch (siea.Status.SubKind) {
            case NcResult.SubKindEnum.Info_DaysToSyncChanged:
                if (Xml.Provision.MaxAgeFilterCode.SyncAll_0 == Account.DaysToSyncEmail) {
                    foreach (var folder in McFolder.QueryByAccountId<McFolder>(AccountId)) {
                        folder.UpdateSet_AsSyncMetaToClientExpected (true);
                    }
                    Sm.PostEvent ((uint)SmEvt.E.Launch, "ASDAYS2SYNC");
                }
                break;
            }
        }

        // PushAssist support.
        public PushAssistParameters PushAssistParameters ()
        {
            var pingKit = Strategy.GenPingKit (AccountId, ProtocolState, true, false, true);
            if (null == pingKit) {
                return null; // should never happen
            }
            var ping = new AsPingCommand (this, pingKit);
            if (null == ping) {
                return null; // should never happen
            }
            return new NachoCore.PushAssistParameters () {
                RequestUrl = ping.PushAssistRequestUrl (),
                Protocol = PushAssistProtocol.ACTIVE_SYNC,
                ResponseTimeoutMsec = (int)pingKit.MaxHeartbeatInterval * 1000,
                WaitBeforeUseMsec = 60 * 1000,

                MailServerCredentials = new Credentials {
                    Username = ProtoControl.Cred.Username,
                    Password = ProtoControl.Cred.GetPassword ()
                },
                RequestData = ping.PushAssistRequestData (),
                RequestHeaders = ping.PushAssistRequestHeaders (),
                ContentHeaders = ping.PushAssistContentHeaders (),
                NoChangeResponseData = ping.PushAssistResponseData (),
            };
        }
    }
}
