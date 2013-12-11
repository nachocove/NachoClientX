// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using DnDns.Enums;
using DnDns.Query;
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
        public enum HttpOpLst : uint
        {
            HttpWait = (St.Last + 1),
            DelayWait}
        ;

        public class HttpOpEvt : SmEvt
        {
            new public enum E : uint
            {
                Cancel = (SmEvt.E.Last + 1),
                Delay,
                Timeout,
                Final}
            ;
        }
        // Constants.
        private const string ContentTypeHtml = "text/html";
        private const string ContentTypeWbxml = "application/vnd.ms-sync.wbxml";
        private const string ContentTypeWbxmlMultipart = "application/vnd.ms-sync.multipart";
        private const string ContentTypeMail = "message/rfc822";
        private const string ContentTypeXml = "text/xml";
        private const string KXsd = "xsd";
        private const string KCommon = "common";
        private const string KRequest = "request";
        private const string KResponse = "response";
        private const uint KDefaultDelaySeconds = 10;
        private const int KDefaultTimeoutSeconds = 10;
        private const uint KDefaultRetries = 2;
        private static XmlSchemaSet commonXmlSchemas;
        private static Dictionary<string,XmlSchemaSet> requestXmlSchemas;
        private static Dictionary<string,XmlSchemaSet> responseXmlSchemas;
        // IVars. FIXME - make m_commandName private when referenced.
        public string m_commandName;
        private IAsDataSource DataSource;
        private IAsHttpOperationOwner Owner;
        private CancellationTokenSource Cts;
        private Timer DelayTimer;
        private Timer TimeoutTimer;
        private StateMachine HttpOpSm;
        private StateMachine OwnerSm;
        private HttpClient Client;
        private Uri ServerUri;
        private bool ServerUriBeingTested;
        private Stream ContentData;
        private string ContentType;
        // Properties.
        public TimeSpan Timeout { set; get; }
        public uint TriesLeft { set; get; }
        public bool Allow451Follow { set; get; }

        // Initializers.
        public AsHttpOperation (string commandName, IAsHttpOperationOwner owner, IAsDataSource dataSource)
        {
            Timeout = new TimeSpan (0, 0, KDefaultTimeoutSeconds);
            TriesLeft = KDefaultRetries + 1;
            Allow451Follow = true;
            m_commandName = commandName;
            Owner = owner;
            DataSource = dataSource;
            var assetMgr = new NachoPlatform.Assets ();

            HttpOpSm = new StateMachine () {
                Name = "as:http_op",
                LocalEventType = typeof(HttpOpEvt),
                LocalStateType = typeof(HttpOpLst),
                TransTable = new [] {
                    new Node {State = (uint)St.Start,
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.TempFail, (uint)SmEvt.E.HardFail,
                            (uint)HttpOpEvt.E.Cancel, (uint)HttpOpEvt.E.Delay, (uint)HttpOpEvt.E.Timeout, (uint)HttpOpEvt.E.Final
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoHttp, State = (uint)HttpOpLst.HttpWait },
                        }
                    },
                    new Node {State = (uint)HttpOpLst.HttpWait,
                        Invalid = new [] { (uint)SmEvt.E.Success, (uint)SmEvt.E.HardFail },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoHttp, State = (uint)HttpOpLst.HttpWait },
                            new Trans {
                                Event = (uint)SmEvt.E.TempFail,
                                Act = DoHttp,
                                State = (uint)HttpOpLst.HttpWait
                            },
                            new Trans { Event = (uint)HttpOpEvt.E.Cancel, Act = DoCancelHttp, State = (uint)St.Stop },
                            new Trans {
                                Event = (uint)HttpOpEvt.E.Delay,
                                Act = DoDelay,
                                State = (uint)HttpOpLst.DelayWait
                            },
                            new Trans {
                                Event = (uint)HttpOpEvt.E.Timeout,
                                Act = DoTimeoutHttp,
                                State = (uint)HttpOpLst.HttpWait
                            },
                            new Trans { Event = (uint)HttpOpEvt.E.Final, Act = DoFinal, State = (uint)St.Stop },
                        }
                    },
                    new Node {State = (uint)HttpOpLst.DelayWait,
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.TempFail, (uint)SmEvt.E.HardFail,
                            (uint)HttpOpEvt.E.Delay, (uint)HttpOpEvt.E.Timeout, (uint)HttpOpEvt.E.Final
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoHttp, State = (uint)HttpOpLst.HttpWait },
                            new Trans { Event = (uint)HttpOpEvt.E.Cancel, Act = DoCancelDelay, State = (uint)St.Stop },
                        }
                    },
                }
            };

            HttpOpSm.Validate ();

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
        public virtual void Execute (StateMachine sm)
        {
            OwnerSm = sm;
            ServerUri = Owner.ServerUri (this);
            HttpOpSm.PostEvent ((uint)SmEvt.E.Launch);
        }

        public void Cancel ()
        {
            var cancelEvent = Event.Create ((uint)HttpOpEvt.E.Cancel);
            cancelEvent.DropIfStopped = true;
            HttpOpSm.PostEvent (cancelEvent);
        }

        private void DoDelay ()
        {
            DelayTimer = new Timer (DelayTimerCallback, null, Convert.ToInt32 (HttpOpSm.Arg),
                System.Threading.Timeout.Infinite);
        }

        private void DoCancelDelay ()
        {
            DelayTimer.Dispose ();
            DelayTimer = null;
        }

        private void DoHttp ()
        {
            if (0 < TriesLeft) {
                --TriesLeft;
                AttemptHttp ();
            } else {
                OwnerSm.PostEvent ((uint)SmEvt.E.HardFail, null, "Too many retries.");
            }
        }

        private void DoCancelHttp ()
        {
            CancelTimeoutTimer ();
            if (null != Cts) {
                Cts.Cancel ();
            }
            Client = null;
            Cts = null;
        }

        private void DoTimeoutHttp ()
        {
            DoCancelHttp ();
            DoHttp ();
        }

        private void DoFinal ()
        {
            // The Arg is the Event we need to post to the Owner.
            OwnerSm.PostEvent ((Event)HttpOpSm.Arg);
        }

        private void DelayTimerCallback (object State)
        {
            DoCancelDelay ();
            HttpOpSm.PostEvent ((uint)SmEvt.E.Launch);
        }

        private void CancelTimeoutTimer ()
        {
            if (null != TimeoutTimer) {
                TimeoutTimer.Dispose ();
                TimeoutTimer = null;
            }
        }

        private void TimeoutTimerCallback (object State)
        {
            if ((HttpClient)State == Client) {
                HttpOpSm.PostEvent ((uint)HttpOpEvt.E.Timeout, null, string.Format ("Uri: {0}", ServerUri));
            }
        }

        // This method should only be called if the response indicates that the new server is a legit AS server.
        private void IndicateUriIfChanged ()
        {
            if (ServerUriBeingTested) {
                Owner.ServerUriChanged (ServerUri, this);
                ServerUriBeingTested = false;
            }
        }

        // Final is how to pass the ultimate Event back to OwnerSm.
        private Event Final (uint eventCode)
        {
            return Final (eventCode, null, null);
        }

        private Event Final (uint eventCode, object arg, string message)
        {
            return Final (Event.Create (eventCode, arg, message));
        }

        private Event Final (Event endEvent)
        {
            return Event.Create ((uint)HttpOpEvt.E.Final, endEvent, null);
        }

        private async void AttemptHttp ()
        {
            var handler = new HttpClientHandler () {
                AllowAutoRedirect = false,
                PreAuthenticate = true
            };
            if (ServerUri.IsHttps ()) {
                // Never send password over unencrypted channel.
                handler.Credentials = new NetworkCredential (DataSource.Cred.Username, DataSource.Cred.Password);
            }
            Client = new HttpClient (handler) { Timeout = this.Timeout };
            var request = new HttpRequestMessage (Owner.Method (this), ServerUri);
            var doc = Owner.ToXDocument (this);
            if (null != doc) {
                /* Need to test with Mono's Validate - may not be fully implemented.
                if (requestXmlSchemas.ContainsKey (m_commandName)) {
                    doc.Validate (requestXmlSchemas [m_commandName],
                                  (xd, err) => {
                        Console.WriteLine ("{0} failed validation: {1}", m_commandName, err);
                    });
                }
                */
                if (Owner.UseWbxml (this)) {
                    var wbxml = doc.ToWbxml ();
                    var content = new ByteArrayContent (wbxml);
                    request.Content = content;
                    request.Content.Headers.Add ("Content-Length", wbxml.Length.ToString ());
                    request.Content.Headers.Add ("Content-Type", ContentTypeWbxml);
                } else {
                    // See http://stackoverflow.com/questions/957124/how-to-print-xml-version-1-0-using-xdocument.
                    // Xamarin bug: this prints out the wrong decl, which breaks autodiscovery. Revert to SO
                    // Method once bug is fixed.
                    var xmlText = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + doc.ToString ();
                    request.Content = new StringContent (xmlText, UTF8Encoding.UTF8, ContentTypeXml);
                }
            }
            var mime = Owner.ToMime (this);
            if (null != mime) {
                request.Content = new StringContent (mime, UTF8Encoding.UTF8, ContentTypeMail);
            }
            request.Headers.Add ("User-Agent", Device.Instance.UserAgent ());
            if (DataSource.ProtocolState.InitialProvisionCompleted) {
                request.Headers.Add ("X-MS-PolicyKey", DataSource.ProtocolState.AsPolicyKey);
            }
            request.Headers.Add ("MS-ASProtocolVersion", DataSource.ProtocolState.AsProtocolVersion);
            Cts = new CancellationTokenSource ();
            CancellationToken token = Cts.Token;
            HttpResponseMessage response = null;

            // HttpClient doesn't respect Timeout sometimes (DNS and TCP connection establishment for sure).
            // If the instance of HttpClient known to the callback (myClient) doesn't match the IVar, then 
            // assume the HttpClient instance has been abandoned.
            var myClient = Client;
            TimeoutTimer = new Timer (TimeoutTimerCallback, myClient, Timeout, 
                System.Threading.Timeout.InfiniteTimeSpan);
            try {
                response = await myClient.SendAsync (request, HttpCompletionOption.ResponseContentRead, token);
            } catch (OperationCanceledException) {
                if (myClient == Client) {
                    CancelTimeoutTimer ();
                    if (!token.IsCancellationRequested) {
                        HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, null, string.Format ("Timeout, Uri: {0}", ServerUri));
                    }
                }
                return;
            } catch (WebException ex) {
                if (myClient == Client) {
                    CancelTimeoutTimer ();
                    // Some of the causes of WebException could be better characterized as HardFail. Not dividing now.
                    HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, null, string.Format ("WebException: {0}, Uri: {1}", ex.Message, ServerUri));
                }
                return;
            } catch (NullReferenceException ex) {
                // As best I can tell, this may be driven by bug(s) in the Mono stack.
                if (myClient == Client) {
                    CancelTimeoutTimer ();
                    HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, null, string.Format ("NullReferenceException: {0}, Uri: {1}", ex.Message, ServerUri));
                }
                return;
            }

            if (myClient == Client) {
                CancelTimeoutTimer ();
                var contentType = response.Content.Headers.ContentType;
                ContentType = (null == contentType) ? null : contentType.MediaType.ToLower ();
                ContentData = await response.Content.ReadAsStreamAsync ();

                try {
                    HttpOpSm.PostEvent (ProcessHttpResponse (response));
                } catch (Exception ex) {
                    HttpOpSm.PostEvent (Final ((uint)SmEvt.E.HardFail, null, 
                        string.Format ("Exception in ProcessHttpResponse: {0}", ex.Message)));
                }
            }
        }

        private Event ProcessHttpResponse (HttpResponseMessage response)
        {
            Event preProcessEvent = Owner.PreProcessResponse (this, response);
            if (null != preProcessEvent) {
                return Final (preProcessEvent);
            }
            XDocument responseDoc;

            switch (response.StatusCode) {
            case HttpStatusCode.OK:
                IndicateUriIfChanged ();
                if (0 != ContentData.Length) {
                    switch (ContentType) {
                    case ContentTypeWbxml:
                        responseDoc = ContentData.LoadWbxml ();
                        var xmlStatus = responseDoc.Root.ElementAnyNs (Xml.AirSync.Status);
                        if (null != xmlStatus) {
                            var statusEvent = Owner.ProcessTopLevelStatus (this, uint.Parse (xmlStatus.Value));
                            if (null != statusEvent) {
                                Console.WriteLine ("Top-level XML Status {0}:{1}", xmlStatus.Value, statusEvent);
                                return Final (statusEvent);
                            }
                        }
                        return Final (Owner.ProcessResponse (this, response, responseDoc));
                    case ContentTypeWbxmlMultipart:
                        throw new Exception ("FIXME: ContentTypeWbxmlMultipart unimplemented.");
                    case ContentTypeXml:
                        responseDoc = XDocument.Load (ContentData);
                        return Final (Owner.ProcessResponse (this, response, responseDoc));
                    default:
                        if (null == ContentType) {
                            Console.WriteLine ("ProcessHttpResponse: received HTTP response with content but no Content-Type.");
                        }
                        return Final (Owner.ProcessResponse (this, response));
                    }
                } 
                return Final (Owner.ProcessResponse (this, response));

            case HttpStatusCode.BadRequest:
            case HttpStatusCode.NotFound:
                return Final ((uint)SmEvt.E.HardFail, null, "HttpStatusCode.BadRequest or NotFound");

            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
            case HttpStatusCode.InternalServerError:
            case HttpStatusCode.Found:
                // MINOR FIXME - fork on OK/not, and dump this output for any non-OK response too.
                if (ContentTypeHtml == ContentType) {
                    var possibleMessage = new StreamReader (ContentData, Encoding.UTF8).ReadToEnd ();
                    Console.WriteLine ("HTML response: {0}", possibleMessage);
                }
                if (response.Headers.Contains ("X-MS-RP")) {
                    // Per MS-ASHTTP 3.2.5.1, we should look for OPTIONS headers. If they are missing, okay.
                    AsOptionsCommand.ProcessOptionsHeaders (response.Headers, DataSource);
                    IndicateUriIfChanged ();
                    return Final ((uint)AsProtoControl.AsEvt.E.ReSync);
                }
                return Final ((uint)AsProtoControl.AsEvt.E.ReDisc);

            case (HttpStatusCode)449:
                IndicateUriIfChanged ();

                return Final ((uint)AsProtoControl.AsEvt.E.ReProv);

            case (HttpStatusCode)451:
                if (!Allow451Follow) {
                    return Event.Create ((uint)SmEvt.E.TempFail, null, "HttpStatusCode.451 follow not allowed.");
                }
                if (response.Headers.Contains ("X-MS-Location")) {
                    try {
                        // Re-try the access using the new URI. If it works, then accept the new URI.
                        var redirUri = new Uri (response.Headers.GetValues ("X-MS-Location").First ());
                        if (! redirUri.IsHttps()) {
                            // Don't be duped into accepting a non-HTTPS URI.
                            return Final ((uint)AsProtoControl.AsEvt.E.ReDisc);
                        }
                        ServerUriBeingTested = true;
                        NcServer dummy = new NcServer() {
                            Scheme = redirUri.Scheme,
                            Fqdn = redirUri.Host,
                            Port = redirUri.Port,
                            Path = redirUri.AbsolutePath
                        };
                        ServerUri = new Uri (AsCommand.BaseUri (dummy), redirUri.Query);
                        return Event.Create ((uint)SmEvt.E.Launch);
                    } catch {
                        return Final ((uint)AsProtoControl.AsEvt.E.ReDisc);
                    }
                }
                // If no X-MS-Location, just treat it as a transient and hope for the best.
                return Event.Create ((uint)SmEvt.E.TempFail, null, "HttpStatusCode.451 with no X-MS-Location.");

            case HttpStatusCode.BadGateway:
                return Event.Create ((uint)SmEvt.E.TempFail, null, "HttpStatusCode.BadGateway");

            case HttpStatusCode.ServiceUnavailable:
                uint seconds = KDefaultDelaySeconds;
                if (response.Headers.Contains ("Retry-After")) {
                    try {
                        seconds = uint.Parse (response.Headers.GetValues ("Retry-After").First ());
                    } catch {
                        return Event.Create ((uint)HttpOpEvt.E.Delay, seconds, "Could not parse Retry-After value.");
                    }
                    if (DataSource.Owner.RetryPermissionReq (DataSource.Control, seconds)) {
                        return Event.Create ((uint)HttpOpEvt.E.Delay, seconds, "Retry-After");
                    }
                }
                return Event.Create ((uint)HttpOpEvt.E.Delay, seconds, "HttpStatusCode.ServiceUnavailable");

            case (HttpStatusCode)507:
                IndicateUriIfChanged ();
                DataSource.Owner.ServerOOSpaceInd (DataSource.Control);
                return Final ((uint)SmEvt.E.HardFail, null, "HttpStatusCode 507 - Out of space on server.");

            default:
                return Final ((uint)SmEvt.E.HardFail, null, 
                    string.Format ("Unknown HttpStatusCode {0}", response.StatusCode));
            }
        }
    }
}