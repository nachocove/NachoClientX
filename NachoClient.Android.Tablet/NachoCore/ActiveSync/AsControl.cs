using System;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{

	public class AsControl
	{
		public enum Lst : uint {DiscWait=(St.Last+1), CredWait, ServConfWait, 
			OptWait, ProvWait, SettingsWait, FSyncWait, SyncWait, Idle};
		public enum Lev : uint {GetCred=(Ev.Last+1), SetCred, SetServConf, ReDisc, ReProv, ReSync};

		private StateMachine m_Sm;
		private IAsDataSource m_dataSource;

		public AsControl (IAsDataSource dataSource)
		{
			m_dataSource = dataSource;
			m_Sm = new StateMachine () {
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
		public void DoUiServConf () {
		}
		public void DoUiCred () {
		}
		public void DoDisc () {
			var cmd = new AsAutodiscover (m_dataSource);
			cmd.Execute (m_Sm);
		}
		public void DoOpt () {
			var cmd = new AsOptions (m_dataSource);
			cmd.Execute (m_Sm);
		}
		public void DoProv () {
			var cmd = new AsProvisionCommand (m_dataSource);
			cmd.Execute (m_Sm);
		}
		public void DoSettings () {
		}
		public void DoFSync () {
		}
		public void DoSync () {
		}
		public void DoSend () {
		}
		public void DoPing () {
		}
		public void DoNop () {}
	}
}

