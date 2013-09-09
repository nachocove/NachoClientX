using System;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
	public class AsAutodiscover
	{
		public enum Lst : uint {S1PostWait=(St.Last+1), S2PostWait, S3GetWait, S4DnsWait, S5PostWait, SubCheck};
		public enum Lev : uint {ReStart=(Ev.Last+1), ReDir};

		private IAsDataSource m_dataSource;
		private CancellationTokenSource m_cts;
		private StateMachine m_Sm;
		private StateMachine m_ParentSm;

		public AsAutodiscover (IAsDataSource dataSource)
		{
			m_dataSource = dataSource;
			m_cts = new CancellationTokenSource();
			m_Sm = new StateMachine () { TransTable = 
				new[] {
					new Node {State = (uint)St.Start, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoS1Post, State = (uint)Lst.S1PostWait}}},
					new Node {State = (uint)Lst.S1PostWait, On = new [] {
							new Trans {Event = (uint)Ev.Success, Act = DoFinish, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.Failure, Act = DoS2Post, State = (uint)Lst.S2PostWait},
							new Trans {Event = (uint)Lev.ReDir, Act = DoS5Post, State = (uint)Lst.S5PostWait},
							new Trans {Event = (uint)Lev.ReStart, Act = DoS1Post, State = (uint)Lst.S1PostWait}}},
					new Node {State = (uint)Lst.S2PostWait, On = new [] {
							new Trans {Event = (uint)Ev.Success, Act = DoFinish, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.Failure, Act = DoS3Get, State = (uint)Lst.S3GetWait},
							new Trans {Event = (uint)Lev.ReDir, Act = DoS5Post, State = (uint)Lst.S5PostWait},
							new Trans {Event = (uint)Lev.ReStart, Act = DoS1Post, State = (uint)Lst.S1PostWait}}},
					new Node {State = (uint)Lst.S3GetWait, On = new [] {
							new Trans {Event = (uint)Ev.Failure, Act = DoS4Dns, State = (uint)Lst.S4DnsWait},
							new Trans {Event = (uint)Lev.ReDir, Act = DoS5Post, State = (uint)Lst.S5PostWait}}},
					new Node {State = (uint)Lst.S4DnsWait, On = new [] {
							new Trans {Event = (uint)Ev.Failure, Act = DoSubCheck, State = (uint)Lst.SubCheck},
							new Trans {Event = (uint)Lev.ReDir, Act = DoS5Post, State = (uint)Lst.S5PostWait}}},
					new Node {State = (uint)Lst.S5PostWait, On = new [] {
							new Trans {Event = (uint)Ev.Success, Act = DoFinish, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.Failure, Act = DoSubCheck, State = (uint)Lst.SubCheck},
							new Trans {Event = (uint)Lev.ReDir, Act = DoS5Post, State = (uint)Lst.S5PostWait},
							new Trans {Event = (uint)Lev.ReStart, Act = DoS1Post, State = (uint)Lst.S1PostWait}}},
					new Node {State = (uint)Lst.SubCheck, On = new [] {
							new Trans {Event = (uint)Ev.Failure, Act = DoFail, State = (uint)St.Stop},
							new Trans {Event = (uint)Lev.ReStart, Act = DoS1Post, State = (uint)Lst.S1PostWait}}}
				}
			};
		}
		public void DoS1Post () {
		}
		public void DoS2Post () {
		}
		public void DoS3Get () {
		}
		public void DoS4Dns () {
		}
		public void DoS5Post () {
		}
		public void DoSubCheck () {
		}
		public void DoFinish () {
		}
		public void DoFail () {
		}
		public XDocument ToXDocument () {
			var doc = AsCommand.ToEmptyXDocument ();
			XNamespace ns = "http://schemas.microsoft.com/exchange/autodiscover/mobilesync/requestschema/2006";
			doc.Add (new XElement(ns + "Autodiscover",
			             new XElement (ns + "Request",
			                 new XElement (ns + "EMailAddress", m_dataSource.Account.EmailAddr),
			                 new XElement (ns + "AcceptableResponseSchema", 
			             "http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006"))));
			return doc;
		}
		public void Execute(StateMachine sm) {
		}
	}
}

