//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using NachoCore.Model;
using System;
using System.Linq;

namespace NachoCore.SFDC
{
    public class SalesForceProtoControl : NcProtoControl
    {
        public const int KDefaultResyncSeconds = 30;
        public const string McMutablesModule = "Salesforce";

        public const McAccount.AccountCapabilityEnum SalesForceCapabilities = (
            McAccount.AccountCapabilityEnum.ContactReader |
            McAccount.AccountCapabilityEnum.ContactWriter);

        public enum Lst : uint
        {
            DiscW = (St.Last + 1),
            // wait for the fetch of the endpoint query paths.
            UiCrdW,
            SyncW,
            Parked,
        };

        public static string StateName (uint state)
        {
            switch (state) {
            case (uint)St.Start:
                return "Start";
            case (uint)St.Stop:
                return "Stop";
            case (uint)Lst.DiscW:
                return "DiscW";
            case (uint)Lst.UiCrdW:
                return "UiCrdW";
            case (uint)Lst.SyncW:
                return "HotQOpW";
            case (uint)Lst.Parked:
                return "Parked";
            default:
                return state.ToString ();
            }
        }

        public override BackEndStateEnum BackEndState {
            get {
                return BackEndStateEnum.PostAutoDPostInboxSync;
            }
        }

        public class SfdcEvt : PcEvt
        {
            new public enum E : uint
            {
                AuthFail = (PcEvt.E.Last + 1),
                UiSetCred,
                Last = UiSetCred,
            };
        }

        public static void PopulateServer (int accountId, Uri serverUri)
        {
            var server = McServer.QueryByAccountIdAndCapabilities (accountId, SalesForceProtoControl.SalesForceCapabilities);
            if (server == null) {
                server = new McServer () {
                    AccountId = accountId,
                    Capabilities = SalesForceProtoControl.SalesForceCapabilities,
                };
                server.Insert ();
            }
            server.UpdateWithOCApply<McServer> (((record) => {
                var target = (McServer)record;
                target.Host = serverUri.Host;
                target.Port = serverUri.Port >= 0 ? serverUri.Port : 443;
                target.Scheme = serverUri.Scheme;
                target.Path = "";
                return true;
            }));
        }

        public SalesForceSetup SFDCSetup { get; protected set; }

        protected SFDCCommand Cmd;

        protected bool DiscoveryDone { get; set; }

        public SalesForceProtoControl (INcProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            Capabilities = SalesForceCapabilities;
            SetupAccount ();
            Sm = new NcStateMachine ("SFDCPC") { 
                Name = string.Format ("SFDCPC({0})", AccountId),
                LocalEventType = typeof(SfdcEvt),
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
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                            (uint)SfdcEvt.E.AuthFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                        }
                    },     
                    new Node {
                        State = (uint)Lst.UiCrdW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)SfdcEvt.E.AuthFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                        }
                    },
                    new Node {
                        State = (uint)Lst.DiscW,
                        Drop = new [] {
                            (uint)PcEvt.E.PendQOrHint,
                            (uint)PcEvt.E.PendQHot,
                        },
                        Invalid = new uint[] {
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = FinishDisc, ActSetsState = true },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SfdcEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.SyncW,
                        Drop = new uint[] {
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                            new Trans { Event = (uint)PcEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSyncSuccess, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoPick, ActSetsState = true },
                            new Trans { Event = (uint)SfdcEvt.E.AuthFail, Act = DoUiCredReq, State = (uint)Lst.UiCrdW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Parked,
                        Drop = new [] {
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)PcEvt.E.Park,
                        },
                        Invalid = new [] {
                            (uint)SfdcEvt.E.AuthFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoDrive, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQOrHint, Act = DoDrive, ActSetsState = true },
                            new Trans { Event = (uint)PcEvt.E.PendQHot, Act = DoDrive, ActSetsState = true },
                            new Trans { Event = (uint)SfdcEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscW },
                        }
                    }         
                }
            };
            Sm.Validate ();
        }

        public override void Remove ()
        {
            if (!((uint)Lst.Parked == Sm.State || (uint)St.Start == Sm.State || (uint)St.Stop == Sm.State)) {
                Log.Warn (Log.LOG_SMTP, "SalesForceProtoControl.Remove called while state is {0}", StateName ((uint)Sm.State));
            }
            base.Remove ();
        }

        void DoDisc ()
        {
            if (SFDCSetup == null) {
                SFDCSetup = new SalesForceSetup (this, AccountId);
            } else {
                Log.Warn (Log.LOG_SFDC, "Starting a discovery on top of a discovery!");
            }
            SFDCSetup.Execute ();
        }

        void FinishDisc ()
        {
            DiscoveryDone = true;
            DoPick ();
        }

        SalesForceContactSync Sync;

        void DoPick ()
        {
            if (null != ReSyncTimer) {
                ReSyncTimer.Dispose ();
                ReSyncTimer = null;
            }

            if (null != Sync) {
                Sync.Cancel ();
                Sync = null;
            }

            // TODO: couple ClearEventQueue with PostEvent inside SM mutex.
            Sm.ClearEventQueue ();
            var next = McPending.QueryEligible (AccountId, McAccount.SmtpCapabilities).
                FirstOrDefault (x => McPending.Operations.Sync == x.Operation);
            if (null != next) {
                Log.Info (Log.LOG_SFDC, "Strategy:FG/BG:Send");
                switch (next.Operation) {
                case McPending.Operations.Sync:
                    StartSync (next);
                    Sm.State = (uint)Lst.SyncW;
                    break;
                default:
                    Log.Warn (Log.LOG_SFDC, "Ignoring command {0}", next.Operation);
                    break;
                }
            } else {
                StartSync (null);
                Sm.State = (uint)Lst.SyncW;
            }
        }

        NcTimer ReSyncTimer;

        void DoSyncSuccess ()
        {
            CancelCmd ();
            if (null != ReSyncTimer) {
                ReSyncTimer.Dispose ();
            }
            ReSyncTimer = new NcTimer ("SFDCResyncTimer", (state) => {
                //Sm.PostEvent ((uint)SmEvt.E.Launch, "SFDCRESYNCTIMER");
                SyncCmd (0);
            }, null, new TimeSpan (0, 0, KDefaultResyncSeconds), TimeSpan.Zero);
        }

        void StartSync (McPending pending)
        {
            if (null != Sync) {
                Sync.Cancel ();
                Sync = null;
            }
            Sync = new SalesForceContactSync (this, AccountId, pending);
            Sync.Execute ();
        }

        void DoPark ()
        {
            CancelCmd ();
        }

        void DoDrive ()
        {
            if (DiscoveryDone) {
                DoPick ();
                Sm.State = (uint)Lst.SyncW;
            } else {
                DoDisc ();
                Sm.State = (uint)Lst.DiscW;
            }
        }

        void DoUiCredReq ()
        {
            CancelCmd ();
            BackEndStatePreset = BackEndStateEnum.CredWait;
            // Send the request toward the UI.
            Owner.CredReq (this);
        }

        public override void CredResp ()
        {
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)SfdcEvt.E.UiSetCred, "SFDCPCUSC");
            }, "SfdcCredResp");
        }

        protected override bool Execute ()
        {
            if (!base.Execute ()) {
                return false;
            }
            NcTask.Run (() => Sm.PostEvent ((uint)SmEvt.E.Launch, "SFDCPCLAUNCH"), "SFDCExecute");
            return true;
        }

        protected override void ForceStop ()
        {
            if (null != ReSyncTimer) {
                ReSyncTimer.Dispose ();
                ReSyncTimer = null;
            }
            base.ForceStop ();
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
            if (Cmd != null &&
                cmd.GetType () == Cmd.GetType ()) {
                // this is a retry. Use the same command to keep track of number of retries.
                return;
            }
            CancelCmd ();
            Cmd = cmd;
        }

        #region EmailToSalesforceAddress

        public static string EmailToSalesforceAddress (int accountId)
        {
            return McMutables.Get (accountId, McMutablesModule, SFDCGetEmailDomainCommand.McMutablesKey);
        }

        public static bool IsSalesForceContact (int accountId, string emailAddress)
        {
            var contacts = McContact.QueryByEmailAddress (accountId, emailAddress);
            foreach (var contact in contacts) {
                if (contact.Source == McAbstrItem.ItemSource.SalesForce) {
                    return true;
                }
            }
            return false;
        }

        public static McEmailMessage MaybeAddSFDCEmailToBcc (McEmailMessage emailMessage)
        {
            // FIXME: Need to test this.
            var SalesforceAccount = McAccount.QueryByAccountType (McAccount.AccountTypeEnum.SalesForce).FirstOrDefault ();
            if (SalesforceAccount != null) {
                string SFDCemail = SalesForceProtoControl.EmailToSalesforceAddress (SalesforceAccount.Id);
                if (!string.IsNullOrEmpty (SFDCemail)) {
                    bool addSFDCemail = IsSalesForceContact (SalesforceAccount.Id, emailMessage.To);
                    if (!addSFDCemail) {
                        foreach (var cc in emailMessage.Cc.Split (new[] {','})) {
                            if (IsSalesForceContact (SalesforceAccount.Id, cc)) {
                                addSFDCemail = true;
                                break;
                            }
                        }
                    }
                    if (!addSFDCemail) {
                        foreach (var bcc in emailMessage.Bcc.Split (new[] {','})) {
                            if (IsSalesForceContact (SalesforceAccount.Id, bcc)) {
                                addSFDCemail = true;
                                break;
                            }
                        }
                    }

                    if (addSFDCemail) {
                        var bccAddresses = emailMessage.Bcc.Split (new[] { ',' }).ToList ();
                        bccAddresses.Add (SFDCemail);
                        emailMessage = emailMessage.UpdateWithOCApply<McEmailMessage> (((record) => {
                            var target = (McEmailMessage)record;
                            target.Bcc = string.Join (",", bccAddresses);
                            return true;
                        }));
                    }
                }
            }
            return emailMessage;
        }

        #endregion
    }
}

