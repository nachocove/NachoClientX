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
using System.Threading.Tasks;
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
            DelayWait,
        };

        public class HttpOpEvt : SmEvt
        {
            new public enum E : uint
            {
                Cancel = (SmEvt.E.Last + 1),
                Delay,
                Timeout,
                Rephrase,
                Final,
            };
        }
        // Constants.
        private const string ContentTypeHtml = "text/html";
        private const string ContentTypeWbxml = "application/vnd.ms-sync.wbxml";
        private const string ContentTypeWbxmlMultipart = "application/vnd.ms-sync.multipart";
        private const string ContentTypeMail = "message/rfc822";
        private const string ContentTypeXml = "text/xml";
        private const string HeaderRetryAfter = "Retry-After";
        private const string HeaderXMsRp = "X-MS-RP";
        private const string HeaderXMsLocation = "X-MS-Location";
        private const string KXsd = "xsd";
        private const string KCommon = "common";
        private const string KRequest = "request";
        private const string KResponse = "response";
        private const uint KDefaultDelaySeconds = 10;
        private const int KDefaultTimeoutSeconds = 15;
        private const uint KDefaultRetries = 15;
        private const string KToXML = "ToXML";

        private string CommandName;
        private IBEContext BEContext;
        private IAsHttpOperationOwner Owner;
        private CancellationTokenSource Cts;
        private NcTimer DelayTimer;
        private NcTimer TimeoutTimer;
        // These DisposedXxx are used to avoid eliminating a reference while still in a callback (can cause crash).
        // They don't leak - they are freed when this AsHttpOperation object is freed.
        #pragma warning disable 414
        private NcTimer DisposedDelayTimer;
        private NcTimer DisposedTimeoutTimer;
        #pragma warning restore 414
        private NcStateMachine HttpOpSm;
        private NcStateMachine OwnerSm;
        private IHttpClient Client;
        private Uri ServerUri;
        private bool ServerUriBeingTested;
        private Stream ContentData;
        private string ContentType;
        // Properties.
        public Type HttpClientType { set; get; }

        public TimeSpan Timeout { set; get; }

        public uint TriesLeft { set; get; }

        public bool Allow451Follow { set; get; }

        public bool DontReportCommResult { set; get; }

        public string Token { set; get; }

        /* Lifecycle:
         * AsHttpOperation object gets created.
         * obj.Execute(sm) called.
         */
        public AsHttpOperation (string commandName, IAsHttpOperationOwner owner, IBEContext beContext)
        {
            NcCapture.AddKind (KToXML);
            HttpClientType = typeof(MockableHttpClient);
            Timeout = new TimeSpan (0, 0, KDefaultTimeoutSeconds);
            TriesLeft = KDefaultRetries + 1;
            Allow451Follow = true;
            CommandName = commandName;
            Owner = owner;
            BEContext = beContext;

            HttpOpSm = new NcStateMachine () {
                Name = "as:http_op",
                LocalEventType = typeof(HttpOpEvt),
                LocalStateType = typeof(HttpOpLst),
                TransTable = new [] {
                    new Node {State = (uint)St.Start,
                        Drop = new [] {
                            (uint)HttpOpEvt.E.Cancel
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)HttpOpEvt.E.Delay,
                            (uint)HttpOpEvt.E.Timeout,
                            (uint)HttpOpEvt.E.Rephrase,
                            (uint)HttpOpEvt.E.Final,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoHttp, State = (uint)HttpOpLst.HttpWait },
                        }
                    },
                    new Node {State = (uint)HttpOpLst.HttpWait,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                        },
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
                            new Trans { Event = (uint)HttpOpEvt.E.Rephrase, Act = DoHttp, State = (uint)HttpOpLst.HttpWait },
                            new Trans { Event = (uint)HttpOpEvt.E.Final, Act = DoFinal, State = (uint)St.Stop },
                        }
                    },
                    new Node {State = (uint)HttpOpLst.DelayWait,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)HttpOpEvt.E.Delay,
                            (uint)HttpOpEvt.E.Timeout,
                            (uint)HttpOpEvt.E.Rephrase,
                            (uint)HttpOpEvt.E.Final,
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
        public virtual void Execute (NcStateMachine sm)
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
            DelayTimer = new NcTimer (DelayTimerCallback, null, Convert.ToInt32 (HttpOpSm.Arg),
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
                Log.Info (Log.LOG_AS, "ASHTTPOP: TriesLeft: {0}", TriesLeft);
                AttemptHttp ();
            } else {
                HttpOpSm.PostEvent (Final ((uint)SmEvt.E.TempFail, "ASHTTPDOH", null, "Too many retries."));
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
            var ultimate = (Event)HttpOpSm.Arg;
            if ((uint)SmEvt.E.Success == ultimate.EventCode) {
                Owner.StatusInd (true);
            } else {
                Owner.StatusInd (false);
            }
            OwnerSm.PostEvent (ultimate);
        }

        private void DelayTimerCallback (object State)
        {
            DoCancelDelayTimer ();
            var timeoutEvent = Event.Create ((uint)SmEvt.E.Launch, "ASHTTPDTC");
            timeoutEvent.DropIfStopped = true;
            HttpOpSm.PostEvent (timeoutEvent);
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
            if ((IHttpClient)State == Client) {
                var timeoutEvent = Event.Create ((uint)HttpOpEvt.E.Timeout, "ASHTTPTTC", null, string.Format ("Uri: {0}", ServerUri));
                timeoutEvent.DropIfStopped = true;
                HttpOpSm.PostEvent (timeoutEvent);
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

        private void ReportCommResult (string host, bool didFailGenerally)
        {
            if (!DontReportCommResult) {
                NcCommStatus.Instance.ReportCommResult (host, didFailGenerally);
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
            Owner.PostProcessEvent (endEvent);
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
                handler.Credentials = new NetworkCredential (BEContext.Cred.Username, BEContext.Cred.Password);
            }
            Client = (IHttpClient)Activator.CreateInstance (HttpClientType, handler);
            Client.Timeout = this.Timeout;
            var request = new HttpRequestMessage (Owner.Method (this), ServerUri);
            var doc = Owner.ToXDocument (this);
            if (null != doc) {
                Log.Info (Log.LOG_XML, "{0}:\n{1}", CommandName, doc);
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
            if (BEContext.ProtocolState.InitialProvisionCompleted && Owner.DoSendPolicyKey (this)) {
                request.Headers.Add ("X-MS-PolicyKey", BEContext.ProtocolState.AsPolicyKey);
            }
            request.Headers.Add ("MS-ASProtocolVersion", BEContext.ProtocolState.AsProtocolVersion);
            Cts = new CancellationTokenSource ();
            CancellationToken cToken = Cts.Token;
            HttpResponseMessage response = null;

            // HttpClient doesn't respect Timeout sometimes (DNS and TCP connection establishment for sure).
            // If the instance of HttpClient known to the callback (myClient) doesn't match the IVar, then 
            // assume the IHttpClient instance has been abandoned.
            var myClient = Client;
            TimeoutTimer = new NcTimer (TimeoutTimerCallback, myClient, Timeout, 
                System.Threading.Timeout.InfiniteTimeSpan);
            try {
                Log.Info (Log.LOG_AS, "HTTPOP:URL:{0}", request.RequestUri.ToString());
                response = await myClient.SendAsync (request, HttpCompletionOption.ResponseHeadersRead, cToken).ConfigureAwait (false);
            } catch (OperationCanceledException ex) {
                Log.Info (Log.LOG_HTTP, "AttempHttp OperationCanceledException {0}: exception {1}", ServerUri, ex.Message);
                if (myClient == Client) {
                    CancelTimeoutTimer ();
                    if (!cToken.IsCancellationRequested) {
                        // See http://stackoverflow.com/questions/12666922/distinguish-timeout-from-user-cancellation
                        ReportCommResult (ServerUri.Host, true);
                        var timeoutEvent = Event.Create ((uint)SmEvt.E.TempFail, "HTTPOPTO", null, string.Format ("Timeout, Uri: {0}", ServerUri));
                        timeoutEvent.DropIfStopped = true;
                        HttpOpSm.PostEvent (timeoutEvent);
                    }
                }
                return;
            } catch (WebException ex) {
                Log.Info (Log.LOG_HTTP, "AttempHttp WebException {0}: exception {1}", ServerUri, ex.Message);
                if (myClient == Client) {
                    CancelTimeoutTimer ();
                    ReportCommResult (ServerUri.Host, true);
                    // Some of the causes of WebException could be better characterized as HardFail. Not dividing now.
                    var webExEvent = Event.Create ((uint)SmEvt.E.TempFail, "HTTPOPWEBEX", null, string.Format ("WebException: {0}, Uri: {1}", ex.Message, ServerUri));
                    webExEvent.DropIfStopped = true;
                    HttpOpSm.PostEvent (webExEvent);
                }
                return;
            } catch (NullReferenceException ex) {
                Log.Info (Log.LOG_HTTP, "AttempHttp NullReferenceException {0}: exception {1}", ServerUri, ex.Message);
                // As best I can tell, this may be driven by bug(s) in the Mono stack.
                if (myClient == Client) {
                    CancelTimeoutTimer ();
                    var nRefEvent = Event.Create ((uint)SmEvt.E.TempFail, "HTTPOPTO", null, string.Format ("Timeout, Uri: {0}", ServerUri));
                    nRefEvent.DropIfStopped = true;
                    HttpOpSm.PostEvent (nRefEvent);
                }
                return;
            }

            if (myClient == Client) {
                CancelTimeoutTimer ();
                var contentType = response.Content.Headers.ContentType;
                ContentType = (null == contentType) ? null : contentType.MediaType.ToLower ();
                ContentData = new BufferedStream (await response.Content.ReadAsStreamAsync ().ConfigureAwait (false));

                try {
                    HttpOpSm.PostEvent (ProcessHttpResponse (response, cToken));
                } catch (Exception ex) {
                    Log.Info ("AttempHttp {0} {1}: exception {2}\n{3}", ex, ServerUri, ex.Message, ex.StackTrace);
                    HttpOpSm.PostEvent (Final ((uint)SmEvt.E.HardFail, "HTTPOPPHREX", null, string.Format ("Exception in ProcessHttpResponse: {0}", ex.Message)));
                 }
            }
        }

        // TODO: move a bunch of this logic into AsCommand.
        private Event ProcessHttpResponse (HttpResponseMessage response, CancellationToken cToken)
        {
            if (HttpStatusCode.OK != response.StatusCode &&
                ContentTypeHtml == ContentType) {
                // There is a chance that the non-OK status comes with an HTML explaination.
                // If so, then dump it.
                var possibleMessage = new StreamReader (ContentData, Encoding.UTF8).ReadToEnd ();
                Log.Info (Log.LOG_AS, "HTML response: {0}", possibleMessage);
            }
            Event preProcessEvent = Owner.PreProcessResponse (this, response);
            if (null != preProcessEvent) {
                return Final (preProcessEvent);
            }
            XDocument responseDoc;
            switch (response.StatusCode) {
            case HttpStatusCode.OK:
                ReportCommResult (ServerUri.Host, false);
                IndicateUriIfChanged ();
                if (0 < ContentData.Length) {
                    switch (ContentType) {
                    case ContentTypeWbxml:
                        var decoder = new ASWBXML (cToken);
                        var cap = NcCapture.CreateAndStart (KToXML);
                        try {
                            decoder.LoadBytes (ContentData);
                        } catch (TaskCanceledException) {
                            // FIXME: we could have orphaned McBody(s). HardFail isn't accurate.
                            return Final ((uint)SmEvt.E.HardFail, "WBXCANCEL");
                        }
                        responseDoc = decoder.XmlDoc;
                        cap.Stop ();
                        var xmlStatus = responseDoc.Root.ElementAnyNs (Xml.AirSync.Status);
                        if (null != xmlStatus) {
                            // FIXME - push TL status into pending.
                            var statusEvent = Owner.ProcessTopLevelStatus (this, uint.Parse (xmlStatus.Value));
                            if (null != statusEvent) {
                                Log.Info (Log.LOG_AS, "Top-level XML Status {0}:{1}", xmlStatus.Value, statusEvent);
                                return Final (statusEvent);
                            }
                        }
                        Log.Info (Log.LOG_XML, "{0} response:\n{1}", CommandName, responseDoc);
                        return Final (Owner.ProcessResponse (this, response, responseDoc));
                    case ContentTypeWbxmlMultipart:
                        throw new Exception ("FIXME: ContentTypeWbxmlMultipart unimplemented.");
                    case ContentTypeXml:
                        responseDoc = XDocument.Load (ContentData);
                        Log.Info (Log.LOG_XML, "{0} response:\n{1}", CommandName, responseDoc);
                        return Final (Owner.ProcessResponse (this, response, responseDoc));
                    default:
                        if (null == ContentType) {
                            Log.Warn (Log.LOG_AS, "ProcessHttpResponse: received HTTP response with content but no Content-Type.");
                        }
                        return Final (Owner.ProcessResponse (this, response));
                    }
                } 
                return Final (Owner.ProcessResponse (this, response));

                // NOTE: ALWAYS resolve pending on Final, and ONLY resolve pending on Final.
                // DO NOT resolve pending on Event.Create!

            case HttpStatusCode.Found:
                ReportCommResult (ServerUri.Host, false);
                Owner.ResoveAllDeferred ();
                if (response.Headers.Contains (HeaderXMsRp)) {
                    McFolder.AsResetState (BEContext.Account.Id);
                    // Per MS-ASHTTP 3.2.5.1, we should look for OPTIONS headers. If they are missing, okay.
                    AsOptionsCommand.ProcessOptionsHeaders (response.Headers, BEContext);
                    IndicateUriIfChanged ();
                }
                return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOP302");

            case HttpStatusCode.BadRequest:
                ReportCommResult (ServerUri.Host, false); // Non-general failure.
                if (Owner.WasAbleToRephrase ()) {
                    return Event.Create ((uint)HttpOpEvt.E.Rephrase, "HTTPOP400R");
                } else {
                    Owner.ResoveAllFailed (NcResult.WhyEnum.ProtocolError);
                    return Final ((uint)SmEvt.E.HardFail, "HTTPOP400F", null, "HttpStatusCode.BadRequest");
                }

            case HttpStatusCode.Unauthorized:
                ReportCommResult (ServerUri.Host, false); // Non-general failure.
                // We are ignoring the auto-d directive of MS-ASHTTP 3.2.5.1 here. It doesn't make sense.
                Owner.ResoveAllDeferred ();
                return Final ((uint)AsProtoControl.AsEvt.E.AuthFail, "HTTPOP401");
                            
            case HttpStatusCode.Forbidden:
                ReportCommResult (ServerUri.Host, false); // Non-general failure.
                if (response.Headers.Contains (HeaderXMsRp)) {
                    McFolder.AsResetState (BEContext.Account.Id);
                    // Per MS-ASHTTP 3.2.5.1, we should look for OPTIONS headers. If they are missing, okay.
                    AsOptionsCommand.ProcessOptionsHeaders (response.Headers, BEContext);
                    IndicateUriIfChanged ();
                }
                // We are following the (iffy) auto-d directive, but failing pending to avoid possible loop.
                Owner.ResoveAllFailed (NcResult.WhyEnum.AccessDeniedOrBlocked);
                return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOP403F");

            case HttpStatusCode.NotFound:
                ReportCommResult (ServerUri.Host, false); // Non-general failure.
                if (Owner.WasAbleToRephrase ()) {
                    return Event.Create ((uint)HttpOpEvt.E.Rephrase, "HTTPOP404R");
                } else {
                    Owner.ResoveAllFailed (NcResult.WhyEnum.MissingOnServer);
                    return Final ((uint)SmEvt.E.HardFail, "HTTPOP404F", null, "HttpStatusCode.NotFound");
                }

            case (HttpStatusCode)449:
                // http://blogs.msdn.com/b/exchangedev/archive/2011/09/28/10198711.aspx
                // This may be a legit re-provision request or prelude to a wipe.
                // TODO: blog post suggests letting admin know and re-trying hourly if 449 repeats (12.1-ism).
                ReportCommResult (ServerUri.Host, false);
                IndicateUriIfChanged ();
                Owner.ResoveAllDeferred ();
                return Final ((uint)AsProtoControl.AsEvt.E.ReProv, "HTTPOP449");

            case (HttpStatusCode)451:
                ReportCommResult (ServerUri.Host, false);
                if (!Allow451Follow) {
                    return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOP451A", null, "HttpStatusCode.451 follow not allowed.");
                }
                if (response.Headers.Contains (HeaderXMsLocation)) {
                    try {
                        // Re-try the access using the new URI. If it works, then accept the new URI.
                        var redirUri = new Uri (response.Headers.GetValues (HeaderXMsLocation).First ());
                        if (!redirUri.IsHttps ()) {
                            // Don't be tricked into accepting a non-HTTPS URI.
                            Owner.ResoveAllDeferred ();
                            return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOP451B");
                        }
                        ServerUriBeingTested = true;
                        McServer dummy = new McServer () {
                            Scheme = redirUri.Scheme,
                            Host = redirUri.Host,
                            Port = redirUri.Port,
                            Path = redirUri.AbsolutePath
                        };
                        ServerUri = new Uri (AsCommand.BaseUri (dummy), redirUri.Query);
                        return Event.Create ((uint)SmEvt.E.Launch, "HTTPOP451C");
                    } catch(Exception ex) {
                        Log.Info (Log.LOG_HTTP, "ProcessHttpResponse {0} {1}: exception {2}", ex, ServerUri, ex.Message);
                        Owner.ResoveAllDeferred ();
                        return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOP451D");
                    }
                }
                // If no X-MS-Location, we are effed.
                return Event.Create ((uint)SmEvt.E.HardFail, "HTTPOP451E", null, "HttpStatusCode.451 with no X-MS-Location.");

            case HttpStatusCode.InternalServerError:
                // FIXME: Have some way to detect big loops (e.g. 500->auto-d->500).
                ReportCommResult (ServerUri.Host, false); // Non-general failure.
                if (response.Headers.Contains (HeaderXMsRp)) {
                    McFolder.AsResetState (BEContext.Account.Id);
                    // Per MS-ASHTTP 3.2.5.1, we should look for OPTIONS headers. If they are missing, okay.
                    AsOptionsCommand.ProcessOptionsHeaders (response.Headers, BEContext);
                    IndicateUriIfChanged ();
                }
                // We are following the (iffy) auto-d directive, but failing pending to avoid possible loop.
                Owner.ResoveAllFailed (NcResult.WhyEnum.AccessDeniedOrBlocked);
                return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOP500");

            case (HttpStatusCode)501:
                ReportCommResult (ServerUri.Host, false);
                Owner.ResoveAllDeferred ();
                return Final ((uint)SmEvt.E.HardFail, "HTTPOP501", null, "HttpStatusCode 501 - Command not implemented.");

            case HttpStatusCode.BadGateway:
                ReportCommResult (ServerUri.Host, true);
                return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOP502", null, "HttpStatusCode.BadGateway");

            case HttpStatusCode.ServiceUnavailable:
                ReportCommResult (ServerUri.Host, true);
                uint seconds = KDefaultDelaySeconds;
                if (response.Headers.Contains (HeaderRetryAfter)) {
                    try {
                        seconds = uint.Parse (response.Headers.GetValues (HeaderRetryAfter).First ());
                    } catch(Exception ex) {
                        Log.Info (Log.LOG_HTTP, "ProcessHttpResponse {0} {1}: exception {2}", ex, ServerUri, ex.Message);
                        return Event.Create ((uint)HttpOpEvt.E.Delay, "HTTPOP503A", seconds, "Could not parse Retry-After value.");
                    }
                    return Event.Create ((uint)HttpOpEvt.E.Delay, "HTTPOP503B", seconds, HeaderRetryAfter);
                }
                return Event.Create ((uint)HttpOpEvt.E.Delay, "HTTPOP503C", seconds, "HttpStatusCode.ServiceUnavailable");

            case (HttpStatusCode)507:
                ReportCommResult (ServerUri.Host, false);
                IndicateUriIfChanged ();
                if (Owner.WasAbleToRephrase ()) {
                    return Event.Create ((uint)HttpOpEvt.E.Rephrase, "HTTPOP507R");
                } else {
                    Owner.ResoveAllFailed (NcResult.WhyEnum.NoSpace);
                    return Final ((uint)SmEvt.E.HardFail, "HTTPOP507", null, "HttpStatusCode 507 - Out of space on server.");
                }

            default:
                ReportCommResult (ServerUri.Host, true);
                Owner.ResoveAllFailed (NcResult.WhyEnum.Unknown);
                return Final ((uint)SmEvt.E.HardFail, "HTTPOPHARD0", null, 
                    string.Format ("Unknown HttpStatusCode {0}", response.StatusCode));
            }
        }
    }
}
