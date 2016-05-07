// # Copyright (C) 2013, 2014, 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml.Linq;
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
        private const string HeaderLocation = "Location";
        private const string HeaderRetryAfter = "Retry-After";
        private const string HeaderXMsRp = "X-MS-RP";
        private const string HeaderXMsLocation = "X-MS-Location";
        private const string HeaderXMsAsThrottle = "X-MS-ASThrottle";
        private const string HeaderXMsCredentialsExpire = "X-MS-Credentials-Expire";
        private const string HeaderXMsCredentialServiceUrl = "X-MS-Credential-Service-Url";
        private const string KXsd = "xsd";
        private const string KCommon = "common";
        private const string KRequest = "request";
        private const string KResponse = "response";
        private const int KDefaultDelaySeconds = 5;
        private const int KDefaultThrottleDelaySeconds = 60;
        private const int KMaxDelaySeconds = 30;
        private const int KMaxTimeoutSeconds = 999;
        private const double KDefaultTimeoutExpander = 1.5;
        private const int KDefaultRetries = 2;
        private const int KConsec401ThenReDisc = 5;
        private const string KLoadBytes = "LoadBytes";
        private const string KToWbxmlStream = "ToWbxmlStream";
        private string CommandName;

        private static object LockObj = new object ();

        private IBEContext BEContext;

        protected int AccountId { get; set; }

        private IAsHttpOperationOwner Owner;
        private CancellationTokenSource Cts;
        private NcTimer DelayTimer;
        private NcTimer TimeoutTimer;
        private NcStateMachine HttpOpSm;
        private NcStateMachine OwnerSm;
        private Uri ServerUri;
        private string RedactedServerUri;
        private bool ServerUriBeingTested;
        private string ContentType;
        private uint ConsecThrottlePriorDelaySecs;
        private bool ExtraRetry451Consumed;

        // Properties.
        // User for mocking.
        public INcCommStatus NcCommStatusSingleton { set; get; }

        public double TimeoutExpander { set; get; }

        public uint MaxRetries { set; get; }
        // Numer of times we'll try again (remaining).
        public uint TriesLeft { set; get; }

        public bool Allow451Follow { set; get; }

        public bool DontReportCommResult { set; get; }

        string CmdNameWithAccount;

        public AsHttpOperation (string commandName, IAsHttpOperationOwner owner, IBEContext beContext)
        {
            NcCapture.AddKind (KLoadBytes);
            NcCapture.AddKind (KToWbxmlStream);
            NcCommStatusSingleton = NcCommStatus.Instance;
            BEContext = beContext;
            AccountId = BEContext.Account.Id;
            TimeoutExpander = KDefaultTimeoutExpander;
            MaxRetries = KDefaultRetries;
            TriesLeft = MaxRetries + 1;
            Allow451Follow = true;
            CommandName = commandName;
            Owner = owner;
            CmdNameWithAccount = string.Format ("HTTPOP({0})", AccountId);

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
            RedactedServerUri = string.Format ("{0}:{1}", Owner.Method (this), Owner.ServerUri (this, true).ToString ());
            HttpOpSm.PostEvent ((uint)SmEvt.E.Launch, "HTTPOPEXE");
        }

        public void Cancel ()
        {
            HttpOpSm.PostEvent ((uint)HttpOpEvt.E.Cancel, "HTTPOPCANCEL");
        }

        private void DoDelay ()
        {
            CancelTimeoutTimer ("DoDelay");
            var secs = Convert.ToInt32 (HttpOpSm.Arg);
            Log.Info (Log.LOG_HTTP, "AsHttpOperation({0}):Delay {1} seconds.", AccountId, secs);
            DelayTimer = new NcTimer ("AsHttpOperation:Delay", DelayTimerCallback, null, secs * 1000, System.Threading.Timeout.Infinite);
        }

        private void DoCancelDelayTimer ()
        {
            if (null != DelayTimer) {
                DelayTimer.Dispose ();
                DelayTimer = null;
            }
        }

        private void DoHttp ()
        {
            if (0 < TriesLeft) {
                --TriesLeft;
                Log.Info (Log.LOG_HTTP, "{0}: TriesLeft: {1}", CmdNameWithAccount, TriesLeft);
                // Using response.Content.ReadAsStreamAsync causes lock-ups sometimes.
                // These lock-ups would cause loss of a thread.
                // We switched back to ReadAsByteArrayAsync to avoid lock-ups. 
                // This will have the side-effect of big responses being memory-resident
                // until NachoHttp comes in.
                Cts = new CancellationTokenSource ();
                AttemptHttp ();
            } else {
                Owner.ResolveAllDeferred ();
                HttpOpSm.PostEvent (Final ((uint)SmEvt.E.TempFail, "ASHTTPDOH", null, "Too many retries."));
            }
        }

        private void DoCancelHttp ()
        {
            CancelTimeoutTimer ("DoCancelHttp");
            DoCancelDelayTimer ();
            if (null != Cts) {
                if (!Cts.IsCancellationRequested) {
                    Cts.Cancel ();
                }
                // Let GC handle the Dispose(). Thread join not worth the pain.
                Cts = null;
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

        private void CancelTimeoutTimer (string mnemonic)
        {
            lock (LockObj) {
                if (null != TimeoutTimer) {
                    Log.Info (Log.LOG_AS, "{0}: CancelTimeoutTimer:{1}", CmdNameWithAccount, mnemonic);
                    TimeoutTimer.Dispose ();
                    TimeoutTimer = null;
                }
            }
        }

        private void TimeoutTimerCallback (object State)
        {
            if (!((CancellationToken)State).IsCancellationRequested) {
                HttpOpSm.PostEvent ((uint)HttpOpEvt.E.Timeout, "ASHTTPTTC", null, string.Format ("Uri: {0}", RedactedServerUri));
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
                NcCommStatusSingleton.ReportCommResult (AccountId, host, didFailGenerally);
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

        public bool CreateHttpRequest (out NcHttpRequest request, CancellationToken cToken)
        {
            request = null;
            XDocument doc;
            if (!Owner.SafeToXDocument (this, out doc)) {
                return false;
            }
            request = new NcHttpRequest (Owner.Method (this), ServerUri);
            if (null != doc) {
                Log.Debug (Log.LOG_XML, "{0}:\n{1}", CmdNameWithAccount, doc);
                if (Owner.UseWbxml (this)) {
                    var diaper = new NcTimer ("AsHttpOperation:ToWbxmlStream diaper", 
                                     (state) => {
                            if (!cToken.IsCancellationRequested) {
                                Log.Error (Log.LOG_HTTP, "{0}:ToWbxmlStream wedged (#1313)", CmdNameWithAccount);
                            }
                        },
                                     cToken, 
                        // We only want to see this Error if truly wedged.
                        // This timer doesn't perform any recovery action.
                                     60 * 1000, 
                                     System.Threading.Timeout.Infinite);
                    var capture = NcCapture.CreateAndStart (KToWbxmlStream);
                    var stream = doc.ToWbxmlStream (AccountId, cToken);
                    capture.Stop ();
                    diaper.Dispose ();
                    request.SetContent (stream, ContentTypeWbxml, true);
                } else {
                    // See http://stackoverflow.com/questions/957124/how-to-print-xml-version-1-0-using-xdocument.
                    // Xamarin bug: this prints out the wrong decl, which breaks autodiscovery. Revert to SO
                    // Method once bug is fixed.
                    var xmlText = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + doc.ToString ();
                    byte[] data = Encoding.UTF8.GetBytes (xmlText);
                    request.SetContent (data, ContentTypeXml);
                }
            }
            FileStream mime;
            if (!Owner.SafeToMime (this, out mime)) {
                return false;
            }
            if (null != mime) {
                request.SetContent (mime, ContentTypeMail, false);
            }
            request.Headers.Add ("User-Agent", Device.Instance.UserAgent ());
            if (Owner.DoSendPolicyKey (this)) {
                request.Headers.Add ("X-MS-PolicyKey", BEContext.ProtocolState.AsPolicyKey);
            }
            request.Headers.Add ("MS-ASProtocolVersion", BEContext.ProtocolState.AsProtocolVersion);
            return true;
        }

        void AttemptHttp ()
        {
            var cToken = Cts.Token;
            McCred cred = null;

            if (ServerUri.IsHttps ()) {
                // Never send password over unencrypted channel.
                try {
                    BEContext.Account.LogHashedPassword (Log.LOG_HTTP, "AsHttpOperation", BEContext.Cred.GetPassword ());
                    cred = BEContext.Cred;
                } catch (KeychainItemNotFoundException ex) {
                    Log.Error (Log.LOG_AS, "{0}: KeychainItemNotFoundException: {1}", CmdNameWithAccount, ex.Message);
                    HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, "HTTPOPKEYCHAIN", null, string.Format ("KeychainItemNotFoundException: {0}, Uri: {1}", ex.Message, RedactedServerUri));
                    return;
                }
            }

            // TimeoutTimer moved north of CreateHttpRequest because of #1313 lockup problem.
            var baseTimeout = Owner.TimeoutInSeconds;
            if (0.0 == baseTimeout) {
                baseTimeout = ((AsProtoControl)BEContext.ProtoControl).Strategy.DefaultTimeoutSecs;
            }
            var timeoutValue = TimeSpan.FromSeconds (baseTimeout * Math.Pow (TimeoutExpander, MaxRetries - TriesLeft));
            TimeoutTimer = new NcTimer ("AsHttpOperation:Timeout", TimeoutTimerCallback, cToken, timeoutValue,
                System.Threading.Timeout.InfiniteTimeSpan);
            NcHttpRequest request = null;
            if (!CreateHttpRequest (out request, cToken)) {
                Log.Info (Log.LOG_HTTP, "{0}: Intentionally aborting HTTP operation.", CmdNameWithAccount);
                CancelTimeoutTimer ("Intentional");
                HttpOpSm.PostEvent (Final ((uint)SmEvt.E.HardFail, "HTTPOPNOCON"));
                return;
            }
            request.Cred = cred; // NB: can be null

            ServicePointManager.FindServicePoint (request.RequestUri).ConnectionLimit = 25;
            Log.Info (Log.LOG_HTTP, "{0}:URL:{1}", CmdNameWithAccount, RedactedServerUri);
            ServerCertificatePeek.ServerCertificateError failedInfo;
            ServerCertificatePeek.Instance.FailedCertificates.TryRemove (ServerUri.Host, out failedInfo);
            BEContext.ProtoControl.HttpClient.SendRequest (request, (int)baseTimeout, AttemptHttpSuccess, AttemptHttpError, cToken);
        }

        void AttemptHttpSuccess (NcHttpResponse response, CancellationToken token)
        {
            if (token.IsCancellationRequested) {
                return;
            }

            MimeKit.ContentType cType;
            if (!string.IsNullOrEmpty (response.ContentType)) {
                if (MimeKit.ContentType.TryParse (response.ContentType, out cType)) {
                    ContentType = cType.MimeType;
                } else {
                    ContentType = null;
                    Log.Warn (Log.LOG_AS, "Could not parse Content Type {0}", response.ContentType);
                }
            } else {
                ContentType = null;
            }

            if (null == response.Content || !(response.Content is FileStream)) {
                CancelTimeoutTimer ("response.Content");
                Log.Error (Log.LOG_HTTP, "{0}: Unable to get response: {1}, response.ContentType: {2}", CmdNameWithAccount, response.Content, response.ContentType);
                HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, "HTTPOPNRC2");
                return;
            }
            CancelTimeoutTimer ("Success");
            try {
                var evt = ProcessHttpResponse (response, token);
                if (token.IsCancellationRequested) {
                    Log.Info (Log.LOG_HTTP, "{0}: Dropping event because of cancellation: {1}/{2}", CmdNameWithAccount, evt.EventCode, evt.Mnemonic);
                } else {
                    HttpOpSm.PostEvent (evt);
                }
            } catch (Exception ex) {
                Log.Error (Log.LOG_HTTP, "{0}: {1} {2}: exception {3}\n{4}", CmdNameWithAccount, ex, RedactedServerUri, ex.Message, ex.StackTrace);
                // Likely a bug in our code if we got here, but likely to get stuck here again unless we resolve-as-failed.
                Owner.ResolveAllFailed (NcResult.WhyEnum.Unknown);
                HttpOpSm.PostEvent (Final ((uint)SmEvt.E.HardFail, "HTTPOPPHREX", null, string.Format ("Exception in ProcessHttpResponse: {0}", ex.Message)));
                return;
            }
        }

        void AttemptHttpError (Exception ex, CancellationToken cToken)
        {
            ServerCertificatePeek.ServerCertificateError failedInfo;
            if (ServerCertificatePeek.Instance.FailedCertificates.TryGetValue (ServerUri.Host, out failedInfo)) {
                ServerCertificatePeek.LogCertificateChainErrors (failedInfo, string.Format ("{0}: AttemptHttp({1}): Cert Validation Error for {2}", CmdNameWithAccount, ex.GetType ().Name, ServerUri.Host));
            }
            if (ex is OperationCanceledException) {
                Log.Info (Log.LOG_HTTP, "{0}: AttemptHttp OperationCanceledException {1}: exception {2}", CmdNameWithAccount, RedactedServerUri, ex.Message);
                CancelTimeoutTimer ("OperationCanceledException");
                if (!cToken.IsCancellationRequested) {
                    // See http://stackoverflow.com/questions/12666922/distinguish-timeout-from-user-cancellation
                    ReportCommResult (ServerUri.Host, true);
                    HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, "HTTPOPTO", null, string.Format ("Timeout, Uri: {0}", RedactedServerUri));
                }
            } else if (ex is WebException) {
                var redactedMessage = HashHelper.HashUserInASUrl (ex.Message);
                redactedMessage = Log.ReplaceFormatting (redactedMessage);
                Log.Info (Log.LOG_HTTP, "{0}: AttemptHttp WebException {1}: exception {2}", CmdNameWithAccount, RedactedServerUri, redactedMessage);
                if (!cToken.IsCancellationRequested) {
                    CancelTimeoutTimer ("WebException");
                    ReportCommResult (ServerUri.Host, true);
                    // Some of the causes of WebException could be better characterized as HardFail. Not dividing now.
                    // TODO: I have seen an expired server cert get us here. We need to catch that case specifically, and alert user/admin.
                    HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, "HTTPOPWEBEX", null, string.Format ("WebException: {0}, Uri: {1}", redactedMessage, RedactedServerUri));
                }
            } else if (ex is NullReferenceException) {
                Log.Info (Log.LOG_HTTP, "{0}: AttemptHttp NullReferenceException {1}: exception {2}", CmdNameWithAccount, RedactedServerUri, ex.Message);
                // As best I can tell, this may be driven by bug(s) in the Mono stack.
                if (!cToken.IsCancellationRequested) {
                    CancelTimeoutTimer ("NullReferenceException");
                    HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, "HTTPOPTO", null, string.Format ("Timeout, Uri: {0}", RedactedServerUri));
                }
            } else {
                if (!cToken.IsCancellationRequested) {
                    CancelTimeoutTimer ("Exception");
                    Log.Error (Log.LOG_HTTP, "{0}: Exception: {1}", CmdNameWithAccount, ex.ToString ());
                    HttpOpSm.PostEvent ((uint)SmEvt.E.TempFail, "HTTPOPFU", null, string.Format ("E, Uri: {0}", RedactedServerUri));
                } else {
                    Log.Error (Log.LOG_HTTP, "{0}: HTTPClient Exception due to cancellation! {1} {2}", CmdNameWithAccount, RedactedServerUri, ex.Message);
                }
            }
        }

        private bool Is2xx (HttpStatusCode statusCode)
        {
            return (HttpStatusCode.OK <= statusCode && HttpStatusCode.MultipleChoices > statusCode);
        }

        private Event ProcessHttpResponse (NcHttpResponse response, CancellationToken cToken)
        {
            var ContentData = response.Content as FileStream;
            NcAssert.NotNull (ContentData);
            if (!Is2xx (response.StatusCode) && ContentTypeHtml == ContentType) {
                // There is a chance that the non-OK status comes with an HTML explaination.
                // If so, then dump it.
                // TODO: find some way to make cancellation token work here.
                var possibleMessage = File.ReadAllText (ContentData.Name);
                Log.Info (Log.LOG_HTTP, "{0}: HTML response: {1}", CmdNameWithAccount, possibleMessage);
            }
            Event preProcessEvent = Owner.PreProcessResponse (this, response);
            if (null != preProcessEvent) {
                // If the owner is returning an event, they MUST have resolved all pendings.
                return Final (preProcessEvent);
            }
            XDocument responseDoc = null;
            if (HttpStatusCode.ServiceUnavailable != response.StatusCode) {
                ConsecThrottlePriorDelaySecs = 0;
            }
            if (HttpStatusCode.Unauthorized != response.StatusCode) {
                var protocolState = BEContext.ProtocolState;
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.Consec401Count = 0;
                    return true;
                });
            }
            if (Is2xx (response.StatusCode)) {
                // 2xx is "This class of status code indicates that the client's request was successfully received, understood, and accepted."
                ReportCommResult (ServerUri.Host, false);
                IndicateUriIfChanged ();
                var credDaysLeft = -1;
                Uri credUri = null;
                if (response.Headers.Contains (HeaderXMsCredentialsExpire)) {
                    var daysString = response.Headers.GetValues (HeaderXMsCredentialsExpire).First ();
                    try {
                        credDaysLeft = int.Parse (daysString);
                    } catch {
                        Log.Error (Log.LOG_AS, "{0}: HttpStatusCode.200 with days left: {1}", CmdNameWithAccount, daysString);
                    }
                }
                if (response.Headers.Contains (HeaderXMsCredentialServiceUrl)) {
                    var urlString = response.Headers.GetValues (HeaderXMsCredentialServiceUrl).First ();
                    try {
                        credUri = new Uri (urlString);
                    } catch {
                        Log.Error (Log.LOG_AS, "{0}: HttpStatusCode.200 with credential URL: {1}", CmdNameWithAccount, urlString);
                    }
                }
                if (0 <= credDaysLeft || null != credUri) {
                    var cred = BEContext.Cred;
                    cred.Expiry = DateTime.UtcNow.AddDays (credDaysLeft);
                    if (null != credUri) {
                        cred.RectificationUrl = credUri.ToString ();
                    }
                    if (0 < cred.AccountId) {
                        // If the UI is doing a validate-config, this McCred may not actually be in the DB.
                        cred.Update ();
                    }
                    var result = NcResult.Error (NcResult.SubKindEnum.Error_PasswordWillExpire);
                    result.Value = new Tuple<int,Uri> (credDaysLeft, credUri);
                    Owner.StatusInd (result);
                }
                if (Owner.IgnoreBody (this)) {
                    if (response.HasBody) {
                        Log.Warn (Log.LOG_HTTP, "{0}: Ignored HTTP Body: ContentType: {1}", CmdNameWithAccount, ContentType);
                        try {
                            Log.Warn (Log.LOG_HTTP, "{0}: Ignored HTTP Body: Length: {0}", CmdNameWithAccount, ContentData.Length);
                        } catch (NotSupportedException) {
                            // FIXME - address Length property support in ModernHttpClient.
                        }
                    }
                } else if (response.HasBody) {
                    switch (ContentType) {
                    case ContentTypeWbxml:
                        var decoder = new ASWBXML (cToken);
                        try {
                            long loadBytesDuration;
                            using (var capture = NcCapture.CreateAndStart (KLoadBytes)) {
                                decoder.LoadBytes (AccountId, ContentData);
                                loadBytesDuration = capture.ElapsedMilliseconds;
                            }
                            if (1000 < loadBytesDuration) {
                                Log.Warn (Log.LOG_HTTP, "{0}: LoadBytes took {1:n0}ms for {2:n0} bytes for command {3}",
                                    CmdNameWithAccount, loadBytesDuration, response.ContentLength, CommandName);
                            }
                            cToken.ThrowIfCancellationRequested ();
                        } catch (OperationCanceledException) {
                            Owner.ResolveAllDeferred ();
                            return Final ((uint)SmEvt.E.HardFail, "WBXCANCEL");
                        } catch (WBXMLReadPastEndException ex) {
                            if (ex.BytesRead == 0) {
                                // nothing was read, i.e. HasBody was wrong.
                                return Final (Owner.ProcessResponse (this, response, cToken));
                            } else {
                                // We are deferring because we think that an invalid WBXML string is likely transient.
                                Owner.ResolveAllDeferred ();
                                return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOPRDPEND");
                            }
                        } catch (InvalidDataException) {
                            Owner.ResolveAllDeferred ();
                            return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOPRDPEND2");
                        } catch (WebException) {
                            return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOPRDPEND3");
                        } catch (Exception ex) {
                            // We just don't have a catalog of all the "valid" ways we can fail due
                            // to network errors. Log as Error so we can see anything that looks like a bug.
                            Log.Error (Log.LOG_HTTP, "{0}: Unanticipated Exception: {1}", CmdNameWithAccount, ex.ToString ());
                            return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOPRDPEND4");
                        }
                        responseDoc = decoder.XmlDoc;
                        Log.Debug (Log.LOG_XML, "{0}:{1} response:\n{1}", CmdNameWithAccount, CommandName, responseDoc);
                        var xmlStatus = responseDoc.Root.ElementAnyNs (Xml.AirSync.Status);
                        if (null != xmlStatus) {
                            // TODO - push TL status into pending.
                            var statusEvent = Owner.ProcessTopLevelStatus (this, uint.Parse (xmlStatus.Value), responseDoc);
                            if (null != statusEvent) {
                                Log.Info (Log.LOG_AS, "{0}: Top-level XML Status {1}:{2}", CmdNameWithAccount, xmlStatus.Value, statusEvent);
                                // If Owner is returning an event, then Owner MUST resolve all pending.
                                return Final (statusEvent);
                            }
                        }
                        Log.Debug (Log.LOG_XML, "{0} response:\n{1}", CommandName, responseDoc);
                        // Owner MUST resolve all pending.
                        return Final (Owner.ProcessResponse (this, response, responseDoc, cToken));
                    case ContentTypeWbxmlMultipart:
                        NcAssert.True (false, "ContentTypeWbxmlMultipart unimplemented.");
                        return null;
                    case ContentTypeXml:
                        Log.Info (Log.LOG_HTTP, "{0}: Attempting to use tempfile {1}", CmdNameWithAccount, ContentData.Name);
                        responseDoc = XDocument.Load (ContentData.Name);
                        // Owner MUST resolve all pending.
                        return Final (Owner.ProcessResponse (this, response, responseDoc, cToken));
                    default:
                        if (null == ContentType) {
                            Log.Warn (Log.LOG_HTTP, "{0}: ProcessHttpResponse: received HTTP response with content but no Content-Type.", CmdNameWithAccount);
                        } else {
                            Log.Warn (Log.LOG_HTTP, "{0}: ProcessHttpResponse: received HTTP response with content but unexpected Content-Type: {1}.", CmdNameWithAccount, ContentType);
                        }
                        // Just *try* to see if it will parse as XML. Could be poorly configured auto-d.
                        try {
                            responseDoc = XDocument.Load (ContentData.Name);
                        } catch {
                        }
                        if (null == responseDoc) {
                            // Owner MUST resolve all pending.
                            return Final (Owner.ProcessResponse (this, response, cToken));
                        } else {
                            return Final (Owner.ProcessResponse (this, response, responseDoc, cToken));
                        }
                    }
                } 
                // Owner MUST resolve all pending.
                return Final (Owner.ProcessResponse (this, response, cToken));

            }
            switch (response.StatusCode) {
            // NOTE: ALWAYS resolve pending on Final, and ONLY resolve pending on Final.
            // DO NOT resolve pending on Event.Create!
            // All these cases are 300 and up.

            case HttpStatusCode.Found:
                ReportCommResult (ServerUri.Host, false);
                Owner.ResolveAllDeferred ();
                if (response.Headers.Contains (HeaderLocation)) {
                    var redirUri = new Uri (response.Headers.GetValues (HeaderLocation).First ());
                    if (!redirUri.IsHttps ()) {
                        // Don't be tricked into accepting a non-HTTPS URI.
                        return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOP302NOSSL");
                    }
                    if (redirUri.Query.Contains ("TARGET")) {
                        // We can bet this is SAML.
                        var cred = BEContext.Cred;
                        cred.CredType = McCred.CredTypeEnum.SAML;
                        cred.RedirectUrl = redirUri.ToString ();
                        cred.DoneSubstring = BEContext.Server.Path;
                        return Final ((uint)AsProtoControl.AsEvt.E.AuthFail, "HTTPOP302SAML");
                    }
                }
                if (response.Headers.Contains (HeaderXMsRp)) {
                    Log.Warn (Log.LOG_AS, "{0}: HTTP Status 302 with X-MS-RP", CmdNameWithAccount);
                    McFolder.UpdateResetSyncState (AccountId);
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
                /*
                 * Exchange Online defines two new HTTP status codes that provide more specific reasons for request
                 * failures caused by authentication issues. However, these status codes are not returned in Exchange
                 * ActiveSync responses, they are only returned in Autodiscover responses. We recommend that Exchange
                 * ActiveSync clients that receive repeated back-to-back 401 Unauthorized responses send an Autodiscover 
                 * request and check for the HTTP status codes in the following table. Status Code Description 456 The
                 * user's account is blocked. The client should stop sending requests to the server and should prompt 
                 * the user to contact their administrator. 457 The user's password is expired. The client should stop
                 * sending requests to the server and should prompt the user to update their password. 
                 * If the X-MS-Credential-Service-Url header is present in the response, the client should direct the 
                 * user to the URL contained in the header.
                 */
                var protocolState = BEContext.ProtocolState;
                if (protocolState.LastAutoDSucceeded &&
                    KConsec401ThenReDisc < protocolState.Consec401Count + 1) {
                    protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.Consec401Count = 0;
                        return true;
                    });
                    return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOP401MAX");
                } else {
                    protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.Consec401Count++;
                        return true;
                    });
                    return Final ((uint)AsProtoControl.AsEvt.E.AuthFail, "HTTPOP401");
                }
            case HttpStatusCode.Forbidden:
                ReportCommResult (ServerUri.Host, false); // Non-general failure.
                if (response.Headers.Contains (HeaderXMsRp)) {
                    Log.Warn (Log.LOG_AS, "{0}: HTTP Status 403 with X-MS-RP", CmdNameWithAccount);
                    McFolder.UpdateResetSyncState (AccountId);
                    // Per MS-ASHTTP 3.2.5.1, we should look for OPTIONS headers. If they are missing, okay.
                    AsOptionsCommand.ProcessOptionsHeaders (response.Headers, BEContext);
                    IndicateUriIfChanged ();
                }
                // We are following the (iffy) auto-d directive, but failing pending to avoid possible loop.
                Owner.ResolveAllFailed (NcResult.WhyEnum.AccessDeniedOrBlocked);
                // If we get a 403 on a Provision from GFE, this is them saying that EAS isn't paid-for.
                if (BEContext.Server.HostIsAsGMail () && (null != Owner as AsProvisionCommand)) {
                    BEContext.ProtoControl.AutoDInfo = AutoDInfoEnum.GoogleForbids;
                }
                return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOP403F");

            case HttpStatusCode.NotFound:
                ReportCommResult (ServerUri.Host, false); // Non-general failure.
                if (Owner.WasAbleToRephrase ()) {
                    return Event.Create ((uint)HttpOpEvt.E.Rephrase, "HTTPOP404R");
                } else {
                    Owner.ResolveAllFailed (NcResult.WhyEnum.MissingOnServer);
                    return Final ((uint)SmEvt.E.HardFail, "HTTPOP404F", (int?)HttpStatusCode.NotFound, "HttpStatusCode.NotFound");
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
                if (!ExtraRetry451Consumed) {
                    ++TriesLeft;
                    ExtraRetry451Consumed = true;
                }
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
                        var dummy = McServer.Create (AccountId, McAccount.ActiveSyncCapabilities, redirUri);
                        var query = (string.Empty == redirUri.Query) ? ServerUri.Query : redirUri.Query;
                        ServerUri = new Uri (dummy.BaseUri (), query);
                        RedactedServerUri = string.Format ("{0}:{1}", Owner.Method (this), HashHelper.HashUserInASUrl (ServerUri.ToString ()));
                        return Event.Create ((uint)SmEvt.E.Launch, "HTTPOP451C");
                    } catch (Exception ex) {
                        Log.Info (Log.LOG_HTTP, "ProcessHttpResponse {0} {1}: exception {2}", ex, RedactedServerUri, ex.Message);
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
                var result = NcResult.Error (NcResult.SubKindEnum.Error_AuthFailPasswordExpired);
                if (response.Headers.Contains (HeaderXMsCredentialServiceUrl)) {
                    Uri credUri = null;
                    var urlString = response.Headers.GetValues (HeaderXMsCredentialServiceUrl).First ();
                    try {
                        credUri = new Uri (urlString);
                        result.Value = credUri;
                    } catch {
                        Log.Error (Log.LOG_AS, "{0}: HttpStatusCode.457 with credential URL: {1}", CmdNameWithAccount, urlString);
                    }
                }
                Owner.StatusInd (result);
                return Event.Create ((uint)SmEvt.E.HardFail, "HTTPOP457");

            case HttpStatusCode.InternalServerError:
                ReportCommResult (ServerUri.Host, false); // Non-general failure.
                if (response.Headers.Contains (HeaderXMsRp)) {
                    Log.Warn (Log.LOG_AS, "{0}: HTTP Status 500 with X-MS-RP", CmdNameWithAccount);
                    McFolder.UpdateResetSyncState (AccountId);
                    // Per MS-ASHTTP 3.2.5.1, we should look for OPTIONS headers. If they are missing, okay.
                    AsOptionsCommand.ProcessOptionsHeaders (response.Headers, BEContext);
                    IndicateUriIfChanged ();
                }
                // We are following the (iffy) auto-d directive, but failing pending to avoid possible loop.
                Owner.ResolveAllFailed (NcResult.WhyEnum.AccessDeniedOrBlocked);
                // if the mail server host is well-known (e.g google.com, hotmail.com) , do not do ReDiscovery.
                if (BEContext.Server.AsHostIsWellKnown ()) {
                    return Final ((uint)SmEvt.E.TempFail, "HTTPOP500A");
                } else {
                    return Final ((uint)AsProtoControl.AsEvt.E.ReDisc, "HTTPOP500B");
                }

            case (HttpStatusCode)501:
                ReportCommResult (ServerUri.Host, false);
                Owner.ResolveAllDeferred ();
                return Final ((uint)SmEvt.E.HardFail, "HTTPOP501", null, "HttpStatusCode 501 - Command not implemented.");

            case HttpStatusCode.BadGateway:
                ReportCommResult (ServerUri.Host, true);
                return Event.Create ((uint)SmEvt.E.TempFail, "HTTPOP502", null, "HttpStatusCode.BadGateway");

            case HttpStatusCode.ServiceUnavailable:
                ReportCommResult (ServerUri.Host, true);
                uint bestSecs, configuredSecs;
                string value = null;
                if (response.Headers.Contains (HeaderXMsAsThrottle)) {
                    Log.Error (Log.LOG_HTTP, "{0}: Explicit throttling ({1}).", CmdNameWithAccount, HeaderXMsAsThrottle);
                    protocolState = BEContext.ProtocolState;
                    protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.HasBeenRateLimited = true;
                        return true;
                    });
                    try {
                        value = response.Headers.GetValues (HeaderXMsAsThrottle).First ();
                        protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                            var target = (McProtocolState)record;
                            target.SetAsThrottleReason (value);
                            return true;
                        });
                    } catch {
                        Log.Error (Log.LOG_HTTP, "{0}: Could not parse header {1}: {2}.", CmdNameWithAccount, HeaderXMsAsThrottle, value);
                    }

                    Owner.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ExplicitThrottling));
                    configuredSecs = (uint)McMutables.GetOrCreateInt (AccountId, "HTTP", "ThrottleDelaySeconds", KDefaultThrottleDelaySeconds);
                } else {
                    configuredSecs = (uint)McMutables.GetOrCreateInt (AccountId, "HTTP", "DelaySeconds", KDefaultDelaySeconds);
                }
                bestSecs = configuredSecs;
                if (response.Headers.Contains (HeaderRetryAfter)) {
                    try {
                        value = response.Headers.GetValues (HeaderRetryAfter).First ();
                        bestSecs = (uint)double.Parse (value, CultureInfo.InvariantCulture);
                    } catch {
                        try {
                            var when = DateTime.Parse (value);
                            var maybe_secs = when.Subtract (DateTime.UtcNow).Seconds;
                            bestSecs = ((maybe_secs > 0) ? (uint)maybe_secs : configuredSecs);
                        } catch (Exception ex) {
                            Log.Info (Log.LOG_HTTP, "{0}: Rejected DateTime string: {1}", CmdNameWithAccount, value);
                            Log.Info (Log.LOG_HTTP, "{0}: ProcessHttpResponse {1} {2}: exception {3}", CmdNameWithAccount, ex, RedactedServerUri, ex.Message);
                            return DelayOrFinalHardFail (bestSecs, "HTTPOP503A", "Could not parse Retry-After value.");
                        }
                    }
                    return DelayOrFinalHardFail (bestSecs, "HTTPOP503B", HeaderRetryAfter);
                } else {
                    if (0 != ConsecThrottlePriorDelaySecs) {
                        bestSecs = 2 * ConsecThrottlePriorDelaySecs;
                    }
                    ConsecThrottlePriorDelaySecs = bestSecs;
                    return DelayOrFinalHardFail (bestSecs, "HTTPOP503C", "HttpStatusCode.ServiceUnavailable");
                }

            case (HttpStatusCode)505:
                // This has been seen to be caused by a mis-typed MS-XX header name.
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

        private Event DelayOrFinalHardFail (uint secs, string mnemonic, string message)
        {
            var result = NcResult.Info (NcResult.SubKindEnum.Info_ServiceUnavailable);
            result.Value = secs;
            Owner.StatusInd (result);
            uint maxSecs = (uint)McMutables.GetOrCreateInt (AccountId, "HTTP", "MaxDelaySeconds", KMaxDelaySeconds);
            if (maxSecs >= secs) {
                return Event.Create ((uint)HttpOpEvt.E.Delay, mnemonic, secs, message);
            }
            Log.Info (Log.LOG_AS, "{0}: Excessive delay requested by server: {1} seconds.", CmdNameWithAccount, secs);
            NcCommStatusSingleton.ReportCommResult (AccountId, ServerUri.Host, DateTime.UtcNow.AddSeconds (secs));
            return Final ((uint)SmEvt.E.HardFail, mnemonic, null, message);
        }
    }
}
