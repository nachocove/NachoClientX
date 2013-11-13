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

        // Events of the form UiXxYy are events coming directly from the UI/App toward the controller.
        // DB-based events (even if UI-driven) and server-based events lack the Ui prefix.
        public enum Lev : uint {
            GetCred=(Ev.Last+1),
            UiSetCred, 
            GetServConf,
            UiSetServConf,
            GetCertOk,
            UiCertOkYes,
            UiCertOkNo,
            ReDisc, 
            ReProv, 
            ReSync, 
            ReFSync, 
            SendMail, 
            DnldAtt
        };

		public NcCred Cred { set; get; }
		public NcProtocolState ProtocolState { set; get; }
		public NcServer Server { set; get; }
		public IProtoControlOwner Owner { set; get; }
		public AsProtoControl Control { set; get; }

		public AsProtoControl (IProtoControlOwner owner, NcAccount account)
		{
			Control = this;
			Owner = owner;
			Account = account;
			// FIXME - property gets must come from the DB each time, no need for public setters.
			Cred = Owner.Db.Table<NcCred> ().Single (rec => rec.Id == Account.CredId);
            try {
			    Server = Owner.Db.Table<NcServer> ().Single (rec => rec.Id == Account.ServerId);
            } catch (System.InvalidOperationException) {}

			Sm = new StateMachine () { 
                Name = "as:control" + account.Id,
				LocalEventType = typeof(Lev),
				LocalStateType = typeof(Lst),
				StateChangeIndication = UpdateSavedState,
				TransTable = new[] {
					new Node {
						State = (uint)St.Start,
                        Drop = new [] {(uint)Lev.SendMail, (uint)Lev.UiSetCred, (uint)Lev.UiSetServConf, (uint)Lev.UiCertOkNo, 
                            (uint)Lev.UiCertOkYes},
                        Invalid = new [] {(uint)Ev.Success, (uint)Ev.TempFail, (uint)Ev.HardFail, (uint)Lev.GetCred, (uint)Lev.ReDisc,
                            (uint)Lev.ReProv, (uint)Lev.ReSync, (uint)Lev.ReFSync, (uint)Lev.DnldAtt, (uint)Lev.GetCertOk, 
                            (uint)Lev.GetServConf},
						On = new [] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoDisc, State=(uint)Lst.DiscWait},
                        }},

					new Node {
                        // NOTE: There is no HardFail. Can't pass DiscWait w/out a working server - period.
						State = (uint)Lst.DiscWait, 
                        Drop = new [] {(uint)Lev.ReSync, (uint)Lev.SendMail, (uint)Lev.UiCertOkNo, (uint)Lev.UiCertOkYes},
						Invalid = new [] {(uint)Ev.HardFail, (uint)Lev.ReDisc, (uint)Lev.ReProv, (uint)Lev.ReFSync, (uint)Lev.DnldAtt},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Ev.Success, Act = DoOpt, State = (uint)Lst.OptWait},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)Lev.GetCred, Act = DoUiCredReq, State = (uint)Lst.UiCredWait},
                            new Trans {Event = (uint)Lev.GetServConf, Act = DoUiServConfReq, State = (uint)Lst.UiServConfWait},
                            new Trans {Event = (uint)Lev.GetCertOk, Act = DoUiCertOkReq, State = (uint)Lst.UiCertOkWait},
                            new Trans {Event = (uint)Lev.UiSetServConf, Act = DoSetServConf, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)Lev.UiSetCred, Act = DoSetCred, State = (uint)Lst.DiscWait},
                        }},

					new Node {
						State = (uint)Lst.UiCredWait,
						Drop = new [] {(uint)Lev.ReSync, (uint)Lev.SendMail, (uint)Lev.UiCertOkNo, (uint)Lev.UiCertOkYes},
						Invalid = new [] {(uint)Ev.Success, (uint)Ev.HardFail, (uint)Ev.TempFail, (uint)Lev.GetCred,
							(uint)Lev.ReDisc, (uint)Lev.ReProv, (uint)Lev.ReFSync,
                            (uint)Lev.DnldAtt, (uint)Lev.GetCertOk, (uint)Lev.GetServConf},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoUiCredReq, State = (uint)Lst.UiCredWait},
							new Trans {Event = (uint)Lev.UiSetCred, Act = DoDisc, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)Lev.UiSetServConf, Act = DoSetServConf, State = (uint)Lst.DiscWait},
                        }},

					new Node {
						State = (uint)Lst.UiServConfWait,
                        Drop = new [] {(uint)Lev.ReSync, (uint)Lev.SendMail, (uint)Lev.UiSetCred, (uint)Lev.UiCertOkNo, (uint)Lev.UiCertOkYes},
						Invalid = new [] {(uint)Ev.Success, (uint)Ev.HardFail, (uint)Ev.TempFail, (uint)Lev.GetCred, (uint)Lev.ReDisc,
                            (uint)Lev.ReProv, (uint)Lev.ReFSync, (uint)Lev.DnldAtt, (uint)Lev.GetCertOk, (uint)Lev.GetServConf},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoUiServConfReq, State = (uint)Lst.UiServConfWait},
							new Trans {Event = (uint)Lev.UiSetServConf, Act = DoSetServConf, State = (uint)Lst.DiscWait},
                        }},

                    new Node {
                        State = (uint)Lst.UiCertOkWait,
                        Drop = new [] {(uint)Lev.ReSync, (uint)Lev.SendMail},
                        Invalid = new [] {(uint)Ev.Success, (uint)Ev.HardFail, (uint)Ev.TempFail, (uint)Lev.GetCred,
                            (uint)Lev.UiSetCred, (uint)Lev.ReDisc, (uint)Lev.ReProv, (uint)Lev.ReFSync, (uint)Lev.GetServConf,
                            (uint)Lev.DnldAtt, (uint)Lev.GetCertOk},
                        On = new [] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoDisc, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)Lev.UiCertOkYes, Act = DoCertOkYes, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)Lev.UiCertOkNo, Act = DoCertOkNo, State = (uint)Lst.DiscWait},
                            new Trans {Event = (uint)Lev.UiSetServConf, Act = DoSetServConf, State = (uint)Lst.DiscWait},
                        }},

					new Node {
						State = (uint)Lst.OptWait,
                        Drop = new [] {(uint)Lev.ReSync, (uint)Lev.SendMail, (uint)Lev.UiCertOkNo, (uint)Lev.UiCertOkYes,
                            (uint)Lev.UiSetCred, (uint)Lev.UiSetServConf},
						Invalid = new [] {(uint)Lev.GetCred, (uint)Lev.ReDisc, (uint)Lev.ReProv, (uint)Lev.ReFSync,
                            (uint)Lev.DnldAtt, (uint)Lev.GetCertOk, (uint)Lev.GetServConf},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoOpt, State = (uint)Lst.OptWait},
							new Trans {Event = (uint)Ev.Success, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoOldProtoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.OptWait}}},

					new Node {
						State = (uint)Lst.ProvWait,
                        Drop = new [] {(uint)Lev.ReSync,  (uint)Lev.SendMail, (uint)Lev.UiCertOkNo, (uint)Lev.UiCertOkYes,
                            (uint)Lev.UiSetCred, (uint)Lev.UiSetServConf},
						Invalid = new [] {(uint)Lev.GetCred, (uint)Lev.ReProv, (uint)Lev.ReFSync, (uint)Lev.DnldAtt,
                            (uint)Lev.GetCertOk, (uint)Lev.GetServConf},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Ev.Success, Act = DoSettings, State = (uint)Lst.SettingsWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
                        }},

					new Node {
						State = (uint)Lst.SettingsWait,
                        Drop = new [] {(uint)Lev.ReSync, (uint)Lev.SendMail, (uint)Lev.UiCertOkNo, (uint)Lev.UiCertOkYes,
                            (uint)Lev.UiSetCred, (uint)Lev.UiSetServConf},
						Invalid = new [] {(uint)Lev.GetCred, (uint)Lev.ReFSync, (uint)Lev.DnldAtt, (uint)Lev.GetCertOk,
                            (uint)Lev.GetServConf},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoSettings, State = (uint)Lst.SettingsWait},
							new Trans {Event = (uint)Ev.Success, Act = DoFSync, State = (uint)Lst.FSyncWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SettingsWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
                        }},

					new Node {
						State = (uint)Lst.FSyncWait,
                        Drop = new [] {(uint)Lev.ReSync, (uint)Lev.SendMail, (uint)Lev.UiCertOkNo, (uint)Lev.UiCertOkYes,
                            (uint)Lev.UiSetCred, (uint)Lev.UiSetServConf},
						Invalid = new [] {(uint)Lev.GetCred,  (uint)Lev.ReFSync, (uint)Lev.DnldAtt, (uint)Lev.GetCertOk, 
                            (uint)Lev.GetServConf},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoFSync, State = (uint)Lst.FSyncWait},
							new Trans {Event = (uint)Ev.Success, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SettingsWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait}}},

					new Node {
						State = (uint)Lst.SyncWait,
                        Drop = new [] {(uint)Lev.SendMail, (uint)Lev.UiCertOkNo, (uint)Lev.UiCertOkYes, (uint)Lev.UiSetCred,
                            (uint)Lev.UiSetServConf},
						Invalid = new [] {(uint)Lev.GetCred, (uint)Lev.DnldAtt, (uint)Lev.GetCertOk, (uint)Lev.GetServConf},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Ev.Success, Act = DoPing, State = (uint)Lst.PingWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReSync, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Lev.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncWait}}},

					new Node {
						State = (uint)Lst.PingWait,
                        Drop = new [] {(uint)Lev.UiCertOkNo, (uint)Lev.UiCertOkYes, (uint)Lev.UiSetCred, (uint)Lev.UiSetServConf},
                        Invalid = new [] {(uint)Lev.GetCred, (uint)Lev.GetCertOk, (uint)Lev.GetServConf},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoPing, State = (uint)Lst.PingWait},
							new Trans {Event = (uint)Ev.Success, Act = DoPing, State = (uint)Lst.PingWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.PingWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReSync, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Lev.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncWait},
							new Trans {Event = (uint)Lev.SendMail, Act = DoSend, State = (uint)Lst.SendMailWait},
							new Trans {Event = (uint)Lev.DnldAtt, Act = DoDnldAtt, State = (uint)Lst.DnldAttWait}}},

					new Node {
						State = (uint)Lst.SendMailWait,
                        Drop = new [] {(uint)Lev.SendMail, (uint)Lev.UiCertOkNo, (uint)Lev.UiCertOkYes, (uint)Lev.UiSetCred,
                            (uint)Lev.UiSetServConf},
						Invalid = new [] {(uint)Lev.GetCred, (uint)Lev.DnldAtt, (uint)Lev.GetCertOk, (uint)Lev.GetServConf},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoSend, State = (uint)Lst.SendMailWait},
							new Trans {Event = (uint)Ev.Success, Act = DoPing, State = (uint)Lst.PingWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SendMailWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReSync, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Lev.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncWait}}},

					new Node {
						State = (uint)Lst.DnldAttWait,
                        Drop = new [] {(uint)Lev.SendMail, (uint)Lev.UiCertOkNo, (uint)Lev.UiCertOkYes, (uint)Lev.UiSetCred,
                            (uint)Lev.UiSetServConf},
						Invalid = new [] {(uint)Lev.GetCred, (uint)Lev.DnldAtt, (uint)Lev.GetCertOk, (uint)Lev.GetServConf},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoDnldAtt, State = (uint)Lst.DnldAttWait},
							new Trans {Event = (uint)Ev.Success, Act = DoPing, State = (uint)Lst.PingWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.DnldAttWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReSync, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Lev.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncWait}}},
				}
			};
            Sm.Validate ();
            // FIXME - generate protocol state here. load it from DB or create & save to DB.
            ProtocolState = Owner.Db.Table<NcProtocolState> ().Single (rec => rec.Id == Account.ProtocolStateId);
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
			Sm.PostAtMostOneEvent ((uint)Ev.Launch);
		}

		public override void CredResp ()
        {
			Sm.PostAtMostOneEvent ((uint)Lev.UiSetCred);
		}

        public override void ServerConfResp ()
        {
            Server = Owner.Db.Table<NcServer> ().Single (rec => rec.Id == Account.ServerId);
            Sm.PostAtMostOneEvent ((uint)Lev.UiSetServConf);
        }

        public override void CertAskResp (bool isOkay)
        {
            // FIXME - make sure that the cmd is an autodiscover command! 
            // Maybe get the AsAutodiscoverCommand.Lev out of this file.
            if ((uint)Lst.UiCertOkWait == Sm.State) {
                Sm.PostEvent ((uint)((isOkay) ? AsAutodiscoverCommand.Lev.ServerCertYes : 
                                     AsAutodiscoverCommand.Lev.ServerCertNo));
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
            autoDiscoCmd.Sm.PostEvent ((uint)AsAutodiscoverCommand.Lev.ServerSet);
        }
		
		private void DoUiCredReq () {
            // Send the request toward the UI.
			Owner.CredReq (this);
		}

        private void DoSetCred ()
        {
            var autoDiscoCmd = (AsAutodiscoverCommand)Cmd;
            autoDiscoCmd.Sm.PostEvent ((uint)AsAutodiscoverCommand.Lev.CredSet);
        }

        private void DoUiCertOkReq () {
            Owner.CertAskReq (this, (X509Certificate2)Sm.Arg);
        }

        private void DoCertOkNo () {
            var autoDiscoCmd = (AsAutodiscoverCommand)Cmd;
            autoDiscoCmd.Sm.PostEvent ((uint)AsAutodiscoverCommand.Lev.ServerCertNo);
        }

        private void DoCertOkYes () {
            var autoDiscoCmd = (AsAutodiscoverCommand)Cmd;
            autoDiscoCmd.Sm.PostEvent ((uint)AsAutodiscoverCommand.Lev.ServerCertYes);
        }

        private void DoUiHardFailInd () {
            // Send the indication toward the UI.
			Owner.HardFailInd (this);
		}

		private void DoUiTempFailInd () {
            // Send the indication toward the UI.
            Owner.TempFailInd (this);
			// Owner needs to send launch to get it going again. Sm.PostEvent ((uint)Ev.Launch);
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
				Sm.PostAtMostOneEvent ((uint)Lev.ReSync);
			} 
			else if (0 < Owner.Db.Table<NcPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
				rec.DataType == NcPendingUpdate.DataTypes.EmailMessage &&
				rec.Operation == NcPendingUpdate.Operations.Send).Count ()) {
				Sm.PostEvent ((uint)Lev.SendMail);
			}
			else if (0 < Owner.Db.Table<NcPendingUpdate> ().Where (rec => rec.AccountId == Account.Id &&
			    rec.DataType == NcPendingUpdate.DataTypes.Attachment &&
			    rec.Operation == NcPendingUpdate.Operations.Download).Count ()) {
				Sm.PostEvent ((uint)Lev.DnldAtt);
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
						Sm.PostAtMostOneEvent ((uint)Lev.ReSync);
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
						Sm.PostEvent ((uint)Lev.SendMail);
					}
					break;
				}
				break;
			}
		}
	}
}

