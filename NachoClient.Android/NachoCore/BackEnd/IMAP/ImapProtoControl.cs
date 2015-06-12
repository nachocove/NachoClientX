//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;
using MailKit;
using MailKit.Net.Imap;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace NachoCore.IMAP
{
    public class ImapProtoControl : NcProtoControl, IPushAssistOwner, INcProtocolLogger
    {
        public ImapClient MainClient;

        public enum Lst : uint
        {
            DiscW = (St.Last + 1),
            UiCrdW,
            UiServConfW,
            FSyncW,
            Pick,
            QOpW,
            HotQOpW,
            Wait,
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

                case (uint)Lst.UiCrdW:
                    return BackEndStateEnum.CredWait;

                case (uint)Lst.UiServConfW:
                    return BackEndStateEnum.ServerConfWait;

                case (uint)Lst.DiscW:
                case (uint)Lst.FSyncW:
                case (uint)Lst.QOpW:
                case (uint)Lst.HotQOpW:
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
            MainClient = newImapClientWithLogger ();

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
                            new Trans { Event = (uint)ImapEvt.E.FromStrat, Act = DoArg, State = (uint)Lst.QOpW },
                            new Trans { Event = (uint)ImapEvt.E.Wait, Act = DoArg, State = (uint)Lst.Wait },
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
                            (uint)ImapEvt.E.FromStrat,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)ImapEvt.E.Wait, Act = DoArg, State = (uint)Lst.Wait },
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
                            (uint)ImapEvt.E.FromStrat,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, State = (uint)Lst.Pick },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoExtraOrDont, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)ImapEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                            new Trans { Event = (uint)ImapEvt.E.Wait, Act = DoArg, State = (uint)Lst.Wait },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Wait,
                        Drop = new [] {
                            (uint)ImapEvt.E.FromStrat,
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
            SetCmd (new ImapDiscoverCommand (this, MainClient));
            ExecuteCmd ();
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
                Log.Error (Log.LOG_IMAP, "Why a forceautodiscovery?");
            }
            Sm.PostEvent ((uint)ImapEvt.E.UiSetServConf, "IMAPPCUSSC");
        }

        #region INcProtocolLogger implementation

        public bool ShouldLog ()
        {
            return false;
        }

        #endregion

        public ImapClient newImapClientWithLogger()
        {
            MailKitProtocolLogger logger = new MailKitProtocolLogger ("IMAP", Log.LOG_IMAP, this);
            return new ImapClient (logger);
        }

        private void DoNop ()
        {
        }

        private void DoExDone ()
        {
            Interlocked.Decrement (ref ConcurrentExtraRequests);
            // Send the PendQHot so that the ProtoControl SM looks to see if there is another hot op
            // to run in parallel.
            Sm.PostEvent ((uint)PcEvt.E.PendQHot, "DOEXDONE1MORE");
        }

        private int MaxConcurrentExtraRequests = 2;
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
                ImapClient Client = newImapClientWithLogger ();  // Presumably this will get cleaned up by GC?
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
                                    (uint)ImapEvt.E.ReDisc,
                                    (uint)ImapEvt.E.UiSetCred,
                                    (uint)ImapEvt.E.UiSetServConf,
                                    (uint)ImapEvt.E.FromStrat,
                                    (uint)ImapEvt.E.Wait,

                                },
                                On = new Trans[] {
                                    new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)St.Start },
                                    new Trans { Event = (uint)SmEvt.E.Success, Act = DoExDone, State = (uint)St.Stop },
                                    new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoExDone, State = (uint)St.Stop },
                                    new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoExDone, State = (uint)St.Stop },
                                    new Trans { Event = (uint)ImapEvt.E.AuthFail, Act = DoExDone, State = (uint)St.Stop },
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
            var pack = Strategy.Pick (MainClient);
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
            lock(MainClient.SyncRoot) {
                MainClient.Disconnect (true); // TODO Where does the Cancellation token come from?
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

