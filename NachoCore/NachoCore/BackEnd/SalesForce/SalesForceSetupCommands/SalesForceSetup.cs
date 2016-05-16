//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.SFDC
{
    public class SalesForceSetup : IBEContext
    {
        #region IBEContext implementation

        public INcProtoControlOwner Owner {
            get {
                return SFDCOwner.Owner;
            }
            set {
                throw new NotImplementedException ();
            }
        }

        public NcProtoControl ProtoControl {
            get {
                return SFDCOwner.ProtoControl;
            }
            set {
                throw new NotImplementedException ();
            }
        }

        public McProtocolState ProtocolState {
            get {
                return SFDCOwner.ProtocolState;
            }
        }

        public McServer Server {
            get {
                return SFDCOwner.Server;
            }
            set {
                throw new NotImplementedException ();
            }
        }

        public McAccount Account {
            get {
                return SFDCOwner.Account;
            }
        }

        public McCred Cred {
            get {
                return SFDCOwner.Cred;
            }
        }

        #endregion

        public enum Lst : uint
        {
            InitW = (St.Last + 1),
            // wait for the fetch of the endpoint query paths.
            ResourceW,
            ObjectsW,
            EmailSetupW,
            Stop,
        };

        /// <summary>
        /// The API-based query path. We store it with the controller in memory, because it may change and
        /// we should query for it each time.
        /// </summary>
        /// <value>The query path.</value>
        public Dictionary<string, string> ResourcePaths { get; set; }

        /// <summary>
        /// SObject URL's
        /// </summary>
        /// <value>The object urls.</value>
        public Dictionary<string, string> ObjectUrls { get; set; }

        protected SFDCCommand Cmd;
        SalesForceProtoControl SFDCOwner;
        protected NcStateMachine Sm;

        public SalesForceSetup (SalesForceProtoControl owner, int accountId)
        {
            SFDCOwner = owner;
            Sm = new NcStateMachine ("SFDCPC:SETUP") { 
                Name = string.Format ("SFDCPC:SETUP({0})", accountId),
                LocalEventType = typeof(SalesForceProtoControl.SfdcEvt),
                LocalStateType = typeof(Lst),
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Drop = new uint[] {
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)SalesForceProtoControl.SfdcEvt.E.AuthFail,
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetCred,
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetServConf,
                            (uint)SalesForceProtoControl.SfdcEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.InitW },
                        }
                    },     
                    new Node {
                        State = (uint)Lst.Stop,
                        Drop = new uint[] {
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)SalesForceProtoControl.SfdcEvt.E.AuthFail,
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetCred,
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetServConf,
                            (uint)SalesForceProtoControl.SfdcEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.InitW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.InitW,
                        Drop = new uint [] {
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Launch,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetCred,
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetServConf,
                            (uint)SalesForceProtoControl.SfdcEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoGetResources, State = (uint)Lst.ResourceW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoError, State = (uint)Lst.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoGetVersion, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)SalesForceProtoControl.SfdcEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.Stop },
                        }
                    },
                    new Node {
                        State = (uint)Lst.ResourceW,
                        Drop = new uint[] {
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Launch,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetCred,
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetServConf,
                            (uint)SalesForceProtoControl.SfdcEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoGetObjects, State = (uint)Lst.ObjectsW },
                            new Trans { Event = (uint)SalesForceProtoControl.SfdcEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.Stop },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoError, State = (uint)Lst.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoGetResources, State = (uint)Lst.ResourceW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.ObjectsW,
                        Drop = new uint [] {
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Launch,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetCred,
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetServConf,
                            (uint)SalesForceProtoControl.SfdcEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoEmailSetupOrStop, ActSetsState = true },
                            new Trans { Event = (uint)SalesForceProtoControl.SfdcEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.Stop },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoError, State = (uint)Lst.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoGetObjects, State = (uint)Lst.ObjectsW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.EmailSetupW,
                        Drop = new uint [] {
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Launch,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetCred,
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetServConf,
                            (uint)SalesForceProtoControl.SfdcEvt.E.GetServConf,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSuccess, State = (uint)Lst.Stop },
                            new Trans { Event = (uint)SalesForceProtoControl.SfdcEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.Stop },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoError, State = (uint)Lst.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoGetObjects, State = (uint)Lst.ObjectsW },
                        }
                    },
                }
            };
            Sm.Validate ();
        }

        void DoDisc ()
        {
            ResourcePaths = null;
            DoGetVersion ();
        }

        void DoGetVersion ()
        {
            SetCmd (new SFDCGetApiVersionsCommand (this));
            ExecuteCmd ();
        }

        void DoGetResources ()
        {
            SetCmd (new SFDCGetResourcesCommand (this));
            ExecuteCmd ();
        }

        void DoGetObjects ()
        {
            SetCmd (new SFDCGetObjectsCommand (this));
            ExecuteCmd ();
        }

        void DoEmailSetupOrStop ()
        {
            string email = SalesForceProtoControl.EmailToSalesforceAddress (Account.Id);
            if (string.IsNullOrEmpty (email)) {
                SetCmd (new SFDCGetEmailDomainCommand (this));
                ExecuteCmd ();
                Sm.State = (uint)Lst.EmailSetupW;
            } else {
                DoSuccess ();
                Sm.State = (uint)Lst.Stop;
            }
        }

        void DoError ()
        {
            CancelCmd ();
            SFDCOwner.Sm.PostEvent (Event.Create ((uint)SmEvt.E.HardFail, "SETUPHARDFAIL"));
        }

        void DoSuccess ()
        {
            CancelCmd ();
            SFDCOwner.Sm.PostEvent (Event.Create ((uint)SmEvt.E.Success, "SETUPSUCCESS"));
        }

        void DoUiCredReq ()
        {
            CancelCmd ();
            // Send the request toward the UI.
            SFDCOwner.Sm.PostEvent (Event.Create ((uint)SalesForceProtoControl.SfdcEvt.E.AuthFail, "SETUPAUTHFAIL"));
        }

        public bool Execute ()
        {
            Sm.PostEvent ((uint)SmEvt.E.Launch, "SFDCSETUPLAUNCH");
            return true;
        }

        public void Cancel ()
        {
            CancelCmd ();
        }

        void CancelCmd ()
        {
            if (Cmd != null) {
                Cmd.Cancel ();
            }
            Cmd = null;
        }

        void ExecuteCmd ()
        {
            NcAssert.NotNull (Cmd);
            Cmd.Execute (Sm);
        }

        void SetCmd (SFDCCommand cmd)
        {
            if (null != Cmd && cmd.GetType () == Cmd.GetType ()) {
                // this is a retry. Use the same command to keep track of number of retries.
                return;
            }
            CancelCmd ();
            Cmd = cmd;
        }

    }
}

