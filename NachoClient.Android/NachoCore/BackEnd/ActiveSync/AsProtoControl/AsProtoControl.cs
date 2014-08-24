// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.ActiveSync
{
    public partial class AsProtoControl : ProtoControl, IBEContext
    {
        private IAsCommand Cmd;
        private AsValidateConfig Validator;
        #pragma warning disable 414
        private IAsCommand DisposedCmd;
        #pragma warning restore 414

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
            FSync2W, // same as FSyncW, but will try to Sync on Success.
            Pick,
            SyncW,
            PingW,
            QOpW,
            FetchW,
            IdleW,
        };

        public override BackEndAutoDStateEnum AutoDState {
            get {
                // Every state above must be mapped here.
                switch (Sm.State) {
                case (uint)Lst.DiscW:
                    return BackEndAutoDStateEnum.Running;

                case (uint)Lst.UiDCrdW:
                case (uint)Lst.UiPCrdW:
                    return BackEndAutoDStateEnum.CredWait;

                case (uint)Lst.UiServConfW:
                    return BackEndAutoDStateEnum.ServerConfWait;

                case (uint)Lst.UiCertOkW:
                    return BackEndAutoDStateEnum.CertAskWait;

                case (uint)Lst.OptW:
                case (uint)Lst.ProvW:
                case (uint)Lst.SettingsW:
                case (uint)Lst.FSyncW:
                case (uint)Lst.FSync2W: 
                case (uint)Lst.Pick:
                case (uint)Lst.SyncW:
                case (uint)Lst.PingW:
                case (uint)Lst.QOpW:
                case (uint)Lst.FetchW:
                case (uint)Lst.IdleW:
                     return (ProtocolState.HasSyncedInbox) ? 
                        BackEndAutoDStateEnum.PostAutoDPostInboxSync : 
                        BackEndAutoDStateEnum.PostAutoDPreInboxSync;

                default:
                    NcAssert.CaseError (string.Format ("Unhandled state {0}", Sm.State));
                    return BackEndAutoDStateEnum.PostAutoDPostInboxSync;
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
        public class AsEvt : SmEvt
        {
            new public enum E : uint
            {
                ReDisc = (SmEvt.E.Last + 1),
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
                PendQ,
                PendQHot,
                ReFSync,
                PkPing,
                PkQOop,
                PkFetch,
                PkWait,
            };
        }

        public AsProtoControl ProtoControl { set; get; }

        public IAsStrategy SyncStrategy { set; get; }

        private NcTimer PendingOnTimeTimer { set; get; }

        public AsProtoControl (IProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            EstablishService ();
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
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.PendQHot,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.UiCertOkNo, 
                            (uint)CtlEvt.E.UiCertOkYes,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },

                    new Node {
                        // There is no HardFail. Can't pass DiscW w/out a working server - period.
                        State = (uint)Lst.DiscW, 
                        Drop = new [] {
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.PendQHot,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
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
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.PendQHot,
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
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
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
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.PendQHot,
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
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW
                            },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)CtlEvt.E.UiSetCred, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)CtlEvt.E.UiSetServConf, Act = DoSetServConf, State = (uint)Lst.ProvW
                            },
                        }
                    },

                    new Node {
                        State = (uint)Lst.UiServConfW,
                        Drop = new [] {
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.PendQHot,
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
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
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
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.PendQHot,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.UiSetCred, // TODO: should we re-consider?
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
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
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.PendQHot,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoOpt, State = (uint)Lst.OptW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoOldProtoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoOpt, State = (uint)Lst.OptW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.ProvW,
                        Drop = new [] {
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.PendQHot,
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
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSettings, State = (uint)Lst.SettingsW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.SettingsW,
                        Drop = new [] {
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.PendQHot,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSettings, State = (uint)Lst.SettingsW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoSettings, State = (uint)Lst.SettingsW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSettings, State = (uint)Lst.SettingsW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.FSyncW,
                        Drop = new [] {
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.PendQHot,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoFSync, State = (uint)Lst.FSyncW },
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
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.PendQHot,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoFSync, State = (uint)Lst.FSync2W },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoFSync, State = (uint)Lst.FSync2W },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoFSync, State = (uint)Lst.FSync2W },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSync2W },
                        }
                    },

                    new Node {
                        State = (uint)Lst.Pick,
                        Drop = new [] {
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.PendQHot,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)CtlEvt.E.PkQOop, Act = DoArg, State = (uint)Lst.QOpW },
                            new Trans { Event = (uint)CtlEvt.E.PkFetch, Act = DoArg, State = (uint)Lst.FetchW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)CtlEvt.E.PkPing, Act = DoArg, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)CtlEvt.E.PkWait, Act = DoArg, State = (uint)Lst.IdleW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.SyncW,
                        Drop = new [] {
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.PendQHot, Act = DoPick, State = (uint)Lst.Pick },
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
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.PendQ, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)CtlEvt.E.PendQHot, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.QOpW,
                        Drop = new [] {
                            (uint)CtlEvt.E.PendQ,
                            // TODO: we should prioritize a search command over in-process commands.
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.PendQHot, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.FetchW,
                        Drop = new [] {
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.PendQHot, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.IdleW,
                        Drop = new [] {
                            (uint)CtlEvt.E.PendQ,
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
                            (uint)CtlEvt.E.PkPing,
                            (uint)CtlEvt.E.PkQOop,
                            (uint)CtlEvt.E.PkFetch,
                            (uint)CtlEvt.E.PkWait,

                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)CtlEvt.E.PendQHot, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        }
                    },
                }
            };
            Sm.Validate ();
            Sm.State = ProtocolState.ProtoControlState;
            SyncStrategy = new AsStrategy (this);

            McPending.ResolveAllDispatchedAsDeferred (Account.Id);
            NcCommStatus.Instance.CommStatusNetEvent += NetStatusEventHandler;
            NcCommStatus.Instance.CommStatusServerEvent += ServerStatusEventHandler;
        }

        private void EstablishService ()
        {
            // Hang our records off Account.
            NcModel.Instance.RunInTransaction (() => {
                bool needUpdate = false;
                var account = Account;
                if (0 == account.PolicyId) {
                    var policy = new McPolicy ();
                    policy.Insert ();
                    account.PolicyId = policy.Id;
                    needUpdate = true;
                }
                if (0 == Account.ProtocolStateId) {
                    var protocolState = new McProtocolState ();
                    protocolState.Insert ();
                    account.ProtocolStateId = protocolState.Id;
                    needUpdate = true;
                }
                if (needUpdate) {
                    account.Update();
                }
            });

            // Make the application-defined folders.
            McFolder freshMade;
            NcModel.Instance.RunInTransaction (() => {
                if (null == McFolder.GetOutboxFolder (AccountId)) {
                    freshMade = McFolder.Create (AccountId, true, false, "0",
                        McFolder.ClientOwned_Outbox, "Device Outbox",
                        Xml.FolderHierarchy.TypeCode.UserCreatedMail_12);
                    freshMade.Insert ();
                }
            });
            NcModel.Instance.RunInTransaction (() => {
                if (null == McFolder.GetOutboxFolder (AccountId)) {
                    freshMade = McFolder.Create (AccountId, true, false, "0",
                        McFolder.ClientOwned_Drafts, "Device Drafts",
                        Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1);
                    freshMade.Insert ();
                }
            });
            NcModel.Instance.RunInTransaction (() => {
                if (null == McFolder.GetGalCacheFolder (AccountId)) {
                    freshMade = McFolder.Create (AccountId, true, true, "0",
                        McFolder.ClientOwned_GalCache, string.Empty,
                        Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14);
                    freshMade.Insert ();
                }
            });
            NcModel.Instance.RunInTransaction (() => {
                if (null == McFolder.GetGleanedFolder (AccountId)) {
                    freshMade = McFolder.Create (AccountId, true, true, "0",
                        McFolder.ClientOwned_Gleaned, string.Empty,
                        Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14);
                    freshMade.Insert ();
                }
            });
            NcModel.Instance.RunInTransaction (() => {
                if (null == McFolder.GetLostAndFoundFolder (AccountId)) {
                    freshMade = McFolder.Create (AccountId, true, true, "0",
                        McFolder.ClientOwned_LostAndFound, string.Empty,
                        Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1);
                    freshMade.Insert ();
                }
            });
        }

        // Methods callable by the owner.
        public override void Execute ()
        {
            if (NachoPlatform.NetStatusStatusEnum.Up != NcCommStatus.Instance.Status) {
                Log.Warn (Log.LOG_AS, "Execute called while network is down.");
                return;
            }
            PendingOnTimeTimer = new NcTimer ("AsProtoControl", state => {
                McPending.MakeEligibleOnTime (Account.Id);
            }, null, 1000, 2000);
            PendingOnTimeTimer.Stfu = true;

            // There isn't really a way to tell whether we are executing currently or not!
            // All states are required to handle the Launch event gracefully, so we just send it.
            Sm.PostEvent ((uint)SmEvt.E.Launch, "ASPCEXE");
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
                Server = NcModel.Instance.Db.Table<McServer> ().Single (rec => rec.Id == Account.ServerId);
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
            var protocolState = ProtocolState;
            uint stateToSave = Sm.State;
            switch (stateToSave) {
            case (uint)Lst.UiDCrdW:
            case (uint)Lst.UiServConfW:
            case (uint)Lst.UiCertOkW:
                stateToSave = (uint)Lst.DiscW;
                break;
            }
            protocolState.ProtoControlState = stateToSave;
            protocolState.Update ();
        }
        // State-machine action methods.
        private void DoNop ()
        {
        }

        private void DoUiServConfReq ()
        {
            // Send the request toward the UI.
            Owner.ServConfReq (this);
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
            // Send the request toward the UI.
            if (null != Cmd && !CmdIs (typeof(AsAutodiscoverCommand))) {
                Cmd.Cancel ();
            }
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
            Cmd.Execute (Sm);
        }

        private void DoOpt ()
        {
            SetCmd (new AsOptionsCommand (this));
            Cmd.Execute (Sm);
        }

        private void DoProv ()
        {
            SetCmd (new AsProvisionCommand (this));
            Cmd.Execute (Sm);
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
            Cmd.Execute (Sm);
        }

        private void DoFSync ()
        {
            SetCmd (new AsFolderSyncCommand (this));
            Cmd.Execute (Sm);
        }

        private void DoPick ()
        {
            // Stop any executing command await.
            // Due to threading race condition we must clear any event possibly posted
            // by a non-cancelled-in-time await.
            StopCurrentOp ();
            Sm.ClearEventQueue ();
            var pack = SyncStrategy.Pick ();
            var transition = pack.Item1;
            var cmd = pack.Item2;
            switch (transition) {
            case PickActionEnum.Fetch:
                Sm.PostEvent ((uint)CtlEvt.E.PkFetch, "PCKFETCH", cmd);
                break;

            case PickActionEnum.Ping:
                Sm.PostEvent ((uint)CtlEvt.E.PkPing, "PCKPING", cmd);
                break;

            case PickActionEnum.QOop:
                Sm.PostEvent ((uint)CtlEvt.E.PkQOop, "PCKQOP", cmd);
                break;

            case PickActionEnum.Sync:
                Sm.PostEvent ((uint)AsEvt.E.ReSync, "PCKSYNC", cmd);
                break;

            case PickActionEnum.Wait:
                Sm.PostEvent ((uint)CtlEvt.E.PkWait, "PCKWAIT", cmd);
                break;

            default:
                NcAssert.CaseError (cmd.ToString ());
                break;
            }
        }

        private void DoSync ()
        {
            var cmd = Sm.Arg as AsCommand;
            if (null == cmd) {
                Log.Info (Log.LOG_AS, "DoSync: not from Pick.");
                cmd = new AsSyncCommand (this, SyncStrategy.GenSyncKit (AccountId, ProtocolState, true));
            }
            SetCmd (cmd);
            Cmd.Execute (Sm);
        }

        private void DoArg ()
        {
            var cmd = Sm.Arg as AsCommand;
            SetCmd (cmd);
            Cmd.Execute (Sm);
        }

        private bool CmdIs (Type cmdType)
        {
            return (null != Cmd && Cmd.GetType () == cmdType);
        }

        private void SetCmd (IAsCommand nextCmd)
        {
            DisposedCmd = Cmd;
            if (null != DisposedCmd) {
                DisposedCmd.Cancel ();
            }
            Cmd = nextCmd;
        }

        public override void ForceStop ()
        {
            StopCurrentOp ();
            if (null != PendingOnTimeTimer) {
                PendingOnTimeTimer.Dispose ();
                PendingOnTimeTimer = null;
            }
        }

        public void StopCurrentOp ()
        {
            SetCmd (null);
        }

        public override void QuickSync ()
        {
            StopCurrentOp ();
            var defaultInbox = McFolder.GetDefaultInboxFolder (Account.Id);
            if (null != defaultInbox) {
                defaultInbox.AsSyncMetaToClientExpected = true;
                defaultInbox.Update ();
            }
            var defaultCal = McFolder.GetDefaultCalendarFolder (Account.Id);
            if (null != defaultCal) {
                defaultCal.AsSyncMetaToClientExpected = true;
                defaultCal.Update ();
            }
            if (null == defaultInbox && null == defaultCal) {
                Log.Info (Log.LOG_AS, "QuickSync called before initial account Sync - ignoring.");
                return;
            }
            if (NachoPlatform.NetStatusStatusEnum.Up != NcCommStatus.Instance.Status) {
                Log.Warn (Log.LOG_AS, "QuickSync called while network is down.");
                return;
            }
            // Don't need to call McFolder.AsSetExpected() - see above!
            Sm.PostEvent ((uint)AsEvt.E.ReSync, "ASPCFORCESYNC");
        }

        public override void Cancel (string token)
        {
            // FIXME - need lock to ensure that pending state does not change while in this function.
            var pendings = McPending.QueryByToken (Account.Id, token);
            foreach (var pending in pendings) {
                switch (pending.State) {
                case McPending.StateEnum.Eligible:
                // FIXME - may need to deal with successors.
                    pending.Delete ();
                    break;

                case McPending.StateEnum.Deferred:
                case McPending.StateEnum.Failed:
                case McPending.StateEnum.PredBlocked:
                case McPending.StateEnum.UserBlocked:
                    if (McPending.Operations.ContactSearch == pending.Operation) {
                        McPending.ResolvePendingSearchReqs (Account.Id, token, false);
                    } else {
                        pending.ResolveAsCancelled ();
                    }
                    break;

                case McPending.StateEnum.Dispatched:
                    if (null != Cmd) {
                        // Command Cancel moves state to Deferred. Maybe many pending objs.
                        Cmd.Cancel ();
                    }
                // FIXME - command should cancel deferred pending.
                    pending.ResolveAsCancelled (false);
                // Don't REALLY know that we killed it before the server saw it.
                    break;

                case McPending.StateEnum.Deleted:
                // Nothing to do.
                    break;

                default:
                    throw new Exception (string.Format ("Unknown State {0}", pending.State));
                }
            }
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
                    Log.Info (Log.LOG_AS, "Server {0} communication quality degrated.", Server.Host);
                    break;

                case NcCommStatus.CommQualityEnum.Unusable:
                    Log.Info (Log.LOG_AS, "Server {0} communication quality unusable.", Server.Host);
                    StopCurrentOp ();
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
                StopCurrentOp ();
            }
        }
    }
}
