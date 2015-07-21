//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.IMAP
{
    public class ImapValidateConfig : IBEContext
    {
        public enum Lst : uint
        {
            ValW = (St.Last + 1),
        };

        private IBEContext BEContext;
        private McServer ServerCandidate;
        private McCred CredCandidate;
        private ImapCommand Cmd;
        NcImapClient Client;
        private NcStateMachine Sm;

        public ImapValidateConfig (IBEContext bEContext)
        {
            BEContext = bEContext;
            Client = new NcImapClient ();
            Sm = new NcStateMachine ("IMAPVCONF") {
                LocalEventType = typeof(ImapProtoControl.ImapEvt),
                LocalStateType = typeof(Lst),
                TransTable = new[] {
                    new Node {State = (uint)St.Start,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail, 
                            (uint)ImapProtoControl.ImapEvt.E.ReDisc,
                            (uint)ImapProtoControl.ImapEvt.E.AuthFail, 
                        },
                        On = new[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoVal, State = (uint)Lst.ValW },
                        }
                    },
                    new Node {State = (uint)Lst.ValW,
                        Drop = new [] {
                            (uint)SmEvt.E.Launch
                        },
                        Invalid = new [] {
                            (uint)ImapProtoControl.ImapEvt.E.ReDisc,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoYes, State = (uint)St.Stop },
                            new Trans { Event = (uint)ImapProtoControl.ImapEvt.E.GetServConf, Act = DoNoComm, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoNoComm, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoNoComm, State = (uint)St.Stop },
                            new Trans { Event = (uint)ImapProtoControl.ImapEvt.E.AuthFail, Act = DoNoAuth, State = (uint)St.Stop },
                        }
                    },
                }
            };
        }

        private void DoVal ()
        {
            Cmd = new ImapDiscoverCommand (this, Client);
            Cmd.Execute (Sm);
        }

        private void DoYes ()
        {
            BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ValidateConfigSucceeded));
        }

        private void DoNoComm ()
        {
            BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ValidateConfigFailedComm));
        }

        private void DoNoAuth ()
        {
            BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ValidateConfigFailedAuth));
        }

        public void Execute (McServer server, McCred cred)
        {
            ServerCandidate = server;
            CredCandidate = cred;
            Sm.Start ();
        }

        public void Cancel ()
        {
            if (null != Cmd) {
                Cmd.Cancel ();
                Cmd = null;
            }
        }

        #region IBEContext implementation

        public INcProtoControlOwner Owner {
            get { return BEContext.Owner; }
            set { BEContext.Owner = value; }
        }

        public NcProtoControl ProtoControl {
            get { return BEContext.ProtoControl; }
            set { BEContext.ProtoControl = value; }
        }

        public McProtocolState ProtocolState {
            get { return BEContext.ProtocolState; }
            set { BEContext.ProtocolState = value; }
        }

        public McServer Server {
            get { return ServerCandidate; }
            set { throw new Exception ("Illegal set of Server."); }
        }

        public McAccount Account {
            get { return BEContext.Account; }
        }

        public McCred Cred {
            get { return CredCandidate; }
        }

        #endregion
    }
}

