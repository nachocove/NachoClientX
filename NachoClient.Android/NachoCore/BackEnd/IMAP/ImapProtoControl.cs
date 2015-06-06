//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Text;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;
using System.Linq;
using MailKit;
using MailKit.Net.Imap;
using System.Security.Cryptography.X509Certificates;
using MailKit.Security;

namespace NachoCore.IMAP
{
    public class ImapProtoControl : NcProtoControl, IPushAssistOwner
    {
        public ImapClient ImapClient;

        public enum Lst : uint
        {
            DiscW = (St.Last + 1),
            UiCrdW,
            UiServConfW,
            FSyncW,
            Pick,
            CmdW,
            Wait,
            Idle,
            Parked,
        };

        public override BackEndStateEnum BackEndState {
            get {
                var state = Sm.State;
                if ((uint)Lst.Parked == state) {
                    state = ProtocolState.ImapProtoControlState;
                }
                // Every state above must be mapped here.
                switch (state) {
                case (uint)St.Start:
                    return BackEndStateEnum.NotYetStarted;

                case (uint)Lst.DiscW:
                    return BackEndStateEnum.Running;

                case (uint)Lst.UiCrdW:
                    return BackEndStateEnum.CredWait;

                case (uint)Lst.UiServConfW:
                    return BackEndStateEnum.ServerConfWait;

                case (uint)Lst.FSyncW:
                case (uint)Lst.CmdW:
                case (uint)Lst.Pick:
                case (uint)Lst.Wait:
                case (uint)Lst.Parked:
                    // FIXME - need to consider ProtocolState.HasSyncedInbox.
                    return BackEndStateEnum.PostAutoDPostInboxSync;

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
                FromStrat,
                FromIdle,
                Wait,
                AuthFail,
                Last = AuthFail,
            };
        }
        public ImapStrategy Strategy { set; get; }

        public ImapProtoControl (INcProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            Capabilities = McAccount.ImapCapabilities;
            SetupAccount ();
            ImapClient = newClientWithLogger ();

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
                            (uint)ImapEvt.E.FromStrat,
                            (uint)ImapEvt.E.FromIdle,
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                        },
                        Invalid = new uint[] {
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.Wait,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
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
                            (uint)ImapEvt.E.FromStrat,
                            (uint)ImapEvt.E.FromIdle,
                        },
                        Invalid = new uint[] {
                            (uint)ImapEvt.E.ReDisc,
                            (uint)ImapEvt.E.Wait,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoFSync, State = (uint)Lst.FSyncW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
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
                            (uint)ImapEvt.E.FromStrat,
                            (uint)ImapEvt.E.FromIdle,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.Wait,
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
                            (uint)ImapEvt.E.FromStrat,
                            (uint)ImapEvt.E.FromIdle,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.Wait,
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
                            (uint)PcEvt.E.PendQHot,
                            (uint)ImapEvt.E.FromStrat,
                            (uint)ImapEvt.E.FromIdle,
                        },
                        Invalid = new [] {
                            (uint)ImapEvt.E.Wait,
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
                        },
                        On = new [] {
                            // FIXME - add states for doing operations - eg Qop, Sync, etc.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.FromStrat, Act = DoArg, State = (uint)Lst.CmdW },
                            new Trans { Event = (uint)ImapEvt.E.FromIdle, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)ImapEvt.E.Wait, Act = DoArg, State = (uint)Lst.Wait },
                        }
                    },
                    new Node {
                        State = (uint)Lst.CmdW,
                        Drop = new [] {
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)ImapEvt.E.FromStrat,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)ImapEvt.E.Wait, Act = DoArg, State = (uint)Lst.Wait },
                            new Trans { Event = (uint)ImapEvt.E.FromIdle, Act = DoPick, State = (uint)Lst.Pick },
                        },
                    },
                    new Node {
                        State = (uint)Lst.Wait,
                        Drop = new [] {
                            (uint)ImapEvt.E.FromStrat,
                            (uint)ImapEvt.E.FromIdle,
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.Wait,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Parked,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)PcEvt.E.Park,
                            (uint)ImapEvt.E.FromStrat,
                            (uint)ImapEvt.E.FromIdle,
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.Wait,
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
            Strategy = new ImapStrategy (this);
            //PushAssist = new PushAssist (this);
            McPending.ResolveAllDispatchedAsDeferred (ProtoControl, Account.Id);
            NcCommStatus.Instance.CommStatusNetEvent += NetStatusEventHandler;
            NcCommStatus.Instance.CommStatusServerEvent += ServerStatusEventHandler;
        }

        // State-machine's state persistance callback.
        private void UpdateSavedState ()
        {
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

        public static string MessageServerId(McFolder folder, UniqueId ImapMessageUid)
        {
            return string.Format ("{0}:{1}", folder.ImapGuid, ImapMessageUid);
        }

        public static UniqueId ImapMessageUid(string MessageServerId)
        {
            uint x = UInt32.Parse (MessageServerId.Split (':') [1]);
            return new UniqueId(x);
        }

        public static string ImapMessageFolderGuid(string MessageServerId)
        {
            return MessageServerId.Split (':') [0];
        }

        public PushAssistParameters PushAssistParameters ()
        {
            NcAssert.True (false);
            return null;
        }

        public override void ForceStop ()
        {
            Sm.PostEvent ((uint)PcEvt.E.Park, "IMAPFORCESTOP");
        }

        public override void Remove ()
        {
            // TODO Move to base
            NcAssert.True ((uint)Lst.Parked == Sm.State || (uint)St.Start == Sm.State || (uint)St.Stop == Sm.State);
            // TODO cleanup stuff on disk like for wipe.
            NcCommStatus.Instance.CommStatusNetEvent -= NetStatusEventHandler;
            NcCommStatus.Instance.CommStatusServerEvent -= ServerStatusEventHandler;
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
            DoConn (); // For now.
        }

        private void DoConn ()
        {
            SetCmd (new ImapAuthenticateCommand (this));
            ExecuteCmd ();
        }

        private void DoFSync ()
        {
            SetCmd (new ImapFolderSyncCommand (this));
            ExecuteCmd ();
        }

        private void DoArg ()
        {
            var cmd = Sm.Arg as ImapCommand;
            /* FIXME
            if (null != cmd as AsPingCommand && null != PushAssist) {
                PushAssist.Execute ();
            }
            */
            SetCmd (cmd);
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
            Cmd.Execute (Sm);
        }

        private void DoUiCertOkReq ()
        {
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
                Log.Error (Log.LOG_IMAP, "Wy a forceautodiscovery?");
            }
            Sm.PostEvent ((uint)ImapEvt.E.UiSetServConf, "IMAPPCUSSC");
        }

        public static ImapClient newClientWithLogger()
        {
            // FIXME - redaction.
            MailKitProtocolLogger logger = new MailKitProtocolLogger ("IMAP", Log.LOG_IMAP);
            return new ImapClient (logger);
        }

        private void DoPick ()
        {
            CancelCmd ();
            Sm.ClearEventQueue ();
            var pack = Strategy.Pick ();
            var transition = pack.Item1;
            var cmd = pack.Item2;
            switch (transition) {
            case PickActionEnum.Sync:
                Sm.PostEvent ((uint)ImapEvt.E.FromStrat, "PCKSYNC", cmd);
                break;
            case PickActionEnum.Ping:
                Sm.PostEvent ((uint)ImapEvt.E.FromStrat, "PCKPING", cmd);
                break;
            case PickActionEnum.HotQOp:
                Sm.PostEvent ((uint)ImapEvt.E.FromStrat, "PCKHOTOP", cmd);
                break;
            case PickActionEnum.QOop:
                Sm.PostEvent ((uint)ImapEvt.E.FromStrat, "PCKOP", cmd);
                break;
            case PickActionEnum.FSync:
                Sm.PostEvent ((uint)ImapEvt.E.FromStrat, "PCKFSYNC", cmd);
                break;
            case PickActionEnum.Wait:
                Sm.PostEvent ((uint)ImapEvt.E.Wait, "PCKWAIT", cmd);
                break;
            default:
                Log.Error (Log.LOG_IMAP, "Unknown PickAction {0}", transition.ToString ());
                Sm.PostEvent ((uint)SmEvt.E.HardFail, "PCKHARD", cmd);
                break;
            }
        }

        private void DoPark ()
        {
            SetCmd (null);
            // Because we are going to stop for a while, we need to fail any
            // pending that aren't allowed to be delayed.
            McPending.ResolveAllDelayNotAllowedAsFailed (ProtoControl, Account.Id);
            if (null != ImapClient) {
                lock(ImapClient.SyncRoot) {
                    ImapClient.Disconnect (true); // TODO Where does the Cancellation token come from?
                }
            }
        }

        private void DoDrive ()
        {
            /*
            if (null != PushAssist) {
                if (PushAssist.IsStartOrParked ()) {
                    PushAssist.Execute ();
                }
            }
            */
            Sm.State = ProtocolState.ImapProtoControlState;
            Sm.PostEvent ((uint)SmEvt.E.Launch, "DRIVE");
        }

        private void DoUiCredReq ()
        {
            // Send the request toward the UI.
            CancelCmd ();
            Owner.CredReq (this);
        }
    }
}

