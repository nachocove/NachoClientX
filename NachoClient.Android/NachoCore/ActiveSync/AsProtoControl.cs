// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NachoCore.Model;
using NachoCore.Utils;

// FIXME - we can't recover from the Stop state, so we should never go to it.
// If we need to wait for ui-ack after hardfail, then have a wait-state.
namespace NachoCore.ActiveSync
{
    public class AsProtoControl : ProtoControl, IAsDataSource
    {
        private IAsCommand Cmd;

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
            SyncW,
            PingW,
            SendMailW,
            DnldAttW,
            SrchW,
        };
        // If you're exposed to AsHttpOperation, you need to cover these.
        public class AsEvt : SmEvt
        {
            new public enum E : uint
            {
                ReDisc = (SmEvt.E.Last + 1),
                ReProv,
                ReSync,
                AuthFail,
                Last = AuthFail}
            ;
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
                SendMail,
                DnldAtt,
                UiSearch,
            };
        }

        public AsProtoControl Control { set; get; }

        public AsProtoControl (IProtoControlOwner owner, McAccount account)
        {
            Control = this;
            Owner = owner;
            AccountId = account.Id;

            Sm = new StateMachine () { 
                Name = string.Format ("ASPC({0})", AccountId),
                LocalEventType = typeof(CtlEvt),
                LocalStateType = typeof(Lst),
                StateChangeIndication = UpdateSavedState,
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Drop = new [] {(uint)CtlEvt.E.SendMail, (uint)CtlEvt.E.UiSetCred, (uint)CtlEvt.E.UiSetServConf, (uint)CtlEvt.E.UiCertOkNo, 
                            (uint)CtlEvt.E.UiCertOkYes, (uint)CtlEvt.E.UiSearch
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.ReSync,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },

                    new Node {
                        // NOTE: There is no HardFail. Can't pass DiscW w/out a working server - period.
                        State = (uint)Lst.DiscW, 
                        Drop = new [] {
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes, (uint)CtlEvt.E.UiSearch
                        },
                        Invalid = new [] {(uint)SmEvt.E.HardFail,
                            (uint)AsEvt.E.ReDisc, (uint)AsEvt.E.ReProv, (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.ReFSync, (uint)CtlEvt.E.DnldAtt
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoOpt, State = (uint)Lst.OptW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans {
                                Event = (uint)AsEvt.E.AuthFail,
                                Act = DoUiCredReq,
                                State = (uint)Lst.UiDCrdW
                            },
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSearch
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.ReSync,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch, Act = DoUiCredReq, State = (uint)Lst.UiDCrdW
                            },
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSearch
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.ReSync,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW
                            },
                            new Trans { Event = (uint)CtlEvt.E.UiSetCred, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)CtlEvt.E.UiSetServConf, Act = DoSetServConf, State = (uint)Lst.ProvW
                            },
                        }
                    },

                    new Node {
                        State = (uint)Lst.UiServConfW,
                        Drop = new [] {
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSearch
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.ReSync,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiSearch
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.ReSync,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.UiSearch
                        },
                        Invalid = new [] {
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.UiSearch
                        },
                        Invalid = new [] {
                            (uint)AsEvt.E.ReProv,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSettings, State = (uint)Lst.SettingsW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.SettingsW,
                        Drop = new [] {
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.UiSearch
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSettings, State = (uint)Lst.SettingsW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSettings, State = (uint)Lst.SettingsW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.FSyncW,
                        Drop = new [] {
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.UiSearch, Act = DoSearch, State = (uint)Lst.SrchW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.SyncW,
                        Drop = new [] {
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)CtlEvt.E.UiSearch, Act = DoSearch, State = (uint)Lst.SrchW },
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
                            (uint)CtlEvt.E.GetServConf
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)CtlEvt.E.SendMail, Act = DoSend, State = (uint)Lst.SendMailW },
                            new Trans { Event = (uint)CtlEvt.E.DnldAtt, Act = DoDnldAtt, State = (uint)Lst.DnldAttW },
                            new Trans { Event = (uint)CtlEvt.E.UiSearch, Act = DoSearch, State = (uint)Lst.SrchW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.SendMailW,
                        Drop = new [] {
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSend, State = (uint)Lst.SendMailW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSend, State = (uint)Lst.SendMailW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)CtlEvt.E.UiSearch, Act = DoSearch, State = (uint)Lst.SrchW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.DnldAttW,
                        Drop = new [] {
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDnldAtt, State = (uint)Lst.DnldAttW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoDnldAtt, State = (uint)Lst.DnldAttW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)CtlEvt.E.UiSearch, Act = DoSearch, State = (uint)Lst.SrchW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.SrchW,
                        Drop = new [] {
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.UiSearch
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf
                        },
                        On = new [] {
                            // NOTE: If we get re-launched in this state, the old search is stale. Go
                            // straight to ping.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSearch, State = (uint)Lst.SrchW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },                     
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        }
                    },
                }
            };
            Sm.Validate ();
            // FIXME - generate protocol state here. load it from DB or create & save to DB.
            Sm.State = ProtocolState.State;

            Log.Info (Log.LOG_STATE, "Initial state: {0}", Sm.State);

            var dispached = Owner.Db.Table<McPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                            rec.IsDispatched == true).ToList ();
            foreach (var update in dispached) {
                update.IsDispatched = false;
                Owner.Db.Update (BackEnd.DbActors.Proto, update);
            }
            McEventable.DbEvent += DbEventHandler;
        }
        // Methods callable by the owner.
        public override void Execute ()
        {
            Sm.PostAtMostOneEvent ((uint)SmEvt.E.Launch, "ASPCEXE");
        }

        public override void CredResp ()
        {
            Sm.PostAtMostOneEvent ((uint)CtlEvt.E.UiSetCred, "ASPCUSC");
        }

        public override void ServerConfResp ()
        {
            Server = Owner.Db.Table<McServer> ().Single (rec => rec.Id == Account.ServerId);
            Sm.PostAtMostOneEvent ((uint)CtlEvt.E.UiSetServConf, "ASPCUSSC");
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
            protocolState.State = Sm.State;
            Owner.Db.Update (BackEnd.DbActors.Proto, protocolState);
        }
        // State-machine action methods.
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
            if (null != Cmd && ! CmdIs (typeof(AsAutodiscoverCommand))) {
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
            Owner.CertAskReq (this, (X509Certificate2)Sm.Arg);
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

        private void DoUiHardFailInd ()
        {
            // FIXME Send the indication toward the UI.
        }

        private void DoDisc ()
        {
            Cmd = new AsAutodiscoverCommand (this);
            Cmd.Execute (Sm);
        }

        private void DoOpt ()
        {
            Cmd = new AsOptionsCommand (this);
            Cmd.Execute (Sm);
        }

        private void DoProv ()
        {
            Cmd = new AsProvisionCommand (this);
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
            Cmd = new AsSettingsCommand (this);
            Cmd.Execute (Sm);
        }

        private void DoFSync ()
        {
            Cmd = new AsFolderSyncCommand (this);
            Cmd.Execute (Sm);
        }

        private void DoSync ()
        {
            Cmd = new AsSyncCommand (this);
            Cmd.Execute (Sm);
        }

        private void DoSend ()
        {
            if (null != Cmd) {
                Cmd.Cancel ();
            }
            Cmd = new AsSendMailCommand (this);
            Cmd.Execute (Sm);
        }

        private void DoDnldAtt ()
        {
            Cmd = new AsItemOperationsCommand (this);
            Cmd.Execute (Sm);
        }

        private void DoPing ()
        {
            // Handle the pending updates in priority order, or if none then Ping & wait.
            if (0 < Owner.Db.Table<McPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                rec.DataType == McPendingUpdate.DataTypes.Contact &&
                rec.Operation == McPendingUpdate.Operations.Search).Count ()) {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.UiSearch, "ASPCDP0");
            } else if (0 < Owner.Db.Table<McPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                       rec.DataType == McPendingUpdate.DataTypes.EmailMessage &&
                       rec.Operation == McPendingUpdate.Operations.Send).Count ()) {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.SendMail, "ASPCDP1");
            } else if (0 < Owner.Db.Table<McPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                       rec.DataType == McPendingUpdate.DataTypes.Attachment &&
                       rec.Operation == McPendingUpdate.Operations.Download).Count ()) {
                Sm.PostEvent ((uint)CtlEvt.E.DnldAtt, "ASPCDP2");
            } else if (0 < Owner.Db.Table<McPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                       rec.DataType == McPendingUpdate.DataTypes.EmailMessage &&
                       rec.Operation == McPendingUpdate.Operations.Delete).Count ()) {
                Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCDP3");
            } else {
                Cmd = new AsPingCommand (this);
                Cmd.Execute (Sm);
            }
        }

        private void DoSearch ()
        {
            if (null != Cmd) {
                Cmd.Cancel ();
            }
            Cmd = new AsSearchCommand (this);
            Cmd.Execute (Sm);
        }

        private bool CmdIs (Type cmdType)
        {
            return (null != Cmd && Cmd.GetType () == cmdType);
        }
        // Methods that inject-into/delete-from the Q.
        private void DbEventHandler (BackEnd.DbActors dbActor, BackEnd.DbEvents dbEvent, McEventable target, EventArgs e)
        {
            if (BackEnd.DbActors.Proto == dbActor || target.AccountId != Account.Id) {
                return;
            }
            switch (target.GetType ().Name) {
            case McEmailMessage.ClassName:
                McEmailMessage emailMessage = (McEmailMessage)target;
                switch (dbEvent) {
                case BackEnd.DbEvents.WillDelete:
                    if (emailMessage.IsAwatingSend) {
                        /* UI is deleting a to-be-sent message. Cancel send by deleting
                         * The pending update if possible.
                         */
                        var existingUpdate = Owner.Db.Table<McPendingUpdate> ().Single (rec => rec.AccountId == Account.Id &&
                                             rec.EmailMessageId == emailMessage.Id);
                        if (!existingUpdate.IsDispatched) {
                            Owner.Db.Delete (BackEnd.DbActors.Proto, existingUpdate);
                        }
                        Owner.Db.Delete (BackEnd.DbActors.Proto, existingUpdate);
                    } else {
                        // UI is deleting a message. We need to delete it on the server.
                        var deleUpdate = new McPendingUpdate () {
                            AccountId = Account.Id,
                            Operation = McPendingUpdate.Operations.Delete,
                            DataType = McPendingUpdate.DataTypes.EmailMessage,
                            FolderId = emailMessage.FolderId,
                            ServerId = emailMessage.ServerId
                        };
                        Owner.Db.Insert (BackEnd.DbActors.Proto, deleUpdate);
                        Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCDELMSG");
                    }
                    break;
                case BackEnd.DbEvents.DidWrite:
                    if (emailMessage.IsAwatingSend) {
                        var sendUpdate = new McPendingUpdate () {
                            AccountId = Account.Id,
                            Operation = McPendingUpdate.Operations.Send,
                            DataType = McPendingUpdate.DataTypes.EmailMessage,
                            EmailMessageId = emailMessage.Id
                        };
                        Owner.Db.Insert (BackEnd.DbActors.Proto, sendUpdate);
                        Sm.PostAtMostOneEvent ((uint)CtlEvt.E.SendMail, "ASPCSEND");
                    }
                    break;
                }
                break;
            }
        }

        private void DeletePendingSearchReqs (string token, bool ignoreDispatched)
        {
            var query = Owner.Db.Table<McPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                        rec.Token == token);
            if (ignoreDispatched) {
                query = query.Where (rec => false == rec.IsDispatched);
            }
            var killList = query.ToList ();
            foreach (var kill in killList) {
                Owner.Db.Delete (BackEnd.DbActors.Proto, kill);
            }
        }

        public override string StartSearchContactsReq (string prefix, uint? maxResults)
        {
            var token = Guid.NewGuid ().ToString ();
            SearchContactsReq (prefix, maxResults, token);
            return token;
        }

        public override void SearchContactsReq (string prefix, uint? maxResults, string token)
        {
            DeletePendingSearchReqs (token, true);
            var newSearch = new McPendingUpdate () {
                AccountId = Account.Id,
                Operation = McPendingUpdate.Operations.Search,
                DataType = McPendingUpdate.DataTypes.Contact,
                Prefix = prefix,
                MaxResults = (null == maxResults) ? 0 : (uint)maxResults,
                Token = token
            };
            Owner.Db.Insert (BackEnd.DbActors.Proto, newSearch);
            Sm.PostAtMostOneEvent ((uint)CtlEvt.E.UiSearch, "ASPCSRCH");
        }

        public override void CancelSearchContactsReq (string token)
        {
            DeletePendingSearchReqs (token, false);
            if (CmdIs (typeof(AsSearchCommand))) {
                Cmd.Cancel ();
            }
        }

        public override void StatusInd (NcResult status)
        {
            Owner.StatusInd (this, status);
        }

        public override void StatusInd (NcResult status, string[] tokens)
        {
            Owner.StatusInd (this, status, tokens);
        }
    }
}

