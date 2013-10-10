using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.ActiveSync
{
	public class AsAutodiscover : IAsCommand
	{
		public enum Lst : uint {S1PostWait=(St.Last+1), S2PostWait, S3GetWait, S4DnsWait, S5PostWait, SubCheck, CredWait};
		public enum Lev : uint {ReStart=(Ev.Last+1), ReDir, GetCred};

		private string m_searchDomain;
		private uint m_redirectDowncounter;
		private Uri m_redirectUri;
		private HttpMethod m_redirectMethod;
		private IAsDataSource m_dataSource;
		private CancellationTokenSource m_cts;
		private StateMachine m_sm;
		private StateMachine m_parentSm;

		public AsAutodiscover (IAsDataSource dataSource)
		{
			m_dataSource = dataSource;
			m_searchDomain = m_dataSource.Account.EmailAddr.Split ('@').Last ();
			m_redirectDowncounter = 10; // NOTE: state external to the state-machine.
			m_redirectUri = null;
			m_redirectMethod = null;
			m_cts = new CancellationTokenSource();
			m_sm = new StateMachine () { Name = "as:autodiscover", 
				LocalEventType = typeof(Lev),
				LocalStateType = typeof(Lst), TransTable = 
				new[] {
					new Node {State = (uint)St.Start, On = new [] {
							new Trans {Event = (uint)Ev.Launch, Act = DoS1Post, State = (uint)Lst.S1PostWait}}},
					new Node {State = (uint)Lst.S1PostWait, On = new [] {
							new Trans {Event = (uint)Ev.Success, Act = DoSucceed, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.HardFail, Act = DoS2Post, State = (uint)Lst.S2PostWait},
							new Trans {Event = (uint)Lev.GetCred, Act = DoGetCred, State = (uint)Lst.CredWait},
							new Trans {Event = (uint)Lev.ReDir, Act = DoS5Post, State = (uint)Lst.S5PostWait},
							new Trans {Event = (uint)Lev.ReStart, Act = DoS1Post, State = (uint)Lst.S1PostWait}}},
					new Node {State = (uint)Lst.S2PostWait, On = new [] {
							new Trans {Event = (uint)Ev.Success, Act = DoSucceed, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.HardFail, Act = DoS3Get, State = (uint)Lst.S3GetWait},
							new Trans {Event = (uint)Lev.ReDir, Act = DoS5Post, State = (uint)Lst.S5PostWait},
							new Trans {Event = (uint)Lev.ReStart, Act = DoS1Post, State = (uint)Lst.S1PostWait}}},
					new Node {State = (uint)Lst.S3GetWait, On = new [] {
							new Trans {Event = (uint)Ev.HardFail, Act = DoS4Dns, State = (uint)Lst.S4DnsWait},
							new Trans {Event = (uint)Lev.ReDir, Act = DoS5Post, State = (uint)Lst.S5PostWait}}},
					new Node {State = (uint)Lst.S4DnsWait, On = new [] {
							new Trans {Event = (uint)Ev.HardFail, Act = DoSubCheck, State = (uint)Lst.SubCheck},
							new Trans {Event = (uint)Lev.ReDir, Act = DoS5Post, State = (uint)Lst.S5PostWait}}},
					new Node {State = (uint)Lst.S5PostWait, On = new [] {
							new Trans {Event = (uint)Ev.Success, Act = DoSucceed, State = (uint)St.Stop},
							new Trans {Event = (uint)Ev.HardFail, Act = DoSubCheck, State = (uint)Lst.SubCheck},
							new Trans {Event = (uint)Lev.ReDir, Act = DoS5Post, State = (uint)Lst.S5PostWait},
							new Trans {Event = (uint)Lev.ReStart, Act = DoS1Post, State = (uint)Lst.S1PostWait}}},
					new Node {State = (uint)Lst.SubCheck, On = new [] {
							new Trans {Event = (uint)Ev.HardFail, Act = DoFail, State = (uint)St.Stop},
							new Trans {Event = (uint)Lev.ReStart, Act = DoS1Post, State = (uint)Lst.S1PostWait}}},
					new Node {State = (uint)Lst.CredWait, On = new [] {
							new Trans {Event = (uint)Ev.Success, Act = DoS1Post, State = (uint)Lst.S1PostWait},
							new Trans {Event = (uint)Ev.HardFail, Act = DoFail, State = (uint)St.Stop}}}
				}
			};
		}
		public void Execute(StateMachine sm) {
			m_parentSm = sm;
			m_sm.Start ();
		}
		public void Cancel () {
			m_cts.Cancel ();
		}
		private void DoGetCred () {
		}
		private void DoS1Post () {
			// FIXME. Clear out existing DB state server.{Scheme, Fqdn, Port}.
			ExecuteHttp (new Uri (string.Format ("https://{0}/autodiscover/autodiscover.xml",
			                                     m_searchDomain)), HttpMethod.Post);
		}
		private void DoS2Post () {
			ExecuteHttp (new Uri (string.Format ("https://autodiscover.{0}/autodiscover/autodiscover.xml",
			                                     m_searchDomain)), HttpMethod.Post);
		}
		private void DoS3Get () {
			ExecuteHttp (new Uri (string.Format ("http://autodiscover.{0}/autodiscover/autodiscover.xml",
			                                     m_searchDomain)), HttpMethod.Get);
		}
		private void DoS4Dns () {
			// FIXME - we should be looking up the SRV record here.
			m_sm.PostEvent ((uint)Ev.HardFail);
		}
		private void DoS5Post () {
			if (0 < m_redirectDowncounter && m_redirectUri.IsHttps ()) {
				ExecuteHttp (ComputeRedirUri (), m_redirectMethod);
			} else {
				m_sm.PostEvent ((uint)Ev.HardFail);
			}
		}
		private void DoSubCheck () {
			// FIXME. Need iOS and Android C libs support for RegDom check API.
			// Compute baseDomain from m_searchDomain - the copy is a crowbar. 
			string baseDomain = m_searchDomain;
			if (baseDomain != m_searchDomain) {
				m_searchDomain = baseDomain;
				m_sm.PostEvent ((uint)Lev.ReStart);
			} else {
				m_sm.PostEvent ((uint)Ev.HardFail);
			}
		}
		private void DoSucceed () {
			m_parentSm.PostEvent ((uint)Ev.Success);
		}
		private void DoFail () {
			m_parentSm.PostEvent ((uint)Ev.HardFail);
		}
		private XDocument ToXDocument () {
			var doc = AsCommand.ToEmptyXDocument ();
			XNamespace ns = "http://schemas.microsoft.com/exchange/autodiscover/mobilesync/requestschema/2006";
			doc.Add (new XElement(ns + "Autodiscover",
			             new XElement (ns + "Request",
			                 new XElement (ns + "EMailAddress", m_dataSource.Account.EmailAddr),
			                 new XElement (ns + "AcceptableResponseSchema", 
			             "http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006"))));
			return doc;
		}
		private async void ExecuteHttp(Uri uri, HttpMethod method) {
			var handler = new HttpClientHandler () {AllowAutoRedirect = false};
			if (uri.IsHttps ()) {
				handler.Credentials = new NetworkCredential (m_dataSource.Cred.Username,
				                                            m_dataSource.Cred.Password);
			}
			var client = AsCommand.HttpClientFactory (handler);
			var request = new HttpRequestMessage (method, uri);

			if (HttpMethod.Post == method) {
				var content = ToXDocument ().ToString ();
				request.Content = new StringContent (content, UTF8Encoding.UTF8, "text/xml");
			}
            request.Headers.Add ("User-Agent", Device.Instance.UserAgent ());
			CancellationToken token = m_cts.Token;
			HttpResponseMessage response = null;
			// FIXME - need to handle untrusted server cert gracefully. THIS VOIDS THE CHECK!
			ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, ssl) =>  true;
			try {
				Console.WriteLine("as:autodiscover: SendAsync");
				response = await client.SendAsync (request, HttpCompletionOption.ResponseContentRead,
				                                       token);
				Console.WriteLine("as:autodiscover: SendAsync response");
			}
			catch (OperationCanceledException) {
				/* Not an error, do nothing. */
				Console.WriteLine ("as:autodiscover: OperationCanceledException");
				if (! token.IsCancellationRequested) {
					// This is really a timeout (MS bug).
					m_sm.PostEvent ((uint)Ev.HardFail);
				}
				return;
			}
			catch (WebException ex) {
				Console.WriteLine ("as:autodiscover: WebException");
				switch (ex.Status) {
				case WebExceptionStatus.NameResolutionFailure:
					m_sm.PostEvent ((uint)Ev.HardFail);
					return;
				default:
					throw ex;
				}
			}
			switch (response.StatusCode) {
				case HttpStatusCode.OK:
				m_sm.PostEvent ((uint)Ev.Success);
				break;
				case HttpStatusCode.Unauthorized:
				m_sm.PostEvent ((uint)Lev.GetCred);
				break;
				case HttpStatusCode.Found:
				m_sm.PostEvent ((uint)Lev.ReDir);
				break;
				default:
				// NOTE: we should add more sophistication here.
				m_sm.PostEvent ((uint)Ev.HardFail);
				break;
			} 
		}
		private void ExecuteDns (string fullname) {
			// FIXME will be async.
		}
		private Uri ComputeRedirUri () { // FIXME - maybe we nix this?
			return new Uri (string.Format ("https://{0}/autodiscover/autodiscover.xml",
			                             m_searchDomain));
		}
	}
}

