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

		public NcCred Cred { set; get; }
		public NcProtocolState ProtocolState { set; get; }
		public NcServer Server { set; get; }
		public StagedChanges Staged { set; get; }
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
			Staged = new StagedChanges () {
				EmailMessageDeletes = new Dictionary<int,List<StagedChange>> ()
			};
			Sm = new StateMachine () { Name = "as:control",
				LocalEventType = typeof(Lev),
				LocalStateType = typeof(Lst),
				StateChangeIndication = UpdateSavedState,
				TransTable = new[] {
					new Node {State = (uint)St.Start, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoDisc, State=(uint)Lst.DiscWait}}},

					new Node {State = (uint)Lst.DiscWait, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Ev.Success, Act = DoOpt, State = (uint)Lst.OptWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiServConfReq, State = (uint)Lst.ServConfWait},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)St.Start},
							new Trans {Event = (uint)Lev.GetCred, Act = DoUiCredReq, State = (uint)Lst.CredWait}}},

					new Node {State = (uint)Lst.CredWait, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoUiCredReq, State = (uint)Lst.CredWait},
							new Trans {Event = (uint)Lev.SetCred, Act = DoDisc, State = (uint)Lst.DiscWait}}},

					new Node {State = (uint)Lst.ServConfWait, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoUiServConfReq, State = (uint)Lst.ServConfWait},
							new Trans {Event = (uint)Lev.SetServConf, Act = DoOpt, State = (uint)Lst.OptWait}}},

					new Node {State = (uint)Lst.OptWait, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoOpt, State = (uint)Lst.OptWait},
							new Trans {Event = (uint)Ev.Success, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoOldProtoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.OptWait}}},

					new Node {State = (uint)Lst.ProvWait, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Ev.Success, Act = DoSettings, State = (uint)Lst.SettingsWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReSync, Act = DoProv, State = (uint)Lst.ProvWait}}}, // Too early to sync.

					new Node {State = (uint)Lst.SettingsWait, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoSettings, State = (uint)Lst.SettingsWait},
							new Trans {Event = (uint)Ev.Success, Act = DoFSync, State = (uint)Lst.FSyncWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SettingsWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReSync, Act = DoProv, State = (uint)Lst.ProvWait}}}, // Too early to sync.

					new Node {State = (uint)Lst.FSyncWait, On = new [] {
							new Trans {Event = (uint)Ev.Success, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SettingsWait},
							new Trans {Event = (uint)Ev.Launch, Act = DoFSync, State = (uint)Lst.FSyncWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReSync, Act = DoFSync, State = (uint)Lst.FSyncWait}}},

					new Node {State = (uint)Lst.SyncWait, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Ev.Success, Act = DoNop, State = (uint)Lst.Idle},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReSync, Act = DoSync, State = (uint)Lst.SyncWait}}},

					new Node {State = (uint)Lst.Idle, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoNop, State = (uint)Lst.Idle},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReSync, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Lev.SendMail, Act = DoSend, State = (uint)Lst.SendMailWait}}},

					new Node {State = (uint)Lst.SendMailWait, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoSend, State = (uint)Lst.SendMailWait},
							new Trans {Event = (uint)Ev.Success, Act = DoNop, State = (uint)Lst.Idle},
							new Trans {Event = (uint)Ev.HardFail, Act = DoUiHardFailInd, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.TempFail, Act = DoUiTempFailInd, State = (uint)Lst.SendMailWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReSync, Act = DoSync, State = (uint)Lst.SyncWait}}},
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
		// FIXME - we need to also save the optional arg. C# serialization?
		// FIXME - do we need to persist the whole event queue (think SendMail).
		// or does that need to go into the update Q?
		private void UpdateSavedState () {
			ProtocolState.State = Sm.State;
			Owner.Db.Update (BackEnd.Actors.Proto, ProtocolState);
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
			// FIXME.
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
		private void DoOldProtoProv () {
			AsOptions.SetOldestProtoVers (this);
			DoProv ();
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
			AsSyncCommand cmd = new AsSyncCommand (this);
			cmd.Execute (Sm);
		}
		private void DoSend () {
			var message = (Dictionary<string,string>)Sm.Arg;
			var cmd = new AsSendMailCommand (this, message);
			cmd.Execute (Sm);
		}
		private void DoPing () {
			// FIXME.
		}
		private void DoNop () {
		}

		/* State management strategy.
		 * When the UI makes DB changes, this module gets events. Those events result in changes to
		 * two data structures: the pending update pool (PUP), and the to-be-sync'd collection (TBS).
		 * The PUQ is persisted in the DB. The PUP is there is make sure we never lose an update for
		 * the server - reboots, exceptions, whatever.
		 * The TBS is never persisted. The TBS is there to make it easy to generate the outbound sync
		 * command, and to make it easy to close-out PUP entries when we get the server response.
		 * 
		 * The PUP has an IsStaged bit that gets set when corresponding TBS entry is created. Before creation
		 * of the TBS, all PUP entries get that bit reset. Then all PUP entries are processed in building
		 * the TBS.
		 * 
		 * NOTE: we will probably end up making a TBM (to-be-mailed) too.
		 */

		private void StageUpdate (NcPendingUpdate update) {
			switch (update.DataType) {
			case NcPendingUpdate.DataTypes.EmailMessage:
				if (NcPendingUpdate.Operations.Delete == update.Operation) {
					if (! Staged.EmailMessageDeletes.ContainsKey (update.FolderId)) {
						Staged.EmailMessageDeletes [update.FolderId] = new List<StagedChange> ();
					}
					Staged.EmailMessageDeletes [update.FolderId].Add (new StagedChange () {
						Update = update
					});
					var folder = Owner.Db.Table<NcFolder> ().Single (rec => rec.Id == update.FolderId);
					folder.AsSyncRequired = true;
					Owner.Db.Update (BackEnd.Actors.Proto, folder);
				} else {
					// FIXME.
				}
				break;
				// FIXME - throw on unknown.
			}
			update.IsStaged = true;
			Owner.Db.Update (BackEnd.Actors.Proto, update);
		}

		private void DidWriteToDbHandler (BackEnd.Actors actor, NcEventable target, EventArgs e) {
			if (BackEnd.Actors.Proto == actor || target.AccountId != Account.Id) {
				return;
			}
			// FIXME
		}
		private void WillDeleteFromDbHandler (BackEnd.Actors actor, NcEventable target, EventArgs e) {
			if (BackEnd.Actors.Proto == actor || target.AccountId != Account.Id) {
				return;
			}
			var update = new NcPendingUpdate () {
				Operation = NcPendingUpdate.Operations.Delete,
				AccountId = Account.Id
			};
			switch (target.GetType().Name) {
			case NcEmailMessage.ClassName:
				NcEmailMessage emailMessage = (NcEmailMessage)target;
				update.DataType = NcPendingUpdate.DataTypes.EmailMessage;
				update.FolderId = emailMessage.FolderId;
				update.ServerId = emailMessage.ServerId;
				break;
			default:
				// Don't care, abandon update before insert.
				return;
			}
			Owner.Db.Insert (BackEnd.Actors.Proto, update);
			StageUpdate (update);
			// FIXME - 2 issues: (a) only want one queued resync pending at a time. (b) make sure every state can cope.
			Sm.ProcEvent ((uint)Lev.ReSync);
		}
	}
}

