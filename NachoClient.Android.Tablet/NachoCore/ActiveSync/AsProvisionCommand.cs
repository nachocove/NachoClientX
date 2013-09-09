using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
	public class AsProvisionCommand : AsCommand
	{
		public enum Lst : uint {GetWait=(St.Last+1), AckWait};

		private StateMachine m_Sm;
		private StateMachine m_ParentSm;

		public AsProvisionCommand (IAsDataSource dataSource) :
		base("Provision", dataSource) {
			m_Sm = new StateMachine () { TransTable = 
				new[] {
					new Node {State = (uint)St.Start, On = new [] {
							new Trans {Event=(uint)Ev.Launch, Act=DoGet, State=(uint)Lst.GetWait}}},
					new Node {State = (uint)Lst.GetWait, On = new [] {
							new Trans {Event=(uint)Ev.Success, Act=DoAck, State=(uint)Lst.AckWait},
							new Trans {Event=(uint)Ev.Failure, Act=DoFail, State=(uint)St.Stop}}},
					new Node {State = (uint)Lst.AckWait, On = new [] {
							new Trans {Event=(uint)Ev.Success, Act=DoFinish, State=(uint)St.Stop},
							new Trans {Event=(uint)Ev.Failure, Act=DoFail, State=(uint)St.Stop}}}
				}
			};
		}
		public override void Execute(StateMachine sm) {
			m_ParentSm = sm;
			m_Sm.Start ();
		}
		public override XDocument ToXDocument () {
			XNamespace ns = "Provision";
			var policy = new XElement (ns + "Policy", 
			                           new XElement (ns + "PolicyType", "MS-EAS-Provisioning-WBXML"));
			if ("0" != m_dataSource.ProtocolState.AsPolicyKey) {
				// MUST appear before Status element.
				policy.Add (new XElement (ns + "PolicyKey", m_dataSource.ProtocolState.AsPolicyKey));
			}
			if (DoAck == m_Sm.Action) {
				// FIXME - need to reflect actual status here.
				policy.Add (new XElement (ns + "Status", "1"));
			}
			var doc = AsCommand.ToEmptyXDocument();
			doc.Add (new XElement (ns + "Provision", policy));
			return doc;
		}

		public void DoGet () {
			base.Execute (m_Sm);
		}
		public void DoAck () {
			base.Execute (m_Sm);
		}
		public void DoFinish () {
			m_ParentSm.ProcEvent ((uint)Ev.Success);
		}
		public void DoFail () {
			m_ParentSm.ProcEvent ((uint)Ev.Failure);
		}
	}
}
