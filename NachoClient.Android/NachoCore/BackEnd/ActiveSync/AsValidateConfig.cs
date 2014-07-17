﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
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
            Sm = new NcStateMachine ("VCONF") {
                LocalEventType = typeof(AsProtoControl.AsEvt),
                LocalStateType = typeof(Lst),
                TransTable = new[] {
                    new Node {State = (uint)St.Start,
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.TempFail, (uint)SmEvt.E.HardFail, 
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync, (uint)AsProtoControl.AsEvt.E.AuthFail, 
                        },
                        On = new[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoVal, State = (uint)Lst.ValW },
                        }
                    },
                    new Node {State = (uint)Lst.ValW,
                        Drop = new [] { (uint)SmEvt.E.Launch },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoYes, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoNoComm, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoNoComm, State = (uint)St.Stop },
                            new Trans { Event = (uint)AsProtoControl.AsEvt.E.ReDisc, Act = DoYes, State = (uint)St.Stop },
                            new Trans { Event = (uint)AsProtoControl.AsEvt.E.ReProv, Act = DoYes, State = (uint)St.Stop },
                            new Trans { Event = (uint)AsProtoControl.AsEvt.E.ReSync, Act = DoYes, State = (uint)St.Stop },
                            new Trans { Event = (uint)AsProtoControl.AsEvt.E.AuthFail, Act = DoNoAuth, State = (uint)St.Stop },
                        }
                    },
                }
            };
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

        public IProtoControlOwner Owner {
            get { return BEContext.Owner; }
            set { BEContext.Owner = value; }
        }

        public AsProtoControl ProtoControl {
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
