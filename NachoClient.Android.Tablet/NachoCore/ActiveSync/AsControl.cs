using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{

	public class AsProtoControl : ProtoControl, IAsDataSource
	{
		public enum Lst : uint {DiscWait=(St.Last+1), CredWait, ServConfWait, 
			OptWait, ProvWait, SettingsWait, FSyncWait, SyncWait, Idle, SendMailWait};
		public enum Lev : uint {GetCred=(Ev.Last+1), SetCred, SetServConf, ReDisc, ReProv, ReSync, SendMail};

		private IProtoControlOwner m_owner;

		public SQLiteConnectionWithEvents Db { set; get; }
		public NcCred Cred { set; get; }
		public NcProtocolState ProtocolState { set; get; }
		public NcServer Server { set; get; }

		public AsProtoControl (IProtoControlOwner owner, NcAccount account)
		{
			m_owner = owner;
			Db = m_owner.Db;
			Account = account;
			Cred = m_owner.Db.Table<NcCred> ().Single (rec => rec.Id == Account.CredId);
			ProtocolState = m_owner.Db.Table<NcProtocolState> ().Where (rec => rec.Id == Account.ProtocolStateId).First ();
			Server = m_owner.Db.Table<NcServer> ().Where (rec => rec.Id == Account.ServerId).First ();

			Sm = new StateMachine () { Name = "as:control",
				LocalEventType = typeof(Lev),
				LocalStateType = typeof(Lst),
				StateChangeIndication = UpdateSavedState,
				TransTable = new[] {
					new Node {State = (uint)St.Start, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoDisc, State=(uint)Lst.DiscWait}}},
					new Node {State = (uint)Lst.DiscWait, On = new [] {
							new Trans {Event = (uint)Ev.Success, Act = DoOpt, State = (uint)Lst.OptWait},
							new Trans {Event = (uint)Ev.Launch, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Ev.Failure, Act = DoUiServConf, State = (uint)Lst.ServConfWait},
							new Trans {Event = (uint)Lev.GetCred, Act = DoUiCred, State = (uint)Lst.CredWait}}},
					new Node {State = (uint)Lst.CredWait, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoUiCred, State = (uint)Lst.CredWait},
							new Trans {Event = (uint)Lev.SetCred, Act = DoDisc, State = (uint)Lst.DiscWait}}},
					new Node {State = (uint)Lst.ServConfWait, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoUiServConf, State = (uint)Lst.ServConfWait},
							new Trans {Event = (uint)Lev.SetServConf, Act = DoOpt, State = (uint)Lst.OptWait}}},
					new Node {State = (uint)Lst.OptWait, On = new [] {
							new Trans {Event = (uint)Ev.Success, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Ev.Launch, Act = DoOpt, State = (uint)Lst.OptWait}}},
					new Node {State = (uint)Lst.ProvWait, On = new [] {
							new Trans {Event = (uint)Ev.Success, Act = DoSettings, State = (uint)Lst.SettingsWait},
							new Trans {Event = (uint)Ev.Failure, Act = DoUiHardFailure, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.Launch, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait}}},
					new Node {State = (uint)Lst.SettingsWait, On = new [] {
							new Trans {Event = (uint)Ev.Success, Act = DoFSync, State = (uint)Lst.FSyncWait},
							new Trans {Event = (uint)Ev.Launch, Act = DoSettings, State = (uint)Lst.SettingsWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait}}},
					new Node {State = (uint)Lst.FSyncWait, On = new [] {
							new Trans {Event = (uint)Ev.Success, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Ev.Launch, Act = DoFSync, State = (uint)Lst.FSyncWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait}}},
					new Node {State = (uint)Lst.SyncWait, On = new [] {
							new Trans {Event = (uint)Ev.Success, Act = DoUpdateQ, State = (uint)Lst.Idle},
							new Trans {Event = (uint)Ev.Launch, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReSync, Act = DoSync, State = (uint)Lst.SyncWait}}},
					new Node {State = (uint)Lst.Idle, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoUpdateQ, State = (uint)Lst.Idle},
							new Trans {Event = (uint)Lev.ReSync, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Lev.SendMail, Act = DoSend, State = (uint)Lst.SendMailWait}}},
					new Node {State = (uint)Lst.SendMailWait, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoSend, State = (uint)Lst.SendMailWait},
							new Trans {Event = (uint)Ev.Success, Act = DoUpdateQ, State = (uint)Lst.Idle}}},
				}
			};
			Sm.State = ProtocolState.State;

			NcEventable.DidWriteToDb += DidWriteToDbHandler;
			NcEventable.WillDeleteFromDb += WillDeleteFromDbHandler;
		}
		// Methods callable by the owner.
		public void Execute () {
			Sm.ProcEvent ((uint)Ev.Launch);
		}
		public void CredResponse () {
			Sm.ProcEvent ((uint)Lev.SetCred);
		}
		public void ServConfResponse () {
			Sm.ProcEvent ((uint)Lev.SetServConf);
		}
		public bool SendEMail(Dictionary<string,string> message) {
			// FIXME - parameter checking.
			// FIXME - be able to handle this event in ALL states.
			Sm.ProcEvent ((uint)Lev.SendMail, message);
			return true;
		}
		// State-machine's state persistance callback.
		// FIXME - we need to also save the optional arg.
		// FIXME - do we need to persist the whole event queue (think SendMail).
		// or does that need to go into the update Q?
		private void UpdateSavedState () {
			ProtocolState.State = Sm.State;
			Db.Update (BackEnd.Actors.Proto, ProtocolState);
		}
		// State-machine actions.
		private void DoUiServConf () {
			m_owner.ServConfRequest (this);
		}
		private void DoUiCred () {
			m_owner.CredRequest (this);
		}
		private void DoUiHardFailure () {
			m_owner.HardFailureIndication (this);
		}
		private void DoDisc () {
			// FIXME - complete autodiscovery.
			//var cmd = new AsAutodiscover (this);
			//cmd.Execute (Sm);
			Sm.ProcEvent ((uint)Ev.Success);
		}
		private void DoOpt () {
			var cmd = new AsOptions (this);
			cmd.Execute (Sm);
		}
		private void DoProv () {
			var cmd = new AsProvisionCommand (this);
			cmd.Execute (Sm);
		}
		private void DoSettings () {
			var cmd = new AsSettingsCommand (this);
			cmd.Execute (Sm);
		}
		private void DoFSync () {
			var cmd = new AsFolderSyncCommand (this);
			cmd.Execute (Sm);
		}
		private void DoSync () {
			var cmd = new AsSyncCommand (this);
			cmd.Execute (Sm);
		}
		private void DoSend () {
			var message = (Dictionary<string,string>)Sm.Arg;
			var cmd = new AsSendMailCommand (this, message);
			cmd.Execute (Sm);
		}
		private void DoPing () {
		}
		private void DoUpdateQ () {
		}

		private void DidWriteToDbHandler (BackEnd.Actors actor,
		                                  int accountId, Type klass, int id, EventArgs e) {
			if (BackEnd.Actors.Proto == actor ||
			    accountId != Account.Id) {
				return;
			}
			Db.Insert (BackEnd.Actors.Proto, new NcPendingUpdate () {
				Operation = NcPendingUpdate.Operations.CreateUpdate,
				AccountId = accountId,
				TargetId = id,
				Klass = klass
			});
		}
		private void WillDeleteFromDbHandler (BackEnd.Actors actor,
		                                      int accountId, Type klass, int Id, EventArgs e) {
			if (BackEnd.Actors.Proto == actor ||
			    accountId != Account.Id) {
				return;
			}
			Db.Insert (BackEnd.Actors.Proto, new NcPendingUpdate () {
				Operation = NcPendingUpdate.Operations.Delete,
				AccountId = accountId,
				TargetId = Id,
				Klass = klass
			});
		}
	}
}

