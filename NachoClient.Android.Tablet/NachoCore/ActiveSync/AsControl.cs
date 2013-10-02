using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{

	public class AsProtoControl : ProtoControl, IAsDataSource
	{
		private IAsCommand m_cmd;

		public enum Lst : uint {DiscWait=(St.Last+1), CredWait, ServConfWait, 
			OptWait, ProvWait, SettingsWait, FSyncWait, SyncWait, PingWait, SendMailWait};
		// NOTE - we may need a default-ignore event list for don't cares in whatever state.
		public enum Lev : uint {GetCred=(Ev.Last+1), SetCred, SetServConf, ReDisc, ReProv, ReSync, ReFSync, SendMail};

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
			// FIXME - property gets should come from the DB each time.
			// FIXME - no need for public setters.
			Cred = Owner.Db.Table<NcCred> ().Single (rec => rec.Id == Account.CredId);
			ProtocolState = Owner.Db.Table<NcProtocolState> ().Single (rec => rec.Id == Account.ProtocolStateId);
			Server = Owner.Db.Table<NcServer> ().Single (rec => rec.Id == Account.ServerId);

			Sm = new StateMachine () { Name = "as:control",
				LocalEventType = typeof(Lev),
				LocalStateType = typeof(Lst),
				StateChangeIndication = UpdateSavedState,
				// New state guidance: ReSync & SendMail can come directly from the UI's writes to the DB.
				// TempFail, HardFail, ReSync, ReDisc, and ReProv can come from using an AsCommand sub-class.
				// Always handle Launch.
				TransTable = new[] {
					new Node {
						State = (uint)St.Start, 
						Invalid = new [] {(uint)Ev.Success, (uint)Ev.HardFail, (uint)Ev.TempFail,
							(uint)Lev.GetCred, (uint)Lev.SetCred, (uint)Lev.SetServConf, (uint)Lev.ReDisc,
							(uint)Lev.ReProv, (uint)Lev.ReSync, (uint)Lev.ReFSync, (uint)Lev.SendMail},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoDisc, State=(uint)Lst.DiscWait}}},

					new Node {
						State = (uint)Lst.DiscWait, 
						Drop = new [] {(uint)Lev.ReSync, (uint)Lev.SendMail},
						Invalid = new [] {(uint)Lev.SetCred, (uint)Lev.SetServConf, (uint)Lev.ReDisc, (uint)Lev.ReProv, (uint)Lev.ReFSync},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Ev.Success, Act = DoOpt, State = (uint)Lst.OptWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiServConfReq, State = (uint)Lst.ServConfWait},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)St.Start},
							new Trans {Event = (uint)Lev.GetCred, Act = DoUiCredReq, State = (uint)Lst.CredWait}}},

					new Node {
						State = (uint)Lst.CredWait,
						Drop = new [] {(uint)Lev.ReSync, (uint)Lev.SendMail},
						Invalid = new [] {(uint)Ev.Success, (uint)Ev.HardFail, (uint)Ev.TempFail, (uint)Lev.GetCred,
							(uint)Lev.SetServConf, (uint)Lev.ReDisc, (uint)Lev.ReProv, (uint)Lev.ReFSync},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoUiCredReq, State = (uint)Lst.CredWait},
							new Trans {Event = (uint)Lev.SetCred, Act = DoDisc, State = (uint)Lst.DiscWait}}},

					new Node {
						State = (uint)Lst.ServConfWait,
						Drop = new [] {(uint)Lev.ReSync, (uint)Lev.SendMail},
						Invalid = new [] {(uint)Ev.Success, (uint)Ev.HardFail, (uint)Ev.TempFail, (uint)Lev.GetCred,
							(uint)Lev.SetCred, (uint)Lev.ReDisc, (uint)Lev.ReProv, (uint)Lev.ReFSync},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoUiServConfReq, State = (uint)Lst.ServConfWait},
							new Trans {Event = (uint)Lev.SetServConf, Act = DoOpt, State = (uint)Lst.OptWait}}},

					new Node {
						State = (uint)Lst.OptWait,
						Drop = new [] {(uint)Lev.ReSync, (uint)Lev.SendMail},
						Invalid = new [] {(uint)Lev.GetCred, (uint)Lev.SetCred, (uint)Lev.SetServConf, (uint)Lev.ReDisc,
							(uint)Lev.ReProv, (uint)Lev.ReFSync},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoOpt, State = (uint)Lst.OptWait},
							new Trans {Event = (uint)Ev.Success, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoOldProtoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.OptWait}}},

					new Node {
						State = (uint)Lst.ProvWait,
						Drop = new [] {(uint)Lev.ReSync,  (uint)Lev.SendMail},
						Invalid = new [] {(uint)Lev.GetCred, (uint)Lev.SetCred, (uint)Lev.SetServConf, (uint)Lev.ReProv, (uint)Lev.ReFSync},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Ev.Success, Act = DoSettings, State = (uint)Lst.SettingsWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait}}},

					new Node {
						State = (uint)Lst.SettingsWait,
						Drop = new [] {(uint)Lev.ReSync, (uint)Lev.SendMail},
						Invalid = new [] {(uint)Lev.GetCred, (uint)Lev.SetCred, (uint)Lev.SetServConf, (uint)Lev.ReFSync},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoSettings, State = (uint)Lst.SettingsWait},
							new Trans {Event = (uint)Ev.Success, Act = DoFSync, State = (uint)Lst.FSyncWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SettingsWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait}}},

					new Node {
						State = (uint)Lst.FSyncWait,
						Drop = new [] {(uint)Lev.ReSync, (uint)Lev.SendMail},
						Invalid = new [] {(uint)Lev.GetCred, (uint)Lev.SetCred, (uint)Lev.SetServConf, (uint)Lev.ReFSync},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoFSync, State = (uint)Lst.FSyncWait},
							new Trans {Event = (uint)Ev.Success, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SettingsWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait}}},

					new Node {
						State = (uint)Lst.SyncWait,
						Drop = new [] {(uint)Lev.SendMail},
						Invalid = new [] {(uint)Lev.GetCred, (uint)Lev.SetCred, (uint)Lev.SetServConf},
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
						Invalid = new [] {(uint)Lev.GetCred, (uint)Lev.SetCred, (uint)Lev.SetServConf},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoPing, State = (uint)Lst.PingWait},
							new Trans {Event = (uint)Ev.Success, Act = DoPing, State = (uint)Lst.PingWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.PingWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReSync, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Lev.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncWait},
							new Trans {Event = (uint)Lev.SendMail, Act = DoSend, State = (uint)Lst.SendMailWait}}},

					new Node {
						State = (uint)Lst.SendMailWait,
						Drop = new [] {(uint)Lev.SendMail},
						Invalid = new [] {(uint)Lev.GetCred, (uint)Lev.SetCred, (uint)Lev.SetServConf},
						On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoSend, State = (uint)Lst.SendMailWait},
							new Trans {Event = (uint)Ev.Success, Act = DoPing, State = (uint)Lst.PingWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SendMailWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReSync, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Lev.ReFSync, Act = DoFSync, State = (uint)Lst.FSyncWait}}},
				}
			};
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
		public void Execute () {
			Sm.PostAtMostOneEvent ((uint)Ev.Launch);
		}
		public void CredResponse () {
			Sm.PostAtMostOneEvent ((uint)Lev.SetCred);
		}
		public void ServConfResponse () {
			Sm.PostAtMostOneEvent ((uint)Lev.SetServConf);
		}
		// State-machine's state persistance callback.
		private void UpdateSavedState () {
			ProtocolState.State = Sm.State;
			Owner.Db.Update (BackEnd.DbActors.Proto, ProtocolState);
		}
		// State-machine action methods.
		private void DoUiServConfReq () {
			Owner.ServConfReq (this);
		}
		private void DoUiCredReq () {
			Owner.CredReq (this);
		}
		private void DoUiHardFailInd () {
			Owner.HardFailInd (this);
		}
		private void DoUiTempFailInd () {
			Console.WriteLine ("TempFail");
			// FIXME - need to tell the app/UI.
			Sm.PostEvent ((uint)Ev.Launch);
		}
		private void DoDisc () {
			// FIXME - complete autodiscovery.
			//var cmd = new AsAutodiscover (this);
			//cmd.Execute (Sm);
			Sm.PostEvent ((uint)Ev.Success);
		}
		private void DoOpt () {
			m_cmd = new AsOptions (this);
			m_cmd.Execute (Sm);
		}
		private void DoProv () {
			m_cmd = new AsProvisionCommand (this);
			m_cmd.Execute (Sm);
		}
		private void DoOldProtoProv () {
			// If OPTIONS gets a hard failure, then assume oldest supported protocol version and try to 
			AsOptions.SetOldestProtoVers (this);
			DoProv ();
		}
		private void DoSettings () {
			m_cmd = new AsSettingsCommand (this);
			m_cmd.Execute (Sm);
		}
		private void DoFSync () {
			m_cmd = new AsFolderSyncCommand (this);
			m_cmd.Execute (Sm);
		}
		private void DoSync () {
			m_cmd = new AsSyncCommand (this);
			m_cmd.Execute (Sm);
		}
		private void DoSend () {
			if ((uint)Lst.PingWait == Sm.State) {
				m_cmd.Cancel ();
			}
			m_cmd = new AsSendMailCommand (this);
			m_cmd.Execute (Sm);
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
			} else {
				m_cmd = new AsPingCommand (this);
				m_cmd.Execute (Sm);
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

