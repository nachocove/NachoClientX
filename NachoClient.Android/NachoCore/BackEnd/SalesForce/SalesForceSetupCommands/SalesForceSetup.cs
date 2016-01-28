//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.Collections.Generic;

namespace NachoCore
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

        public NachoCore.Model.McProtocolState ProtocolState {
            get {
                return SFDCOwner.ProtocolState;
            }
        }

        public NachoCore.Model.McServer Server {
            get {
                return SFDCOwner.Server;
            }
            set {
                throw new NotImplementedException ();
            }
        }

        public NachoCore.Model.McAccount Account {
            get {
                return SFDCOwner.Account;
            }
        }

        public NachoCore.Model.McCred Cred {
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
            Stop,
        };

        public class SfdcSetupEvt : NcProtoControl.PcEvt
        {
            new public enum E : uint
            {
                AuthFail = (NcProtoControl.PcEvt.E.Last + 1),
                Last = AuthFail,
            };
        }

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
            Sm = new NcStateMachine ("SFDCPCSETUP") { 
                Name = string.Format ("SFDCPCSETUP({0})", accountId),
                LocalEventType = typeof(SfdcSetupEvt),
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
                            (uint)SfdcSetupEvt.E.AuthFail,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
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
                            (uint)SfdcSetupEvt.E.AuthFail,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
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
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoGetResources, State = (uint)Lst.ResourceW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoError, State = (uint)Lst.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoGetVersion, State = (uint)Lst.InitW },
                            new Trans { Event = (uint)SfdcSetupEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.Stop },
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
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoGetObjects, State = (uint)Lst.ObjectsW },
                            new Trans { Event = (uint)SfdcSetupEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.Stop },
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
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSuccess, State = (uint)Lst.Stop },
                            new Trans { Event = (uint)SfdcSetupEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.Stop },
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

