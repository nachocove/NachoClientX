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
		private enum StatusProvision : uint {Success=1, ProtocolError=2, ServerError=3};
		private enum StatusPolicy : uint {Success=1, NoPolicy=2, UnknownPolicyType=3, ServerCorrupt=4, WrongPolicyKey=5};
		private enum StatusRemoteWipe : uint {Success=1, Failure=2};

		private StateMachine m_sm;

		public AsProvisionCommand (IAsDataSource dataSource) : base("Provision", "Provision", dataSource) {
			m_sm = new StateMachine () { Name = "as:provision",
				LocalStateType = typeof(Lst),
				TransTable = 
				new[] {
					new Node {State = (uint)St.Start, On = new [] {
							new Trans {Event=(uint)Ev.Launch, Act=DoGet, State=(uint)Lst.GetWait}}},
					new Node {State = (uint)Lst.GetWait, On = new [] {
							new Trans {Event=(uint)Ev.Success, Act=DoAck, State=(uint)Lst.AckWait},
							new Trans {Event=(uint)Ev.HardFail, Act=DoFail, State=(uint)St.Stop}}},
					new Node {State = (uint)Lst.AckWait, On = new [] {
							new Trans {Event=(uint)Ev.Success, Act=DoSucceed, State=(uint)St.Stop},
							new Trans {Event=(uint)Ev.HardFail, Act=DoFail, State=(uint)St.Stop}}}
				}
			};
		}
		public override void Execute(StateMachine sm) {
			m_parentSm = sm;
			m_sm.Start ();
		}
		protected override XDocument ToXDocument () {
			var policy = new XElement (m_ns + "Policy", 
		                            new XElement (m_ns + "PolicyType", "MS-EAS-Provisioning-WBXML"),
		                            new XElement (m_ns + "PolicyKey", m_dataSource.ProtocolState.AsPolicyKey));
			if (DoAck == m_sm.Action) {
				// FIXME - need to reflect actual status here.
				policy.Add (new XElement (m_ns+"Status", "1"));
			}
			var doc = AsCommand.ToEmptyXDocument();
			doc.Add (new XElement (m_ns+"Provision", new XElement (m_ns+"Policies", policy)));
			return doc;
		}
		protected override uint ProcessResponse (HttpResponseMessage response, XDocument doc) {
			switch ((StatusProvision)Convert.ToUInt32 (doc.Root.Element (m_ns+"Status").Value)) {
			case StatusProvision.Success:
				m_dataSource.ProtocolState.AsPolicyKey = doc.Root.Element (m_ns+"Policies").
					Element (m_ns+"Policy").Element (m_ns+"PolicyKey").Value;
				return (uint)Ev.Success;
			case StatusProvision.ProtocolError:
				break;
			case StatusProvision.ServerError:
				break;
			}
			return (uint)Ev.HardFail;
		}
		private void DoGet () {
			base.Execute (m_sm);
		}
		private void DoAck () {
			base.Execute (m_sm);
		}
	}
}
