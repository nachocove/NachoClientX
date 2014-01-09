// # Copyright (C) 2013, 2014 Nacho Cove, Inc. All rights reserved.
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
        private const uint KDefaultRetries = 15;
        // IVars. FIXME - make m_commandName private when referenced.
        public string m_commandName;
        private IAsDataSource DataSource;
        private IAsHttpOperationOwner Owner;
        private CancellationTokenSource Cts;
        private NachoTimer DelayTimer;
        private NachoTimer TimeoutTimer;
        // These DisposedXxx are used to avoid eliminating a reference while still in a callback.
        #pragma warning disable 414
        private NachoTimer DisposedDelayTimer;
        private NachoTimer DisposedTimeoutTimer;
        #pragma warning restore 414
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

        public string Token { set; get; }

        // Initializers.
        public AsHttpOperation (string commandName, IAsHttpOperationOwner owner, IAsDataSource dataSource)
        {
            Timeout = new TimeSpan (0, 0, KDefaultTimeoutSeconds);
            TriesLeft = KDefaultRetries + 1;
            Allow451Follow = true;
            m_commandName = commandName;
            Owner = owner;
            DataSource = dataSource;

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
                            new Trans { Event = (uint)HttpOpEvt.E.Cancel, Act = DoCancelDelayTimer, State = (uint)St.Stop },
                        }
                    },
                }
            };

            HttpOpSm.Validate ();
        }
        // Public Methods.
        public virtual void Execute (StateMachine sm)
        {
            OwnerSm = sm;
            HttpOpSm.Name = OwnerSm.Name + ":HTTPOP";
            ServerUri = Owner.ServerUri (this);
            HttpOpSm.PostEvent ((uint)SmEvt.E.Launch, "HTTPOPEXE");
        }

        public void Cancel ()
        {
            var cancelEvent = Event.Create ((uint)HttpOpEvt.E.Cancel, "HTTPOPCANCEL");
            cancelEvent.DropIfStopped = true;
            HttpOpSm.PostEvent (cancelEvent);
        }

        private void DoDelay ()
        {
            DelayTimer = new NachoTimer (DelayTimerCallback, null, Convert.ToInt32 (HttpOpSm.Arg),
                System.Threading.Timeout.Infinite);
        }

        private void DoCancelDelayTimer ()
        {
            if (null != DelayTimer) {
                DelayTimer.Dispose ();
                DisposedDelayTimer = DelayTimer;
                DelayTimer = null;
            }
        }

        private void DoHttp ()
        {
            if (0 < TriesLeft) {
                --TriesLeft;
                Console.WriteLine ("ASHTTPOP: TriesLeft: {0}", TriesLeft);
                AttemptHttp ();
            } else {
                HttpOpSm.PostEvent (Final ((uint)SmEvt.E.HardFail, "ASHTTPDOH", null, "Too many retries."));
            }
        }

        private void DoCancelHttp ()
        {
            CancelTimeoutTimer ();
            DoCancelDelayTimer ();
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
            DoCancelDelayTimer ();
            HttpOpSm.PostEvent ((uint)SmEvt.E.Launch, "ASHTTPDTC");
        }

        private void CancelTimeoutTimer ()
        {
            if (null != TimeoutTimer) {
                TimeoutTimer.Dispose ();
                DisposedTimeoutTimer = TimeoutTimer;
                TimeoutTimer = null;
            }
        }

        private void TimeoutTimerCallback (object State)
        {
            if ((HttpClient)State == Client) {
                HttpOpSm.PostEvent ((uint)HttpOpEvt.E.Timeout, "ASHTTPTTC", null, string.Format ("Uri: {0}", ServerUri));
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
        private Event Final (uint eventCode, string mnemonic)
        {
            return Final (eventCode, mnemonic, null, null);
        }

        private Event Final (uint eventCode, string mnemonic, object arg, string message)
        {
            return Final (Event.Create (eventCode, mnemonic, arg, message));
        }

        private Event Final (Event endEvent)
        {
            return Event.Create ((uint)HttpOpEvt.E.Final, "HTTPOPFIN", endEvent, null);
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
                // Sadly, Xamarin does not support schema-based XML validation APIs.
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
            TimeoutTimer = new NachoTimer (TimeoutTimerCallback, myClient, Timeout, 
                System.Threading.Timeout.InfiniteTimeSpan);
            try {
                Console.WriteLine("HTTPOP:URL:{0}", request.RequestUri.ToString());
                response = await myClient.SendAsync (request, HttpCompletionOption.ResponseContentRead, token);
            } catch (OperationCanceledException ex) {
                Log.Info (Log.LOG_HTTP, "AttempHttp OperationCanceledException {0}: exception {1}", ServerUri, ex.Message);
                if (myClient == Client) {
                    CancelTimeoutTimer ();
                    if (!token.IsCancellationRequested) {
                        HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, "HTTPOPTO", null, string.Format ("Timeout, Uri: {0}", ServerUri));
                    }
                }
                return;
            } catch (WebException ex) {
                Log.Info (Log.LOG_HTTP, "AttempHttp WebException {0}: exception {1}", ServerUri, ex.Message);
                if (myClient == Client) {
                    CancelTimeoutTimer ();
                    // Some of the causes of WebException could be better characterized as HardFail. Not dividing now.
                    HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, "HTTPOPWEBEX", null, string.Format ("WebException: {0}, Uri: {1}", ex.Message, ServerUri));
                }
                return;
            } catch (NullReferenceException ex) {
                Log.Info (Log.LOG_HTTP, "AttempHttp NullReferenceException {0}: exception {1}", ServerUri, ex.Message);
                // As best I can tell, this may be driven by bug(s) in the Mono stack.
                if (myClient == Client) {
                    CancelTimeoutTimer ();
                    HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, "HTTPOPNRE", null, string.Format ("NullReferenceException: {0}, Uri: {1}", ex.Message, ServerUri));
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
                    Log.Info (Log.LOG_HTTP, "AttempHttp {0} {1}: exception {2}", ex, ServerUri, ex.Message);
                    HttpOpSm.PostEvent (Final ((uint)SmEvt.E.HardFail, "HTTPOPPHREX", null, string.Format ("Exception in ProcessHttpResponse: {0}", ex.Message)));
                 }
            }
        }

        private Event ProcessHttpResponse (HttpResponseMessage response)
        {
            if (HttpStatusCode.OK != response.StatusCode &&
                ContentTypeHtml == ContentType) {
                // There is a chance that the non-OK status comes with an HTML explaination.
                // If so, then dump it.
                var possibleMessage = new StreamReader (ContentData, Encoding.UTF8).ReadToEnd ();
                Console.WriteLine ("HTML response: {0}", possibleMessage);
            }
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
                return Final ((uint)SmEvt.E.HardFail, "HTTPOP400404", null, "HttpStatusCode.BadRequest or NotFound");

            case HttpStatusCode.Unauthorized:
                return Final ((uint)AsProtoControl.AsEvt.E.AuthFail, "HTTPOP401");

            case HttpStatusCode.Forbidden:
            case HttpStatusCode.InternalServerError:
            case HttpStatusCode.Found:
                if (response.Headers.Contains ("X-MS-RP")) {
                    // Per MS-ASHTTP 3.2.5.1, we should look for OPTIONS headers. If they are missing, okay.
                    AsOptionsCommand.ProcessOptionsHeaders (response.Headers, DataSource);
                    IndicateUriIfChanged ();
                    // FIXME - not ReSync event, rather set the sync-key to 0 and recover post auto-d.
                    return Final ((uint)AsProtoControl.AsEvt.E.ReSync, "HTTPOPRESYNC");
                }
                return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOPREDISC0");

            case (HttpStatusCode)449:
                IndicateUriIfChanged ();
                return Final ((uint)AsProtoControl.AsEvt.E.ReProv, "HTTPOP449");

            case (HttpStatusCode)451:
                if (!Allow451Follow) {
                    return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOPNO451", null, "HttpStatusCode.451 follow not allowed.");
                }
                if (response.Headers.Contains ("X-MS-Location")) {
                    try {
                        // Re-try the access using the new URI. If it works, then accept the new URI.
                        var redirUri = new Uri (response.Headers.GetValues ("X-MS-Location").First ());
                        if (!redirUri.IsHttps ()) {
                            // Don't be duped into accepting a non-HTTPS URI.
                            return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOPREDISC1");
                        }
                        ServerUriBeingTested = true;
                        McServer dummy = new McServer () {
                            Scheme = redirUri.Scheme,
                            Fqdn = redirUri.Host,
                            Port = redirUri.Port,
                            Path = redirUri.AbsolutePath
                        };
                        ServerUri = new Uri (AsCommand.BaseUri (dummy), redirUri.Query);
                        return Event.Create ((uint)SmEvt.E.Launch, "HTTPOPOK451");
                    } catch(Exception ex) {
                        Log.Info (Log.LOG_HTTP, "ProcessHttpResponse {0} {1}: exception {2}", ex, ServerUri, ex.Message);
                        return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOPREDISC2");
                    }
                }
                // If no X-MS-Location, just treat it as a transient and hope for the best.
                return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOP451NOX", null, "HttpStatusCode.451 with no X-MS-Location.");

            case HttpStatusCode.BadGateway:
                return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOP502", null, "HttpStatusCode.BadGateway");

            case HttpStatusCode.ServiceUnavailable:
                uint seconds = KDefaultDelaySeconds;
                if (response.Headers.Contains ("Retry-After")) {
                    try {
                        seconds = uint.Parse (response.Headers.GetValues ("Retry-After").First ());
                    } catch(Exception ex) {
                        Log.Info (Log.LOG_HTTP, "ProcessHttpResponse {0} {1}: exception {2}", ex, ServerUri, ex.Message);
                        return Event.Create ((uint)HttpOpEvt.E.Delay, "HTTPOPSURAEX", seconds, "Could not parse Retry-After value.");
                    }
                    return Event.Create ((uint)HttpOpEvt.E.Delay, "HTTPOPSURA", seconds, "Retry-After");
                }
                return Event.Create ((uint)HttpOpEvt.E.Delay, "HTTPOPSU", seconds, "HttpStatusCode.ServiceUnavailable");

            case (HttpStatusCode)507:
                IndicateUriIfChanged ();
                ReportError ("Exchange server is out of space.");
                return Final ((uint)SmEvt.E.HardFail, "HTTPOP507", null, "HttpStatusCode 507 - Out of space on server.");

            default:
                return Final ((uint)SmEvt.E.HardFail, "HTTPOPHARD0", null, 
                    string.Format ("Unknown HttpStatusCode {0}", response.StatusCode));
            }
        }

        private void ReportError (string message)
        {
            var result = NcResult.Error (message);
            if (null != Token && string.Empty != Token) {
                DataSource.Owner.StatusInd (DataSource.Control, result, new string[] { Token });
            } else {
                DataSource.Owner.StatusInd (DataSource.Control, result);
            }
        }
    }
}
