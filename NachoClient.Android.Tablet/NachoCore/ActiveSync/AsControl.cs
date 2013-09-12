using System;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{

	public class AsControl
	{
		public enum Lst : uint {DiscWait=(St.Last+1), CredWait, ServConfWait, 
			OptWait, ProvWait, SettingsWait, FSyncWait, SyncWait, Idle};
		public enum Lev : uint {GetCred=(Ev.Last+1), SetCred, SetServConf, ReDisc, ReProv, ReSync};

		private IAsOwner m_owner;
		private IAsDataSource m_dataSource;
		private StateMachine m_sm;

		public AsControl (IAsOwner owner, IAsDataSource dataSource)
		{
			m_owner = owner;
			m_dataSource = dataSource;
			m_sm = new StateMachine () { Name = "as:control",
				LocalEventType = typeof(Lev),
				LocalStateType = typeof(Lst),
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
							new Trans {Event = (uint)Ev.Success, Act = DoNop, State = (uint)Lst.Idle},
							new Trans {Event = (uint)Ev.Launch, Act = DoSync, State = (uint)Lst.SyncWait},
							new Trans {Event = (uint)Lev.ReProv, Act = DoProv, State = (uint)Lst.ProvWait},
							new Trans {Event = (uint)Lev.ReDisc, Act = DoDisc, State = (uint)Lst.DiscWait},
							new Trans {Event = (uint)Lev.ReSync, Act = DoSync, State = (uint)Lst.SyncWait}}},
					new Node {State = (uint)Lst.Idle, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoNop, State = (uint)Lst.Idle},
							new Trans {Event = (uint)Lev.ReSync, Act = DoSync, State = (uint)Lst.SyncWait}}}
				}
			};
		}
		public void Execute () {
			m_sm.ProcEvent ((uint)Ev.Launch);
		}
		public void CredResponse () {
			m_sm.ProcEvent ((uint)Lev.SetCred);
		}
		public void ServConfResponse () {
			m_sm.ProcEvent ((uint)Lev.SetServConf);
		}
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
			var cmd = new AsAutodiscover (m_dataSource);
			cmd.Execute (m_sm);
		}
		private void DoOpt () {
			var cmd = new AsOptions (m_dataSource);
			cmd.Execute (m_sm);
		}
		private void DoProv () {
			var cmd = new AsProvisionCommand (m_dataSource);
			cmd.Execute (m_sm);
		}
		private void DoSettings () {
			var cmd = new AsSettingsCommand (m_dataSource);
			cmd.Execute (m_sm);
		}
		private void DoFSync () {
			var cmd = new AsFolderSyncCommand (m_dataSource);
			cmd.Execute (m_sm);
		}
		private void DoSync () {
			var headers = new Dictionary<string, string> {
				{"to", "chrisp@nachocove.com"},
				{"from", "jeffe@nachocove.com"},
				{"subject", "wow"},
				{"date", "Mon, 29 Jul 2013 13:42:22 -0700"}
			};
			var cmd = new AsSendMailCommand (m_dataSource,
			                                 headers,
			                                "Here is message #1");
			cmd.Execute (m_sm);
			//var cmd = new AsSyncCommand (m_dataSource);
			//cmd.Execute (m_sm);
		}
		private void DoSend () {
		}
		private void DoPing () {
		}
		private void DoNop () {}
	}
}

