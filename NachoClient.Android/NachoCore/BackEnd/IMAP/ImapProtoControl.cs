//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;
using MailKit;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using MailKit.Security;
using System.Net;
using System.Text;

namespace NachoCore.IMAP
{
    public partial class ImapProtoControl : NcProtoControl, IPushAssistOwner
    {
        public NcImapClient MainClient;
        private const int KDiscoveryMaxRetries = 5;

        public enum Lst : uint
        {
            DiscW = (St.Last + 1),
            UiCrdW,
            UiServConfW,
            FSyncW,
            Pick,
            SyncW,
            PingW,
            QOpW,
            HotQOpW,
            FetchW,
            IdleW,
            Parked,
        };

        public override BackEndStateEnum BackEndState {
            get {
                if (null != BackEndStatePreset) {
                    return (BackEndStateEnum)BackEndStatePreset;
                }
                var state = Sm.State;
                if ((uint)Lst.Parked == state) {
                    state = ProtocolState.ImapProtoControlState;
                }
                // Every state above must be mapped here.
                switch (state) {
                case (uint)St.Start:
                    return BackEndStateEnum.NotYetStarted;

                case (uint)Lst.UiCrdW:
                    return BackEndStateEnum.CredWait;

                case (uint)Lst.UiServConfW:
                    return BackEndStateEnum.ServerConfWait;

                case (uint)Lst.DiscW:
                    return BackEndStateEnum.Running;

                case (uint)Lst.FSyncW:
                case (uint)Lst.SyncW:
                case (uint)Lst.QOpW:
                case (uint)Lst.HotQOpW:
                case (uint)Lst.Pick:
                case (uint)Lst.IdleW:
                case (uint)Lst.PingW:
                case (uint)Lst.FetchW:
                case (uint)Lst.Parked:
                    return (ProtocolState.HasSyncedInbox) ? 
                        BackEndStateEnum.PostAutoDPostInboxSync : 
                        BackEndStateEnum.PostAutoDPreInboxSync;
                    
                default:
                    NcAssert.CaseError (string.Format ("Unhandled state {0}", Sm.State));
                    return BackEndStateEnum.PostAutoDPostInboxSync;
                }
            }
        }

        public class ImapEvt : PcEvt
        {
            new public enum E : uint
            {
                ReDisc = (PcEvt.E.Last + 1),
                UiSetCred,
                UiSetServConf,
                GetServConf,
                PkWait,
                ReFSync,
                PkSync,
                PkPing,
                PkQOp,
                PkHotQOp,
                PkFetch,
                Wait,
                AuthFail,
                Last = AuthFail,
            };
        }

        public ImapStrategy Strategy { set; get; }

        private PushAssist PushAssist { set; get; }

        private const string KImapStrategyPick = "ImapStrategy Pick";
        public ImapProtoControl (INcProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            Capabilities = McAccount.ImapCapabilities;
            SetupAccount ();
            MainClient = new NcImapClient ();
            NcCapture.AddKind (KImapStrategyPick);

            Sm = new NcStateMachine ("IMAPPC") { 
                Name = string.Format ("IMAPPC({0})", AccountId),
                LocalEventType = typeof(ImapEvt),
                LocalStateType = typeof(Lst),
                StateChangeIndication = UpdateSavedState,
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Drop = new uint[] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        Invalid = new uint[] {
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.PkWait,
                            (uint)ImapEvt.E.PkSync,
                            (uint)ImapEvt.E.ReFSync,
                            (uint)ImapEvt.E.PkPing,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)ImapEvt.E.PkFetch,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.Wait,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.DiscW,
                        Drop = new uint[] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                        },
                        Invalid = new uint[] {
                            (uint)ImapEvt.E.ReDisc,
                            (uint)ImapEvt.E.PkWait,
                            (uint)ImapEvt.E.PkSync,
                            (uint)ImapEvt.E.ReFSync,
                            (uint)ImapEvt.E.PkPing,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)ImapEvt.E.PkFetch,
                            (uint)ImapEvt.E.Wait,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoDiscTempFail, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.GetServConf, Act = DoUiServConfReq, State = (uint)Lst.UiServConfW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.UiCrdW,
                        Drop = new uint[] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                        },
                        Invalid = new uint[] {
                            (uint)ImapEvt.E.ReDisc,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.PkWait,
                            (uint)ImapEvt.E.PkSync,
                            (uint)ImapEvt.E.ReFSync,
                            (uint)ImapEvt.E.PkPing,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)ImapEvt.E.PkFetch,
                            (uint)ImapEvt.E.Wait,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new Trans[] {
                            // If the creds are still bad, then disc will ask for new ones again.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.UiServConfW,
                        Drop = new uint[] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                        },
                        Invalid = new uint[] {
                            (uint)ImapEvt.E.ReDisc,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.PkWait,
                            (uint)ImapEvt.E.PkSync,
                            (uint)ImapEvt.E.ReFSync,
                            (uint)ImapEvt.E.PkPing,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)ImapEvt.E.PkFetch,
                            (uint)ImapEvt.E.Wait,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new Trans[] {
                            // If the creds are still bad, then disc will ask for new ones again.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.FSyncW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                        },
                        Invalid = new [] {
                            (uint)ImapEvt.E.PkWait,
                            (uint)ImapEvt.E.PkSync,
                            (uint)ImapEvt.E.ReFSync,
                            (uint)ImapEvt.E.PkPing,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)ImapEvt.E.PkFetch,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoDisc, State = (uint)Lst.DiscW  },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.Wait, Act = DoWait, State = (uint)Lst.IdleW },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoExtraOrDont, ActSetsState = true },
                        },
                    },
                    new Node {
                        State = (uint)Lst.Pick,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.Wait,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.PkSync, Act = DoArg, State = (uint)Lst.SyncW },
                            new Trans { Event = (uint)ImapEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)ImapEvt.E.PkPing, Act = DoArg, State = (uint)Lst.PingW },
                            new Trans { Event = (uint)ImapEvt.E.PkQOp, Act = DoArg, State = (uint)Lst.QOpW },
                            new Trans { Event = (uint)ImapEvt.E.PkHotQOp, Act = DoArg, State = (uint)Lst.HotQOpW },
                            new Trans { Event = (uint)ImapEvt.E.PkFetch, Act = DoArg, State = (uint)Lst.FetchW },
                            new Trans { Event = (uint)ImapEvt.E.PkWait, Act = DoArg, State = (uint)Lst.IdleW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.SyncW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)ImapEvt.E.PkPing,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)ImapEvt.E.PkFetch,
                            (uint)ImapEvt.E.PkWait,
                            (uint)ImapEvt.E.PkSync,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoExtraOrDont, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)ImapEvt.E.Wait, Act = DoWait, State = (uint)Lst.IdleW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.PingW,
                        Drop = new [] {
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)ImapEvt.E.PkPing,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)ImapEvt.E.PkFetch,
                            (uint)ImapEvt.E.PkWait,
                            (uint)ImapEvt.E.PkSync,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)ImapEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)ImapEvt.E.Wait, Act = DoWait, State = (uint)Lst.IdleW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.QOpW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)ImapEvt.E.PkPing,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)ImapEvt.E.PkFetch,
                            (uint)ImapEvt.E.PkWait,
                            (uint)ImapEvt.E.PkSync,
                            (uint)ImapEvt.E.Wait,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)ImapEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        },
                    },
                    new Node {
                        State = (uint)Lst.HotQOpW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)ImapEvt.E.PkPing,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)ImapEvt.E.PkFetch,
                            (uint)ImapEvt.E.PkWait,
                            (uint)ImapEvt.E.PkSync,
                            (uint)ImapEvt.E.Wait,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNopOrPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoExtraOrDont, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)ImapEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.FetchW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)ImapEvt.E.PkPing,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)ImapEvt.E.PkFetch,
                            (uint)ImapEvt.E.PkWait,
                            (uint)ImapEvt.E.PkSync,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)ImapEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)ImapEvt.E.Wait, Act = DoWait, State = (uint)Lst.IdleW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.IdleW,
                        Drop = new [] {
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.HardFail,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.PkPing,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)ImapEvt.E.PkFetch,
                            (uint)ImapEvt.E.PkWait,
                            (uint)ImapEvt.E.PkSync,
                            (uint)ImapEvt.E.Wait,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, State = (uint)Lst.Pick },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Parked,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)PcEvt.E.Park,
                            (uint)ImapEvt.E.PkPing,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)ImapEvt.E.PkFetch,
                            (uint)ImapEvt.E.PkWait,
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.PkSync,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.ReFSync,
                            (uint)ImapEvt.E.Wait,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDrive, ActSetsState = true },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    }
                }
            };
            Sm.Validate ();
            Sm.State = ProtocolState.ImapProtoControlState;
            LastBackEndState = BackEndState;
            Strategy = new ImapStrategy (this);
            PushAssist = new PushAssist (this);
            NcCommStatus.Instance.CommStatusNetEvent += NetStatusEventHandler;
            NcCommStatus.Instance.CommStatusServerEvent += ServerStatusEventHandler;
        }

        // State-machine's state persistance callback.
        private void UpdateSavedState ()
        {
            BackEndStatePreset = null;
            var protocolState = ProtocolState;
            uint stateToSave = Sm.State;
            if ((uint)Lst.Parked != stateToSave) {
                // We never save Parked.
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.ImapProtoControlState = stateToSave;
                    return true;
                });
            }
            if (LastBackEndState != BackEndState) {
                var res = NcResult.Info (NcResult.SubKindEnum.Info_BackEndStateChanged);
                res.Value = AccountId;
                StatusInd (res);
            }
            LastBackEndState = BackEndState;
        }

        public void ServerStatusEventHandler (Object sender, NcCommStatusServerEventArgs e)
        {
            if (e.ServerId == Server.Id) {
                switch (e.Quality) {
                case NcCommStatus.CommQualityEnum.OK:
                    Log.Info (Log.LOG_IMAP, "Server {0} communication quality OK.", Server.Host);
                    Execute ();
                    break;

                default:
                case NcCommStatus.CommQualityEnum.Degraded:
                    Log.Info (Log.LOG_IMAP, "Server {0} communication quality degraded.", Server.Host);
                    break;

                case NcCommStatus.CommQualityEnum.Unusable:
                    Log.Info (Log.LOG_IMAP, "Server {0} communication quality unusable.", Server.Host);
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
                Sm.PostEvent ((uint)PcEvt.E.Park, "IMEHPARK");
            }
        }

        public static string MessageServerId (McFolder folder, UniqueId ImapMessageUid)
        {
            return string.Format ("{0}:{1}", folder.ImapGuid, ImapMessageUid);
        }

        public override void ForceStop ()
        {
            if (null != PushAssist) {
                PushAssist.Park ();
            }
            Sm.PostEvent ((uint)PcEvt.E.Park, "IMAPFORCESTOP");
        }

        public override void Remove ()
        {
            // TODO Move to base
            if (!((uint)Lst.Parked == Sm.State || (uint)St.Start == Sm.State || (uint)St.Stop == Sm.State)) {
                Log.Warn (Log.LOG_IMAP, "ImapProtoControl.Remove called while state is {0}", Sm.State);
            }
            // TODO cleanup stuff on disk like for wipe.
            NcCommStatus.Instance.CommStatusNetEvent -= NetStatusEventHandler;
            NcCommStatus.Instance.CommStatusServerEvent -= ServerStatusEventHandler;
            if (null != PushAssist) {
                PushAssist.Dispose ();
                PushAssist = null;
            }
            base.Remove ();
        }

        public override bool Execute ()
        {
            if (!base.Execute ()) {
                return false;
            }
            Sm.PostEvent ((uint)SmEvt.E.Launch, "IMAPPCEXE");
            return true;
        }

        private void DoDisc ()
        {
            // HACK HACK: There appears to be a race-condition when the NcBackend (via UI) 
            // starts this service, and when the state gets properly recognized. This is 
            // because there are two services (IMAP and SMTP) and either can run ahead of the other
            // and send a StatusInd, causing the UI to check the services (both!) state
            // via EventFromEnum(). This can lead to invalid states being recognized.
            // Example: 
            //  SMTP and IMAP Both have moved to DiscW, but only SMTP has actually started:
            //  UI:Info:1:: avl: handleStatusEnums 2 sender=Running reader=CredWait
            // The CredWait causes the login SM to move to:
            //  STATE:Info:1:: SM(Account:3): S=SyncWait & E=CredReqCallback/avl: EventFromEnum cred req => S=SubmitWait
            // Then, later, IMAP starts and sends a status Ind:
            //  UI:Info:1:: avl: handleStatusEnums 2 sender=Running reader=Running
            // But this is an illegal state in SubMitWait:
            //  STATE:Error:1:: SM(Account:3): S=SubmitWait & E=Running/avl: EventFromEnum running => INVALID EVENT
            BackEndStatePreset = BackEndStateEnum.Running;
            SetCmd (new ImapDiscoverCommand (this, MainClient));
            ExecuteCmd ();
        }

        private int DiscoveryRetries = 0;
        private void DoDiscTempFail ()
        {
            Log.Info (Log.LOG_SMTP, "IMAP DoDisc Attempt {0}", DiscoveryRetries++);
            if (DiscoveryRetries >= KDiscoveryMaxRetries) {
                Sm.PostEvent ((uint)ImapEvt.E.GetServConf, "IMAPMAXDISC");
            } else {
                DoDisc ();
            }
        }

        private void DoUiServConfReq ()
        {
            BackEndStatePreset = BackEndStateEnum.ServerConfWait;
            // Send the request toward the UI.
            Owner.ServConfReq (this, Sm.Arg);
        }

        private void DoConn ()
        {
            SetCmd (new ImapAuthenticateCommand (this, MainClient));
            ExecuteCmd ();
        }

        private void DoFSync ()
        {
            SetCmd (new ImapFolderSyncCommand (this, MainClient));
            ExecuteCmd ();
        }

        private void DoArg ()
        {
            var cmd = Sm.Arg as ImapCommand;
            SetCmd (cmd);
            ExecuteCmd ();
        }

        private void DoWait ()
        {
            var waitTime = (int)Sm.Arg;
            SetCmd (new ImapWaitCommand (this, MainClient, waitTime, true));
            ExecuteCmd ();
        }

        private X509Certificate2 _ServerCertToBeExamined;

        public override X509Certificate2 ServerCertToBeExamined {
            get {
                return _ServerCertToBeExamined;
            }
        }

        private ImapCommand Cmd;

        private bool CmdIs (Type cmdType)
        {
            return (null != Cmd && Cmd.GetType () == cmdType);
        }

        private void CancelCmd ()
        {
            if (null != Cmd) {
                Cmd.Cancel ();
                Cmd = null;
            }
        }

        private void SetCmd (ImapCommand nextCmd)
        {
            CancelCmd ();
            Cmd = nextCmd;
        }

        private void ExecuteCmd ()
        {
            PossiblyKickPushAssist ();
            Cmd.Execute (Sm);
        }

        private void DoUiCertOkReq ()
        {
            BackEndStatePreset = BackEndStateEnum.CertAskWait;
            _ServerCertToBeExamined = (X509Certificate2)Sm.Arg;
            Owner.CertAskReq (this, _ServerCertToBeExamined);
        }

        public override void CredResp ()
        {
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)ImapEvt.E.UiSetCred, "IMAPPCUSC");
            }, "ImapCredResp");
        }

        public override void ServerConfResp (bool forceAutodiscovery)
        {
            if (forceAutodiscovery) {
                Log.Error (Log.LOG_IMAP, "Why a forceautodiscovery?");
            }
            Sm.PostEvent ((uint)ImapEvt.E.UiSetServConf, "IMAPPCUSSC");
        }

        private void DoExDone ()
        {
            Interlocked.Decrement (ref ConcurrentExtraRequests);
            // Send the PendQHot so that the ProtoControl SM looks to see if there is another hot op
            // to run in parallel.
            Sm.PostEvent ((uint)PcEvt.E.PendQHot, "DOEXDONE1MORE");
        }

        private const int MaxConcurrentExtraRequests = 4;
        private int ConcurrentExtraRequests = 0;

        private void DoExtraOrDont ()
        {
            /* TODO
             * Move decision logic into strategy.
             * Evaluate server success rate based on number of outstanding requests.
             * Let those rates drive the allowed concurrency, rather than "1 + 2".
             */
            if (NcCommStatus.CommQualityEnum.OK == NcCommStatus.Instance.Quality (Server.Id) &&
                NetStatusSpeedEnum.CellSlow_2 != NcCommStatus.Instance.Speed &&
                MaxConcurrentExtraRequests > ConcurrentExtraRequests)
            {
                NcImapClient Client = new NcImapClient ();  // Presumably this will get cleaned up by GC?
                Interlocked.Increment (ref ConcurrentExtraRequests);
                var pack = Strategy.PickUserDemand (Client);
                if (null == pack) {
                    // If strategy could not find something to do, we won't be using the side channel.
                    Interlocked.Decrement (ref ConcurrentExtraRequests);
                    Log.Info (Log.LOG_IMAP, "DoExtraOrDont: Strategy could not find anything to do.");
                } else {
                    Log.Info (Log.LOG_IMAP, "DoExtraOrDont: starting extra request.");
                    var dummySm = new NcStateMachine ("IMAPPC:EXTRA") { 
                        Name = string.Format ("IMAPPC:EXTRA({0})", AccountId),
                        LocalEventType = typeof(ImapEvt),
                        TransTable = new[] {
                            new Node {
                                State = (uint)St.Start,
                                Invalid = new [] {
                                    (uint)NcProtoControl.PcEvt.E.PendQ,
                                    (uint)NcProtoControl.PcEvt.E.PendQHot,
                                    (uint)NcProtoControl.PcEvt.E.Park,
                                    (uint)ImapEvt.E.UiSetCred,
                                    (uint)ImapEvt.E.UiSetServConf,
                                    (uint)ImapEvt.E.PkWait,
                                    (uint)ImapEvt.E.PkPing,
                                    (uint)ImapEvt.E.PkFetch,
                                    (uint)ImapEvt.E.PkHotQOp,
                                    (uint)ImapEvt.E.PkQOp,
                                    (uint)ImapEvt.E.PkSync,
                                    (uint)ImapEvt.E.ReFSync,
                                    (uint)ImapEvt.E.GetServConf,
                                },
                                On = new Trans[] {
                                    new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)St.Start },
                                    new Trans { Event = (uint)SmEvt.E.Success, Act = DoExDone, State = (uint)St.Stop },
                                    new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoExDone, State = (uint)St.Stop },
                                    new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoExDone, State = (uint)St.Stop },
                                    new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoExDone, State = (uint)St.Stop },
                                    new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoExDone, State = (uint)St.Stop },
                                    new Trans { Event = (uint)ImapEvt.E.Wait, Act = DoExDone, State = (uint)St.Stop },
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
                    Log.Info (Log.LOG_IMAP, "DoExtraOrDont: calling Pick.");
                    DoPick ();
                    Sm.State = (uint)Lst.Pick;
                } else {
                    Log.Info (Log.LOG_IMAP, "DoExtraOrDont: not calling Pick (HotQOpW).");
                }
            } else {
                // ... and we are capable of processing extra requests, just not now.
                Log.Info (Log.LOG_IMAP, "DoExtraOrDont: not starting extra request on top of {0}.", ConcurrentExtraRequests);
            }
        }

        private void DoPick ()
        {
            CancelCmd ();
            Sm.ClearEventQueue ();
            Tuple<PickActionEnum, ImapCommand> pack;
            using (var cap = NcCapture.CreateAndStart (KImapStrategyPick)) {
                pack = Strategy.Pick (MainClient);
                cap.Stop ();
            }
            var transition = pack.Item1;
            var cmd = pack.Item2;
            var exeCtxt = NcApplication.Instance.ExecutionContext;
            switch (transition) {
            case PickActionEnum.Fetch:
                Sm.PostEvent ((uint)ImapEvt.E.PkFetch, "PCKFETCH", cmd);
                break;
            case PickActionEnum.Sync:
                Sm.PostEvent ((uint)ImapEvt.E.PkSync, "PCKSYNC", cmd);
                break;
            case PickActionEnum.Ping:
                Sm.PostEvent ((uint)ImapEvt.E.PkPing, "PCKPING", cmd);
                break;
            case PickActionEnum.HotQOp:
                Sm.PostEvent ((uint)ImapEvt.E.PkHotQOp, "PCKHOTOP", cmd);
                break;
            case PickActionEnum.QOop:
                Sm.PostEvent ((uint)ImapEvt.E.PkQOp, "PCKOP", cmd);
                break;
            case PickActionEnum.FSync:
                Sm.PostEvent ((uint)ImapEvt.E.ReFSync, "PCKFSYNC", cmd);
                break;
            case PickActionEnum.Wait:
                Sm.PostEvent ((uint)ImapEvt.E.PkWait, "PCKWAIT", cmd);
                break;
            default:
                Log.Error (Log.LOG_IMAP, "Unknown PickAction {0}", transition.ToString ());
                Sm.PostEvent ((uint)SmEvt.E.HardFail, "PCKHARD", cmd);
                break;
            }
        }

        private void DoNopOrPick ()
        {
            // If we are parked, the Cmd has been set to null.
            // Otherwise, it has the last command executed (or still executing).
            if (null == Cmd) {
                // We are not running, go figure out what to do.
                DoPick ();
                Sm.State = (uint)Lst.Pick;
            } else {
                // We are running, ignore the Launch, stay in the current state.
            }
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
            lock (MainClient.SyncRoot) {
                MainClient.Disconnect (true); // TODO Where does the Cancellation token come from?
            }
        }

        private void DoDrive ()
        {
            PossiblyKickPushAssist ();
            Sm.State = ProtocolState.ImapProtoControlState;
            Sm.PostEvent ((uint)SmEvt.E.Launch, "DRIVE");
        }

        private void DoUiCredReq ()
        {
            CancelCmd ();
            BackEndStatePreset = BackEndStateEnum.CredWait;
            // Send the request toward the UI.
            Owner.CredReq (this);
        }

        #region ValidateConfig

        private ImapValidateConfig Validator;
        public override void ValidateConfig (McServer server, McCred cred)
        {
            CancelValidateConfig ();
            Validator = new ImapValidateConfig (this);
            Validator.Execute (server, cred);
        }

        public override void CancelValidateConfig ()
        {
            if (null != Validator) {
                Validator.Cancel ();
                Validator = null;
            }
        }

        #endregion

        #region PushAssist support.

        private bool CanStartPushAssist ()
        {
            // We need to be able to get the right capabilities, so must have auth'd at least once
            // This happens during discovery, so this shouldn't be an issue.
            return McAccount.AccountServiceEnum.None != ProtoControl.ProtocolState.ImapServiceType;
        }

        private void PossiblyKickPushAssist ()
        {
            if (CanStartPushAssist ()) {
                // uncomment for testing on the simulator
                //PushAssist.SetDeviceToken ("SIMULATOR");
                if (PushAssist.IsStartOrParked ()) {
                    PushAssist.Execute ();
                }
            }
        }

        private byte[] PushAssistAuthBlob ()
        {

            SaslMechanism sasl;
            switch (ProtoControl.Cred.CredType) {
            case McCred.CredTypeEnum.OAuth2:
                sasl = SaslMechanism.Create ("XOAUTH2",
                    new Uri (string.Format ("imap://{0}", ProtoControl.Server.Host)),
                    new NetworkCredential (ProtoControl.Cred.Username, ProtoControl.Cred.GetAccessToken ()));
                break;

            default:
                sasl = SaslMechanism.Create ("PLAIN",
                    new Uri (string.Format ("imap://{0}", ProtoControl.Server.Host)),
                    new NetworkCredential (ProtoControl.Cred.Username, ProtoControl.Cred.GetPassword ()));
                break;
            }
            string command = string.Format ("AUTHENTICATE {0}", sasl.MechanismName);
            if (sasl.SupportsInitialResponse &&
                (0 != (ProtoControl.ProtocolState.ImapServerCapabilitiesUnAuth & McProtocolState.NcImapCapabilities.SaslIR)) ||
                (0 != (ProtoControl.ProtocolState.ImapServerCapabilities & McProtocolState.NcImapCapabilities.SaslIR)))
            {
                command += " ";
            } else {
                command += "\n";
            }
            command += sasl.Challenge (null);
            return Encoding.UTF8.GetBytes (command);
        }

        public PushAssistParameters PushAssistParameters ()
        {
            if (!CanStartPushAssist ()) {
                // We need to have logged in at least once. We shouldn't have started the PA SM
                // CanStartPushAssist is false, so this should realistically never happen.
                Log.Error (Log.LOG_IMAP, "Can't set up protocol parameters yet");
                return null;
            }

            bool supportsExpunged = ProtoControl.ProtocolState.ImapServiceType != McAccount.AccountServiceEnum.Yahoo;
            bool supportsIdle = (0 != (ProtoControl.ProtocolState.ImapServerCapabilities & McProtocolState.NcImapCapabilities.Idle));

            return new PushAssistParameters () {
                RequestUrl = string.Format ("imap://{0}:{1}", ProtoControl.Server.Host, ProtoControl.Server.Port),
                Protocol = PushAssistProtocol.IMAP,
                ResponseTimeoutMsec = 600 * 1000,
                WaitBeforeUseMsec = 60 * 1000,

                IMAPAuthenticationBlob = PushAssistAuthBlob (),
                IMAPFolderName = "INBOX",
                IMAPSupportsIdle = supportsIdle,
                IMAPSupportsExpunge = supportsExpunged,
            };
        }

        #endregion
    }
}

