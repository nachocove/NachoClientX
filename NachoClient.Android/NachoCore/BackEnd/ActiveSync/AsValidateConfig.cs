//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public class AsValidateConfig : IBEContext
    {
        public enum Lst : uint
        {
            ValW = (St.Last + 1),
        };

        private IBEContext BEContext;
        private McServer ServerCandidate;
        private McCred CredCandidate;
        private AsOptionsCommand OptCmd;
        private NcStateMachine Sm;

        public AsValidateConfig (IBEContext bEContext)
        {
            BEContext = bEContext;
            Sm = new NcStateMachine ("ASVCONF") {
                LocalEventType = typeof(AsProtoControl.AsEvt),
                LocalStateType = typeof(Lst),
                TransTable = new[] {
                    new Node {State = (uint)St.Start,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail, 
                            (uint)AsProtoControl.AsEvt.E.ReDisc,
                            (uint)AsProtoControl.AsEvt.E.ReProv,
                            (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)AsProtoControl.AsEvt.E.AuthFail, 
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                        },
                        On = new[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoVal, State = (uint)Lst.ValW },
                        }
                    },
                    new Node {State = (uint)Lst.ValW,
                        Drop = new [] {
                            (uint)SmEvt.E.Launch,
                        },
                        Invalid = new uint[] {
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoYes, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoNoComm, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoNoUOrC, State = (uint)St.Stop },
                            new Trans { Event = (uint)AsProtoControl.AsEvt.E.ReDisc, Act = DoYes, State = (uint)St.Stop },
                            new Trans { Event = (uint)AsProtoControl.AsEvt.E.ReProv, Act = DoYes, State = (uint)St.Stop },
                            new Trans { Event = (uint)AsProtoControl.AsEvt.E.ReSync, Act = DoYes, State = (uint)St.Stop },
                            new Trans { Event = (uint)AsProtoControl.AsEvt.E.AuthFail, Act = DoNoAuth, State = (uint)St.Stop },
                        }
                    },
                }
            };
            Sm.Validate ();
        }

        private void DoVal ()
        {
            OptCmd = new AsOptionsCommand (this) {
                DontReportCommResult = true,
            };
            OptCmd.Execute (Sm);
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
            if (((int?)HttpStatusCode.NotFound) == Sm.Arg as int?) {
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ValidateConfigFailedUser));
            } else {
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ValidateConfigFailedAuth));
            }
        }

        public void Execute (McServer server, McCred cred)
        {
            ServerCandidate = server;
            CredCandidate = cred;
            Sm.Start ();
        }

        public void Cancel ()
        {
            if (null != OptCmd) {
                OptCmd.Cancel ();
                OptCmd = null;
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
