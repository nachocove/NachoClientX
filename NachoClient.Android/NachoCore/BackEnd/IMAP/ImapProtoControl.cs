﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;
using System.Linq;
using MailKit.Net.Imap;
using System.Security.Cryptography.X509Certificates;
using MailKit.Security;

namespace NachoCore.IMAP
{
    public class ImapProtoControl : NcProtoControl, IPushAssistOwner
    {
        ImapClient m_imapClient;

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

        public class ImapEvt : PcEvt
        {
            new public enum E : uint
            {
                ReDisc = (PcEvt.E.Last + 1),
                ReConn,
                UiSetCred,
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

        public ImapProtoControl (IProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            Capabilities = McAccount.ImapCapabilities;

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
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)ImapEvt.E.UiSetCred,
                            (uint)ImapEvt.E.UiSetServConf,
                            (uint)ImapEvt.E.UiCertOkNo,
                            (uint)ImapEvt.E.UiCertOkYes,
                        },
                        Invalid = new uint[] {
                            (uint)ImapEvt.E.AuthFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.GetCertOk,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReConn, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
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
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.GetCertOk,
                            (uint)ImapEvt.E.UiSetCred, // TODO: should we re-consider?
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.ReConn, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)ImapEvt.E.UiCertOkYes, Act = DoCertOkYes, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.UiCertOkNo, Act = DoCertOkNo, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.DiscW,
                        Drop = new uint[] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)ImapEvt.E.UiCertOkNo,
                            (uint)ImapEvt.E.UiCertOkYes,
                        },
                        Invalid = new uint[] {
                            (uint)ImapEvt.E.ReDisc,
                            (uint)ImapEvt.E.ReConn,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoConn, State = (uint)Lst.ConnW }, // TODO How do we keep from looping forever?
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoConn, State = (uint)Lst.ConnW }, // TODO Should go back to discovery, not connection.
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)St.Start },
                            new Trans { Event = (uint)ImapEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.GetCertOk, Act = DoUiCertOkReq, State = (uint)Lst.UiCertOkW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.UiDCrdW,
                        Drop = new uint[] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                        },
                        Invalid = new uint[] {
                            (uint)ImapEvt.E.ReDisc,
                            (uint)ImapEvt.E.ReConn,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.GetCertOk,
                            (uint)ImapEvt.E.UiCertOkNo,
                            (uint)ImapEvt.E.UiCertOkYes,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.ConnW,
                        Drop = new uint[] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                        },
                        Invalid = new uint[] {
                            (uint)ImapEvt.E.ReDisc,
                            (uint)ImapEvt.E.ReConn,
                            (uint)ImapEvt.E.GetCertOk,
                            (uint)ImapEvt.E.UiCertOkNo,
                            (uint)ImapEvt.E.UiCertOkYes,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoConn, State = (uint)Lst.ConnW }, // TODO How do we keep from looping forever?
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoConn, State = (uint)Lst.ConnW  }, // TODO Should go back to discovery, not connection.
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiDCrdW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Pick,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.GetCertOk,
                            (uint)ImapEvt.E.UiCertOkNo,
                            (uint)ImapEvt.E.UiCertOkYes,
                        },
                        On = new [] {
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoConn, State = (uint)Lst.ConnW }, // TODO FIXME
                            new Trans { Event = (uint)ImapEvt.E.ReConn, Act = DoConn, State = (uint)Lst.ConnW }, // TODO FIXME
                            new Trans { Event = (uint)ImapEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Parked,
                        Drop = new uint[] {
                            (uint)ImapEvt.E.PkQOp,
                            (uint)ImapEvt.E.PkHotQOp,
                            (uint)PcEvt.E.Park,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapEvt.E.AuthFail,
                            (uint)ImapEvt.E.GetCertOk,
                            (uint)ImapEvt.E.UiCertOkNo,
                            (uint)ImapEvt.E.UiCertOkYes,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoConn, State = (uint)Lst.ConnW }, // TODO FIXME
                            new Trans { Event = (uint)ImapEvt.E.ReConn, Act = DoConn, State = (uint)Lst.ConnW }, // TODO FIXME
                            new Trans { Event = (uint)ImapEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.UiSetServConf, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                        }
                    }
                }
            };
            Sm.Validate ();
            SetupProtocolState ();
            Sm.State = ProtocolState.ProtoControlState;
            //SyncStrategy = new ImapStrategy (this);
            //PushAssist = new PushAssist (this);
            McPending.ResolveAllDispatchedAsDeferred (ProtoControl, Account.Id);
            NcCommStatus.Instance.CommStatusNetEvent += NetStatusEventHandler;
            NcCommStatus.Instance.CommStatusServerEvent += ServerStatusEventHandler;
        }

        private void SetupProtocolState()
        {
            // Hang our records off Account.
            NcModel.Instance.RunInTransaction (() => {
                var account = Account;
                var policy = McPolicy.QueryByAccountId<McPolicy> (account.Id).SingleOrDefault ();
                if (null == policy) {
                    policy = new McPolicy () {
                        AccountId = account.Id,
                    };
                    policy.Insert ();
                }
                var protocolState = McProtocolState.QueryByAccountId<McProtocolState> (account.Id).SingleOrDefault ();
                if (null == protocolState) {
                    protocolState = new McProtocolState () {
                        AccountId = account.Id,
                    };
                    protocolState.Insert ();
                }
            });

        }
        // State-machine's state persistance callback.
        private void UpdateSavedState ()
        {
            var protocolState = ProtocolState;
            uint stateToSave = Sm.State;
            protocolState.ProtoControlState = stateToSave;
            protocolState.Update ();
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
                Sm.PostEvent ((uint)PcEvt.E.Park, "NSEHPARK");
            }
        }

        public PushAssistParameters PushAssistParameters ()
        {
            NcAssert.True (false);
            return null;
        }

        private void EstablishService ()
        {
            SetupProtocolState ();
            // Create file directories.
            NcModel.Instance.InitializeDirs (AccountId);
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

        public override void Execute ()
        {
            if (NetStatusStatusEnum.Up != NcCommStatus.Instance.Status) {
                Log.Warn (Log.LOG_IMAP, "Execute called while network is down.");
                return;
            }
            Sm.PostEvent ((uint)SmEvt.E.Launch, "ASPCEXE");
        }
        private void DoDisc ()
        {
            NcTask.Run (async delegate {
                if (null != m_imapClient) {
                    if (m_imapClient.IsConnected) {
                        lock(m_imapClient.SyncRoot) {
                            m_imapClient.Disconnect (true);
                        }
                        m_imapClient = null;
                    }
                }
                Sm.PostEvent ((uint)SmEvt.E.Success, "IMAPAUTODDASC");
            }, "ImapDoDisc");
        }

        private X509Certificate2 _ServerCertToBeExamined;

        public override X509Certificate2 ServerCertToBeExamined {
            get {
                return _ServerCertToBeExamined;
            }
        }

        private IImapCommand Cmd;

        private bool CmdIs (Type cmdType)
        {
            return (null != Cmd && Cmd.GetType () == cmdType);
        }

        private void SetCmd (IImapCommand nextCmd)
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
                Sm.PostEvent ((uint)ImapEvt.E.UiSetCred, "IMAPPCUSC");
            }, "ImapCredResp");
        }

        public override void ServerConfResp (bool forceAutodiscovery)
        {
            if (forceAutodiscovery) {
                Log.Error (Log.LOG_IMAP, "Wy a forceautodiscovery?");
            }
            Sm.PostEvent ((uint)ImapEvt.E.UiSetServConf, "ASPCUSSC");
        }

        public static ImapClient newClientWithLogger()
        {
            MailKitProtocolLogger logger = new MailKitProtocolLogger ("IMAP", Log.LOG_IMAP);
            return new ImapClient (logger);
        }

        private ImapClient temp_client { get; set; }
        private async void DoConn ()
        {
            if (null == m_imapClient) {
                NcTask.Run (delegate {
                    try {
                        temp_client = newClientWithLogger();
                        var cmd = new ImapAuthenticateCommand(Server, Cred, temp_client);
                        SetCmd (cmd);
                        ExecuteCmd ();
                    } catch (ImapProtocolException e) {
                        Log.Error (Log.LOG_IMAP, "Could not set up authenticated client: {0}", e);
                        Sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPPROTOFAIL");
                    } catch (AuthenticationException e) {
                        Log.Error (Log.LOG_IMAP, "Authentication failed: {0}", e);
                        Sm.PostEvent ((uint)ImapEvt.E.AuthFail, "IMAPAUTHFAIL");
                    }
                }, "ImapDoConn");
            }
        }

        private async void DoPick ()
        {
            if (null != temp_client) {
                m_imapClient = temp_client;
                temp_client = null;
            }

            // Due to threading race condition we must clear any event possibly posted
            // by a non-cancelled-in-time await.
            // TODO: find a way to detect already running op and log an error.
            // TODO: couple ClearEventQueue with PostEvent inside SM mutex.
            if (null != Cmd) {
                Cmd.Cancel ();
            }
            Sm.ClearEventQueue ();
            var send = McPending.QueryEligible (AccountId).
                Where (x => 
                    McPending.Operations.EmailSend == x.Operation ||
                    McPending.Operations.EmailForward == x.Operation ||
                    McPending.Operations.EmailReply == x.Operation ||
                    McPending.Operations.CalRespond == x.Operation ||
                    McPending.Operations.CalForward == x.Operation
                ).FirstOrDefault ();
            if (null != send) {
                Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Send");
                switch (send.Operation) {
                default:
                    NcAssert.CaseError (send.Operation.ToString ());
                    break;
                }
                // Get a new one.
                Sm.PostEvent ((uint)ImapEvt.E.PkQOp, "IMAPGETNEXT");
            } else {
                Sm.PostEvent ((uint)PcEvt.E.Park, "IMAPPARK");
            }
        }

        private async void DoPark ()
        {
            SetCmd (null);
            // Because we are going to stop for a while, we need to fail any
            // pending that aren't allowed to be delayed.
            McPending.ResolveAllDelayNotAllowedAsFailed (ProtoControl, Account.Id);
            if (null != m_imapClient) {
                await m_imapClient.DisconnectAsync (true).ConfigureAwait (false); // TODO Where does the Cancellation token come from?
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


    }
}

