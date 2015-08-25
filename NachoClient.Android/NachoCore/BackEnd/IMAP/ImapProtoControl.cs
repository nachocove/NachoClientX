﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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
                case (uint)Lst.IdleW:
                case (uint)Lst.PingW:
                case (uint)Lst.FetchW:
                case (uint)Lst.Parked:
                    return (ProtocolState.HasSyncedInbox) ? 
                        BackEndStateEnum.PostAutoDPostInboxSync : 
                        BackEndStateEnum.PostAutoDPreInboxSync;
                    
                default:
                    NcAssert.CaseError (string.Format ("BackEndState: Unhandled state {0}", StateName ((uint)Sm.State)));
                    return BackEndStateEnum.PostAutoDPostInboxSync;
                }
            }
        }

        public static string StateName (uint state)
        {
            switch (state) {
            case (uint)St.Start:
                return "Start";
            case (uint)St.Stop:
                return "Stop";
            case (uint)Lst.DiscW:
                return "DiscW";
            case (uint)Lst.UiCrdW:
                return "UiCrdW";
            case (uint)Lst.UiServConfW:
                return "UiServConfW";
            case (uint)Lst.FSyncW:
                return "FSyncW";
            case (uint)Lst.SyncW:
                return "SyncW";
            case (uint)Lst.PingW:
                return "PingW";
            case (uint)Lst.QOpW:
                return "QOpW";
            case (uint)Lst.HotQOpW:
                return "HotQOpW";
            case (uint)Lst.FetchW:
                return "FetchW";
            case (uint)Lst.IdleW:
                return "IdleW";
            case (uint)Lst.Parked:
                return "Parked";
            default:
                Log.Error (Log.LOG_IMAP, "Missing case in StateName {0}", state);
                return state.ToString ();
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
                ReFSync,
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
                            (uint)ImapEvt.E.ReFSync,
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
                            (uint)ImapEvt.E.ReFSync,
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
                            (uint)ImapEvt.E.ReFSync,
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
                            (uint)ImapEvt.E.ReFSync,
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
                            (uint)ImapEvt.E.ReFSync,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
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
                        State = (uint)Lst.SyncW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, ActSetsState = true },
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
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, ActSetsState = true },
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
                            (uint)ImapEvt.E.Wait,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, ActSetsState = true },
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
                            (uint)ImapEvt.E.Wait,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNopOrPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, ActSetsState = true },
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
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, ActSetsState = true },
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
                            (uint)ImapEvt.E.Wait,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, ActSetsState = true },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Parked,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)PcEvt.E.Park,
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                            (uint)SmEvt.E.TempFail,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.HardFail,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.ReFSync,
                            (uint)ImapEvt.E.Wait,
                            (uint)ImapEvt.E.GetServConf,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoNop, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDrive, ActSetsState = true },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    }
                }
            };
            Sm.Validate ();
            Sm.State = ProtocolState.ImapProtoControlState;
            LastBackEndState = BackEndState;
            LastIsDoNotDelayOk = IsDoNotDelayOk;
            Strategy = new ImapStrategy (this);
            PushAssist = new PushAssist (this);
            NcCommStatus.Instance.CommStatusNetEvent += NetStatusEventHandler;
            NcCommStatus.Instance.CommStatusServerEvent += ServerStatusEventHandler;
            NcApplication.Instance.StatusIndEvent += StatusIndEventHandler;
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
            if (LastIsDoNotDelayOk && !IsDoNotDelayOk) {
                ResolveDoNotDelayAsHardFail ();
            }
            LastIsDoNotDelayOk = IsDoNotDelayOk;
        }

        #region NcCommStatus

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

        #endregion

        public void StatusIndEventHandler (Object sender, EventArgs ea)
        {
            var siea = (StatusIndEventArgs)ea;
            if (null == siea.Account || siea.Account.Id != AccountId) {
                return;
            }
            switch (siea.Status.SubKind) {
            case NcResult.SubKindEnum.Info_DaysToSyncChanged:
                Sm.PostEvent ((uint)SmEvt.E.Launch, "IMAPDAYSYNC");
                break;
            }
        }

        public static string MessageServerId (McFolder folder, UniqueId ImapMessageUid)
        {
            return string.Format ("{0}:{1}", folder.ImapGuid, ImapMessageUid);
        }

        public override void ForceStop ()
        {
            base.ForceStop ();

            if (null != PushAssist) {
                PushAssist.Park ();
            }
            Sm.PostEvent ((uint)PcEvt.E.Park, "IMAPFORCESTOP");
        }

        public override void Remove ()
        {
            // TODO Move to base
            if (!((uint)Lst.Parked == Sm.State || (uint)St.Start == Sm.State || (uint)St.Stop == Sm.State)) {
                Log.Warn (Log.LOG_IMAP, "ImapProtoControl.Remove called while state is {0}", StateName ((uint)Sm.State));
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
            if (!(Cmd is ImapDiscoverCommand)) {
                PossiblyKickPushAssist ();
            }
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
                // stop push assist. We just got a new credential, so chances are
                // we need to tell push assist to restart with new ones.
                if (null != PushAssist) {
                    PushAssist.Stop ();
                }
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
            if (!ForceStopped) {
                Sm.PostEvent ((uint)PcEvt.E.PendQHot, "DOEXDONE1MORE");
            }
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
                MaxConcurrentExtraRequests > ConcurrentExtraRequests) {
                NcImapClient Client = new NcImapClient ();  // Presumably this will get cleaned up by GC?
                Interlocked.Increment (ref ConcurrentExtraRequests);
                var pack = Strategy.PickUserDemand (Client);
                if (null == pack) {
                    // If strategy could not find something to do, we won't be using the side channel.
                    Interlocked.Decrement (ref ConcurrentExtraRequests);
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
                    SideChannelCommandAdd (cmd);
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
            CancelCmd ();
            Sm.ClearEventQueue ();
            Tuple<PickActionEnum, ImapCommand> pack;
            using (var cap = NcCapture.CreateAndStart (KImapStrategyPick)) {
                pack = Strategy.Pick (MainClient);
                cap.Stop ();
            }
            var transition = pack.Item1;
            var cmd = pack.Item2;
            switch (transition) {
            case PickActionEnum.Fetch:
                SetAndExecute (cmd);
                return Lst.FetchW;
            case PickActionEnum.Sync:
                SetAndExecute (cmd);
                return Lst.SyncW;
            case PickActionEnum.Ping:
                SetAndExecute (cmd);
                return Lst.IdleW;
            case PickActionEnum.HotQOp:
                SetAndExecute (cmd);
                return Lst.HotQOpW;
            case PickActionEnum.QOop:
                SetAndExecute (cmd);
                return Lst.QOpW;
            case PickActionEnum.FSync:
                SetAndExecute (cmd);
                return Lst.FSyncW;
            case PickActionEnum.Wait:
                SetAndExecute (cmd);
                return Lst.IdleW;
            default:
                NcAssert.CaseError (cmd.ToString ());
                return Lst.IdleW;
            }
        }

        private void DoNopOrPick ()
        {
            // If we are parked, the Cmd has been set to null.
            // Otherwise, it has the last command executed (or still executing).
            if (null == Cmd) {
                // We are not running, go figure out what to do.
                DoPick ();
            } else {
                // We are running, ignore the Launch, stay in the current State.
            }
        }

        private void SetAndExecute (ImapCommand cmd)
        {
            if (null != cmd as ImapIdleCommand && null != PushAssist) {
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

            var disconnect = new ImapDisconnectCommand (this, MainClient);
            disconnect.Execute (this.Sm);
        }

        private void DoDrive ()
        {
            PossiblyKickPushAssist ();
            Sm.State = ProtocolState.ImapProtoControlState;
            Sm.PostEvent ((uint)SmEvt.E.Launch, "DRIVE");
        }

        private void DoUiCredReq ()
        {
            // our creds are bad. Stop pinger.
            if (null != PushAssist) {
                PushAssist.Stop ();
            }
            CancelCmd ();
            BackEndStatePreset = BackEndStateEnum.CredWait;
            // Send the request toward the UI.
            Owner.CredReq (this);
        }

        #region ValidateConfig

        private ImapValidateConfig Validator;

        public override void ValidateConfig (McServer server, McCred cred)
        {
            // Stop push assist. We just got a new credential, so chances are
            // we need to tell push assist to restart with new ones.
            if (null != PushAssist) {
                PushAssist.Stop ();
            }
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
            if (null != PushAssist && CanStartPushAssist ()) {
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
                (0 != (ProtoControl.ProtocolState.ImapServerCapabilities & McProtocolState.NcImapCapabilities.SaslIR))) {
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

