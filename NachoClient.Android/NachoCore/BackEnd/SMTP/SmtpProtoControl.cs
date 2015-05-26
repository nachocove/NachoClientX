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
        private SmtpValidateConfig Validator;

        public enum Lst : uint
        {
            DiscW = (St.Last + 1),
            UiDCrdW,
            UiPCrdW,
            UiCertOkW,
            ConnW,
            Pick,
            Parked,
        };

        public override BackEndStateEnum BackEndState {
            get {
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

                case (uint)Lst.UiCertOkW:
                    return BackEndStateEnum.CertAskWait;

                case (uint)Lst.ConnW:
                case (uint)Lst.Pick:
                case (uint)Lst.Parked:
                    return BackEndStateEnum.PostAutoDPostInboxSync;

                default:
                    NcAssert.CaseError (string.Format ("Unhandled state {0}", Sm.State));
                    return BackEndStateEnum.PostAutoDPostInboxSync;
                }
            }
        }

        public class SmtpEvt : PcEvt
        {
            new public enum E : uint
            {
                ReDisc = (PcEvt.E.Last + 1),
                ReConn,
                UiSetCred,
//                GetServConf,
                UiSetServConf,
                GetCertOk,
                UiCertOkYes,
                UiCertOkNo,
                PkQOp,
                PkHotQOp,
                AuthFail,
                Last = AuthFail,
            };
        }

        public SmtpProtoControl (INcProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            Capabilities = McAccount.SmtpCapabilities;
            SetupAccount ();

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
                            (uint)SmtpEvt.E.PkQOp,
                            (uint)SmtpEvt.E.PkHotQOp,
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
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
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
                            (uint)SmtpEvt.E.PkQOp,
                            (uint)SmtpEvt.E.PkHotQOp,
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
                            (uint)SmtpEvt.E.PkQOp,
                            (uint)SmtpEvt.E.PkHotQOp,
                            (uint)SmtpEvt.E.UiCertOkNo,
                            (uint)SmtpEvt.E.UiCertOkYes,
                        },
                        Invalid = new uint[] {
                            (uint)SmtpEvt.E.ReDisc,
                            (uint)SmtpEvt.E.ReConn,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoConn, State = (uint)Lst.ConnW }, // TODO How do we keep from looping forever?
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoConn, State = (uint)Lst.ConnW }, // TODO Should go back to discovery, not connection.
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)St.Start },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.GetCertOk, Act = DoUiCertOkReq, State = (uint)Lst.UiCertOkW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.UiDCrdW,
                        Drop = new uint[] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                        },
                        Invalid = new uint[] {
                            (uint)SmtpEvt.E.ReDisc,
                            (uint)SmtpEvt.E.ReConn,
                            (uint)SmtpEvt.E.PkQOp,
                            (uint)SmtpEvt.E.PkHotQOp,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmtpEvt.E.AuthFail,
                            (uint)SmtpEvt.E.GetCertOk,
                            (uint)SmtpEvt.E.UiCertOkNo,
                            (uint)SmtpEvt.E.UiCertOkYes,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.ConnW,
                        Drop = new uint[] {
                            (uint)SmtpEvt.E.PkQOp,
                            (uint)SmtpEvt.E.PkHotQOp,
                        },
                        Invalid = new uint[] {
                            (uint)SmtpEvt.E.ReDisc,
                            (uint)SmtpEvt.E.ReConn,
                            (uint)SmtpEvt.E.GetCertOk,
                            (uint)SmtpEvt.E.UiCertOkNo,
                            (uint)SmtpEvt.E.UiCertOkYes,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoConn, State = (uint)Lst.ConnW }, // TODO How do we keep from looping forever?
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoConn, State = (uint)Lst.ConnW  }, // TODO Should go back to discovery, not connection.
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiDCrdW },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Pick,
                        Drop = new [] {
                            (uint)SmtpEvt.E.PkHotQOp,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmtpEvt.E.AuthFail,
                            (uint)SmtpEvt.E.GetCertOk,
                            (uint)SmtpEvt.E.UiCertOkNo,
                            (uint)SmtpEvt.E.UiCertOkYes,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.ReDisc, Act = DoConn, State = (uint)Lst.ConnW }, // TODO FIXME
                            new Trans { Event = (uint)SmtpEvt.E.ReConn, Act = DoConn, State = (uint)Lst.ConnW }, // TODO FIXME
                            new Trans { Event = (uint)SmtpEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmtpEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmtpEvt.E.PkQOp, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Parked,
                        Drop = new uint[] {
                            (uint)SmtpEvt.E.PkQOp,
                            (uint)SmtpEvt.E.PkHotQOp,
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
            Sm.State = ProtocolState.ProtoControlState;
            //SyncStrategy = new SmtpStrategy (this);
            //PushAssist = new PushAssist (this);
            McPending.ResolveAllDispatchedAsDeferred (ProtoControl, Account.Id);
            NcCommStatus.Instance.CommStatusNetEvent += NetStatusEventHandler;
            NcCommStatus.Instance.CommStatusServerEvent += ServerStatusEventHandler;
        }

        public override void Remove ()
        {
            // TODO Move to base? That might require moving the NcCommStatus stuff to base as well.
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
            Sm.PostEvent ((uint)SmEvt.E.Launch, "ASPCEXE");
            return true;
        }

        private ISmtpCommand Cmd;

        private bool CmdIs (Type cmdType)
        {
            return (null != Cmd && Cmd.GetType () == cmdType);
        }

        private void SetCmd (ISmtpCommand nextCmd)
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

        private SmtpClient m_smtpClient;

        private void DoDisc ()
        {
            NcTask.Run (delegate {
                if (null != m_smtpClient) {
                    if (m_smtpClient.IsConnected) {
                        lock(m_smtpClient.SyncRoot) {
                            m_smtpClient.Disconnect (true);
                        }
                        m_smtpClient = null;
                    }
                }
                SmtpClient client = newClientWithLogger();
                var cmd = new SmtpAuthenticateCommand(client, Server, Cred);
                cmd.Execute (Sm);
            }, "SmtpDoDisc");
        }

        private X509Certificate2 _ServerCertToBeExamined;

        public override X509Certificate2 ServerCertToBeExamined {
            get {
                return _ServerCertToBeExamined;
            }
        }

        private void DoUiCertOkReq ()
        {
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
            Sm.PostEvent ((uint)SmtpEvt.E.UiSetServConf, "ASPCUSSC");
        }

        public static SmtpClient newClientWithLogger()
        {
            MailKitProtocolLogger logger = new MailKitProtocolLogger ("SMTP", Log.LOG_SMTP);
            return new SmtpClient (logger);
        }

        private void DoConn ()
        {
            if (null != m_smtpClient && m_smtpClient.IsConnected) {
                m_smtpClient = null;
            }
            if (null == m_smtpClient) {
                NcTask.Run (delegate {
                    m_smtpClient = newClientWithLogger();
                    var cmd = new SmtpAuthenticateCommand(m_smtpClient, Server, Cred);
                    cmd.Execute (Sm);
                }, "SmtpDoConn");
            }
        }

        private void DoPick ()
        {
            // Due to threading race condition we must clear any event possibly posted
            // by a non-cancelled-in-time await.
            // TODO: find a way to detect already running op and log an error.
            // TODO: couple ClearEventQueue with PostEvent inside SM mutex.
            Sm.ClearEventQueue ();
            var send = McPending.QueryEligible (AccountId).
                Where (x => 
                    McPending.Operations.EmailSend == x.Operation ||
                       McPending.Operations.EmailForward == x.Operation ||
                       McPending.Operations.EmailReply == x.Operation
                       ).FirstOrDefault ();
            if (null != send) {
                Log.Info (Log.LOG_SMTP, "Strategy:FG/BG:Send");
                switch (send.Operation) {
                case McPending.Operations.EmailSend:
                case McPending.Operations.EmailForward:
                case McPending.Operations.EmailReply:
                    var cmd = new SmtpSendMailCommand (m_smtpClient, send);
                    cmd.Execute (Sm);
                    NcTask.Run (delegate {
                        Sm.PostEvent ((uint)SmtpEvt.E.PkQOp, "SMTPGETNEXT");
                    }, "SMTPSEND");
                    break;
                default:
                    NcAssert.CaseError (send.Operation.ToString ());
                    break;
                }
            } else {
                NcTask.Run (delegate {
                    Sm.PostEvent ((uint)PcEvt.E.Park, "SMTPPICKPARK");
                }, "SMTPSENDPA");
            }
        }

        private void DoPark ()
        {
            // Because we are going to stop for a while, we need to fail any
            // pending that aren't allowed to be delayed.
            McPending.ResolveAllDelayNotAllowedAsFailed (ProtoControl, Account.Id);
            if (null != m_smtpClient) {
                if (m_smtpClient.IsConnected) {
                    lock (m_smtpClient.SyncRoot) {
                        m_smtpClient.Disconnect (true);
                    }
                }
                m_smtpClient = null;
            }
        }

        private void DoUiCredReq ()
        {
            // Send the request toward the UI.
            if (null != Cmd) {
                Cmd.Cancel ();
            }
            Owner.CredReq (this);
        }


        // State-machine's state persistance callback.
        private void UpdateSavedState ()
        {
            var protocolState = ProtocolState;
            uint stateToSave = Sm.State;
            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.SmtpProtoControlState = stateToSave;
                return true;
            });
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

        public override void ValidateConfig (McServer server, McCred cred)
        {
            CancelValidateConfig ();
            Validator = new SmtpValidateConfig(this);
            Validator.Execute (server, cred);
        }

        public override void CancelValidateConfig ()
        {
            if (null != Validator) {
                Validator.Cancel ();
                Validator = null;
            }
        }

        public override NcResult SendEmailCmd (int emailMessageId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            Log.Info (Log.LOG_SMTP, "SendEmailCmd({0})", emailMessageId);
            NcModel.Instance.RunInTransaction (() => {
                var emailMessage = McAbstrObject.QueryById<McEmailMessage> (emailMessageId);
                if (null == emailMessage) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }
                var pending = new McPending (Account.Id, emailMessage) {
                    Operation = McPending.Operations.EmailSend,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQHot, "PCPCSEND");
            }, "SendEmailCmd");
            Log.Info (Log.LOG_AS, "SendEmailCmd({0}) returning {1}", emailMessageId, result.Value as string);
            return result;
        }
    }
}

