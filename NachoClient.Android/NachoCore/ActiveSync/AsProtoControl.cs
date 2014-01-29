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

        public AsProtoControl (IProtoControlOwner owner, McAccount account)
        {
            Control = this;
            Owner = owner;
            AccountId = account.Id;

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

            var dispached = Owner.Db.Table<McPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                            rec.IsDispatched == true).ToList ();
            foreach (var update in dispached) {
                update.IsDispatched = false;
                Owner.Db.Update (update);
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
            Owner.Db.Update (protocolState);
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

        private void DoMove ()
        {
            Cmd = new AsMoveItemsCommand (this);
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
                       rec.DataType == McPendingUpdate.DataTypes.EmailMessage &&
                       rec.Operation == McPendingUpdate.Operations.Move).Count ()) {
                Sm.PostEvent ((uint)CtlEvt.E.Move, "ASPCDPM");
            } else if (0 < Owner.Db.Table<McPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                       rec.DataType == McPendingUpdate.DataTypes.Attachment &&
                       rec.Operation == McPendingUpdate.Operations.Download).Count ()) {
                Sm.PostEvent ((uint)CtlEvt.E.DnldAtt, "ASPCDP2");
            } else if (0 < Owner.Db.Table<McPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                       rec.DataType == McPendingUpdate.DataTypes.EmailMessage &&
                       rec.Operation == McPendingUpdate.Operations.Delete).Count ()) {
                Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCDP3");
            } else if (0 < Owner.Db.Table<McPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                       rec.DataType == McPendingUpdate.DataTypes.EmailMessage &&
                       rec.Operation == McPendingUpdate.Operations.MarkRead).Count ()) {
                Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCDP4");
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

        private void DeletePendingSearchReqs (string token, bool ignoreDispatched)
        {
            var query = Owner.Db.Table<McPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                        rec.Token == token);
            if (ignoreDispatched) {
                query = query.Where (rec => false == rec.IsDispatched);
            }
            var killList = query.ToList ();
            foreach (var kill in killList) {
                Owner.Db.Delete (kill);
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
            var newSearch = new McPendingUpdate (Account.Id) {
                Operation = McPendingUpdate.Operations.Search,
                DataType = McPendingUpdate.DataTypes.Contact,
                Prefix = prefix,
                MaxResults = (null == maxResults) ? 0 : (uint)maxResults,
                Token = token
            };
            Owner.Db.Insert (newSearch);
            Sm.PostAtMostOneEvent ((uint)CtlEvt.E.UiSearch, "ASPCSRCH");
        }

        public override void ForceSync ()
        {
            if (! CmdIs (typeof(AsSyncCommand))) {
                if (null != Cmd) {
                    Cmd.Cancel ();
                }
                Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCFORCESYNC");
            }
        }

        public override bool Cancel (string token)
        {
            var update = Owner.Db.Table<McPendingUpdate> ().SingleOrDefault (rec => rec.AccountId == Account.Id && rec.Token == token);
            if (null == update) {
                return false;
            }
            if (McPendingUpdate.Operations.Send == update.Operation && McPendingUpdate.DataTypes.EmailMessage == update.DataType) {
                // FIXME.
                return false;
            } else if (McPendingUpdate.Operations.Delete == update.Operation && McPendingUpdate.DataTypes.EmailMessage == update.DataType) {
                // FIXME.
                return false;
            } else if (McPendingUpdate.Operations.Search == update.Operation && McPendingUpdate.DataTypes.Contact == update.DataType) {
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
            var sendUpdate = new McPendingUpdate (Account.Id) {
                Operation = McPendingUpdate.Operations.Send,
                DataType = McPendingUpdate.DataTypes.EmailMessage,
                EmailMessageId = emailMessageId
            };
            Owner.Db.Insert (sendUpdate);
            Sm.PostAtMostOneEvent ((uint)CtlEvt.E.SendMail, "ASPCSEND");
            return sendUpdate.Token;
        }

        public override string DeleteEmailCmd (int emailMessageId)
        {
            var emailMessage = Owner.Db.Table<McEmailMessage> ().SingleOrDefault (x => emailMessageId == x.Id);
            if (null == emailMessage) {
                return null;
            }
            var folder = Owner.Db.Table<McFolder> ().Single (x => emailMessage.FolderId == x.Id);

            var deleUpdate = new McPendingUpdate (Account.Id) {
                Operation = McPendingUpdate.Operations.Delete,
                DataType = McPendingUpdate.DataTypes.EmailMessage,
                FolderServerId = folder.ServerId,
                ServerId = emailMessage.ServerId
            };   
            Owner.Db.Insert (deleUpdate);

            // Delete the actual item.
            emailMessage.DeleteBody (Owner.Db);
            Owner.Db.Delete (emailMessage);
            Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCDELMSG");
            return deleUpdate.Token;
        }

        public override string MoveItemCmd (int emailMessageId, int destFolderId)
        {
            var emailMessage = Owner.Db.Table<McEmailMessage> ().SingleOrDefault (x => emailMessageId == x.Id);
            if (null == emailMessage) {
                return null;
            }
            var destFolder = Owner.Db.Table<McFolder> ().SingleOrDefault (x => destFolderId == x.Id);
            if (null == destFolder) {
                return null;
            }
            var srcFolder = Owner.Db.Table<McFolder> ().Single (x => emailMessage.FolderId == x.Id);

            var moveUpdate = new McPendingUpdate (Account.Id) {
                Operation = McPendingUpdate.Operations.Move,
                DataType = McPendingUpdate.DataTypes.EmailMessage,
                EmailMessageServerId = emailMessage.ServerId,
                EmailMessageId = emailMessageId,
                FolderServerId = srcFolder.ServerId,
                DestFolderServerId = destFolder.ServerId,
            };

            Owner.Db.Insert (moveUpdate);
            // Move the actual item.
            emailMessage.FolderId = destFolder.Id;
            Owner.Db.Update (emailMessage);
            Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCMOVMSG");
            return moveUpdate.Token;
        }

        public override string MarkEmailReadCmd (int emailMessageId)
        {
            var emailMessage = Owner.Db.Table<McEmailMessage> ().SingleOrDefault (x => emailMessageId == x.Id);
            if (null == emailMessage) {
                return null;
            }

            var folder = Owner.Db.Table<McFolder> ().Single (x => emailMessage.FolderId == x.Id);

            var markUpdate = new McPendingUpdate (Account.Id) {
                Operation = McPendingUpdate.Operations.MarkRead,
                DataType = McPendingUpdate.DataTypes.EmailMessage,
                ServerId = emailMessage.ServerId,
                FolderServerId = folder.ServerId,
            };   
            Owner.Db.Insert (markUpdate);

            // Mark the actual item.
            emailMessage.IsRead = true;
            Owner.Db.Update (emailMessage);
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMarkedRead));
            Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCMRMSG");
            return markUpdate.Token;
        }

        public override string DnldAttCmd (int attId)
        {
            var att = Owner.Db.Table<McAttachment> ().SingleOrDefault (x => x.Id == attId);
            if (null == att || att.IsDownloaded) {
                return null;
            }
            var update = new McPendingUpdate {
                Operation = McPendingUpdate.Operations.Download,
                DataType = McPendingUpdate.DataTypes.Attachment,
                AccountId = AccountId,
                IsDispatched = false,
                AttachmentId = attId,
            };
            Owner.Db.Insert (update);
            att.PercentDownloaded = 1;
            Owner.Db.Update (att);
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

