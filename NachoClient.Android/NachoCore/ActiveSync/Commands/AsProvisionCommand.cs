using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{

	public class AsProvisionCommand : AsCommand
	{
		public enum Lst : uint {GetWait=(St.Last+1), AckWait};

		private enum StatusProvision : uint {Success=1, ProtocolError=2, ServerError=3};
		private enum StatusPolicy : uint {Success=1, NoPolicy=2, UnknownPolicyType=3, ServerCorrupt=4, WrongPolicyKey=5};
		private enum StatusRemoteWipe : uint {Success=1, Failure=2};

		private StateMachine Sm;
        private AsHttpOperation GetOp, AckOp;

		public AsProvisionCommand (IAsDataSource dataSource) : base("Provision", "Provision", dataSource) {
			Sm = new StateMachine () { Name = "as:provision",
				LocalStateType = typeof(Lst),
				TransTable = new[] {
					new Node {
                        State = (uint)St.Start,
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.HardFail, (uint)SmEvt.E.TempFail},
                        On = new [] {
							new Trans {Event = (uint)SmEvt.E.Launch, Act = DoGet, State = (uint)Lst.GetWait},
                        }},
					new Node {
                        State = (uint)Lst.GetWait,
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoGet, State = (uint)Lst.GetWait},
							new Trans {Event = (uint)SmEvt.E.Success, Act = DoAck, State = (uint)Lst.AckWait},
							new Trans {Event = (uint)SmEvt.E.HardFail, Act = DoHardFail, State = (uint)St.Stop},
                            new Trans {Event = (uint)SmEvt.E.TempFail, Act = DoGet, State = (uint)Lst.GetWait},
                        }},
					new Node {
                        State = (uint)Lst.AckWait,
                        On = new [] {
                            new Trans {Event = (uint)SmEvt.E.Launch, Act = DoAck, State = (uint)Lst.AckWait},
							new Trans {Event = (uint)SmEvt.E.Success, Act = DoSucceed, State = (uint)St.Stop},
							new Trans {Event = (uint)SmEvt.E.HardFail, Act = DoHardFail, State = (uint)St.Stop},
                            new Trans {Event = (uint)SmEvt.E.TempFail, Act = DoAck, State = (uint)Lst.AckWait},
                        }},
				}
			};
            Sm.Validate ();
		}

		public override void Execute(StateMachine sm) {
			OwnerSm = sm;
			Sm.Start ();
		}

        public override XDocument ToXDocument (AsHttpOperation Sender) {
			var policy = new XElement (m_ns + "Policy", 
		                            new XElement (m_ns + "PolicyType", "MS-EAS-Provisioning-WBXML"),
		                            new XElement (m_ns + "PolicyKey", DataSource.ProtocolState.AsPolicyKey));
			if (AckOp == Sender) {
				// FIXME - need to reflect actual status here.
				policy.Add (new XElement (m_ns+"Status", "1"));
			}
			var doc = AsCommand.ToEmptyXDocument();
			doc.Add (new XElement (m_ns+"Provision", new XElement (m_ns+"Policies", policy)));
			return doc;
		}

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc) {
			switch ((StatusProvision)Convert.ToUInt32 (doc.Root.Element (m_ns+"Status").Value)) {
            case StatusProvision.Success:
                NcProtocolState update = DataSource.ProtocolState;
                update.AsPolicyKey = doc.Root.Element (m_ns + "Policies").Element (m_ns + "Policy").Element (m_ns + "PolicyKey").Value;
                DataSource.ProtocolState = update;
                return new Event () { EventCode = (uint)SmEvt.E.Success };
			case StatusProvision.ProtocolError:
				break;
			case StatusProvision.ServerError:
				break;
			}
            return Event.Create ((uint)SmEvt.E.HardFail);
		}

		private void DoGet () {
            if (0 < RetriesLeft) {
                --RetriesLeft;
                base.Execute (Sm, ref GetOp);
            } else {
                Sm.PostEvent ((uint)SmEvt.E.HardFail);
            }
		}

		private void DoAck () {
            if (0 < RetriesLeft) {
                --RetriesLeft;
                base.Execute (Sm, ref AckOp);
            } else {
                Sm.PostEvent ((uint)SmEvt.E.HardFail);
            }
		}
	}
}
