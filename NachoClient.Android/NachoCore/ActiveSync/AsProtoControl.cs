using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NachoCore.Model;
using NachoCore.Utils;


namespace NachoCore.ActiveSync
{
    public class AsProtoControl : ProtoControl, IAsDataSource
    {
        private IAsCommand Cmd;

        public enum Lst : uint {
            DiscWait=(St.Last+1), 
            UiCredWait, 
            UiServConfWait,
            UiCertOkWait,
            OptWait, 
            ProvWait, 
            SettingsWait, 
            FSyncWait, 
            SyncWait, 
            PingWait, 
            SendMailWait, 
            DnldAttWait,
        };

        // If you're exposed to AsHttpOperation, you need to cover these.
        public class AsEvt : SmEvt {
            new public enum E : uint {
                ReDisc=(SmEvt.E.Last+1), 
                ReProv, 
                ReSync,
                Last=ReSync 
            };
        }

        // Events of the form UiXxYy are events coming directly from the UI/App toward the controller.
        // DB-based events (even if UI-driven) and server-based events lack the Ui prefix.
        public class CtlEvt : AsEvt {
            new public enum E : uint {
                GetCred=(AsEvt.E.Last+1),
                UiSetCred, 
                GetServConf,
                UiSetServConf,
                GetCertOk,
                UiCertOkYes,
                UiCertOkNo,
                ReFSync,
                SendMail, 
                DnldAtt,
            };
        }

        public AsProtoControl Control { set; get; }

        public AsProtoControl (IProtoControlOwner owner, NcAccount account)
        {
            Control = this;
            Owner = owner;
            AccountId = account.Id;

            Sm = new StateMachine () { 
                Name = "as:control" + account.Id,
                LocalEventType = typeof(CtlEvt),
                LocalStateType = typeof(Lst),
                StateChangeIndication = UpdateSavedState,
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Drop = new [] {(uint)CtlEvt.E.SendMail, (uint)CtlEvt.E.UiSetCred, (uint)CtlEvt.E.UiSetServConf, (uint)CtlEvt.E.UiCertOkNo, 
                            (uint)CtlEvt.E.UiCertOkYes},
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.TempFail, (uint)SmEvt.E.HardFail,
                            (uint)AsEvt.E.ReDisc, (uint)AsEvt.E.ReProv, (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.GetCred, (uint)CtlEvt.E.ReFSync, (uint)CtlEvt.E.DnldAtt, (uint)CtlEvt.E.GetCertOk, (uint)CtlEvt.E.GetServConf},
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoDisc, State=(uint)Lst.DiscWait},
                        }},

                    new Node {
                        // NOTE: There is no HardFail. Can't pass DiscWait w/out a working server - period.
                        State = (uint)Lst.DiscWait, 
                        Drop = new [] {(uint)CtlEvt.E.SendMail, (uint)CtlEvt.E.UiCertOkNo, (uint)CtlEvt.E.UiCertOkYes},
                        Invalid = new [] {(uint)SmEvt.E.HardFail,
                            (uint)AsEvt.E.ReDisc, (uint)AsEvt.E.ReProv, (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.ReFSync, (uint)CtlEvt.E.DnldAtt},
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)SmEvt.E.Success, Act = DoOpt, State = (uint)Lst.OptWait},
                            new Trans {Event = (uint)SmEvt.E.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)CtlEvt.E.GetCred, Act = DoUiCredReq, State = (uint)Lst.UiCredWait},
                            new Trans {Event = (uint)CtlEvt.E.GetServConf, Act = DoUiServConfReq, State = (uint)Lst.UiServConfWait},
                            new Trans {Event = (uint)CtlEvt.E.GetCertOk, Act = DoUiCertOkReq, State = (uint)Lst.UiCertOkWait},
                            new Trans {Event = (uint)CtlEvt.E.UiSetServConf, Act = DoSetServConf, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)CtlEvt.E.UiSetCred, Act = DoSetCred, State = (uint)Lst.DiscWait},
                        }},

                    new Node {
                        State = (uint)Lst.UiCredWait,
                        Drop = new [] {(uint)CtlEvt.E.SendMail, (uint)CtlEvt.E.UiCertOkNo, (uint)CtlEvt.E.UiCertOkYes},
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.HardFail, (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReDisc, (uint)AsEvt.E.ReProv, (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.GetCred, (uint)CtlEvt.E.ReFSync, (uint)CtlEvt.E.DnldAtt, (uint)CtlEvt.E.GetCertOk, (uint)CtlEvt.E.GetServConf},
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoUiCredReq, State = (uint)Lst.UiCredWait},
                            new Trans {Event = (uint)CtlEvt.E.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)CtlEvt.E.UiSetServConf, Act = DoSetServConf, State = (uint)Lst.DiscWait},
                        }},

                    new Node {
                        State = (uint)Lst.UiServConfWait,
                        Drop = new [] {(uint)CtlEvt.E.SendMail, (uint)CtlEvt.E.UiSetCred, (uint)CtlEvt.E.UiCertOkNo, (uint)CtlEvt.E.UiCertOkYes},
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.HardFail, (uint)SmEvt.E.TempFail,
                            (uint)AsEvt.E.ReDisc, (uint)AsEvt.E.ReProv, (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.GetCred, (uint)CtlEvt.E.ReFSync, (uint)CtlEvt.E.DnldAtt, (uint)CtlEvt.E.GetCertOk, (uint)CtlEvt.E.GetServConf},
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)CtlEvt.E.UiSetServConf, Act = DoSetServConf, State = (uint)Lst.DiscWait},
                        }},

                    new Node {
                        State = (uint)Lst.UiCertOkWait,
                        Drop = new [] {(uint)CtlEvt.E.SendMail},
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.HardFail, (uint)SmEvt.E.TempFail,
                            (uint)CtlEvt.E.GetCred,
                            (uint)AsEvt.E.ReDisc, (uint)AsEvt.E.ReProv, (uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.UiSetCred, (uint)CtlEvt.E.ReFSync, (uint)CtlEvt.E.GetServConf, (uint)CtlEvt.E.DnldAtt, (uint)CtlEvt.E.GetCertOk},
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoDisc, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)CtlEvt.E.UiCertOkYes, Act = DoCertOkYes, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)CtlEvt.E.UiCertOkNo, Act = DoCertOkNo, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)CtlEvt.E.UiSetServConf, Act = DoSetServConf, State = (uint)Lst.DiscWait},
                        }},

                    new Node {
                        State = (uint)Lst.OptWait,
                        Drop = new [] {(uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.SendMail, (uint)CtlEvt.E.UiCertOkNo, (uint)CtlEvt.E.UiCertOkYes, (uint)CtlEvt.E.UiSetCred, (uint)CtlEvt.E.UiSetServConf},
                        Invalid = new [] {(uint)AsEvt.E.ReDisc, (uint)AsEvt.E.ReProv,
                            (uint)CtlEvt.E.GetCred, (uint)CtlEvt.E.ReFSync, (uint)CtlEvt.E.DnldAtt, (uint)CtlEvt.E.GetCertOk, (uint)CtlEvt.E.GetServConf},
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoOpt, State = (uint)Lst.OptWait},
                            new Trans {Event = (uint)SmEvt.E.Success, Act = DoProv, State = (uint)Lst.ProvWait},
                            new Trans {Event = (uint)SmEvt.E.HardFail, Act = DoOldProtoProv, State = (uint)Lst.ProvWait},
                            new Trans {Event = (uint)SmEvt.E.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.OptWait}}},

                    new Node {
                        State = (uint)Lst.ProvWait,
                        Drop = new [] {(uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.SendMail, (uint)CtlEvt.E.UiCertOkNo, (uint)CtlEvt.E.UiCertOkYes, (uint)CtlEvt.E.UiSetCred, (uint)CtlEvt.E.UiSetServConf},
                        Invalid = new [] {(uint)AsEvt.E.ReProv,
                            (uint)CtlEvt.E.GetCred, (uint)CtlEvt.E.ReFSync, (uint)CtlEvt.E.DnldAtt, (uint)CtlEvt.E.GetCertOk, (uint)CtlEvt.E.GetServConf},
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoProv, State = (uint)Lst.ProvWait},
                            new Trans {Event = (uint)SmEvt.E.Success, Act = DoSettings, State = (uint)Lst.SettingsWait},
                            new Trans {Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
                            new Trans {Event = (uint)SmEvt.E.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.ProvWait},
                            new Trans {Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
                        }},

                    new Node {
                        State = (uint)Lst.SettingsWait,
                        Drop = new [] {(uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.SendMail, (uint)CtlEvt.E.UiCertOkNo, (uint)CtlEvt.E.UiCertOkYes, (uint)CtlEvt.E.UiSetCred, (uint)CtlEvt.E.UiSetServConf},
                        Invalid = new [] {(uint)CtlEvt.E.GetCred, (uint)CtlEvt.E.ReFSync, (uint)CtlEvt.E.DnldAtt, (uint)CtlEvt.E.GetCertOk, (uint)CtlEvt.E.GetServConf},
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoSettings, State = (uint)Lst.SettingsWait},
                            new Trans {Event = (uint)SmEvt.E.Success, Act = DoFSync, State = (uint)Lst.FSyncWait},
                            new Trans {Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
                            new Trans {Event = (uint)SmEvt.E.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SettingsWait},
                            new Trans {Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
                        }},

                    new Node {
                        State = (uint)Lst.FSyncWait,
                        Drop = new [] {(uint)AsEvt.E.ReSync,
                            (uint)CtlEvt.E.SendMail, (uint)CtlEvt.E.UiCertOkNo, (uint)CtlEvt.E.UiCertOkYes, (uint)CtlEvt.E.UiSetCred, (uint)CtlEvt.E.UiSetServConf},
                        Invalid = new [] {(uint)CtlEvt.E.GetCred,  (uint)CtlEvt.E.ReFSync, (uint)CtlEvt.E.DnldAtt, (uint)CtlEvt.E.GetCertOk, (uint)CtlEvt.E.GetServConf},
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoFSync, State = (uint)Lst.FSyncWait},
                            new Trans {Event = (uint)SmEvt.E.Success, Act = DoSync, State = (uint)Lst.SyncWait},
                            new Trans {Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
                            new Trans {Event = (uint)SmEvt.E.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SettingsWait},
                            new Trans {Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
                        }},

                    new Node {
                        State = (uint)Lst.SyncWait,
                        Drop = new [] {(uint)CtlEvt.E.SendMail, (uint)CtlEvt.E.UiCertOkNo, (uint)CtlEvt.E.UiCertOkYes, (uint)CtlEvt.E.UiSetCred, (uint)CtlEvt.E.UiSetServConf},
                        Invalid = new [] {(uint)CtlEvt.E.GetCred, (uint)CtlEvt.E.DnldAtt, (uint)CtlEvt.E.GetCertOk, (uint)CtlEvt.E.GetServConf},
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoSync, State = (uint)Lst.SyncWait},
                            new Trans {Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingWait},
                            new Trans {Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
                            new Trans {Event = (uint)SmEvt.E.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SyncWait},
                            new Trans {Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
                            new Trans {Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncWait},
                            new Trans {Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncWait},
                        }},

                    new Node {
                        State = (uint)Lst.PingWait,
                        Drop = new [] {(uint)CtlEvt.E.UiCertOkNo, (uint)CtlEvt.E.UiCertOkYes, (uint)CtlEvt.E.UiSetCred, (uint)CtlEvt.E.UiSetServConf},
                        Invalid = new [] {(uint)CtlEvt.E.GetCred, (uint)CtlEvt.E.GetCertOk, (uint)CtlEvt.E.GetServConf},
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoPing, State = (uint)Lst.PingWait},
                            new Trans {Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingWait},
                            new Trans {Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
                            new Trans {Event = (uint)SmEvt.E.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.PingWait},
                            new Trans {Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
                            new Trans {Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncWait},
                            new Trans {Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncWait},
                            new Trans {Event = (uint)CtlEvt.E.SendMail, Act = DoSend, State = (uint)Lst.SendMailWait},
                            new Trans {Event = (uint)CtlEvt.E.DnldAtt, Act = DoDnldAtt, State = (uint)Lst.DnldAttWait},
                        }},

                    new Node {
                        State = (uint)Lst.SendMailWait,
                        Drop = new [] {(uint)CtlEvt.E.SendMail, (uint)CtlEvt.E.UiCertOkNo, (uint)CtlEvt.E.UiCertOkYes, (uint)CtlEvt.E.UiSetCred, (uint)CtlEvt.E.UiSetServConf},
                        Invalid = new [] {(uint)CtlEvt.E.GetCred, (uint)CtlEvt.E.DnldAtt, (uint)CtlEvt.E.GetCertOk, (uint)CtlEvt.E.GetServConf},
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoSend, State = (uint)Lst.SendMailWait},
                            new Trans {Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingWait},
                            new Trans {Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
                            new Trans {Event = (uint)SmEvt.E.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SendMailWait},
                            new Trans {Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
                            new Trans {Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncWait},
                            new Trans {Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncWait},
                        }},

                    new Node {
                        State = (uint)Lst.DnldAttWait,
                        Drop = new [] {(uint)CtlEvt.E.SendMail, (uint)CtlEvt.E.UiCertOkNo, (uint)CtlEvt.E.UiCertOkYes, (uint)CtlEvt.E.UiSetCred, (uint)CtlEvt.E.UiSetServConf},
                        Invalid = new [] {(uint)CtlEvt.E.GetCred, (uint)CtlEvt.E.DnldAtt, (uint)CtlEvt.E.GetCertOk, (uint)CtlEvt.E.GetServConf},
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoDnldAtt, State = (uint)Lst.DnldAttWait},
                            new Trans {Event = (uint)SmEvt.E.Success, Act = DoPing, State = (uint)Lst.PingWait},
                            new Trans {Event = (uint)SmEvt.E.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
                            new Trans {Event = (uint)SmEvt.E.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.DnldAttWait},
                            new Trans {Event = (uint)AsEvt.E.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)AsEvt.E.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
                            new Trans {Event = (uint)AsEvt.E.ReSync, Act = DoSync, State = (uint)Lst.SyncWait},
                            new Trans {Event = (uint)CtlEvt.E.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncWait},
                        }},
                }
            };
            Sm.Validate ();
            // FIXME - generate protocol state here. load it from DB or create & save to DB.
            Sm.State = ProtocolState.State;

            var dispached = Owner.Db.Table<NcPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                rec.IsDispatched == true).ToList ();
            foreach (var update in dispached) {
                update.IsDispatched = false;
                Owner.Db.Update (BackEnd.DbActors.Proto, update);
            }
            NcEventable.DbEvent += DbEventHandler;
        }
        // Methods callable by the owner.
        public override void Execute ()
        {
            Sm.PostAtMostOneEvent ((uint)SmEvt.E.Launch);
        }

        public override void CredResp ()
        {
            Sm.PostAtMostOneEvent ((uint)CtlEvt.E.UiSetCred);
        }

        public override void ServerConfResp ()
        {
            Server = Owner.Db.Table<NcServer> ().Single (rec => rec.Id == Account.ServerId);
            Sm.PostAtMostOneEvent ((uint)CtlEvt.E.UiSetServConf);
        }

        public override void CertAskResp (bool isOkay)
        {
            // FIXME - make sure that the cmd is an autodiscover command! 
            // Maybe get the AsAutodiscoverCommand.Lev out of this file.
            if ((uint)Lst.UiCertOkWait == Sm.State) {
                Sm.PostEvent ((uint)((isOkay) ? 
                    AsAutodiscoverCommand.SharedEvt.E.ServerCertYes : 
                    AsAutodiscoverCommand.SharedEvt.E.ServerCertNo));
            }
        }

        // State-machine's state persistance callback.
        private void UpdateSavedState () {
            ProtocolState.State = Sm.State;
            Owner.Db.Update (BackEnd.DbActors.Proto, ProtocolState);
        }

        // State-machine action methods.

        private void DoUiServConfReq () {
            // Send the request toward the UI.
            Owner.ServConfReq (this);
        }

        private void DoSetServConf ()
        {
            // Send the event to the command.
            var autoDiscoCmd = (AsAutodiscoverCommand)Cmd;
            autoDiscoCmd.Sm.PostEvent ((uint)AsAutodiscoverCommand.TlEvt.E.ServerSet);
        }
        
        private void DoUiCredReq () {
            // Send the request toward the UI.
            Owner.CredReq (this);
        }

        private void DoSetCred ()
        {
            var autoDiscoCmd = (AsAutodiscoverCommand)Cmd;
            autoDiscoCmd.Sm.PostEvent ((uint)AsAutodiscoverCommand.TlEvt.E.CredSet);
        }

        private void DoUiCertOkReq () {
            Owner.CertAskReq (this, (X509Certificate2)Sm.Arg);
        }

        private void DoCertOkNo () {
            var autoDiscoCmd = (AsAutodiscoverCommand)Cmd;
            autoDiscoCmd.Sm.PostEvent ((uint)AsAutodiscoverCommand.SharedEvt.E.ServerCertNo);
        }

        private void DoCertOkYes () {
            var autoDiscoCmd = (AsAutodiscoverCommand)Cmd;
            autoDiscoCmd.Sm.PostEvent ((uint)AsAutodiscoverCommand.SharedEvt.E.ServerCertYes);
        }

        private void DoUiHardFailInd () {
            // Send the indication toward the UI.
            Owner.HardFailInd (this);
        }

        private void DoUiTempFailInd () {
            // Send the indication toward the UI.
            Owner.TempFailInd (this);
            // Owner needs to send launch to get it going again. Sm.PostEvent ((uint)SmEvt.E.Launch);
        }
        private void DoDisc () {
            Cmd = new AsAutodiscoverCommand (this);
            Cmd.Execute (Sm);
        }
        private void DoOpt () {
            Cmd = new AsOptionsCommand (this);
            Cmd.Execute (Sm);
        }
        private void DoProv () {
            Cmd = new AsProvisionCommand (this);
            Cmd.Execute (Sm);
        }
        private void DoOldProtoProv () {
            // If OPTIONS gets a hard failure, then assume oldest supported protocol version and try to keep going.
            AsOptionsCommand.SetOldestProtoVers (this);
            DoProv ();
        }
        private void DoSettings () {
            Cmd = new AsSettingsCommand (this);
            Cmd.Execute (Sm);
        }
        private void DoFSync () {
            Cmd = new AsFolderSyncCommand (this);
            Cmd.Execute (Sm);
        }
        private void DoSync () {
            Cmd = new AsSyncCommand (this);
            Cmd.Execute (Sm);
        }
        private void DoSend () {
            if ((uint)Lst.PingWait == Sm.State) {
                Cmd.Cancel ();
            }
            Cmd = new AsSendMailCommand (this);
            Cmd.Execute (Sm);
        }
        private void DoDnldAtt () {
            Cmd = new AsItemOperationsCommand (this);
            Cmd.Execute (Sm);
        }
        private void DoPing () {
            if (0 < Owner.Db.Table<NcPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                rec.DataType == NcPendingUpdate.DataTypes.EmailMessage &&
                rec.Operation == NcPendingUpdate.Operations.Delete).Count ()) {
                Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync);
            } 
            else if (0 < Owner.Db.Table<NcPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                rec.DataType == NcPendingUpdate.DataTypes.EmailMessage &&
                rec.Operation == NcPendingUpdate.Operations.Send).Count ()) {
                Sm.PostEvent ((uint)CtlEvt.E.SendMail);
            }
            else if (0 < Owner.Db.Table<NcPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
                rec.DataType == NcPendingUpdate.DataTypes.Attachment &&
                rec.Operation == NcPendingUpdate.Operations.Download).Count ()) {
                Sm.PostEvent ((uint)CtlEvt.E.DnldAtt);
            } else {
                Cmd = new AsPingCommand (this);
                Cmd.Execute (Sm);
            }
        }

        private void DbEventHandler (BackEnd.DbActors dbActor, BackEnd.DbEvents dbEvent, NcEventable target, EventArgs e)
        {
            if (BackEnd.DbActors.Proto == dbActor || target.AccountId != Account.Id) {
                return;
            }
            switch (target.GetType().Name) {
            case NcEmailMessage.ClassName:
                NcEmailMessage emailMessage = (NcEmailMessage)target;
                switch (dbEvent) {
                case BackEnd.DbEvents.WillDelete:
                    if (emailMessage.IsAwatingSend) {
                        /* UI is deleting a to-be-sent message. Cancel send by deleting
                         * The pending update if possible.
                         */
                        var existingUpdate = Owner.Db.Table<NcPendingUpdate> ().Single (rec => rec.AccountId == Account.Id &&
                            rec.EmailMessageId == emailMessage.Id);
                        if (! existingUpdate.IsDispatched) {
                            Owner.Db.Delete (BackEnd.DbActors.Proto, existingUpdate);
                        }
                        Owner.Db.Delete (BackEnd.DbActors.Proto, existingUpdate);
                    } else {
                        // UI is deleting a message. We need to delete it on the server.
                        var deleUpdate = new NcPendingUpdate () {
                            AccountId = Account.Id,
                            Operation = NcPendingUpdate.Operations.Delete,
                            DataType = NcPendingUpdate.DataTypes.EmailMessage,
                            FolderId = emailMessage.FolderId,
                            ServerId = emailMessage.ServerId
                        };
                        Owner.Db.Insert (BackEnd.DbActors.Proto, deleUpdate);
                        Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync);
                    }
                    break;
                case BackEnd.DbEvents.DidWrite:
                    if (emailMessage.IsAwatingSend) {
                        var sendUpdate = new NcPendingUpdate () {
                            AccountId = Account.Id,
                            Operation = NcPendingUpdate.Operations.Send,
                            DataType = NcPendingUpdate.DataTypes.EmailMessage,
                            EmailMessageId = emailMessage.Id
                        };
                        Owner.Db.Insert (BackEnd.DbActors.Proto, sendUpdate);
                        Sm.PostEvent ((uint)CtlEvt.E.SendMail);
                    }
                    break;
                }
                break;
            }
        }
    }
}

