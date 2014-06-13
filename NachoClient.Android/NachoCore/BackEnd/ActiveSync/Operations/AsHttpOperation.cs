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
        /* THREAD SAFETY:
         * For the "owner" who created the object: 100% thread-safe, except that properties may only be set 
         * between the creation and the first action with the object (e.g. Execute()).
         * 
         * W/r/t events from the system: 100% thread-safe.
         * Any API/callback can be called on any thread at any time.
         * Execute() must be called before Cancel() is called.
         * 
         * Events:
         * - (API) Execute ().
         * - (API) Cancel ().
         * - Return from HttpClient (or Exception).
         * - TimeoutTimer expiry.
         * - DelayTimer expiry.
         */
        private enum HttpOpLst : uint
        {
            HttpWait = (St.Last + 1),
            DelayWait,
        };

        private class HttpOpEvt : SmEvt
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
        private const string KDefaultDelaySeconds = "10";
        private const string KDefaultTimeoutSeconds = "20";
        private const string KDefaultTimeoutExpander = "1.2";
        private const string KDefaultRetries = "8";
        private const string KToXML = "ToXML";
        private string CommandName;
        private IBEContext BEContext;
        private IAsHttpOperationOwner Owner;
        private CancellationTokenSource Cts;
        private List<object> OldCrap;
        private NcTimer DelayTimer;
        private NcTimer TimeoutTimer;
        // The OldXxxTimer lists is used to avoid eliminating a reference while still in a callback (can cause timer crash).
        // Also to avoid God knows what kind of bad GC interaction with HttpClient that won't cancel, etc.
        // They don't leak - they are freed when this AsHttpOperation object is freed.
        // Yes - if it isn't enough to hold onto these objects until then, we'd need a longer-term persistence plan.
        private NcStateMachine HttpOpSm;
        private NcStateMachine OwnerSm;
        private IHttpClient Client;
        private Uri ServerUri;
        private bool ServerUriBeingTested;
        private Stream ContentData;
        private string ContentType;
        // Properties.
        // Used for mocking.
        public Type HttpClientType { set; get; }
        // User for mocking.
        public INcCommStatus NcCommStatusSingleton { set; get; }
        // Timer for timing out a single access.
        public TimeSpan Timeout { set; get; }
        public double TimeoutExpander { set; get; }
        public uint MaxRetries { set; get; }
        // Numer of times we'll try again (remaining).
        public uint TriesLeft { set; get; }

        public bool Allow451Follow { set; get; }

        public bool DontReportCommResult { set; get; }

        public AsHttpOperation (string commandName, IAsHttpOperationOwner owner, IBEContext beContext)
        {
            OldCrap = new List<object> ();
            NcCapture.AddKind (KToXML);
            HttpClientType = typeof(MockableHttpClient);
            NcCommStatusSingleton = NcCommStatus.Instance;
            var timeoutSeconds = McMutables.GetOrCreate ("HTTPOP", "TimeoutSeconds", KDefaultTimeoutSeconds);
            Timeout = new TimeSpan (0, 0, timeoutSeconds.ToInt ());
            var timeoutExpander = McMutables.GetOrCreate ("HTTPOP", "TimeoutExpander", KDefaultTimeoutExpander);
            TimeoutExpander = double.Parse (timeoutExpander);
            MaxRetries = uint.Parse (McMutables.GetOrCreate ("HTTPOP", "Retries", KDefaultRetries));
            TriesLeft = MaxRetries + 1;
            Allow451Follow = true;
            CommandName = commandName;
            Owner = owner;
            BEContext = beContext;

            HttpOpSm = new NcStateMachine ("HTTPOP") {
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
                            new Trans {
                                Event = (uint)HttpOpEvt.E.Rephrase,
                                Act = DoHttp,
                                State = (uint)HttpOpLst.HttpWait
                            },
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
                            new Trans {
                                Event = (uint)HttpOpEvt.E.Cancel,
                                Act = DoCancelDelayTimer,
                                State = (uint)St.Stop
                            },
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
            HttpOpSm.PostEvent ((uint)HttpOpEvt.E.Cancel, "HTTPOPCANCEL");
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
                OldCrap.Add (DelayTimer);
                DelayTimer = null;
            }
        }

        private void DoHttp ()
        {
            if (0 < TriesLeft) {
                --TriesLeft;
                if (TriesLeft != MaxRetries) {
                    Timeout = new TimeSpan (0, 0, (int)(Timeout.Seconds * TimeoutExpander));
                }
                Log.Info (Log.LOG_HTTP, "ASHTTPOP: TriesLeft: {0}", TriesLeft);
                AttemptHttp ();
            } else {
                Owner.ResolveAllDeferred ();
                HttpOpSm.PostEvent (Final ((uint)SmEvt.E.TempFail, "ASHTTPDOH", null, "Too many retries."));
            }
        }

        private void DoCancelHttp ()
        {
            CancelTimeoutTimer ();
            DoCancelDelayTimer ();
            if (null != Cts) {
                if (!Cts.IsCancellationRequested) {
                    Cts.Cancel ();
                }
                OldCrap.Add (Cts);
                Cts = null;
            }
            if (null != Client) {
                OldCrap.Add (Client);
                Client = null;
            }
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
            HttpOpSm.PostEvent ((uint)SmEvt.E.Launch, "ASHTTPDTC");
        }

        private void CancelTimeoutTimer ()
        {
            if (null != TimeoutTimer) {
                TimeoutTimer.Dispose ();
                OldCrap.Add (TimeoutTimer);
                TimeoutTimer = null;
            }
        }

        private void TimeoutTimerCallback (object State)
        {
            if ((IHttpClient)State == Client) {
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

        private void ReportCommResult (string host, bool didFailGenerally)
        {
            if (!DontReportCommResult) {
                NcCommStatusSingleton.ReportCommResult (host, didFailGenerally);
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

        private bool CreateHttpRequest (out HttpRequestMessage request, CancellationToken cToken)
        {
            XDocument doc;
            try {
                doc = Owner.ToXDocument (this);
            } catch (AsCommand.AbortCommandException) {
                request = null;
                return false;
            }
            request = new HttpRequestMessage (Owner.Method (this), ServerUri);
            if (null != doc) {
                Log.Info (Log.LOG_XML, "{0}:\n{1}", CommandName, doc);
                if (Owner.UseWbxml (this)) {
                    var stream = doc.ToWbxmlStream (Owner.IsContentLarge (this), cToken);
                    var content = new StreamContent (stream);
                    request.Content = content;
                    request.Content.Headers.Add ("Content-Length", stream.Length.ToString ());
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
                request.Content = mime;
            }
            request.Headers.Add ("User-Agent", Device.Instance.UserAgent ());
            if (Owner.DoSendPolicyKey (this)) {
                request.Headers.Add ("X-MS-PolicyKey", BEContext.ProtocolState.AsPolicyKey);
            }
            request.Headers.Add ("MS-ASProtocolVersion", BEContext.ProtocolState.AsProtocolVersion);
            return true;
        }

        private async void AttemptHttp ()
        {
            Cts = new CancellationTokenSource ();
            var cToken = Cts.Token;

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

            HttpRequestMessage request = null;
            if (!CreateHttpRequest (out request, cToken)) {
                Log.Info (Log.LOG_HTTP, "Intentionally aborting HTTP operation.");
                HttpOpSm.PostEvent (Final ((uint)SmEvt.E.HardFail, "HTTPOPNOCON"));
                return;
            }
            HttpResponseMessage response = null;

            try {
                // HttpClient doesn't respect Timeout sometimes (DNS and TCP connection establishment for sure).
                // If the instance of HttpClient known to the callback (myClient) doesn't match the IVar, then 
                // assume the IHttpClient instance has been abandoned.
                var myClient = Client;
                TimeoutTimer = new NcTimer (TimeoutTimerCallback, myClient, Timeout, 
                    System.Threading.Timeout.InfiniteTimeSpan);
                try {
                    Log.Info (Log.LOG_HTTP, "HTTPOP:URL:{0}", request.RequestUri.ToString ());
                    response = await myClient.SendAsync (request, HttpCompletionOption.ResponseHeadersRead, cToken).ConfigureAwait (false);
                } catch (OperationCanceledException ex) {
                    Log.Info (Log.LOG_HTTP, "AttempHttp OperationCanceledException {0}: exception {1}", ServerUri, ex.Message);
                    if (myClient == Client) {
                        CancelTimeoutTimer ();
                        if (!cToken.IsCancellationRequested) {
                            // See http://stackoverflow.com/questions/12666922/distinguish-timeout-from-user-cancellation
                            ReportCommResult (ServerUri.Host, true);
                            HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, "HTTPOPTO", null, string.Format ("Timeout, Uri: {0}", ServerUri));
                        }
                    }
                    return;
                } catch (WebException ex) {
                    Log.Info (Log.LOG_HTTP, "AttempHttp WebException {0}: exception {1}", ServerUri, ex.Message);
                    if (myClient == Client) {
                        CancelTimeoutTimer ();
                        ReportCommResult (ServerUri.Host, true);
                        // Some of the causes of WebException could be better characterized as HardFail. Not dividing now.
                        // TODO: I have seen an expired server cert get us here. We need to catch that case specifically, and alert user/admin.
                        HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, "HTTPOPWEBEX", null, string.Format ("WebException: {0}, Uri: {1}", ex.Message, ServerUri));
                    }
                    return;
                } catch (NullReferenceException ex) {
                    Log.Info (Log.LOG_HTTP, "AttempHttp NullReferenceException {0}: exception {1}", ServerUri, ex.Message);
                    // As best I can tell, this may be driven by bug(s) in the Mono stack.
                    if (myClient == Client) {
                        CancelTimeoutTimer ();
                        HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, "HTTPOPTO", null, string.Format ("Timeout, Uri: {0}", ServerUri));
                    }
                    return;
                } catch (Exception ex) {
                    // We've seen HttpClient barf due to Cancel().
                    if (myClient == Client) {
                        CancelTimeoutTimer ();
                        Log.Error (Log.LOG_HTTP, "Exception: {0}", ex.ToString ());
                        HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, "HTTPOPFU", null, string.Format ("E, Uri: {0}", ServerUri));
                    }
                    return;
                }

                if (myClient == Client) {
                    CancelTimeoutTimer ();
                    var contentType = response.Content.Headers.ContentType;
                    ContentType = (null == contentType) ? null : contentType.MediaType.ToLower ();
                    try {
                        ContentData = new BufferedStream (await response.Content.ReadAsStreamAsync ().ConfigureAwait (false));
                    } catch (Exception ex) {
                        // If we see this, it is most likely a bug in error processing above in AttemptHttp().
                        Log.Error (Log.LOG_HTTP, "AttempHttp {0} {1}: exception in ReadAsStreamAsync {2}\n{3}", ex, ServerUri, ex.Message, ex.StackTrace);
                        HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, "HTTPOPODE", null, string.Format ("E, Uri: {0}", ServerUri));
                        return;
                    }

                    try {
                        HttpOpSm.PostEvent (ProcessHttpResponse (response, cToken));
                    } catch (Exception ex) {
                        Log.Error (Log.LOG_HTTP, "AttempHttp {0} {1}: exception {2}\n{3}", ex, ServerUri, ex.Message, ex.StackTrace);
                        // Likely a bug in our code if we got here, but likely to get stuck here again unless we resolve-as-failed.
                        Owner.ResolveAllFailed (NcResult.WhyEnum.Unknown);
                        HttpOpSm.PostEvent (Final ((uint)SmEvt.E.HardFail, "HTTPOPPHREX", null, string.Format ("Exception in ProcessHttpResponse: {0}", ex.Message)));
                        return;
                    }
                }
            } finally {
                if (null != request) {
                    request.Dispose ();
                }
                if (null != response) {
                    response.Dispose ();
                }
            }
        }

        private Event ProcessHttpResponse (HttpResponseMessage response, CancellationToken cToken)
        {
            if (HttpStatusCode.OK != response.StatusCode &&
                ContentTypeHtml == ContentType) {
                // There is a chance that the non-OK status comes with an HTML explaination.
                // If so, then dump it.
                // FIXME: find some way to make cancellation token work here.
                var possibleMessage = new StreamReader (ContentData, Encoding.UTF8).ReadToEnd ();
                Log.Info (Log.LOG_HTTP, "HTML response: {0}", possibleMessage);
            }
            Event preProcessEvent = Owner.PreProcessResponse (this, response);
            if (null != preProcessEvent) {
                // If the owner is returning an event, they MUST have resolved all pendings.
                return Final (preProcessEvent);
            }
            XDocument responseDoc;
            switch (response.StatusCode) {
            case HttpStatusCode.OK:
                ReportCommResult (ServerUri.Host, false);
                IndicateUriIfChanged ();
                if (0 > ContentData.Length) {
                    // We have seen this, but we've never see doc stating why possible.
                    return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOPZORNEG");
                } else if (0 < ContentData.Length) {
                    switch (ContentType) {
                    case ContentTypeWbxml:
                        var decoder = new ASWBXML (cToken);
                        try {
                            decoder.LoadBytes (ContentData);
                        } catch (OperationCanceledException) {
                            // FIXME: we could have orphaned McBody(s). HardFail isn't accurate.
                            Owner.ResolveAllDeferred ();
                            return Final ((uint)SmEvt.E.HardFail, "WBXCANCEL");
                        } catch (WBXMLReadPastEndException) {
                            // FIXME: we could have orphaned McBody(s). HardFail isn't accurate.
                            // We are deferring because we think that an invalid WBXML string is likely transient.
                            Owner.ResolveAllDeferred ();
                            return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOPRDPEND");
                        } catch (InvalidDataException) {
                            Owner.ResolveAllDeferred ();
                            return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOPRDPEND2");
                        } catch (WebException) {
                            return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOPRDPEND3");
                        } catch (Exception ex) {
                            // We just don't have a catalog of all the "valid" ways we can fail due
                            // to network errors. Log as Error so we can see anything that looks like a bug.
                            Log.Error (Log.LOG_HTTP, "Unanticipated Exception: {0}", ex.ToString ());
                            return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOPRDPEND4");
                        }
                        responseDoc = decoder.XmlDoc;
                        var xmlStatus = responseDoc.Root.ElementAnyNs (Xml.AirSync.Status);
                        if (null != xmlStatus) {
                            // FIXME - push TL status into pending.
                            var statusEvent = Owner.ProcessTopLevelStatus (this, uint.Parse (xmlStatus.Value));
                            if (null != statusEvent) {
                                Log.Info (Log.LOG_AS, "Top-level XML Status {0}:{1}", xmlStatus.Value, statusEvent);
                                // If Owner is returning an event, then Owner MUST resolve all pending.
                                return Final (statusEvent);
                            }
                        }
                        Log.Info (Log.LOG_AS, "{0} response:\n{1}", CommandName, responseDoc);
                        // Owner MUST resolve all pending.
                        return Final (Owner.ProcessResponse (this, response, responseDoc));
                    case ContentTypeWbxmlMultipart:
                        throw new Exception ("FIXME: ContentTypeWbxmlMultipart unimplemented.");
                    case ContentTypeXml:
                        responseDoc = XDocument.Load (ContentData);
                        Log.Info (Log.LOG_AS, "{0} response:\n{1}", CommandName, responseDoc);
                        // Owner MUST resolve all pending.
                        return Final (Owner.ProcessResponse (this, response, responseDoc));
                    default:
                        if (null == ContentType) {
                            Log.Warn (Log.LOG_HTTP, "ProcessHttpResponse: received HTTP response with content but no Content-Type.");
                        }
                        // Owner MUST resolve all pending.
                        return Final (Owner.ProcessResponse (this, response));
                    }
                } 
                // Owner MUST resolve all pending.
                return Final (Owner.ProcessResponse (this, response));

            // NOTE: ALWAYS resolve pending on Final, and ONLY resolve pending on Final.
            // DO NOT resolve pending on Event.Create!

            case HttpStatusCode.Found:
                ReportCommResult (ServerUri.Host, false);
                Owner.ResolveAllDeferred ();
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
                    Owner.ResolveAllFailed (NcResult.WhyEnum.ProtocolError);
                    return Final ((uint)SmEvt.E.HardFail, "HTTPOP400F", null, "HttpStatusCode.BadRequest");
                }

            case HttpStatusCode.Unauthorized:
                ReportCommResult (ServerUri.Host, false); // Non-general failure.
                // We are ignoring the auto-d directive of MS-ASHTTP 3.2.5.1 here. It doesn't make sense.
                Owner.ResolveAllDeferred ();
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
                Owner.ResolveAllFailed (NcResult.WhyEnum.AccessDeniedOrBlocked);
                return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOP403F");

            case HttpStatusCode.NotFound:
                ReportCommResult (ServerUri.Host, false); // Non-general failure.
                if (Owner.WasAbleToRephrase ()) {
                    return Event.Create ((uint)HttpOpEvt.E.Rephrase, "HTTPOP404R");
                } else {
                    Owner.ResolveAllFailed (NcResult.WhyEnum.MissingOnServer);
                    return Final ((uint)SmEvt.E.HardFail, "HTTPOP404F", null, "HttpStatusCode.NotFound");
                }

            case (HttpStatusCode)449:
                // http://blogs.msdn.com/b/exchangedev/archive/2011/09/28/10198711.aspx
                // This may be a legit re-provision request or prelude to a wipe.
                // TODO: blog post suggests letting admin know and re-trying hourly if 449 repeats (12.1-ism).
                ReportCommResult (ServerUri.Host, false);
                IndicateUriIfChanged ();
                Owner.ResolveAllDeferred ();
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
                            Owner.ResolveAllDeferred ();
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
                    } catch (Exception ex) {
                        Log.Info (Log.LOG_HTTP, "ProcessHttpResponse {0} {1}: exception {2}", ex, ServerUri, ex.Message);
                        Owner.ResolveAllDeferred ();
                        return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOP451D");
                    }
                }
                // If no X-MS-Location, we are effed.
                return Event.Create ((uint)SmEvt.E.HardFail, "HTTPOP451E", null, "HttpStatusCode.451 with no X-MS-Location.");

            case (HttpStatusCode)456:
                ReportCommResult (ServerUri.Host, false);
                Owner.ResolveAllDeferred ();
                Owner.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_AuthFailBlocked));
                return Event.Create ((uint)SmEvt.E.HardFail, "HTTPOP456");

            case (HttpStatusCode)457:
                ReportCommResult (ServerUri.Host, false);
                Owner.ResolveAllDeferred ();
                Owner.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_AuthFailPasswordExpired));
                return Event.Create ((uint)SmEvt.E.HardFail, "HTTPOP457");

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
                Owner.ResolveAllFailed (NcResult.WhyEnum.AccessDeniedOrBlocked);
                return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOP500");

            case (HttpStatusCode)501:
                ReportCommResult (ServerUri.Host, false);
                Owner.ResolveAllDeferred ();
                return Final ((uint)SmEvt.E.HardFail, "HTTPOP501", null, "HttpStatusCode 501 - Command not implemented.");

            case HttpStatusCode.BadGateway:
                ReportCommResult (ServerUri.Host, true);
                return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOP502", null, "HttpStatusCode.BadGateway");

            case HttpStatusCode.ServiceUnavailable:
                ReportCommResult (ServerUri.Host, true);
                uint configuredSecs = uint.Parse (McMutables.GetOrCreate ("HTTP", "DelaySeconds", KDefaultDelaySeconds));
                uint bestSecs = configuredSecs;
                if (response.Headers.Contains (HeaderRetryAfter)) {
                    string value = null;
                    try {
                        value = response.Headers.GetValues (HeaderRetryAfter).First ();
                        bestSecs = (uint)double.Parse (value);
                    } catch {
                        try {
                            var when = DateTime.Parse (value);
                            var maybe_secs = when.Subtract(DateTime.UtcNow).Seconds;
                            bestSecs = ((maybe_secs > 0) ? (uint)maybe_secs : configuredSecs);
                        } catch (Exception ex) {
                            Log.Info (Log.LOG_HTTP, "Rejected DateTime string: {0}", value);
                            Log.Info (Log.LOG_HTTP, "ProcessHttpResponse {0} {1}: exception {2}", ex, ServerUri, ex.Message);
                            return Event.Create ((uint)HttpOpEvt.E.Delay, "HTTPOP503A", bestSecs, "Could not parse Retry-After value.");
                        }
                    }
                    return Event.Create ((uint)HttpOpEvt.E.Delay, "HTTPOP503B", bestSecs, HeaderRetryAfter);
                }
                return Event.Create ((uint)HttpOpEvt.E.Delay, "HTTPOP503C", bestSecs, "HttpStatusCode.ServiceUnavailable");

            case (HttpStatusCode)505:
                ReportCommResult (ServerUri.Host, false);
                Owner.ResolveAllFailed (NcResult.WhyEnum.Unknown);
                return Final ((uint)SmEvt.E.HardFail, "HTTPOP505", null, "HttpStatusCode 505 - Server says it doesn't like our HTTP version.");

            case (HttpStatusCode)507:
                ReportCommResult (ServerUri.Host, false);
                IndicateUriIfChanged ();
                if (Owner.WasAbleToRephrase ()) {
                    return Event.Create ((uint)HttpOpEvt.E.Rephrase, "HTTPOP507R");
                } else {
                    Owner.ResolveAllFailed (NcResult.WhyEnum.NoSpace);
                    return Final ((uint)SmEvt.E.HardFail, "HTTPOP507", null, "HttpStatusCode 507 - Out of space on server.");
                }

            default:
                ReportCommResult (ServerUri.Host, true);
                Owner.ResolveAllFailed (NcResult.WhyEnum.Unknown);
                return Final ((uint)SmEvt.E.HardFail, "HTTPOPHARD0", null, 
                    string.Format ("Unknown HttpStatusCode {0}", response.StatusCode));
            }
        }
    }
}
