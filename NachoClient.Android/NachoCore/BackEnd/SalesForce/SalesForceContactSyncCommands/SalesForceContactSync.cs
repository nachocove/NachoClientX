//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;

namespace NachoCore.SFDC
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

        protected SFDCCommand Cmd;
        public SalesForceProtoControl SFDCOwner { get; protected set; }
        protected NcStateMachine Sm;
        McPending Pending;

        public SalesForceContactSync (SalesForceProtoControl owner, int accountId, McPending pending = null)
        {
            Pending = pending;
            SFDCOwner = owner;
            Sm = new NcStateMachine ("SFDCPC:CONTACTSYNC") { 
                Name = string.Format ("SFDCPC:CONTACTSYNC({0})", accountId),
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
                            (uint)SalesForceProtoControl.SfdcEvt.E.AuthFail,
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetCred,
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
                            (uint)SalesForceProtoControl.SfdcEvt.E.AuthFail,
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetCred,
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
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetCred,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSync, ActSetsState = true },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoError, State = (uint)Lst.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSyncIds, State = (uint)Lst.SyncWIds },
                            new Trans { Event = (uint)SalesForceProtoControl.SfdcEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.Stop },
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
                            (uint)SalesForceProtoControl.SfdcEvt.E.UiSetCred,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSyncContinue, ActSetsState = true },
                            new Trans { Event = (uint)SalesForceProtoControl.SfdcEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.Stop },
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
            SendCommandToOwnerSm (Event.Create ((uint)SmEvt.E.HardFail, "SYNCHARDFAIL"),
                NcResult.Error (NcResult.SubKindEnum.Error_SyncFailed));
        }

        void DoSuccess ()
        {
            CancelCmd ();
            SendCommandToOwnerSm (Event.Create ((uint)SmEvt.E.Success, "SYNCSUCCESS"), NcResult.OK ());
        }

        void DoUiCredReq ()
        {
            CancelCmd ();
            // Send the request toward the UI.
            SendCommandToOwnerSm (Event.Create ((uint)SalesForceProtoControl.SfdcEvt.E.AuthFail, "SYNCAUTHFAIL"),
                NcResult.Error (NcResult.SubKindEnum.Error_AuthFailBlocked));
        }

        void SendCommandToOwnerSm (Event evt, NcResult result)
        {
            if (null != Pending) {
                if (!result.isOK ()) {
                    Pending.ResolveAsHardFail (SFDCOwner, result);
                } else {
                    Pending.ResolveAsSuccess (SFDCOwner);
                }
            }
            SFDCOwner.Sm.PostEvent (evt);
        }

        void DoSyncIds ()
        {
            SetCmd (new SFDCGetContactIdsCommand (this));
            ExecuteCmd ();
        }

        List<string> NeedFetchIds;

        public int ContactsAdded { get; set; }
        public int ContactsModified { get; set; }
        public int ContactsDeleted { get; set; }

        void DoSync ()
        {
            Tuple<List<string>, List<string>> pack = Sm.Arg as Tuple<List<string>, List<string>>;
            NcAssert.NotNull (pack, "No ids passed back");
            NeedFetchIds = pack.Item1;
            NeedFetchIds.AddRange (pack.Item2);
            DoSyncBase ();
        }

        void DoSyncContinue ()
        {
            string contactId = Sm.Arg as string;
            DoSyncBase ();
        }

        void DoSyncBase (string contactId = null)
        {
            uint nextState;
            if (NeedFetchIds.Any ()) {
                Log.Info (Log.LOG_SFDC, "SalesForceContactSync: left to sync: {0} ", NeedFetchIds.Count);
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
                if (ContactsAdded != 0 || ContactsModified != 0 || ContactsDeleted != 0) {
                    Owner.StatusInd (SFDCOwner.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
                }
                Log.Info (Log.LOG_SFDC, "Finished Syncing Contacts");
                SendCommandToOwnerSm (Event.Create ((uint)SmEvt.E.Success, "SFDCALLCONTACTDONE"), NcResult.OK ());
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

