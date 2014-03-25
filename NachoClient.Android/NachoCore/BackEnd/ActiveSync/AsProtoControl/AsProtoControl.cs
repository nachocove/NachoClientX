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
            SyncW,
            PingW,
            SendMailW,
            SFwdMailW,
            SRplyMailW,
            DnldAttW,
            SrchW,
            MoveW,
            FCreW,
            FDelW,
            FUpW,
            CalRespW,
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
                PendQ,
                ReFSync,
                SendMail,
                SFwdMail,
                SRplyMail,
                DnldAtt,
                UiSearch,
                Move,
                FCre,
                FDel,
                FUp,
                CalResp,
            };
        }

        public AsProtoControl ProtoControl { set; get; }

        private NcTimer PendingOnTimeTimer { set; get; }

        public AsProtoControl (IProtoControlOwner owner, int accountId)
        {
            ProtoControl = this;
            Owner = owner;
            AccountId = accountId;

            Sm = new NcStateMachine () { 
                Name = string.Format ("ASPC({0})", AccountId),
                LocalEventType = typeof(CtlEvt),
                LocalStateType = typeof(Lst),
                StateChangeIndication = UpdateSavedState,
                TransTable = new[] {
                    // GENERAL CONVENTIONS:
                    //
                    // TempFail: for scenarios where a command can return TempFail, just keep re-trying the 
                    // command. NcCommStatus will eventually shut us down as TempFail counts against Quality.
                    //
                    new Node {
                        State = (uint)St.Start,
                        Drop = new [] {
                            (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.UiCertOkNo, 
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSearch,
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp, 
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
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSearch,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)AsEvt.E.ReProv,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
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
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSearch,
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoUiCredReq, State = (uint)Lst.UiDCrdW },
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
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSearch,
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
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
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSearch,
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
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
                            (uint)CtlEvt.E.UiSearch,
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
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
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.UiSearch,
                        },
                        Invalid = new [] {
                            (uint)AsEvt.E.ReDisc, // TODO: should we be more defensive here?
                            (uint)AsEvt.E.ReProv, // TODO: same issue.
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
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
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.UiSearch,
                        },
                        Invalid = new [] {
                            (uint)AsEvt.E.ReProv,
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
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
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.UiSearch,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
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
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.ReFSync,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
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
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
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
                            new Trans { Event = (uint)CtlEvt.E.PendQ, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)CtlEvt.E.SendMail, Act = DoSend, State = (uint)Lst.SendMailW },
                            new Trans { Event = (uint)CtlEvt.E.SFwdMail, Act = DoSFwd, State = (uint)Lst.SFwdMailW },
                            new Trans { Event = (uint)CtlEvt.E.SRplyMail, Act = DoSRply, State = (uint)Lst.SRplyMailW },
                            new Trans { Event = (uint)CtlEvt.E.DnldAtt, Act = DoDnldAtt, State = (uint)Lst.DnldAttW },
                            new Trans { Event = (uint)CtlEvt.E.UiSearch, Act = DoSearch, State = (uint)Lst.SrchW },
                            new Trans { Event = (uint)CtlEvt.E.Move, Act = DoMove, State = (uint)Lst.MoveW },
                            new Trans { Event = (uint)CtlEvt.E.FCre, Act = DoFCre, State = (uint)Lst.FCreW },
                            new Trans { Event = (uint)CtlEvt.E.FDel, Act = DoFDel, State = (uint)Lst.FDelW },
                            new Trans { Event = (uint)CtlEvt.E.FUp, Act = DoFUp, State = (uint)Lst.FUpW },
                            new Trans { Event = (uint)CtlEvt.E.CalResp, Act = DoCalResp, State = (uint)Lst. CalRespW },

                        }
                    },
                    // CONVENTIONS FOR PING-INITIATED, FROM PENDING-Q COMMANDS && ASSOC. STATES.
                    // 
                    // Launch: just DoPing. The pending that got you to this state may no longer exist.
                    // Success: DoPing, which will check the pending Q.
                    // HardFail: same as Success. Rely on pending "ResolveAs" logic to make it right.
                    // TempFail: see general conventions way above - try again.
                    // Re(Disc|Prov|Sync/FSync): Do(Disc|Prov|Sync|FSync).
                    // AuthFail and UiSearch require immediate attention.
                    //
                    new Node {
                        State = (uint)Lst.SendMailW,
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPing, State = (uint)Lst.PingW },
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
                        State = (uint)Lst.SFwdMailW,
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSFwd, State = (uint)Lst.SFwdMailW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)CtlEvt.E.UiSearch, Act = DoSearch, State = (uint)Lst.SrchW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.SRplyMailW,
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSRply, State = (uint)Lst.SRplyMailW },
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
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPing, State = (uint)Lst.PingW },
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
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                            (uint)CtlEvt.E.UiSearch,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
                        },
                        On = new [] {
                            // NOTE: If we get re-launched in this state, the old search is stale. Go
                            // straight to ping.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPing, State = (uint)Lst.PingW },
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
                            (uint)CtlEvt.E.PendQ,
                            (uint)CtlEvt.E.UiCertOkNo,
                            (uint)CtlEvt.E.UiCertOkYes,
                            (uint)CtlEvt.E.UiSetCred,
                            (uint)CtlEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)CtlEvt.E.GetServConf,
                            (uint)CtlEvt.E.GetCertOk,
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoMove, State = (uint)Lst.MoveW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)CtlEvt.E.UiSearch, Act = DoSearch, State = (uint)Lst.SrchW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.FCreW,
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoFCre, State = (uint)Lst.FCreW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)CtlEvt.E.UiSearch, Act = DoSearch, State = (uint)Lst.SrchW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.FDelW,
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoFDel, State = (uint)Lst.FDelW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)CtlEvt.E.UiSearch, Act = DoSearch, State = (uint)Lst.SrchW },
                        }
                    },

                    new Node {
                        State = (uint)Lst.FUpW,
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoFUp, State = (uint)Lst.FUpW },
                            new Trans { Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvW },
                            new Trans { Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)AsEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiPCrdW },
                            new Trans { Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)CtlEvt.E.UiSearch, Act = DoSearch, State = (uint)Lst.SrchW },
                        }
                    },

                    new Node {
                        State = (uint)Lst. CalRespW,
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
                            (uint)CtlEvt.E.SendMail,
                            (uint)CtlEvt.E.SFwdMail,
                            (uint)CtlEvt.E.SRplyMail,
                            (uint)CtlEvt.E.DnldAtt,
                            (uint)CtlEvt.E.Move,
                            (uint)CtlEvt.E.FCre,
                            (uint)CtlEvt.E.FDel,
                            (uint)CtlEvt.E.FUp,
                            (uint)CtlEvt.E.CalResp,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPing, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoCalResp, State = (uint)Lst.CalRespW },
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
            Sm.State = ProtocolState.State;

            Log.Info (Log.LOG_STATE, "Initial state: {0}", Sm.State);

            McPending.ResolveAllDispatchedAsDeferred (Account.Id);
            NcCommStatus.Instance.CommStatusNetEvent += NetStatusEventHandler;
            NcCommStatus.Instance.CommStatusServerEvent += ServerStatusEventHandler;
            // FIXME - make pretty.
            PendingOnTimeTimer = new NcTimer (state => {
                McPending.MakeEligibleOnTime (Account.Id);
            }, null, 1000, 2000);
        }
        // Methods callable by the owner.
        public override void Execute ()
        {
            if (NachoPlatform.NetStatusStatusEnum.Up != NcCommStatus.Instance.Status) {
                Log.Warn (Log.LOG_AS, "Execute called while network is down.");
                return;
            }
            // There isn't really a way to tell whether we are executing currently or not!
            // All states are required to handle the Launch event gracefully, so we just send it.
            Sm.PostAtMostOneEvent ((uint)SmEvt.E.Launch, "ASPCEXE");
        }

        public override void CredResp ()
        {
            Sm.PostAtMostOneEvent ((uint)CtlEvt.E.UiSetCred, "ASPCUSC");
        }

        public override void ServerConfResp (bool forceAutodiscovery)
        {
            if (forceAutodiscovery) {
                Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReDisc, "ASPCURD");
            } else {
                Server = BackEnd.Instance.Db.Table<McServer> ().Single (rec => rec.Id == Account.ServerId);
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.UiSetServConf, "ASPCUSSC");
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
            if (0 == AsSyncCommand.FoldersNeedingSync (AccountId).Count) {
                // If there is nothing to Sync, then just post SUCCESS.
                Sm.PostEvent (Event.Create ((uint)SmEvt.E.Success, "ASPCNOFLD"));
            } else {
                SetCmd (new AsSyncCommand (this));
                Cmd.Execute (Sm);
            }
        }

        private void DoSend ()
        {
            ForceStop ();
            SetCmd (new AsSendMailCommand (this));
            Cmd.Execute (Sm);
        }

        private void DoSFwd ()
        {
            ForceStop ();
            SetCmd (new AsSmartForwardCommand (this));
            Cmd.Execute (Sm);
        }

        private void DoSRply ()
        {
            ForceStop ();
            SetCmd (new AsSmartReplyCommand (this));
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

        private void DoFCre ()
        {
            SetCmd (new AsFolderCreateCommand (this));
            Cmd.Execute (Sm);
        }

        private void DoFDel ()
        {
            SetCmd (new AsFolderDeleteCommand (this));
            Cmd.Execute (Sm);
        }

        private void DoFUp ()
        {
            SetCmd (new AsFolderUpdateCommand (this));
            Cmd.Execute (Sm);
        }

        private void DoCalResp ()
        {
            SetCmd (new AsMeetingResponseCommand (this));
            Cmd.Execute (Sm);
        }

        private void DoPing ()
        {
            // FirePendingInstead will post an event and return true if there is a pending.
            // In this way DoPing just passes through and the SM jumps to the right state for
            // dealing with the pending.
            if (!FirePendingInsteadOfPing ()) {
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

        private bool FirePendingInsteadOfPing ()
        {
            var pendingEligible = McPending.QueryEligible (Account.Id);
            var next = pendingEligible.FirstOrDefault ();
            if (0 < pendingEligible.Count ()) {
                next = pendingEligible.First ();
                switch (next.Operation) {
                case McPending.Operations.ContactSearch:
                    Sm.PostAtMostOneEvent ((uint)CtlEvt.E.UiSearch, "ASPCDP0");
                    return true;

                case McPending.Operations.FolderCreate:
                    Sm.PostAtMostOneEvent ((uint)CtlEvt.E.FCre, "ASPCFCRE");
                    return true;

                case McPending.Operations.FolderUpdate:
                    Sm.PostAtMostOneEvent ((uint)CtlEvt.E.FUp, "ASPCFUP");
                    return true;

                case McPending.Operations.FolderDelete:
                    Sm.PostAtMostOneEvent ((uint)CtlEvt.E.FDel, "ASPCFDEL");
                    return true;

                case McPending.Operations.EmailSend:
                    Sm.PostAtMostOneEvent ((uint)CtlEvt.E.SendMail, "ASPCDP1");
                    return true;

                case McPending.Operations.EmailForward:
                    Sm.PostEvent ((uint)CtlEvt.E.SFwdMail, "ASPCDSF");
                    return true;

                case McPending.Operations.EmailReply:
                    Sm.PostEvent ((uint)CtlEvt.E.SRplyMail, "ASPCDSR");
                    return true;

                case McPending.Operations.EmailMove:
                    Sm.PostEvent ((uint)CtlEvt.E.Move, "ASPCDPM");
                    return true;

                case McPending.Operations.AttachmentDownload:
                    Sm.PostEvent ((uint)CtlEvt.E.DnldAtt, "ASPCDP2");
                    return true;

                case McPending.Operations.CalRespond:
                    Sm.PostEvent ((uint)CtlEvt.E.CalResp, "ASPCCR");
                    return true;

                case McPending.Operations.EmailDelete:
                case McPending.Operations.EmailMarkRead:
                case McPending.Operations.EmailSetFlag:
                case McPending.Operations.EmailClearFlag:
                case McPending.Operations.EmailMarkFlagDone:
                case McPending.Operations.CalCreate:
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
            if (null != DisposedCmd) {
                DisposedCmd.Cancel ();
            }
            Cmd = nextCmd;
        }

        public override void ForceStop ()
        {
            SetCmd (null);
        }

        public override void ForceSync ()
        {
            ForceStop ();
            var defaultInbox = BackEnd.Instance.Db.Table<McFolder> ().SingleOrDefault (x => x.Type == (uint)Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            if (null != defaultInbox) {
                defaultInbox.AsSyncRequired = true;
                defaultInbox.Update ();
            }
            Task.Run (delegate {
                if (NachoPlatform.NetStatusStatusEnum.Up != NcCommStatus.Instance.Status) {
                    Log.Warn (Log.LOG_AS, "Execute called while network is down.");
                    return;
                }
                Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCFORCESYNC");
            });
        }

        public override void Cancel (string token)
        {
            var pending = McPending.QueryByToken (Account.Id, token);
            if (null == pending) {
                return;
            }
            switch (pending.State) {
            case McPending.StateEnum.Eligible:
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
                pending.ResolveAsCancelled ();
                // Don't REALLY know that we killed it before the server saw it.
                break;

            case McPending.StateEnum.Deleted:
                // Nothing to do.
                break;

            default:
                throw new Exception (string.Format ("Unknown State {0}", pending.State));
            }
        }

        public void ServerStatusEventHandler (Object sender, NcCommStatusServerEventArgs e)
        {
            if (e.ServerId == Server.Id) {
                switch (e.Quality) {
                case NcCommStatus.CommQualityEnum.OK:
                    Log.Info ("Server {0} communication quality OK.", Server.Host);
                    Execute ();
                    break;

                default:
                case NcCommStatus.CommQualityEnum.Degraded:
                    Log.Warn ("Server {0} communication quality degrated.", Server.Host);
                    break;

                case NcCommStatus.CommQualityEnum.Unusable:
                    Log.Error ("Server {0} communication quality unusable.", Server.Host);
                    ForceStop ();
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
                ForceStop ();
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

