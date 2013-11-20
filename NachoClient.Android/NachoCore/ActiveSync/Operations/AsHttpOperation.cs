// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
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
using NachoPlatform;

namespace NachoCore.ActiveSync
{
    public class AsHttpOperation : IAsOperation
    {
        // Constants.
        private const string ContentTypeWbxml = "application/vnd.ms-sync.wbxml";
        private const string ContentTypeWbxmlMultipart = "application/vnd.ms-sync.multipart";
        private const string ContentTypeMail = "message/rfc822";
        private const string ContentTypeXml = "text/xml";
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
        protected IAsDataSource m_dataSource;
        protected IAsHttpOperationOwner m_owner;
        protected CancellationTokenSource m_cts;

        public TimeSpan Timeout { set; get; }
        // Initializers.
        public AsHttpOperation (string commandName, string nsName, IAsHttpOperationOwner owner, IAsDataSource dataSource) :
            this (commandName, owner, dataSource)
        {
            m_ns = nsName;
        }

        public AsHttpOperation (string commandName, IAsHttpOperationOwner owner, IAsDataSource dataSource)
        {
            Timeout = TimeSpan.Zero;
            m_commandName = commandName;
            m_owner = owner;
            m_dataSource = dataSource;
            m_cts = new CancellationTokenSource ();
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
        public virtual async void Execute (StateMachine sm)
        {
            var uri = m_owner.ServerUriCandidate (this);
            var handler = new HttpClientHandler () {
                AllowAutoRedirect = false,
                PreAuthenticate = true
            };
            if (uri.IsHttps ()) {
                // Never send password over unencrypted channel.
                handler.Credentials = new NetworkCredential (m_dataSource.Cred.Username, m_dataSource.Cred.Password);
            }
            var client = HttpClientFactory (handler);
            if (TimeSpan.Zero != Timeout) {
                client.Timeout = Timeout;
            }
            // FIXME. Need to refer to delegate for URI.
            var request = new HttpRequestMessage (m_owner.Method (this), uri);
            var doc = m_owner.ToXDocument (this);
            if (null != doc) {
                /* WAIT on Xamarin support. Can't find assembly with Validate
                if (requestXmlSchemas.ContainsKey (m_commandName)) {
                    doc.Validate (requestXmlSchemas [m_commandName],
                                  (xd, err) => {
                        Console.WriteLine ("{0} failed validation: {1}", m_commandName, err);
                    });
                }
                */
                if (m_owner.UseWbxml (this)) {
                    var wbxml = doc.ToWbxml ();
                    var content = new ByteArrayContent (wbxml);
                    request.Content = content;
                    request.Content.Headers.Add ("Content-Length", wbxml.Length.ToString ());
                    request.Content.Headers.Add ("Content-Type", ContentTypeWbxml);
                } else {
                    request.Content = new StringContent (doc.ToString (), UTF8Encoding.UTF8, ContentTypeXml);
                }
            }
            var mime = m_owner.ToMime (this);
            if (null != mime) {
                request.Content = new StringContent (mime, UTF8Encoding.UTF8, ContentTypeMail);
            }
            request.Headers.Add ("User-Agent", Device.Instance.UserAgent ());
            request.Headers.Add ("X-MS-PolicyKey", m_dataSource.ProtocolState.AsPolicyKey);
            request.Headers.Add ("MS-ASProtocolVersion", m_dataSource.ProtocolState.AsProtocolVersion);
            CancellationToken token = m_cts.Token;
            HttpResponseMessage response = null;

            try {
                response = await client.SendAsync (request, HttpCompletionOption.ResponseContentRead, token);
            } catch (OperationCanceledException) {
                Console.WriteLine ("as:command: OperationCanceledException");
                m_owner.CancelCleanup (this);
                if (!token.IsCancellationRequested) {
                    sm.PostEvent ((uint)SmEvt.E.TempFail, null, "Timeout");
                }
                return;
            } catch (WebException ex) {
                // FIXME - look at all the causes of this, and figure out right-thing-to-do in each case.
                Console.WriteLine ("as:command: WebException");
                m_owner.CancelCleanup (this);
                sm.PostEvent ((uint)SmEvt.E.TempFail, null, string.Format ("WebException: {0}", ex.Message));
                return;
            }
            if (HttpStatusCode.OK != response.StatusCode) {
                m_owner.CancelCleanup (this);
            }
            Event preProcessEvent = m_owner.PreProcessResponse (this, response);
            if (null != preProcessEvent) {
                sm.PostEvent (preProcessEvent);
                return;
            }
            XDocument responseDoc;
            switch (response.StatusCode) {
            case HttpStatusCode.OK:
                switch (response.Content.Headers.ContentType.MediaType.ToLower ()) {
                case ContentTypeWbxml:
                    byte[] wbxmlMessage = await response.Content.ReadAsByteArrayAsync ();
                    responseDoc = wbxmlMessage.LoadWbxml ();
                    var xmlStatus = responseDoc.Root.Element (m_ns + Xml.AirSync.Status);
                    if (null != xmlStatus) {
                        var statusEvent = m_owner.TopLevelStatusToEvent (this, uint.Parse (xmlStatus.Value));
                        // FIXME - need to use the event generated!
                        Console.WriteLine ("STATUS {0}:{1}", xmlStatus.Value, statusEvent);
                    }
                    sm.PostEvent (m_owner.ProcessResponse (this, response, responseDoc));
                    break;
                case ContentTypeWbxmlMultipart:
                    throw new Exception ("FIXME: ContentTypeWbxmlMultipart unimplemented.");
                case ContentTypeXml:
                    var xmlMessage = await response.Content.ReadAsStreamAsync ();
                    responseDoc = XDocument.Load (xmlMessage);
                    sm.PostEvent (m_owner.ProcessResponse (this, response, responseDoc));
                    break;
                default:
                    sm.PostEvent (m_owner.ProcessResponse (this, response));
                    break;
                }
                break;
            case HttpStatusCode.BadRequest:
            case HttpStatusCode.NotFound:
                sm.PostEvent ((uint)SmEvt.E.HardFail, null, "HttpStatusCode.BadRequest or NotFound");
                break;
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
            case HttpStatusCode.InternalServerError:
            case HttpStatusCode.Found:
                if (response.Headers.Contains ("X-MS-RP")) {
                    // Per MS-ASHTTP 3.2.5.1, we should look for OPTIONS headers. If they are missing, okay.
                    AsOptionsCommand.ProcessOptionsHeaders (response.Headers, m_dataSource);
                    sm.PostEvent ((uint)AsProtoControl.AsEvt.E.ReSync);
                } else {
                    sm.PostEvent ((uint)AsProtoControl.AsEvt.E.ReDisc);
                }
                break;
            case (HttpStatusCode)449:
                sm.PostEvent ((uint)AsProtoControl.AsEvt.E.ReProv);
                break;
            case (HttpStatusCode)451:
                if (response.Headers.Contains ("X-MS-Location")) {
                    Uri redirUri;
                    try {
                        redirUri = new Uri (response.Headers.GetValues ("X-MS-Location").First ());
                    } catch {
                        sm.PostEvent ((uint)AsProtoControl.AsEvt.E.ReDisc);
                        break;
                    }
                    // FIXME - anyone else need to know?
                    var server = m_dataSource.Server;
                    server.Fqdn = redirUri.Host;
                    server.Path = redirUri.AbsolutePath;
                    server.Port = redirUri.Port;
                    server.Scheme = redirUri.Scheme;
                    m_dataSource.Owner.Db.Update (BackEnd.DbActors.Proto, m_dataSource.Server);
                    sm.PostEvent ((uint)SmEvt.E.Launch);
                }
                // FIXME - what to do when no X-MS-Location?
                break;
            case HttpStatusCode.ServiceUnavailable:
                if (response.Headers.Contains ("Retry-After")) {
                    uint seconds = 0;
                    try {
                        seconds = uint.Parse (response.Headers.GetValues ("Retry-After").First ());
                    } catch {
                    }
                    if (m_dataSource.Owner.RetryPermissionReq (m_dataSource.Control, seconds)) {
                        sm.PostEvent ((uint)SmEvt.E.Launch, seconds, "Retry-After"); // FIXME - PostDelayedEvent.
                        break;
                    }
                }
                sm.PostEvent ((uint)SmEvt.E.TempFail, null, "HttpStatusCode.ServiceUnavailable");
                break;
            case (HttpStatusCode)507:
                m_dataSource.Owner.ServerOOSpaceInd (m_dataSource.Control);
                sm.PostEvent ((uint)SmEvt.E.TempFail, null, "HttpStatusCode 507");
                break;
            default:
                sm.PostEvent ((uint)SmEvt.E.HardFail, null, 
                    string.Format ("Unknown HttpStatusCode {0}", response.StatusCode));
                break;
            }
        }

        public void Cancel ()
        {
            m_cts.Cancel ();
        }
        // Static internal helper methods.
        static internal HttpClient HttpClientFactory (HttpClientHandler handler)
        {
            return new HttpClient (handler) { Timeout = new TimeSpan (0, 0, 9) };
        }
    }
}

