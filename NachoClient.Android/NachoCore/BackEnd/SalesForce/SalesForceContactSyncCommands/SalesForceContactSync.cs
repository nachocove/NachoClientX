//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;

namespace NachoCore
{
    public class SalesForceContactSync : IBEContext
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
            SyncWIds = (St.Last + 1),
            SyncW,
            Stop,
        }

        public class SfdcSyncEvt : NcProtoControl.PcEvt
        {
            new public enum E : uint
            {
                AuthFail = (NcProtoControl.PcEvt.E.Last + 1),
                Last = AuthFail,
            };
        }

        protected SFDCCommand Cmd;
        SalesForceProtoControl SFDCOwner;
        protected NcStateMachine Sm;

        public SalesForceContactSync (SalesForceProtoControl owner, int accountId)
        {
            SFDCOwner = owner;
            Sm = new NcStateMachine ("SFDCPC:CONTACTSYNC") { 
                Name = string.Format ("SFDCPC:CONTACTSYNC({0})", accountId),
                LocalEventType = typeof(SfdcSyncEvt),
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
                            (uint)SfdcSyncEvt.E.AuthFail,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSyncIds, State = (uint)Lst.SyncWIds },
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
                            (uint)SfdcSyncEvt.E.AuthFail,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSyncIds, State = (uint)Lst.SyncWIds },
                        }
                    },
                    new Node {
                        State = (uint)Lst.SyncWIds,
                        Drop = new uint[] {
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Launch,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSync, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoError, State = (uint)Lst.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSyncIds, State = (uint)Lst.SyncWIds },
                            new Trans { Event = (uint)SfdcSyncEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.Stop },
                        }
                    },
                    new Node {
                        State = (uint)Lst.SyncW,
                        Drop = new uint[] {
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Launch,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSyncContinue, ActSetsState = true },
                            new Trans { Event = (uint)SfdcSyncEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.Stop },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoError, State = (uint)Lst.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSyncContinue, ActSetsState = true  },
                        }
                    },
                }
            };
            Sm.Validate ();
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

        void DoSyncIds ()
        {
            SetCmd (new SFDCGetContactIdsCommand (this));
            ExecuteCmd ();
        }

        List<string> NeedFetchIds;

        void DoSync ()
        {
            NeedFetchIds = Sm.Arg as List<string>;
            NcAssert.NotNull (NeedFetchIds, "No ids passed back");
            DoSyncBase ();
        }

        void DoSyncContinue ()
        {
            DoSyncBase ();
        }

        void DoSyncBase ()
        {
            uint nextState;
            if (NeedFetchIds.Any ()) {
                Log.Info (Log.LOG_SFDC, "SalesForceContactSync: {0} left to sync", NeedFetchIds.Count);
                string id = NeedFetchIds.First ();
                NeedFetchIds.Remove (id);

                SetCmd (new SFDCGetContactsDataCommand (this, id));
                ExecuteCmd ();
                nextState = (uint)Lst.SyncW;
            } else {
                ProtocolState.UpdateWithOCApply<McProtocolState> (((record) => {
                    var target = (McProtocolState)record;
                    target.SFDCLastContactsSynced = DateTime.UtcNow;
                    return true;
                }));
                Log.Info (Log.LOG_SFDC, "Finished Syncing Contacts");
                SFDCOwner.Sm.PostEvent ((uint)SmEvt.E.Success, "SFDCALLCONTACTDONE");
                nextState = (uint)Lst.Stop;
            }
            Sm.State = nextState;
        }

        public bool Execute ()
        {
            Sm.PostEvent ((uint)SmEvt.E.Launch, "SFDCCONTACTSYNCLAUNCH");
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
            if (null != Cmd &&
                !(cmd is SFDCGetContactsDataCommand) &&
                cmd.GetType () == Cmd.GetType ()) {
                // this is a retry. Use the same command to keep track of number of retries.
                return;
            }
            CancelCmd ();
            Cmd = cmd;
        }
    }
}

