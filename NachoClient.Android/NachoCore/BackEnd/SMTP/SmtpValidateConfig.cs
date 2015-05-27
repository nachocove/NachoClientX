//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.SMTP
{
    public class SmtpValidateConfig : IBEContext
    {
        public enum Lst : uint
        {
            ValW = (St.Last + 1),
        };

        private IBEContext BEContext;
        private McServer ServerCandidate;
        private McCred CredCandidate;
        private NcStateMachine Sm;
        private SmtpAuthenticateCommand Cmd;

        public SmtpValidateConfig (IBEContext bEContext)
        {
            BEContext = bEContext;
            Sm = new NcStateMachine ("SMTPVCONF") {
                LocalEventType = typeof(SmtpProtoControl.SmtpEvt),
                LocalStateType = typeof(Lst),
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail, 
                            (uint)SmtpProtoControl.SmtpEvt.E.ReDisc,
                            (uint)SmtpProtoControl.SmtpEvt.E.AuthFail, 
                        },
                        On = new[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoVal, State = (uint)Lst.ValW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.ValW,
                        Drop = new [] {
                            (uint)SmEvt.E.Launch
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoYes, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoNoComm, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoNoUOrC, State = (uint)St.Stop },
                            new Trans {
                                Event = (uint)SmtpProtoControl.SmtpEvt.E.ReDisc,
                                Act = DoYes,
                                State = (uint)St.Stop
                            },
                            new Trans {
                                Event = (uint)SmtpProtoControl.SmtpEvt.E.AuthFail,
                                Act = DoNoAuth,
                                State = (uint)St.Stop
                            },
                        }
                    },
                }
            };
            Sm.Validate ();
        }

        private void DoVal ()
        {
            var client = SmtpProtoControl.newClientWithLogger ();
            try {
                Cmd = new SmtpAuthenticateCommand (client, BEContext.Server, BEContext.Cred);
                Cmd.Execute (Sm);
            }
            finally {
                lock (client.SyncRoot) {
                    client.Disconnect (true);
                }
            }
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

        private void DoNoUOrC ()
        {
//            if (((int?)HttpStatusCode.NotFound) == Sm.Arg as int?) {
//                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ValidateConfigFailedUser));
//            } else {
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ValidateConfigFailedAuth));
//            }
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

    }
}

