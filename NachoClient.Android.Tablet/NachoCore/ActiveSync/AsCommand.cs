using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using NachoCore.Model;
using NachoCore.Wbxml;
using NachoCore.Utils;

// NOTE: The class that interfaces with HttpClient (or other low-level network API) needs 
// to manage retries & network conditions. If the operation fails "enough", then the
// state machine gets the failure event. There are three classes of failure:
// #1 - unable to perform because of present conditions.
// #2 - unable to perform because of some protocol issue, expected to persist.
namespace NachoCore.ActiveSync {
	abstract public class AsCommand : IAsCommand {
		// Constants.
		private const string ContentTypeWbxml = "application/vnd.ms-sync.wbxml";
		private const string ContentTypeWbxmlMultipart = "application/vnd.ms-sync.multipart";
		private const string ContentTypeMail = "message/rfc822";
		private const string KXsd = "xsd";
		private const string KCommon = "common";
		private const string KRequest = "request";
		private const string KResponse = "response";

		private static XmlSchemaSet commonXmlSchemas;
		private static Dictionary<string,XmlSchemaSet> requestXmlSchemas;
		private static Dictionary<string,XmlSchemaSet> responseXmlSchemas;

		// Properties & IVars.
		protected string m_commandName;
		protected XNamespace m_ns;
		protected XNamespace m_baseNs = Xml.AirSyncBase.Ns;
		protected StateMachine m_parentSm;
		protected IAsDataSource m_dataSource;
		CancellationTokenSource m_cts;

		public TimeSpan Timeout { set; get; }
		public HttpMethod Method { set; get; }

		// Initializers.
		public AsCommand (string commandName, string nsName, IAsDataSource dataSource) :
			this (commandName, dataSource) {
			m_ns = nsName;
		}

		public AsCommand (string commandName, IAsDataSource dataSource) {
			Method = HttpMethod.Post;
			Timeout = TimeSpan.Zero;
			m_commandName = commandName;
			m_dataSource = dataSource;
			m_cts = new CancellationTokenSource();
			var assetMgr = new NachoPlatform.Assets ();
			if (null == commonXmlSchemas) {
				commonXmlSchemas = new XmlSchemaSet ();
				foreach (var xsdFile in assetMgr.List (Path.Combine(KXsd, KCommon))) {
					commonXmlSchemas.Add (null, new XmlTextReader (assetMgr.Open (xsdFile)));
				}
			}
			if (null == requestXmlSchemas) {
				requestXmlSchemas = new Dictionary<string, XmlSchemaSet> ();
				foreach (var xsdRequest in assetMgr.List (Path.Combine(KXsd, KRequest))) {
					var requestSchema = new XmlSchemaSet ();
					requestSchema.Add (null, new XmlTextReader (assetMgr.Open (xsdRequest)));
					requestXmlSchemas [Path.GetFileNameWithoutExtension (xsdRequest)] = requestSchema;
				}
			}
			if (null == responseXmlSchemas) {
				responseXmlSchemas = new Dictionary<string, XmlSchemaSet> ();
				foreach (var xsdResponse in assetMgr.List (Path.Combine(KXsd, KResponse))) {
					var requestSchema = new XmlSchemaSet ();
					requestSchema.Add (null, new XmlTextReader (assetMgr.Open (xsdResponse)));
					responseXmlSchemas [Path.GetFileNameWithoutExtension (xsdResponse)] = requestSchema;
				}
			}
		}

		// Public Methods.
		public virtual async void Execute(StateMachine sm) {
			if (null == m_parentSm) {
				m_parentSm = sm;
			}
			// FIXME: need to fully understand URL escaping in .NET.
			var requestLine = QueryString ();
			var rlParams = ExtraQueryStringParams ();
			if (null != rlParams) {
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
			var client = HttpClientFactory (handler);
			if (TimeSpan.Zero != Timeout) {
				client.Timeout = Timeout;
			}
			var request = new HttpRequestMessage (Method, new Uri(AsCommand.BaseUri (m_dataSource.Server), requestLine));
			var doc = ToXDocument ();
			if (null != doc) {
				/* WAIT on Xamarin support. Can't find assembly with Validate
				if (requestXmlSchemas.ContainsKey (m_commandName)) {
					doc.Validate (requestXmlSchemas [m_commandName],
					              (xd, err) => {
						Console.WriteLine ("{0} failed validation: {1}", m_commandName, err);
					});
				}
				*/
				var wbxml = doc.ToWbxml ();
				var content = new ByteArrayContent (wbxml);
				request.Content = content;
				request.Content.Headers.Add ("Content-Length", wbxml.Length.ToString());
				request.Content.Headers.Add ("Content-Type", ContentTypeWbxml);
			}
			var mime = ToMime ();
			if (null != mime) {
				request.Content = new StringContent (mime, UTF8Encoding.UTF8, ContentTypeMail);
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
				CancelCleanup ();
				if (! token.IsCancellationRequested) {
					// This is how MS' HttpClient presents a timeout.
					sm.PostEvent ((uint)Ev.TempFail);
				}
				return;
			}
			catch (WebException) {
				// FIXME - look at all the causes of this, and figure out right-thing-to-do in each case.
				Console.WriteLine ("as:command: WebException");
				CancelCleanup ();
				sm.PostEvent ((uint)Ev.TempFail);
				return;
			}
			if (HttpStatusCode.OK != response.StatusCode) {
				CancelCleanup ();
			}
			switch (response.StatusCode) {
			case HttpStatusCode.OK:
				switch (response.Content.Headers.ContentType.MediaType.ToLower()) {
				case ContentTypeWbxml:
					byte[] wbxmlMessage = await response.Content.ReadAsByteArrayAsync ();
					var responseDoc = wbxmlMessage.LoadWbxml ();
					var xmlStatus = responseDoc.Root.Element (m_ns + Xml.AirSync.Status);
					if (null != xmlStatus) {
						var statusEvent = TopLevelStatusToEvent (uint.Parse (xmlStatus.Value));
						Console.WriteLine ("STATUS {0}:{1}", xmlStatus.Value, statusEvent);
					}
					sm.PostEvent (ProcessResponse (response, responseDoc));
					break;
				case ContentTypeWbxmlMultipart:
					// FIXME.
					throw new Exception ();
				default:
					sm.PostEvent(ProcessResponse(response));
					break;
				}
				break;
			case HttpStatusCode.BadRequest:
			case HttpStatusCode.NotFound:
				sm.PostEvent((uint)Ev.HardFail);
				break;
			case HttpStatusCode.Unauthorized:
			case HttpStatusCode.Forbidden:
			case HttpStatusCode.InternalServerError:
			case HttpStatusCode.Found:
				if (response.Headers.Contains ("X-MS-RP")) {
					// Per MS-ASHTTP 3.2.5.1, we should look for OPTIONS headers. If they are missing, okay.
					AsOptionsCommand.ProcessOptionsHeaders (response.Headers, m_dataSource);
					sm.PostEvent ((uint)AsProtoControl.Lev.ReSync);
				} else {
					sm.PostEvent ((uint)AsProtoControl.Lev.ReDisc);
				}
				break;
			case (HttpStatusCode)449:
				sm.PostEvent ((uint)AsProtoControl.Lev.ReProv);
				break;
			case (HttpStatusCode)451:
				if (response.Headers.Contains ("X-MS-Location")) {
					Uri redirUri;
					try {
						redirUri = new Uri (response.Headers.GetValues ("X-MS-Location").First ());
					} catch {
						sm.PostEvent ((uint)AsProtoControl.Lev.ReDisc);
						break;
					}
					var server = m_dataSource.Server;
					server.Fqdn = redirUri.Host;
					server.Path = redirUri.AbsolutePath;
					server.Port = redirUri.Port;
					server.Scheme = redirUri.Scheme;
					m_dataSource.Owner.Db.Update (BackEnd.DbActors.Proto, m_dataSource.Server);
					sm.PostEvent ((uint)Ev.Launch);
				}
				break;
			case HttpStatusCode.ServiceUnavailable:
				if (response.Headers.Contains ("Retry-After")) {
					uint seconds = 0;
					try {
						seconds = uint.Parse(response.Headers.GetValues ("Retry-After").First ());
					} catch {}
					if (m_dataSource.Owner.RetryPermissionReq (m_dataSource.Control, seconds)) {
						sm.PostEvent ((uint)Ev.Launch, seconds); // FIXME - PostDelayedEvent.
						break;
					}
				}
				sm.PostEvent ((uint)Ev.TempFail);
				break;
			case (HttpStatusCode)507:
				m_dataSource.Owner.ServerOOSpaceInd (m_dataSource.Control);
				sm.PostEvent ((uint)Ev.TempFail);
				break;
			default:
				sm.PostEvent ((uint)Ev.HardFail);
				break;
			}
		}
		public void Cancel() {
			m_cts.Cancel ();
		}
		// Virtual Methods.
		// Override if the subclass wants to add more parameters to the query string.
		protected virtual Dictionary<string,string> ExtraQueryStringParams () {
			return null;
		}
		// Override if the subclass wants total control over the query string.
		protected virtual string QueryString () {
			return string.Format ("?Cmd={0}&User={1}&DeviceId={2}&DeviceType={3}",
			                      m_commandName, 
			                      m_dataSource.Cred.Username,
			                      NcDevice.Identity (),
			                      NcDevice.Type ());
		}
		// The subclass should for any given instatiation only return non-null from ToXDocument XOR ToMime.
		protected virtual XDocument ToXDocument () {
			return null;
		} 
		protected virtual string ToMime () {
			return null;
		}
		// Called for non-WBXML HTTP 200 responses.
		protected virtual uint ProcessResponse (HttpResponseMessage response) {
			return (uint)Ev.Success;
		}
		protected virtual uint ProcessResponse (HttpResponseMessage response, XDocument doc) {
			return (uint)Ev.Success;
		}
		// Subclass can cleanup in the case where a ProcessResponse will never be called.
		protected virtual void CancelCleanup ( ) {
		}
		// Subclass can override and add specialized support for top-level status codes as needed.
		// Subclass must call base if it does not handle the status code itself.
		protected virtual int TopLevelStatusToEvent (uint status) {
			// returning -1 means that this function did not know how to convert the status value.
			// NOTE(A): Subclass can possibly make this a TempFail or Success if the id issue is just a sync issue.
			// NOTE(B): Subclass can retry with a formatting simplification.
			// NOTE(C): Subclass MUST catch & handle this code.
			// FIXME - package enough telemetry information so that we can fix our bugs.
			// FIXME - catch TempFail loops and convert to HardFail.
			// FIXME(A): MUST provide user with information about how to possibly rectify.
			switch ((Xml.StatusCode)status) {
			case Xml.StatusCode.InvalidContent:
			case Xml.StatusCode.InvalidWBXML:
			case Xml.StatusCode.InvalidXML:
				return (int)Ev.HardFail;

			case Xml.StatusCode.InvalidDateTime: // Maybe the next time generated may parse okay.
				return (int)Ev.TempFail;

			case Xml.StatusCode.InvalidCombinationOfIDs: // NOTE(A).
			case Xml.StatusCode.InvalidMIME: // NOTE(B).
			case Xml.StatusCode.DeviceIdMissingOrInvalid:
			case Xml.StatusCode.DeviceTypeMissingOrInvalid:
			case Xml.StatusCode.ServerError:
				return (int)Ev.HardFail;

			case Xml.StatusCode.ServerErrorRetryLater:
				return (int)Ev.TempFail;

			case Xml.StatusCode.ActiveDirectoryAccessDenied: // FIXME(A).
			case Xml.StatusCode.MailboxQuotaExceeded: // FIXME(A).
			case Xml.StatusCode.MailboxServerOffline: // FIXME(A).
			case Xml.StatusCode.SendQuotaExceeded: // NOTE(C).
			case Xml.StatusCode.MessageRecipientUnresolved: // NOTE(C).
			case Xml.StatusCode.MessageReplyNotAllowed: // NOTE(C).
			case Xml.StatusCode.MessagePreviouslySent:
			case Xml.StatusCode.MessageHasNoRecipient: // NOTE(C).
			case Xml.StatusCode.MailSubmissionFailed:
			case Xml.StatusCode.MessageReplyFailed:
			case Xml.StatusCode.UserHasNoMailbox: // FIXME(A).
			case Xml.StatusCode.UserCannotBeAnonymous: // FIXME(A).
			case Xml.StatusCode.UserPrincipalCouldNotBeFound: // FIXME(A).
				return (int)Ev.HardFail;
				// Meh. do some cases end-to-end, with user messaging (before all this typing).
			}
			return -1;
		}
		protected void DoSucceed () {
			m_parentSm.PostEvent ((uint)Ev.Success);
		}
		protected void DoFail () {
			m_parentSm.PostEvent ((uint)Ev.HardFail);
		}
		// Static internal helper methods.
		static internal XDocument ToEmptyXDocument () {
			return new XDocument (new XDeclaration ("1.0", "utf8", null));
		}
		static internal HttpClient HttpClientFactory (HttpClientHandler handler) {
			return new HttpClient (handler) { Timeout = new TimeSpan (0,0,9) };
		}
		static internal Uri BaseUri(NcServer server) {
			var retval = string.Format ("{0}://{1}:{2}{3}",
			                            server.Scheme, server.Fqdn, server.Port, server.Path);
			return new Uri(retval);
		}
	}
}

