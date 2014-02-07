// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsProtoControl : ProtoControl, IAsDataSource
    {
        private IAsCommand Cmd;
        private IAsCommand DisposedCmd;

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
            MoveW,
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
                Move,
            };
        }

        public AsProtoControl Control { set; get; }

        public AsProtoControl (IProtoControlOwner owner, int accountId)
        {
            Control = this;
            Owner = owner;
            AccountId = accountId;

            Sm = new NcStateMachine () { 
                Name = string.Format ("ASPC({0})", AccountId),
                LocalEventType = typeof(CtlEvt),
                LocalStateType = typeof(Lst),
                StateChangeIndication = UpdateSavedState,
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Drop = new [] {
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.SendMail, (uint)CtlEvt.E.UiSetCred, (uint)CtlEvt.E.UiSetServConf, (uint)CtlEvt.E.UiCertOkNo, 
                            (uint)CtlEvt.E.UiCertOkYes, (uint)CtlEvt.E.UiSearch
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
                            (uint)AsEvt.E.AuthFail,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.Move,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },

                    new Node {
                        // NOTE: There is no HardFail. Can't pass DiscW w/out a working server - period.
                        State = (uint)Lst.DiscW, 
                        Drop = new [] {
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSearch,
                            (uint)CtlEvt.E.Move,
                        },
                        Invalid = new [] {(uint)SmEvt.E.HardFail,
                            (uint)AsEvt.E.ReDisc, (uint)AsEvt.E.ReProv,
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
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSearch,
                            (uint)CtlEvt.E.Move,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
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
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSearch,
                            (uint)CtlEvt.E.Move,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
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
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSearch,
                            (uint)CtlEvt.E.Move,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
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
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiSearch,
                            (uint)CtlEvt.E.Move,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
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
                            (uint)CtlEvt.E.UiSearch,
                            (uint)CtlEvt.E.Move,
                        },
                        Invalid = new [] {
                            (uint)AsEvt.E.ReDisc,
                            (uint)AsEvt.E.ReProv,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf,
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
                            (uint)CtlEvt.E.UiSearch,
                            (uint)CtlEvt.E.Move,
                        },
                        Invalid = new [] {
                            (uint)AsEvt.E.ReProv,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf,
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.UiSearch,
                            (uint)CtlEvt.E.Move,
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
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.Move,
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
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoFSync, State = (uint)Lst.FSyncW },
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
                            (uint)CtlEvt.E.Move,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoSync, State = (uint)Lst.SyncW },
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
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)CtlEvt.E.SendMail, Act = DoSend, State = (uint)Lst.SendMailW },
                            new Trans { Event = (uint)CtlEvt.E.DnldAtt, Act = DoDnldAtt, State = (uint)Lst.DnldAttW },
                            new Trans { Event = (uint)CtlEvt.E.UiSearch, Act = DoSearch, State = (uint)Lst.SrchW },
                            new Trans { Event = (uint)CtlEvt.E.Move, Act = DoMove, State = (uint)Lst.MoveW },
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
                            (uint)CtlEvt.E.Move,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSend, State = (uint)Lst.SendMailW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoSend, State = (uint)Lst.SendMailW },
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
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.Move,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDnldAtt, State = (uint)Lst.DnldAttW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoDnldAtt, State = (uint)Lst.DnldAttW },
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
                            (uint)CtlEvt.E.UiSearch,
                            (uint)CtlEvt.E.Move,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf,
                        },
                        On = new [] {
                            // NOTE: If we get re-launched in this state, the old search is stale. Go
                            // straight to ping.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoSearch, State = (uint)Lst.SrchW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSearch, State = (uint)Lst.SrchW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },                     
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.MoveW,
                        Drop = new [] {
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.Move,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.GetServConf
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoMove, State = (uint)Lst.MoveW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoMove, State = (uint)Lst.MoveW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoMove, State = (uint)Lst.MoveW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)CtlEvt.E.UiSearch, Act = DoSearch, State = (uint)Lst.SrchW },
                        }
                    },
                }
            };
            Sm.Validate ();
            // FIXME - generate protocol state here. load it from DB or create & save to DB.
            Sm.State = ProtocolState.State;

            Log.Info (Log.LOG_STATE, "Initial state: {0}", Sm.State);

            var dispached = BackEnd.Instance.Db.Table<McPending> ().Where (rec => rec.AccountId == Account.Id &&
                            rec.IsDispatched == true).ToList ();
            foreach (var update in dispached) {
                update.IsDispatched = false;
                update.Update ();
            }
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
            Server = BackEnd.Instance.Db.Table<McServer> ().Single (rec => rec.Id == Account.ServerId);
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
            protocolState.Update ();
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

        private void DoSync ()
        {
            ForceStop ();
            SetCmd (new AsSyncCommand (this));
            Cmd.Execute (Sm);
        }

        private void DoSend ()
        {
            ForceStop ();
            SetCmd (new AsSendMailCommand (this));
            Cmd.Execute (Sm);
        }

        private void DoDnldAtt ()
        {
            SetCmd (new AsItemOperationsCommand (this));
            Cmd.Execute (Sm);
        }

        private void DoMove ()
        {
            SetCmd (new AsMoveItemsCommand (this));
            Cmd.Execute (Sm);
        }

        private void DoPing ()
        {
            if (!FirePendingInstead ()) {
                SetCmd (new AsPingCommand (this));
                Cmd.Execute (Sm);
            }
        }

        private void DoSearch ()
        {
            ForceStop ();
            SetCmd (new AsSearchCommand (this));
            Cmd.Execute (Sm);
        }

        private bool FirePendingInstead ()
        {
            var pending = BackEnd.Instance.Db.Table<McPending> ().Where (rec => rec.AccountId == Account.Id).OrderBy (x => x.Id);
            if (0 < pending.Count ()) {
                var next = pending.First ();
                switch (next.Operation) {
                case McPending.Operations.ContactSearch:
                    Sm.PostAtMostOneEvent ((uint)CtlEvt.E.UiSearch, "ASPCDP0");
                    return true;

                case McPending.Operations.EmailSend:
                    Sm.PostAtMostOneEvent ((uint)CtlEvt.E.SendMail, "ASPCDP1");
                    return true;

                case McPending.Operations.EmailMove:
                    Sm.PostEvent ((uint)CtlEvt.E.Move, "ASPCDPM");
                    return true;

                case McPending.Operations.AttachmentDownload:
                    Sm.PostEvent ((uint)CtlEvt.E.DnldAtt, "ASPCDP2");
                    return true;

                case McPending.Operations.EmailDelete:
                case McPending.Operations.EmailMarkRead:
                case McPending.Operations.EmailSetFlag:
                case McPending.Operations.EmailClearFlag:
                case McPending.Operations.EmailMarkFlagDone:
                    Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCPDIRS");
                    return true;
                }
            }
            return false;
        }

        private bool CmdIs (Type cmdType)
        {
            return (null != Cmd && Cmd.GetType () == cmdType);
        }

        private void SetCmd (IAsCommand nextCmd)
        {
            DisposedCmd = Cmd;
            Cmd = nextCmd;
        }

        private void DeletePendingSearchReqs (string token, bool ignoreDispatched)
        {
            var query = BackEnd.Instance.Db.Table<McPending> ().Where (rec => rec.AccountId == Account.Id &&
                        rec.Token == token);
            if (ignoreDispatched) {
                query = query.Where (rec => false == rec.IsDispatched);
            }
            var killList = query.ToList ();
            foreach (var kill in killList) {
                kill.Delete ();
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
            var newSearch = new McPending (Account.Id) {
                Operation = McPending.Operations.ContactSearch,
                Prefix = prefix,
                MaxResults = (null == maxResults) ? 0 : (uint)maxResults,
                Token = token
            };
            newSearch.Insert ();
            Sm.PostAtMostOneEvent ((uint)CtlEvt.E.UiSearch, "ASPCSRCH");
        }

        public override void ForceStop ()
        {
            if (null != Cmd) {
                Cmd.Cancel ();
            }
        }

        public override void ForceSync ()
        {
            ForceStop ();
            var defaultInbox = BackEnd.Instance.Db.Table<McFolder> ().SingleOrDefault (x => x.Type == (uint)Xml.FolderHierarchy.TypeCode.DefaultInbox);
            if (null != defaultInbox) {
                defaultInbox.AsSyncRequired = true;
                defaultInbox.Update ();
            }
            Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCFORCESYNC");
        }

        public override bool Cancel (string token)
        {
            var update = BackEnd.Instance.Db.Table<McPending> ().SingleOrDefault (rec => rec.AccountId == Account.Id && rec.Token == token);
            if (null == update) {
                return false;
            }
            if (McPending.Operations.EmailSend == update.Operation) {
                // FIXME.
                return false;
            } else if (McPending.Operations.EmailDelete == update.Operation) {
                // FIXME.
                return false;
            } else if (McPending.Operations.ContactSearch == update.Operation) {
                DeletePendingSearchReqs (token, false);
                if (CmdIs (typeof(AsSearchCommand))) {
                    Cmd.Cancel ();
                }
                return true;
            }
            return false;
        }

        public override string SendEmailCmd (int emailMessageId)
        {
            var sendUpdate = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailSend,
                EmailMessageId = emailMessageId
            };
            sendUpdate.Insert ();
            Sm.PostAtMostOneEvent ((uint)CtlEvt.E.SendMail, "ASPCSEND");
            return sendUpdate.Token;
        }

        public override string DeleteEmailCmd (int emailMessageId)
        {
            var emailMessage = BackEnd.Instance.Db.Table<McEmailMessage> ().SingleOrDefault (x => emailMessageId == x.Id);
            if (null == emailMessage) {
                return null;
            }

            var folders = McFolder.QueryByItemId (Account.Id, emailMessageId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            var folder = folders.First ();

            var deleUpdate = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailDelete,
                FolderServerId = folder.ServerId,
                ServerId = emailMessage.ServerId
            };   
            deleUpdate.Insert ();

            // Delete the actual item.
            var maps = BackEnd.Instance.Db.Table<McMapFolderItem> ().Where (x =>
                x.AccountId == Account.Id &&
                       x.ItemId == emailMessageId &&
                       x.ClassCode == (uint)McItem.ClassCodeEnum.Email);

            foreach (var map in maps) {
                map.Delete ();
            }
            emailMessage.DeleteBody (BackEnd.Instance.Db);
            emailMessage.Delete ();
            Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCDELMSG");
            return deleUpdate.Token;
        }

        public override string MoveItemCmd (int emailMessageId, int destFolderId)
        {
            var emailMessage = BackEnd.Instance.Db.Table<McEmailMessage> ().SingleOrDefault (x => emailMessageId == x.Id);
            if (null == emailMessage) {
                return null;
            }
            var destFolder = BackEnd.Instance.Db.Table<McFolder> ().SingleOrDefault (x => destFolderId == x.Id);
            if (null == destFolder) {
                return null;
            }
            var srcFolders = McFolder.QueryByItemId (Account.Id, emailMessageId);
            if (null == srcFolders || 0 == srcFolders.Count) {
                return null;
            }
            var srcFolder = srcFolders.First ();
            var moveUpdate = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailMove,
                EmailMessageServerId = emailMessage.ServerId,
                EmailMessageId = emailMessageId,
                FolderServerId = srcFolder.ServerId,
                DestFolderServerId = destFolder.ServerId,
            };

            moveUpdate.Insert ();
            // Move the actual item.
            var newMapEntry = new McMapFolderItem (Account.Id) {
                FolderId = destFolderId,
                ItemId = emailMessageId,
                ClassCode = (uint)McItem.ClassCodeEnum.Email,
            };
            newMapEntry.Insert ();

            var oldMapEntry = BackEnd.Instance.Db.Table<McMapFolderItem> ().Single (x =>
                x.AccountId == Account.Id &&
                              x.ItemId == emailMessageId &&
                              x.FolderId == srcFolder.Id &&
                              x.ClassCode == (uint)McItem.ClassCodeEnum.Email);
            oldMapEntry.Delete ();

            Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCMOVMSG");
            return moveUpdate.Token;
        }

        private bool GetEmailMessageAndFolder (int emailMessageId, 
            out McEmailMessage emailMessage,
            out McFolder folder)
        {
            folder = null;
            emailMessage = BackEnd.Instance.Db.Table<McEmailMessage> ().SingleOrDefault (x => emailMessageId == x.Id);
            if (null == emailMessage) {
                return false;
            }

            var folders = McFolder.QueryByItemId (Account.Id, emailMessageId);
            if (null == folders || 0 == folders.Count) {
                return false;
            }

            folder = folders.First ();
            return true;
        }

        public override string MarkEmailReadCmd (int emailMessageId)
        {
            McEmailMessage emailMessage;
            McFolder folder;
            if (!GetEmailMessageAndFolder (emailMessageId, out emailMessage, out folder)) {
                return null;
            }

            var markUpdate = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailMarkRead,
                ServerId = emailMessage.ServerId,
                FolderServerId = folder.ServerId,
            };   
            markUpdate.Insert ();

            // Mark the actual item.
            emailMessage.IsRead = true;
            emailMessage.Update ();
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMarkedRead));
            Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCMRMSG");
            return markUpdate.Token;
        }

        public override string SetEmailFlagCmd (int emailMessageId, string flagMessage, DateTime utcStart, DateTime utcDue)
        {
            McEmailMessage emailMessage;
            McFolder folder;
            if (!GetEmailMessageAndFolder (emailMessageId, out emailMessage, out folder)) {
                return null;
            }

            var setFlag = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailSetFlag,
                ServerId = emailMessage.ServerId,
                FolderServerId = folder.ServerId,
                Message = flagMessage,
                UtcStart = utcStart,
                UtcDue = utcDue,
            };
            setFlag.Insert ();

            // Set the Flag info in the DB item.
            emailMessage.UtcDeferUntil = utcStart;
            emailMessage.UtcDue = utcDue;
            emailMessage.Update ();
            Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCSF");
            return setFlag.Token;
        }

        public override string ClearEmailFlagCmd (int emailMessageId)
        {
            McEmailMessage emailMessage;
            McFolder folder;
            if (!GetEmailMessageAndFolder (emailMessageId, out emailMessage, out folder)) {
                return null;
            }

            var clearFlag = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailClearFlag,
                ServerId = emailMessage.ServerId,
                FolderServerId = folder.ServerId,
            };
            clearFlag.Insert ();

            // Clear the Flag info in the DB item.
            emailMessage.UtcDeferUntil = DateTime.MinValue;
            emailMessage.UtcDue = DateTime.MinValue;
            emailMessage.Update ();
            Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCCF");
            return clearFlag.Token;
        }

        public override string MarkEmailFlagDone (int emailMessageId)
        {
            McEmailMessage emailMessage;
            McFolder folder;
            if (!GetEmailMessageAndFolder (emailMessageId, out emailMessage, out folder)) {
                return null;
            }

            var markFlagDone = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailMarkFlagDone,
                ServerId = emailMessage.ServerId,
                FolderServerId = folder.ServerId,
            };
            markFlagDone.Insert ();

            // Clear the Flag info in the DB item.
            emailMessage.UtcDeferUntil = DateTime.MinValue;
            emailMessage.UtcDue = DateTime.MinValue;
            emailMessage.Update ();
            Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCCF");
            return markFlagDone.Token;
        }

        public override string DnldAttCmd (int attId)
        {
            var att = BackEnd.Instance.Db.Table<McAttachment> ().SingleOrDefault (x => x.Id == attId);
            if (null == att || att.IsDownloaded) {
                return null;
            }
            var update = new McPending {
                Operation = McPending.Operations.AttachmentDownload,
                AccountId = AccountId,
                IsDispatched = false,
                AttachmentId = attId,
            };
            update.Insert ();
            att.PercentDownloaded = 1;
            att.Update ();
            Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCDNLDATT");
            return update.Token;
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

