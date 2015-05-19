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

namespace NachoCore.SMTP
{
    public class SmtpProtoControl : NcProtoControl
    {
        public enum Lst : uint
        {
            DiscW = (St.Last + 1),
            ConnW,
            Pick,
            //UiDCrdW,
            //UiPCrdW,
            Parked,
        };

        public class SmtpEvt : PcEvt
        {
            new public enum E : uint
            {
                ReDisc = (PcEvt.E.Last + 1),
                ReConn,
                //                ReProv,
                //                UiSetCred,
                //                GetServConf,
                //                UiSetServConf,
                //                GetCertOk,
                //                UiCertOkYes,
                //                UiCertOkNo,
                PkQOp,
                PkHotQOp,
                AuthFail,
                Last = AuthFail,
            };
        }

        public SmtpProtoControl (IProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            Capabilities = McAccount.SmtpCapabilities;

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
                        },
                        Invalid = new uint[] {
                            (uint)SmtpEvt.E.ReDisc,
                            (uint)SmtpEvt.E.AuthFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.ReConn, Act = DoConn, State = (uint)Lst.ConnW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.DiscW,
                        Drop = new uint[] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)SmtpEvt.E.PkQOp,
                            (uint)SmtpEvt.E.PkHotQOp,
                        },
                        Invalid = new uint[] {
                            (uint)SmtpEvt.E.ReDisc,
                            (uint)SmtpEvt.E.ReConn,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoConn, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPark, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoConn, State = (uint)Lst.ConnW }, // TODO How do we keep from looping forever?
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoConn, State = (uint)Lst.ConnW  }, // TODO Should go back to discovery, not connection.
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)St.Start },
                        }
                    },
                    new Node {
                        State = (uint)Lst.ConnW,
                        Drop = new uint[] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)SmtpEvt.E.PkQOp,
                            (uint)SmtpEvt.E.PkHotQOp,
                        },
                        Invalid = new uint[] {
                            (uint)SmEvt.E.Launch,
                            (uint)SmtpEvt.E.ReDisc,
                            (uint)SmtpEvt.E.ReConn,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPark, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoConn, State = (uint)Lst.ConnW }, // TODO How do we keep from looping forever?
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoConn, State = (uint)Lst.ConnW  }, // TODO Should go back to discovery, not connection.
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)St.Start },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Pick,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQ,
                            (uint)PcEvt.E.PendQHot,
                            (uint)SmtpEvt.E.PkQOp,
                            (uint)SmtpEvt.E.PkHotQOp,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmtpEvt.E.AuthFail,
                            (uint)SmEvt.E.Launch,
                        },
                        On = new [] {
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmtpEvt.E.ReDisc, Act = DoConn, State = (uint)Lst.ConnW }, // TODO FIXME
                            new Trans { Event = (uint)SmtpEvt.E.ReConn, Act = DoConn, State = (uint)Lst.ConnW }, // TODO FIXME
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
                            (uint)SmEvt.E.Launch,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)PcEvt.E.PendQ, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoConn, State = (uint)Lst.ConnW },
                            new Trans { Event = (uint)SmtpEvt.E.ReDisc, Act = DoConn, State = (uint)Lst.ConnW }, // TODO FIXME
                            new Trans { Event = (uint)SmtpEvt.E.ReConn, Act = DoConn, State = (uint)Lst.ConnW }, // TODO FIXME
                        }
                    }
                }
            };
            Sm.Validate ();
            SetupProtocolState ();
            Sm.State = ProtocolState.ProtoControlState;
            //SyncStrategy = new SmtpStrategy (this);
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
        private void EstablishService ()
        {
            SetupProtocolState ();
            // Create file directories.
            NcModel.Instance.InitializeDirs (AccountId);
        }

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

                default:
                    NcAssert.CaseError (string.Format ("Unhandled state {0}", Sm.State));
                    return BackEndStateEnum.PostAutoDPostInboxSync;
                }
            }
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

        private async void DoPark ()
        {
            SetCmd (null);
            // Because we are going to stop for a while, we need to fail any
            // pending that aren't allowed to be delayed.
            McPending.ResolveAllDelayNotAllowedAsFailed (ProtoControl, Account.Id);
            if (null != m_smtpClient) {
                await m_smtpClient.DisconnectAsync (true).ConfigureAwait (false); // TODO Where does the Cancellation token come from?
            }
        }

        private async void DoConn ()
        {
            if (null == m_smtpClient) {
                try {
                    var server = McServer.QueryByAccountId<McServer> (Account.Id).SingleOrDefault ();
                    if (null == server) {
                        // Yes, the SM is SOL at this point.
                        Log.Error (Log.LOG_PUSH, "DoConn: No McServer for accountId {0}", AccountId);
                        Sm.PostEvent ((uint)SmEvt.E.HardFail, "SMTP_START_NO_SERVER");
                        return;
                    }
                    var cred = McCred.QueryByAccountId<McCred> (AccountId).FirstOrDefault ();
                    if (null == cred) {
                        // Yes, the SM is SOL at this point.
                        Log.Error (Log.LOG_PUSH, "DoConn: No McCred for accountId {0}", AccountId);
                        Sm.PostEvent ((uint)SmEvt.E.HardFail, "SMTP_START_NO_CRED");
                        return;
                    }
                    SmtpProtocolLogger logger = new SmtpProtocolLogger ();
                    m_smtpClient = new SmtpClient (logger);
                    //m_smtpClient.ClientCertificates = new X509CertificateCollection ();
                    await m_smtpClient.ConnectAsync (server.Host, server.Port, false).ConfigureAwait (false);

                    // Note: since we don't have an OAuth2 token, disable
                    // the XOAUTH2 authentication mechanism.
                    m_smtpClient.AuthenticationMechanisms.Remove ("XOAUTH2");

                    await m_smtpClient.AuthenticateAsync (cred.Username, cred.GetPassword ()).ConfigureAwait (false);
                    Sm.PostEvent ((uint)SmEvt.E.Success, "SMTPCONEST");
                } catch (SmtpProtocolException e) {
                    Log.Error (Log.LOG_SMTP, "Could not set up authenticated client: {0}", e);
                    Sm.PostEvent ((uint)SmEvt.E.HardFail, "SMTPPROTOFAIL");
                } catch (AuthenticationException e) {
                    Log.Error (Log.LOG_SMTP, "Authentication failed: {0}", e);
                    Sm.PostEvent ((uint)SmtpEvt.E.AuthFail, "SMTPAUTHFAIL");
                }
            }
        }

        private async void DoPick ()
        {
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
                Log.Info (Log.LOG_AS, "Strategy:FG/BG:Send");
                switch (send.Operation) {
                case McPending.Operations.EmailSend:
                    // TODO Fill me in
                    break;
                default:
                    NcAssert.CaseError (send.Operation.ToString ());
                    break;
                }
                // Get a new one.
                Sm.PostEvent ((uint)SmtpEvt.E.PkQOp, "SMTPGETNEXT");
            } else {
                Sm.PostEvent ((uint)PcEvt.E.Park, "SMTPPARK");
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
            protocolState.ProtoControlState = stateToSave;
            protocolState.Update ();
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

        public class SmtpProtocolLogger : IProtocolLogger
        {
            public void LogConnect (Uri uri)
            {
                if (uri == null)
                    throw new ArgumentNullException ("uri");

                Log.Info (Log.LOG_SMTP, "Connected to {0}", uri);
            }

            private void logBuffer (string prefix, byte[] buffer, int offset, int count)
            {
                char[] delimiterChars = { '\n' };
                var lines = Encoding.UTF8.GetString (buffer.Skip (offset).Take (count).ToArray ()).Split (delimiterChars);

                Array.ForEach (lines, (line) => {
                    if (line.Length > 0) {
                        Log.Info (Log.LOG_SMTP, "{0}{1}", prefix, line);
                    }
                });
            }

            public void LogClient (byte[] buffer, int offset, int count)
            {
                logBuffer ("SMTP C: ", buffer, offset, count);
            }

            public void LogServer (byte[] buffer, int offset, int count)
            {
                logBuffer ("SMTP S: ", buffer, offset, count);
            }

            public void Dispose ()
            {
            }
        }

    }
}

