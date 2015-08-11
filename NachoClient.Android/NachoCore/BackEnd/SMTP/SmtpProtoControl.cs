//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Text;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using System.Threading;
using System.Security.Cryptography.X509Certificates;

namespace NachoCore.SMTP
{
    public class SmtpProtoControl : NcProtoControl, IBEContext
    {
        private const int KDiscoveryMaxRetries = 5;

        public enum Lst : uint
        {
            DiscW = (St.Last + 1),
            UiCrdW,
            UiCertOkW,
            UiServConfW,
            ConnW,
            QOpW,
            IdleW,
            HotQOpW,
            Parked,
        };

        public override BackEndStateEnum BackEndState {
            get {
                if (null != BackEndStatePreset) {
                    return (BackEndStateEnum)BackEndStatePreset;
                }
                var state = Sm.State;
                if ((uint)Lst.Parked == state) {
                    state = ProtocolState.SmtpProtoControlState;
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

                case (uint)Lst.UiCertOkW:
                    return BackEndStateEnum.CertAskWait;

                case (uint)Lst.ConnW:
                case (uint)Lst.HotQOpW:
                case (uint)Lst.IdleW:
                case (uint)Lst.QOpW:
                case (uint)Lst.Parked:
                    return BackEndStateEnum.PostAutoDPostInboxSync;

                default:
                    NcAssert.CaseError (string.Format ("Unhandled state {0}", StateName ((uint)Sm.State)));
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
            case (uint)Lst.UiCertOkW:
                return "UiCertOkW";
            case (uint)Lst.UiServConfW:
                return "UiServConfW";
            case (uint)Lst.ConnW:
                return "ConnW";
            case (uint)Lst.QOpW:
                return "QOpW";
            case (uint)Lst.IdleW:
                return "IdleW";
            case (uint)Lst.HotQOpW:
                return "HotQOpW";
            case (uint)Lst.Parked:
                return "Parked";
            default:
                return state.ToString ();
            }
        }

        public class SmtpEvt : PcEvt
        {
            new public enum E : uint
            {
                ReDisc = (PcEvt.E.Last + 1),
                ReConn,
                UiSetCred,
                GetServConf,
                UiSetServConf,
                GetCertOk,
                UiCertOkYes,
                UiCertOkNo,
                AuthFail,
                Last = AuthFail,
            };
        }

        public SmtpProtoControl (INcProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            Capabilities = McAccount.SmtpCapabilities;
            SetupAccount ();
            SmtpClient = new NcSmtpClient ();

            Sm = new NcStateMachine ("SMTPPC") { 
                Name = string.Format ("SMTPPC({0})", AccountId),
                LocalEventType = typeof(SmtpEvt),
                LocalStateType = typeof(Lst),
                StateChangeIndication = UpdateSavedState,
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Drop = new uint[] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)SmtpEvt.E.UiSetCred,
                            (uint)SmtpEvt.E.UiSetServConf,
                            (uint)SmtpEvt.E.UiCertOkNo,
                            (uint)SmtpEvt.E.UiCertOkYes,
                        },
                        Invalid = new uint[] {
                            (uint)SmtpEvt.E.AuthFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmtpEvt.E.GetCertOk,
                            (uint)SmtpEvt.E.GetServConf,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.ReConn, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)SmtpEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.UiCertOkW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmtpEvt.E.AuthFail,
                            (uint)SmtpEvt.E.GetCertOk,
                            (uint)SmtpEvt.E.UiSetCred, // TODO: should we re-consider?
                            (uint)SmtpEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.ReConn, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)SmtpEvt.E.UiCertOkYes, Act = DoCertOkYes, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.UiCertOkNo, Act = DoCertOkNo, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.DiscW,
                        Drop = new uint[] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)SmtpEvt.E.UiCertOkNo,
                            (uint)SmtpEvt.E.UiCertOkYes,
                        },
                        Invalid = new uint[] {
                            (uint)SmtpEvt.E.ReDisc,
                            (uint)SmtpEvt.E.ReConn,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoDiscTempFail, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.GetCertOk, Act = DoUiCertOkReq, State = (uint)Lst.UiCertOkW },
                            new Trans { Event = (uint)SmtpEvt.E.GetServConf, Act = DoUiServConfReq, State = (uint)Lst.UiServConfW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.UiCrdW,
                        Drop = new uint[] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                        },
                        Invalid = new uint[] {
                            (uint)SmtpEvt.E.ReDisc,
                            (uint)SmtpEvt.E.ReConn,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmtpEvt.E.AuthFail,
                            (uint)SmtpEvt.E.GetCertOk,
                            (uint)SmtpEvt.E.UiCertOkNo,
                            (uint)SmtpEvt.E.UiCertOkYes,
                            (uint)SmtpEvt.E.GetServConf,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.UiServConfW,
                        Drop = new uint[] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                        },
                        Invalid = new uint[] {
                            (uint)SmtpEvt.E.ReDisc,
                            (uint)SmtpEvt.E.ReConn,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmtpEvt.E.AuthFail,
                            (uint)SmtpEvt.E.GetCertOk,
                            (uint)SmtpEvt.E.UiCertOkNo,
                            (uint)SmtpEvt.E.UiCertOkYes,
                            (uint)SmtpEvt.E.GetServConf,
                        },
                        On = new Trans[] {
                            // If the creds are still bad, then disc will ask for new ones again.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.ConnW,
                        Invalid = new uint[] {
                            (uint)SmtpEvt.E.ReDisc,
                            (uint)SmtpEvt.E.ReConn,
                            (uint)SmtpEvt.E.GetCertOk,
                            (uint)SmtpEvt.E.UiCertOkNo,
                            (uint)SmtpEvt.E.UiCertOkYes,
                            (uint)SmtpEvt.E.GetServConf,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoConn, State = (uint)Lst.ConnW }, // TODO How do we keep from looping forever?
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoDisc, State = (uint)Lst.DiscW  },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.IdleW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)SmtpEvt.E.UiSetCred,
                            (uint)SmtpEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)SmtpEvt.E.GetCertOk,
                            (uint)SmtpEvt.E.UiCertOkNo,
                            (uint)SmtpEvt.E.UiCertOkYes,
                            (uint)SmtpEvt.E.GetServConf,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmtpEvt.E.ReDisc,
                            (uint)SmtpEvt.E.AuthFail,
                            (uint)SmtpEvt.E.ReConn,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoNop, State = (uint)Lst.IdleW },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                        },
                    },
                    new Node {
                        State = (uint)Lst.QOpW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)SmtpEvt.E.UiSetCred,
                            (uint)SmtpEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)SmtpEvt.E.GetCertOk,
                            (uint)SmtpEvt.E.UiCertOkNo,
                            (uint)SmtpEvt.E.UiCertOkYes,
                            (uint)SmtpEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)SmtpEvt.E.ReConn, Act = DoConn, State = (uint)Lst.ConnW },
                        },
                    },
                    new Node {
                        State = (uint)Lst.HotQOpW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)SmtpEvt.E.UiSetCred,
                            (uint)SmtpEvt.E.UiSetServConf,
                        },
                        Invalid = new [] {
                            (uint)SmtpEvt.E.GetCertOk,
                            (uint)SmtpEvt.E.UiCertOkNo,
                            (uint)SmtpEvt.E.UiCertOkYes,
                            (uint)SmtpEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)SmtpEvt.E.ReConn, Act = DoConn, State = (uint)Lst.ConnW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Parked,
                        Drop = new uint[] {
                            (uint)PcEvt.E.Park,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmtpEvt.E.AuthFail,
                            (uint)SmtpEvt.E.GetCertOk,
                            (uint)SmtpEvt.E.UiCertOkNo,
                            (uint)SmtpEvt.E.UiCertOkYes,
                            (uint)SmtpEvt.E.GetServConf,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)SmtpEvt.E.ReDisc, Act = DoConn, State = (uint)Lst.ConnW }, // TODO FIXME
                            new Trans { Event = (uint)SmtpEvt.E.ReConn, Act = DoConn, State = (uint)Lst.ConnW }, // TODO FIXME
                            new Trans { Event = (uint)SmtpEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                        }
                    }
                }
            };
            Sm.Validate ();
            Sm.State = ProtocolState.SmtpProtoControlState;
            LastBackEndState = BackEndState;
            LastIsDoNotDelayOk = IsDoNotDelayOk;
            //SyncStrategy = new SmtpStrategy (this);
            //PushAssist = new PushAssist (this);
            NcCommStatus.Instance.CommStatusNetEvent += NetStatusEventHandler;
            NcCommStatus.Instance.CommStatusServerEvent += ServerStatusEventHandler;
        }

        public override void ForceStop ()
        {
            Sm.PostEvent ((uint)PcEvt.E.Park, "SMTPFORCESTOP");
        }

        public override void Remove ()
        {
            // TODO Move to base? That might require moving the NcCommStatus stuff to base as well.
            if (!((uint)Lst.Parked == Sm.State || (uint)St.Start == Sm.State || (uint)St.Stop == Sm.State)) {
                Log.Warn (Log.LOG_SMTP, "SmtpProtoControl.Remove called while state is {0}", StateName ((uint)Sm.State));
            }
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
            Sm.PostEvent ((uint)SmEvt.E.Launch, "SMTPPCEXE");
            return true;
        }

        private SmtpCommand Cmd;

        private bool CmdIs (Type cmdType)
        {
            return (null != Cmd && Cmd.GetType () == cmdType);
        }

        private void SetCmd (SmtpCommand nextCmd)
        {
            if (null != Cmd) {
                Cmd.Cancel ();
            }
            Cmd = nextCmd;
        }

        private void ExecuteCmd ()
        {
            Cmd.Execute (Sm);
        }

        public NcSmtpClient SmtpClient;

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
            var cmd = new SmtpDiscoveryCommand (this, SmtpClient);
            cmd.Execute (Sm);
        }

        private int DiscoveryRetries = 0;
        private void DoDiscTempFail ()
        {
            Log.Info (Log.LOG_SMTP, "SMTP DoDisc Attempt {0}", DiscoveryRetries++);
            if (DiscoveryRetries >= KDiscoveryMaxRetries) {
                Sm.PostEvent ((uint)SmtpEvt.E.GetServConf, "SMTPMAXDISC");
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

        private X509Certificate2 _ServerCertToBeExamined;

        public override X509Certificate2 ServerCertToBeExamined {
            get {
                return _ServerCertToBeExamined;
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
            DoDisc ();
        }

        private void DoCertOkYes ()
        {
            DoDisc ();
        }
        public override void CredResp ()
        {
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)SmtpEvt.E.UiSetCred, "SMTPPCUSC");
            }, "SmtpCredResp");
        }

        public override void ServerConfResp (bool forceAutodiscovery)
        {
            if (forceAutodiscovery) {
                Log.Error (Log.LOG_SMTP, "Why a forceautodiscovery?");
            }
            Sm.PostEvent ((uint)SmtpEvt.E.UiSetServConf, "SMTPPCUSSC");
        }

        private void DoConn ()
        {
            DiscoveryRetries = 0;
            var cmd = new SmtpAuthenticateCommand(this, SmtpClient);
            cmd.Execute (Sm);
        }

        private void CancelCmd ()
        {
            if (null != Cmd) {
                Cmd.Cancel ();
                Cmd = null;
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
            // Due to threading race condition we must clear any event possibly posted
            // by a non-cancelled-in-time await.
            // TODO: find a way to detect already running op and log an error.
            // TODO: couple ClearEventQueue with PostEvent inside SM mutex.
            Sm.ClearEventQueue ();
            var send = McPending.QueryEligible (AccountId, McAccount.SmtpCapabilities).
                Where (x => 
                    McPending.Operations.EmailSend == x.Operation ||
                       McPending.Operations.EmailForward == x.Operation ||
                       McPending.Operations.EmailReply == x.Operation
                       ).FirstOrDefault ();
            if (null != send) {
                Log.Info (Log.LOG_SMTP, "Strategy:FG/BG:Send");
                switch (send.Operation) {
                case McPending.Operations.EmailSend:
                    SetAndExecute (new SmtpSendMailCommand (this, SmtpClient, send));
                    return Lst.QOpW;
                case McPending.Operations.EmailForward:
                    SetAndExecute (new SmtpForwardMailCommand (this, SmtpClient, send));
                    return Lst.QOpW;
                case McPending.Operations.EmailReply:
                    SetAndExecute (new SmtpReplyMailCommand (this, SmtpClient, send));
                    return Lst.QOpW;
                default:
                    NcAssert.CaseError (send.Operation.ToString ());
                    return Lst.IdleW;
                }
            } else {
                SetAndExecute (new SmtpDisconnectCommand (this, SmtpClient));
                return Lst.IdleW;
            }
        }

        private void SetAndExecute (SmtpCommand cmd)
        {
            SetCmd (cmd);
            ExecuteCmd ();
        }

        protected void DoIdle ()
        {
            SetCmd (new SmtpDisconnectCommand (this, SmtpClient));
            ExecuteCmd ();
        }

        private void DoPark ()
        {
            // Because we are going to stop for a while, we need to fail any
            // pending that aren't allowed to be delayed.
            SetCmd (null);
            McPending.ResolveAllDelayNotAllowedAsFailed (ProtoControl, Account.Id);
            lock (SmtpClient.SyncRoot) {
                SmtpClient.Disconnect (true);
            }
        }

        private void DoDrive ()
        {
            Sm.State = ProtocolState.SmtpProtoControlState;
            Sm.PostEvent ((uint)SmEvt.E.Launch, "DRIVE");
        }

        private void DoUiCredReq ()
        {
            if (null != Cmd) {
                Cmd.Cancel ();
            }
            BackEndStatePreset = BackEndStateEnum.CredWait;
            // Send the request toward the UI.
            Owner.CredReq (this);
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
                    target.SmtpProtoControlState = stateToSave;
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

        public void ServerStatusEventHandler (Object sender, NcCommStatusServerEventArgs e)
        {
            if (e.ServerId == Server.Id) {
                switch (e.Quality) {
                case NcCommStatus.CommQualityEnum.OK:
                    Log.Info (Log.LOG_SMTP, "Server {0} communication quality OK.", Server.Host);
                    Execute ();
                    break;

                default:
                case NcCommStatus.CommQualityEnum.Degraded:
                    Log.Info (Log.LOG_SMTP, "Server {0} communication quality degraded.", Server.Host);
                    break;

                case NcCommStatus.CommQualityEnum.Unusable:
                    Log.Info (Log.LOG_SMTP, "Server {0} communication quality unusable.", Server.Host);
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

        #region ValidateConfig

        private SmtpValidateConfig Validator;
        public override void ValidateConfig (McServer server, McCred cred)
        {
            CancelValidateConfig ();
            Validator = new SmtpValidateConfig (this);
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
    }
}

