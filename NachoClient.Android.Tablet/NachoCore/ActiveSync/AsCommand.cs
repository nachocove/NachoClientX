using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Wbxml;
using NachoCore.Utils;

// NOTE: The class that interfaces with HttpClient (or other low-level network API) needs 
// to manage retries & network conditions. If the operation fails "enough", then the
// state machine gets the failure event. There are three classes of failure:
// #1 - unable to perform because of present conditions.
// #2 - unable to perform because of some protocol issue, expected to persist.
namespace NachoCore.ActiveSync {
	abstract public class AsCommand {
		// Constants.
		public const string ContentTypeWbxml = "application/vnd.ms-sync.wbxml";

		// Properties & IVars.
		string m_commandName;
		protected StateMachine m_parentSm;
		protected IAsDataSource m_dataSource;
		CancellationTokenSource m_cts;

		// Class Methods.
		static internal Uri BaseUri(NcServer server) {
			var retval = string.Format ("{0}://{1}:{2}{3}",
			                            server.Scheme, server.Fqdn, server.Port, server.Path);
			return new Uri(retval);
		}

		// Initializer.
		public AsCommand(string commandName, IAsDataSource dataSource) {
			m_commandName = commandName;
			m_dataSource = dataSource;
			m_cts = new CancellationTokenSource();
		}

		// Public Methods.
		public virtual async void Execute(StateMachine sm) {
			if (null == m_parentSm) {
				m_parentSm = sm;
			}
			// FIXME: need to understand URL escaping.
			var requestLine = string.Format ("?Cmd={0}&User={1}&DeviceId={2}&DeviceType={3}",
			                                 m_commandName, 
			                                 m_dataSource.Cred.Username,
			                                 NcDevice.Identity (),
			                                 NcDevice.Type ());
			var rlParams = Params();
			if (null != rlParams) {
				// FIXME: untested.
				var pairs = new List<string>();
				foreach (KeyValuePair<string,string> pair in rlParams) {
					pairs.Add (string.Format ("{0}={1}", pair.Key, pair.Value));
					requestLine = requestLine + '&' + string.Join ("&", pair);
				}
			}
			var handler = new HttpClientHandler () {
				Credentials = new NetworkCredential(m_dataSource.Cred.Username,
				                                    m_dataSource.Cred.Password),
				AllowAutoRedirect = false,
				PreAuthenticate = true
			};
			var client = HttpClientFactory(handler);
			var request = new HttpRequestMessage 
				(HttpMethod.Post, new Uri(AsCommand.BaseUri (m_dataSource.Server), requestLine));
			var doc = ToXDocument ();
			if (null != doc) {
				var wbxml = doc.ToWbxml ();
				var content = new ByteArrayContent (wbxml);
				request.Content = content;
				request.Content.Headers.Add ("Content-Length", wbxml.Length.ToString());
				request.Content.Headers.Add ("Content-Type", ContentTypeWbxml);
			}
			var mime = ToMime ();
			if (null != mime) {
				request.Content = new StringContent (mime, UTF8Encoding.UTF8, "message/rfc822");
			}
			request.Headers.Add ("User-Agent", NcDevice.UserAgent ());
			request.Headers.Add ("X-MS-PolicyKey", m_dataSource.ProtocolState.AsPolicyKey);
			request.Headers.Add ("MS-ASProtocolVersion", m_dataSource.ProtocolState.AsProtocolVersion);
			CancellationToken token = m_cts.Token;
			HttpResponseMessage response = null;

			try {
				response = await client.SendAsync (request, HttpCompletionOption.ResponseContentRead, token);
			}
			catch (OperationCanceledException) {
				Console.WriteLine ("as:command: OperationCanceledException");
				if (! token.IsCancellationRequested) {
					// This is really a timeout.
					sm.ProcEvent ((uint)Ev.TempFail);
				}
				CancelCleanup ();
				return;
			}
			if (HttpStatusCode.OK != response.StatusCode) {
				CancelCleanup ();
			}
			switch (response.StatusCode) {
			case HttpStatusCode.OK:
				if (ContentTypeWbxml ==
				    response.Content.Headers.ContentType.MediaType.ToLower()) {
					byte[] wbxmlMessage = await response.Content.ReadAsByteArrayAsync ();
					var responseDoc = wbxmlMessage.LoadWbxml();
					sm.ProcEvent(ProcessResponse(response, responseDoc));
				} else {
					sm.ProcEvent(ProcessResponse(response));
				}
				break;
			case HttpStatusCode.BadRequest:
			case HttpStatusCode.NotFound:
				sm.ProcEvent((uint)Ev.HardFail);
				break;
			case HttpStatusCode.Unauthorized:
			case HttpStatusCode.Forbidden:
			case HttpStatusCode.InternalServerError:
			case HttpStatusCode.Found:
				if (response.Headers.Contains ("X-MS-RP")) {
					// Per MS-ASHTTP 3.2.5.1, we should look for OPTIONS headers.
					AsOptions.ProcessOptionsHeaders (response.Headers, m_dataSource);
					sm.ProcEvent ((uint)AsProtoControl.Lev.ReSync);
				} else {
					sm.ProcEvent ((uint)AsProtoControl.Lev.ReDisc);
				}
				break;
			case (HttpStatusCode)449:
				sm.ProcEvent ((uint)AsProtoControl.Lev.ReProv);
				break;
			case (HttpStatusCode)451:
				if (response.Headers.Contains ("X-MS-Location")) {
					Uri redirUri;
					try {
						redirUri = new Uri (response.Headers.GetValues ("X-MS-Location").First ());
					} catch {
						sm.ProcEvent ((uint)AsProtoControl.Lev.ReDisc);
						break;
					}
					var server = m_dataSource.Server;
					server.Fqdn = redirUri.Host;
					server.Path = redirUri.AbsolutePath;
					server.Port = redirUri.Port;
					server.Scheme = redirUri.Scheme;
					m_dataSource.Owner.Db.Update (BackEnd.Actors.Proto, m_dataSource.Server);
					sm.ProcEvent ((uint)Ev.Retry);
				}
				break;
			case HttpStatusCode.ServiceUnavailable:
				if (response.Headers.Contains ("Retry-After")) {
					uint seconds = 0;
					try {
						seconds = uint.Parse(response.Headers.GetValues ("Retry-After").First ());
					} catch {}
					if (m_dataSource.Owner.RetryPermissionReq (m_dataSource.Control, seconds)) {
						sm.ProcEvent ((uint)Ev.Retry, seconds);
						break;
					}
				}
				sm.ProcEvent ((uint)Ev.TempFail);
				break;
			case (HttpStatusCode)507:
				m_dataSource.Owner.ServerOOSpaceInd (m_dataSource.Control);
				sm.ProcEvent ((uint)Ev.TempFail);
				break;
			default:
				sm.ProcEvent ((uint)Ev.HardFail);
				break;
			}
		}
		public void Cancel() {
			m_cts.Cancel ();
		}
		// Virtual Methods.
		public virtual Dictionary<string,string> Params () {
			return null;
		}
		// Subclass should implement neither or only one of the ToXxx... methods.
		protected virtual XDocument ToXDocument () {
			return null;
		} 
		protected virtual string ToMime () {
			return null;
		}
		protected virtual uint ProcessResponse (HttpResponseMessage response) {
			return (uint)Ev.Success;
		}
		protected virtual uint ProcessResponse (HttpResponseMessage response, XDocument doc) {
			return (uint)Ev.Success;
		}
		protected virtual void CancelCleanup ( ) {
		}
		protected void DoSucceed () {
			m_parentSm.ProcEvent ((uint)Ev.Success);
		}
		protected void DoFail () {
			m_parentSm.ProcEvent ((uint)Ev.HardFail);
		}
		// Internal Methods.
		internal static XDocument ToEmptyXDocument () {
			return new XDocument (new XDeclaration ("1.0", "utf8", null));
		}
		internal static HttpClient HttpClientFactory (HttpClientHandler handler) {
			return new HttpClient (handler) { Timeout = new TimeSpan (0,0,9) };
		}
	}
}

