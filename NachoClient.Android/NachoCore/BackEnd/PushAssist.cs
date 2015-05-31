﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Http;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using ModernHttpClient;
using Newtonsoft.Json;
using NachoCore.Utils;
using NachoCore.Model;
using NachoClient.Build;

/* INTEGRATION NOTES (JAN/HENRY)
 * PushAssist.cs is to be platform-independent and protocol-independent (AS or IMAP or ...)
 * iOS platform code uses status-ind to "call" SetDeviceToken()/ResetDeviceToken().
 * There can be one PushAssist object per ProtoControl object. ONE PER ACCOUNT. 
 *      remember one app could end up having sessions with N different pinger servers (on-prem, etc).
 * ProtoControl allocates/frees and maintains the reference to the PushAssist object.
 * ProtoControl Execute()s PA once we are at Ping.
 * Although Client token currently == AWS Cognito Id, we must keep AWS-isms out of this code. Think on-prem too.
 * JEFF_TODO - really want to enable/disable based on narrow-ping. how to do that?
 * JEFF_TODO - exactly how to we "cancel" rather than hold-off the pinger? (let it die).
 */

namespace NachoCore
{
    public delegate void NotificationFetchFunc (int accountId);

    public class PushAssistHttpResult
    {
        public HttpResponseMessage Response;
        public Exception Exception;
    }

    public class PushAssist : IDisposable
    {
        public static Type HttpClientType = typeof(MockableHttpClient);
        public static int IncrementalDelayMsec = 500;
        public static int MinDelayMsec = 5000;
        public static int MaxDelayMsec = 15000;
        public static int MaxTimeoutMsec = 10000;
        public static int DeferPeriodMsec = 30 * 1000;
        protected static string DeviceToken;

        protected IPushAssistOwner Owner;
        protected NcStateMachine Sm;
        protected IHttpClient Client;

        private int AccountId {
            get {
                if ((null == Owner) || (null == Owner.Account)) {
                    return 0;
                }
                return Owner.Account.Id;
            }
        }

        private bool IsDisposed;
        private string ClientContext;
        protected string SessionToken;
        protected int NumRetries;
        private object LockObj;
        // This is the first 8 bytes of SHA-256 hash of the session token;
        protected string DebugSessionToken;

        private static ConcurrentDictionary <string, WeakReference> ContextObjectMap =
            new ConcurrentDictionary <string, WeakReference> ();

        public static string PingerHostName = BuildInfo.PingerHostname;

        public const int ApiVersion = 1;

        protected NcTimer RetryTimer;
        protected NcTimer TimeoutTimer;
        protected NcTimer DeferTimer;
        protected CancellationTokenSource Cts;

        private string BaseUrl {
            get {
                return String.Format ("https://{0}/{1}", PingerHostName, ApiVersion);
            }
        }

        protected string StartSessionUrl {
            get {
                return BaseUrl + "/register";
            }
        }

        protected string DeferSessionUrl {
            get {
                return BaseUrl + "/defer";
            }
        }

        protected string StopSessionUrl {
            get {
                return BaseUrl + "/stop";
            }
        }

        protected int RetryDelayMsec {
            get {
                // Linear backoff with clipping
                int delay = (MinDelayMsec + (NumRetries * IncrementalDelayMsec));
                return (MaxDelayMsec > delay ? delay : MaxDelayMsec);
            }
        }

        public enum Lst : uint
        {
            // We start out waiting for the device token from iOS (app registration process).
            DevTokW = (St.Last + 1),
            // We then check for and possibly wait for the AWS Cognitio Identity Id.
            CliTokW,
            // We then wait for establishment of a session with the Nacho pinger server.
            SessTokW,
            // We are finally active. We push-back the Nacho pinger server until we can't!
            Active,
            // We are capable of being active, but we don't want to be right now.
            Parked,
        };

        public class PAEvt : SmEvt
        {
            new public enum E : uint
            {
                // A device token has arrived.
                DevTok = (SmEvt.E.Last + 1),
                // The device token has been removed.
                DevTokLoss,
                // The AWS Cognito IdentityId has arrived.
                CliTok,
                // The client token has disappeared.
                CliTokLoss,
                // The protocol controller wants us to hold-off the pinger.
                Defer,
                // The protocol controller wants us to stop any current request and future retries but keep
                // pinger (to-server) session alive.
                Park,
                // The protocol controller wants us to stop any current request, future retries and any
                // pinger session.
                Stop,
            };
        }

        public static void Initialize ()
        {
            var identity = new ServerIdentity (new Uri ("https://" + BuildInfo.PingerHostname));
            var pem = System.Text.ASCIIEncoding.ASCII.GetBytes (BuildInfo.PingerCertPem);
            var rootCert = new X509Certificate2 (pem);
            var crlUrls = CertificateHelper.CrlDistributionPoint (rootCert);
            CrlMonitor.Register (crlUrls);
            var policy = new ServerValidationPolicy () {
                PinnedCert = rootCert,
            };
            ServerCertificatePeek.Instance.AddPolicy (identity, policy);
        }

        public static bool ValidatorHack (IHttpWebRequest sender, X509Certificate2 certificate, X509Chain chain, bool result)
        {
            // FIXME - Until we have the cert hierarchy fully verified, just accept pinger cert. 
            Log.Warn (Log.LOG_PUSH, "Blindly accept the certificate. Alpha build only");
            return true;
        }

        public static bool SetDeviceToken (string token)
        {
            if (DeviceToken == token) {
                return false; // no change
            }
            DeviceToken = token;

            // Notify others a new device token is set
            var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_PushAssistDeviceToken);
            result.Value = token;
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = result,
                Account = ConstMcAccount.NotAccountSpecific,
            });
            return true;
        }

        public static PushAssist GetPAObjectByContext (string context)
        {
            WeakReference weakRef;
            if (ContextObjectMap.TryGetValue (context, out weakRef)) {
                return (PushAssist)weakRef.Target;
            }
            return null;
        }

        public static void ProcessRemoteNotification (PingerNotification pinger, NotificationFetchFunc fetch)
        {
            bool ranOnce = false;
            DateTime timestamp;
            // Look for the timestamp and measure the pinger to client latency
            if ((null != pinger.meta) && (pinger.meta.HasTimestamp (out timestamp))) {
                var elapsed = (DateTime.UtcNow - timestamp).TotalSeconds;
                if (300 <= elapsed) {
                    Log.Warn (Log.LOG_PUSH, "Push notification takes {0} seconds to propagate", elapsed);
                } else {
                    Log.Info (Log.LOG_PUSH, "Push notification takes {0} seconds to propagate", elapsed);
                }
            } else {
                Log.Error (Log.LOG_PUSH, "Push notification without metadata or timestamp");
            }
            foreach (var context in pinger.ctxs) {
                // Look up the account
                var pa = GetPAObjectByContext (context.Key);
                if (null == pa) {
                    Log.Warn (Log.LOG_PUSH, "Cannot find account for context {0} for remote notification", context.Key);
                    continue;
                }
                if (0 == pa.AccountId) {
                    Log.Error (Log.LOG_PUSH, "Invalid account for context {0} for remote notification", context.Key);
                    continue;
                }

                // TODO - We don't have multiple account support yet. So, for now, perform fetch always
                //        fetches the one account. In the future, we have to figure out which account
                //        should participate in the fetch and pass them in.
                if (ranOnce) {
                    continue;
                }
                ranOnce = true;

                switch (context.Value.cmd) {
                case PingerContext.NEW:
                case PingerContext.REGISTER:
                    fetch (pa.AccountId);
                    break;
                default:
                    Log.Error (Log.LOG_PUSH, "Unknown action {0} for context {1}", context.Value, context.Key);
                    continue;
                }
            }
        }

        public static void RemovePAObjectByContext (string context)
        {
            WeakReference dummy;
            if (!ContextObjectMap.TryRemove (context, out dummy)) {
                Log.Warn (Log.LOG_PUSH, "Cannot remove unknown context {0}", context);
            }
        }

        public PushAssist (IPushAssistOwner owner)
        {
            LockObj = new object ();
            var handler = new NativeMessageHandler (false, true);
            Client = (IHttpClient)Activator.CreateInstance (HttpClientType, handler, true);
            Owner = owner;
            var account = McAccount.QueryById<McAccount> (AccountId);
            ClientContext = GetClientContext (account);
            // This entry is never freed even if the account is deleted. If the account is
            // recreated, the existing entry will be overwritten. If the account is just 
            // deleted, this entry is orphaned. I am assuming account deletion is rare
            // enough that leaking a few tens of bytes every once a while is ok.
            var paRef = new WeakReference (this);
            if (!ContextObjectMap.TryAdd (ClientContext, paRef)) {
                WeakReference oldPaRef = null;
                var got = ContextObjectMap.TryGetValue (ClientContext, out oldPaRef);
                NcAssert.True (got); // since we don't delete, this can never fail
                var updated = ContextObjectMap.TryUpdate (ClientContext, paRef, oldPaRef);
                NcAssert.True (updated); // since we don't delete, this can never fail
            }

            // Normally, the state transition should be:
            //
            // Start -> DevTokW -> CliTokW -> SessTokW -> Active
            Sm = new NcStateMachine ("PA") {
                Name = string.Format ("PA({0})", AccountId),
                LocalStateType = typeof(Lst),
                LocalEventType = typeof(PAEvt),
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Drop = new [] {
                            (uint)PAEvt.E.DevTok,
                            (uint)PAEvt.E.DevTokLoss,
                            (uint)PAEvt.E.CliTok,
                            (uint)PAEvt.E.CliTokLoss,
                            (uint)PAEvt.E.Defer,
                            (uint)PAEvt.E.Park,
                            (uint)PAEvt.E.Stop,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoGetDevTok, State = (uint)Lst.DevTokW },
                        }
                    },
                    new Node { 
                        State = (uint)Lst.DevTokW,
                        Drop = new [] {
                            (uint)PAEvt.E.CliTok,
                            (uint)PAEvt.E.CliTokLoss,
                            (uint)PAEvt.E.Defer,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoGetDevTok, State = (uint)Lst.DevTokW },
                            new Trans { Event = (uint)PAEvt.E.DevTok, Act = DoGetCliTok, State = (uint)Lst.CliTokW },
                            new Trans {
                                Event = (uint)PAEvt.E.DevTokLoss,
                                Act = DoGetDevTok,
                                State = (uint)Lst.DevTokW
                            },
                            new Trans { Event = (uint)PAEvt.E.Park, Act = DoNop, State = (uint)St.Start },
                            new Trans { Event = (uint)PAEvt.E.Stop, Act = DoNop, State = (uint)St.Start },
                        },
                    },
                    new Node {
                        State = (uint)Lst.CliTokW,
                        Drop = new [] {
                            (uint)PAEvt.E.DevTok,
                            (uint)PAEvt.E.Defer,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoGetCliTok, State = (uint)Lst.CliTokW },
                            new Trans {
                                Event = (uint)PAEvt.E.DevTokLoss,
                                Act = DoGetDevTok,
                                State = (uint)Lst.DevTokW
                            },
                            new Trans {
                                Event = (uint)PAEvt.E.CliTok,
                                Act = DoStartSession,
                                State = (uint)Lst.SessTokW
                            },
                            new Trans {
                                Event = (uint)PAEvt.E.CliTokLoss,
                                Act = DoGetCliTok,
                                State = (uint)Lst.CliTokW
                            },
                            new Trans { Event = (uint)PAEvt.E.Park, Act = DoNop, State = (uint)St.Start },
                            new Trans { Event = (uint)PAEvt.E.Stop, Act = DoNop, State = (uint)St.Start },
                        }
                    },
                    new Node {
                        State = (uint)Lst.SessTokW,
                        Drop = new [] {
                            (uint)PAEvt.E.DevTok,
                            (uint)PAEvt.E.CliTok,
                            (uint)PAEvt.E.Defer,
                        },
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = DoStartSession,
                                State = (uint)Lst.SessTokW
                            },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoActive, State = (uint)Lst.Active },
                            new Trans {
                                Event = (uint)SmEvt.E.TempFail,
                                Act = DoStartSession,
                                State = (uint)Lst.SessTokW
                            },
                            new Trans {
                                Event = (uint)SmEvt.E.HardFail,
                                Act = DoStartSession,
                                State = (uint)Lst.SessTokW
                            },
                            new Trans {
                                Event = (uint)PAEvt.E.DevTokLoss,
                                Act = DoGetDevTok,
                                State = (uint)Lst.DevTokW
                            },
                            new Trans {
                                Event = (uint)PAEvt.E.CliTokLoss,
                                Act = DoGetCliTok,
                                State = (uint)Lst.CliTokW
                            },
                            new Trans { Event = (uint)PAEvt.E.Park, Act = DoPark, State = (uint)St.Start },
                            new Trans { Event = (uint)PAEvt.E.Stop, Act = DoNop, State = (uint)St.Start },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Active,
                        Drop = new [] {
                            (uint)SmEvt.E.Success,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)Lst.Active },
                            new Trans {
                                Event = (uint)PAEvt.E.DevTok,
                                Act = DoStartSession,
                                State = (uint)Lst.SessTokW
                            },
                            new Trans {
                                Event = (uint)PAEvt.E.DevTokLoss,
                                Act = DoGetDevTok,
                                State = (uint)Lst.DevTokW
                            },
                            new Trans {
                                Event = (uint)PAEvt.E.CliTok,
                                Act = DoStartSession,
                                State = (uint)Lst.SessTokW
                            },
                            new Trans {
                                Event = (uint)PAEvt.E.CliTokLoss,
                                Act = DoGetCliTok,
                                State = (uint)Lst.CliTokW
                            },
                            new Trans { Event = (uint)PAEvt.E.Defer, Act = DoDeferSession, State = (uint)Lst.Active },
                            new Trans {
                                Event = (uint)SmEvt.E.TempFail,
                                Act = DoDeferSession,
                                State = (uint)Lst.Active
                            },
                            new Trans {
                                Event = (uint)SmEvt.E.HardFail,
                                Act = DoStartSession,
                                State = (uint)Lst.SessTokW,
                            },
                            new Trans { Event = (uint)PAEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)PAEvt.E.Stop, Act = DoStopSession, State = (uint)St.Start },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Parked,
                        Drop = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)PAEvt.E.Defer,
                            (uint)PAEvt.E.Park,
                        },
                        On = new [] {
                            new Trans {
                                Event = (uint)PAEvt.E.DevTok,
                                Act = DoStartSession,
                                State = (uint)St.Start
                            },
                            new Trans {
                                Event = (uint)PAEvt.E.DevTokLoss,
                                Act = DoGetDevTok,
                                State = (uint)St.Start
                            },
                            new Trans {
                                Event = (uint)PAEvt.E.CliTok,
                                Act = DoStartSession,
                                State = (uint)St.Start
                            },
                            new Trans {
                                Event = (uint)PAEvt.E.CliTokLoss,
                                Act = DoGetCliTok,
                                State = (uint)St.Start
                            },
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoActive, State = (uint)Lst.Active },
                            new Trans { Event = (uint)PAEvt.E.Stop, Act = DoStopSession, State = (uint)St.Start },
                        }
                    }
                }
            };
            Sm.Validate ();
            NcApplication.Instance.StatusIndEvent += StatusIndWatcher;
        }

        protected string GetClientContext (McAccount account)
        {
            string prefix = null;
            string id = null;
            switch (account.AccountType) {
            case McAccount.AccountTypeEnum.Device:
                Log.Error (Log.LOG_PUSH, "GetClientContext: device account should not need pinger");
                prefix = "device";
                id = account.EmailAddr;
                break;
            case McAccount.AccountTypeEnum.Exchange:
                prefix = "exchange";
                id = account.EmailAddr;
                break;
            default:
                Log.Error (Log.LOG_PUSH, "GetClientContext: Unexpected account type {0}", (uint)account.AccountType);
                break;
            }
            return HashHelper.Sha256 (prefix + ":" + id).Substring (0, 8);
        }

        public void Dispose ()
        {
            if (!IsDisposed) {
                IsDisposed = true;
                RemovePAObjectByContext (ClientContext);
                NcApplication.Instance.StatusIndEvent -= StatusIndWatcher;
                DisposeRetryTimer ();
                DisposeTimeoutTimer ();
                DisposeDeferTimer ();
                DisposeCts ();
                Client.Dispose ();
            }
        }

        // PUBLIC API SECTION
        //
        // Four public API:
        // 1. Execute - Start advancing the statet machine.
        // 2. Defer - Delay pigner from pinging. The client should only call this when it is pinging.
        // 3. Park - Stop push assist SM from further communication with pinger. But the last issued start or defer
        //           session is not canceled.
        // 4. Stop - Like Park, but the outstanding pinger session (to server) is canceled as well.
        public void Execute ()
        {
            var account = Owner.Account;
            if (account.FastNotificationEnabled) {
                PostEvent (SmEvt.E.Launch, "PAEXE");
            } else {
                Log.Info (Log.LOG_PUSH, "PA is disabled in account setting (accountId={0})", account.Id);
            }
        }

        public void Defer ()
        {
            PostEvent (PAEvt.E.Defer, "PAHO");
        }

        public void Park ()
        {
            if (!IsActive () && !IsStartOrParked ()) {
                Log.Warn (Log.LOG_PUSH, "A start session is not established before being parked. Notifications will not be pushed.");
            }
            // Cancel any HTTP request to pinger. Otherwise, the task that makes the HTTP request
            // may delay the PA SM from going to Park state immediately.
            if (null != Cts) {
                Cts.Cancel ();
            }
            PostEvent (PAEvt.E.Park, "PAPARK");
        }

        public void Stop ()
        {
            PostEvent (PAEvt.E.Stop, "PASTOP");
        }

        public bool IsActive ()
        {
            return ((uint)Lst.Active == Sm.State);
        }

        public bool IsParked ()
        {
            return ((uint)Lst.Parked == Sm.State);
        }

        public bool IsStart ()
        {
            return ((uint)St.Start == Sm.State);
        }

        public bool IsStartOrParked ()
        {
            return IsStart () || IsParked ();
        }

        private void PostEvent (SmEvt.E evt, string mnemonic)
        {
            Sm.PostEvent ((uint)evt, mnemonic);
        }

        private void PostEvent (PAEvt.E evt, string mnemoic)
        {
            Sm.PostEvent ((uint)evt, mnemoic);
        }

        private void PostTempFail (string mnemonic)
        {
            PostEvent (SmEvt.E.TempFail, mnemonic);
        }

        private void PostHardFail (string mnemonic)
        {
            PostEvent (SmEvt.E.HardFail, mnemonic);
        }

        private void PostSuccess (string mnemonic)
        {
            PostEvent (SmEvt.E.Success, mnemonic);
        }

        private string SafeToBase64 (byte[] bytes)
        {
            if (null == bytes) {
                return null;
            }
            return Convert.ToBase64String (bytes);
        }

        private string ExtractHttpHeaderValue (KeyValuePair<string, IEnumerable<string>> keyValue)
        {
            var valueList = keyValue.Value.ToList ();
            if (0 == valueList.Count) {
                return null;
            }
            if (1 < valueList.Count) {
                Log.Warn (Log.LOG_PUSH, "HTTP header value for {0} has {1} values. Use only first one.", keyValue.Key, valueList.Count);
            }
            return valueList [0];
        }

        private static PingerResponse ParsePingerResponse (string jsonResponse)
        {
            try {
                var response = JsonConvert.DeserializeObject<PingerResponse> (jsonResponse);
                switch (response.Status) {
                case PingerResponse.Ok:
                    if (!String.IsNullOrEmpty (response.Message)) {
                        Log.Info (Log.LOG_PUSH, "ParsePingerResposne: response={0}", response.Message);
                    }
                    break;
                case PingerResponse.Warn:
                    Log.Warn (Log.LOG_PUSH, "ParsePingerResposne: response={0}", response.Message);
                    break;
                case PingerResponse.Error:
                    Log.Error (Log.LOG_PUSH, "ParsePingerResponse: response={0}", response.Message);
                    break;
                default:
                    Log.Error (Log.LOG_PUSH, "ParsePingerResponse: unknown status {0}", response.Status);
                    return null;
                }
                return response;
            } catch (Exception e) {
                Log.Error (Log.LOG_PUSH, "ParsePingerResponse: Fail to parse JSON response (jsonResponse={0}, exception={1})", jsonResponse, e);
                return null;
            }
        }

        private void FillOutIdentInfo (BaseRequest request)
        {
            request.ClientId = NcApplication.Instance.UserId;
            request.DeviceId = NachoPlatform.Device.Instance.Identity ();
            request.ClientContext = ClientContext;
            request.OSVersion = NachoPlatform.Device.Instance.OsVersion ();
            request.AppBuildVersion = NachoClient.Build.BuildInfo.Version;
            request.AppBuildNumber = NachoClient.Build.BuildInfo.BuildNumber;
        }

        private static string GetPlatformName ()
        {
            var platform = NachoPlatform.Device.Instance.OsType ().ToLower ();
            if ("iphone os" == platform) {
                return "ios";
            }
            return platform;
        }

        // ACTION FUNCTIONS FOR STATE MACHINE
        private void DoNop ()
        {
        }

        private void DoGetDevTok ()
        {
            var devTok = DeviceToken;
            if (null != devTok) {
                PostEvent (PAEvt.E.DevTok, "DEV_TOK_FOUND");
            }
        }

        private void DoGetCliTok ()
        {
            var clientId = NcApplication.Instance.ClientId;
            if (null != clientId) {
                NumRetries = 0;
                PostEvent (PAEvt.E.CliTok, "GOT_CLI_TOK");
            }
        }

        private async void DoStartSession ()
        {
            // TODO - maybe turn these to debug logs once ping stablizes??
            Log.Info (Log.LOG_PUSH, "[PA] start session starts: client_id={0}, context={1}",
                NcApplication.Instance.ClientId, ClientContext);
            
            var clientId = NcApplication.Instance.ClientId;
            if (null == clientId) {
                PostEvent (PAEvt.E.CliTokLoss, "START_NO_CLI");
                return;
            }
            var cred = McCred.QueryByAccountId<McCred> (AccountId).FirstOrDefault ();
            if (null == cred) {
                // Yes, the SM is SOL at this point.
                Log.Error (Log.LOG_PUSH, "DoStartSession: No McCred for accountId {0}", AccountId);
                PostHardFail ("START_NO_CRED");
                return;
            }
            var parameters = Owner.PushAssistParameters ();
            if (null == parameters) {
                ScheduleRetry ((uint)SmEvt.E.Launch, "START_NO_PARAMS");
                return;
            }

            Dictionary<string, string> httpHeadersDict = new Dictionary<string, string> ();
            if (null != parameters.RequestHeaders) {
                foreach (var header in parameters.RequestHeaders) {
                    httpHeadersDict.Add (header.Key, ExtractHttpHeaderValue (header));
                }
            }
            if (null != parameters.ContentHeaders) {
                foreach (var header in parameters.ContentHeaders) {
                    httpHeadersDict.Add (header.Key, ExtractHttpHeaderValue (header));
                }
            }
            var jsonRequest = new StartSessionRequest () {
                MailServerUrl = parameters.RequestUrl,
                MailServerCredentials = new Credentials {
                    Username = cred.Username,
                    Password = cred.GetPassword ()
                },
                Protocol = ProtocolToString (parameters.Protocol),
                Platform = GetPlatformName (),
                HttpHeaders = httpHeadersDict,
                RequestData = SafeToBase64 (parameters.RequestData),
                ExpectedReply = SafeToBase64 (parameters.ExpectedResponseData),
                NoChangeReply = SafeToBase64 (parameters.NoChangeResponseData),
                CommandTerminator = SafeToBase64 (parameters.CommandTerminator),
                CommandAcknowledgement = SafeToBase64 (parameters.CommandAcknowledgement),
                ResponseTimeout = parameters.ResponseTimeoutMsec,
                WaitBeforeUse = parameters.WaitBeforeUseMsec,
                PushToken = DeviceToken,
                PushService = NcApplication.Instance.GetPushService (),
            };
            FillOutIdentInfo (jsonRequest);

            var task = DoHttpRequest (StartSessionUrl, jsonRequest, NcTask.Cts.Token);
            if (null == task) {
                return;
            }
            if (null != task.Result.Exception) {
                NumRetries++;
                var ex = task.Result.Exception;
                string mnemonic;
                if (ex is OperationCanceledException) {
                    PostEvent (PAEvt.E.Park, "START_CANCELED");
                    return;
                }
                if (ex is WebException) {
                    mnemonic = "START_NET_RETRY";
                } else if (ex is TimeoutException) {
                    mnemonic = "START_TIMEOUT";
                } else {
                    mnemonic = "START_UNEXPECTED_RETRY";
                }
                ScheduleRetry ((uint)SmEvt.E.Launch, mnemonic);
                return;
            }
            var httpResponse = task.Result.Response;
            if (HttpStatusCode.OK != httpResponse.StatusCode) {
                Log.Warn (Log.LOG_PUSH, "DoStartSession: HTTP failure (statusCode={0}",
                    httpResponse.StatusCode);
                NumRetries++;
                ScheduleRetry ((uint)SmEvt.E.Launch, "START_HTTP_RETRY");
                return;
            }
            var jsonResponse = await httpResponse.Content.ReadAsStringAsync ().ConfigureAwait (false);
            var response = ParsePingerResponse (jsonResponse);
            if (!response.IsOkOrWarn () || String.IsNullOrEmpty (response.Token)) {
                NumRetries++;
                ScheduleRetry ((uint)SmEvt.E.Launch, "START_SESS_RETRY");
            } else {
                SessionToken = response.Token;
                DebugSessionToken = HashHelper.Sha256 (SessionToken).Substring (0, 8);
                ClearRetry ();
                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                    Status = NcResult.Info (NcResult.SubKindEnum.Info_PushAssistArmed),
                    Account = Owner.Account,
                });
                PostSuccess ("START_SESSION_OK");
                Log.Info (Log.LOG_PUSH, "[PA] start session succeeds: client_id={0}, context={1}, token={2}",
                    NcApplication.Instance.ClientId, ClientContext, DebugSessionToken);
            }
        }

        private void DoActive ()
        {
            // Enter Active state. Start periodic defer timer.
            ResetDefer ();
        }

        private async void DoDeferSession ()
        {
            Log.Info (Log.LOG_PUSH, "[PA] defer session starts: client_id={0}, context={1}, token={2}",
                NcApplication.Instance.ClientId, ClientContext, DebugSessionToken);
            
            var clientId = NcApplication.Instance.ClientId;
            var parameters = Owner.PushAssistParameters ();
            if (String.IsNullOrEmpty (clientId) ||
                String.IsNullOrEmpty (ClientContext) ||
                String.IsNullOrEmpty (SessionToken)) {
                Log.Error (Log.LOG_PUSH,
                    "DoDeferSession: missing required parameters (clientId={0}, clientContext={1}, token={2})",
                    clientId, ClientContext, DebugSessionToken);
                PostHardFail ("DEFER_PARAM_ERROR");
            }
            var jsonRequest = new DeferSessionRequest () {
                Token = SessionToken,
                ResponseTimeout = parameters.ResponseTimeoutMsec
            };
            FillOutIdentInfo (jsonRequest);

            var task = DoHttpRequest (DeferSessionUrl, jsonRequest, NcTask.Cts.Token);
            if (null == task) {
                return;
            }
            if (null != task.Result.Exception) {
                NumRetries++;
                var ex = task.Result.Exception;
                string mnemonic;
                if (ex is OperationCanceledException) {
                    PostEvent (PAEvt.E.Park, "DEFER_CANCELED");
                    return;
                }
                if (ex is WebException) {
                    mnemonic = "DEFER_NET_RETRY";
                } else if (ex is TimeoutException) {
                    mnemonic = "DEFER_TIMEOUT";
                } else {
                    mnemonic = "DEFER_UNEXPECTED_RETRY";
                }
                ScheduleRetry ((uint)PAEvt.E.Defer, mnemonic);
                return;
            }
            var httpResponse = task.Result.Response;
            NcAssert.True (null != httpResponse);
            if (HttpStatusCode.OK != httpResponse.StatusCode) {
                Log.Warn (Log.LOG_PUSH, "DoDeferSession: HTTP failure (statusCode={0})",
                    httpResponse.StatusCode);
                NumRetries++;
                ScheduleRetry ((uint)PAEvt.E.Defer, "DEFER_HTTP_RETRY");
                return;
            }

            var jsonResponse = await httpResponse.Content.ReadAsStringAsync ().ConfigureAwait (false);
            var response = ParsePingerResponse (jsonResponse);
            if (response.IsOk ()) {
                ClearRetry ();
                Log.Info (Log.LOG_PUSH, "[PA] defer session ends: client_id={0}, context={1}, token={2}",
                    NcApplication.Instance.ClientId, ClientContext, DebugSessionToken);
                ResetDefer ();
            } else if (response.IsWarn ()) {
                NumRetries++;
                ScheduleRetry ((uint)PAEvt.E.Defer, "DEFER_SESS_RETRY");
            } else {
                NcAssert.True (response.IsError ());
                ClearRetry ();
                ScheduleRetry ((uint)SmEvt.E.HardFail, "DEFER_SESS_ERROR");
            }
        }

        private async void DoStopSession ()
        {
            DisposeDeferTimer ();
            Sm.ClearEventQueue ();
            var clientId = NcApplication.Instance.ClientId;
            if (String.IsNullOrEmpty (clientId) ||
                String.IsNullOrEmpty (ClientContext) ||
                String.IsNullOrEmpty (SessionToken)) {
                Log.Error (Log.LOG_PUSH,
                    "DoStopSession: missing required parameters (clientId={0}, clientContext={1}, token={2})",
                    clientId, ClientContext, DebugSessionToken);
                PostHardFail ("PARAM_ERROR");
            }
            var jsonRequest = new StopSessionRequest () {
                Token = SessionToken,
            };
            FillOutIdentInfo (jsonRequest);

            var task = DoHttpRequest (StopSessionUrl, jsonRequest, NcTask.Cts.Token);
            if (null == task.Result) {
                return;
            }
            if (null != task.Result.Exception) {
                NumRetries++;
                var ex = task.Result.Exception;
                if (ex is WebException) {
                    PostTempFail ("STOP_NET_ERROR");
                } else if (ex is OperationCanceledException) {
                    PostTempFail ("STOP_CANCELED");
                } else if (ex is TimeoutException) {
                    PostTempFail ("STOP_TIMEOUT");
                } else {
                    PostHardFail ("STOP_UNEXPECTED_ERROR");
                }
                return;
            }
            var httpResponse = task.Result.Response;
            NcAssert.True (null != task.Result.Response);
            if (HttpStatusCode.OK != httpResponse.StatusCode) {
                Log.Warn (Log.LOG_PUSH, "DoStopSession: HTTP failure (statusCode={0})",
                    httpResponse.StatusCode);
                PostTempFail ("STOP_HTTP_ERROR");
                return;
            }

            var jsonResponse = await httpResponse.Content.ReadAsStringAsync ().ConfigureAwait (false);
            var response = ParsePingerResponse (jsonResponse);
            if (!response.IsOk ()) {
                PostTempFail ("STOP_SESS_ERROR");
            } else {
                ClearRetry ();
            }
        }

        private void DoPark ()
        {
            ClearRetry ();
            DisposeTimeoutTimer ();
            DisposeDeferTimer ();
        }

        // MISCELLANEOUS STUFF
        private void StatusIndWatcher (object sender, EventArgs ea)
        {
            StatusIndEventArgs siea = (StatusIndEventArgs)ea;
            switch (siea.Status.SubKind) {
            case NcResult.SubKindEnum.Info_PushAssistDeviceToken:
                NcTask.Run (() => {
                    if (null == siea.Status.Value) {
                        // Because we aren't interlocking the DB delete and the SM event, all code
                        // must check device token before using it.
                        PostEvent (PAEvt.E.DevTokLoss, "DEV_TOK_LOSS");
                    } else {
                        PostEvent (PAEvt.E.DevTok, "DEV_TOK_SET");
                    }
                }, "PushAssistDeviceToken");
                break;
            case NcResult.SubKindEnum.Info_FastNotificationChanged:
                if (Owner.Account.Id == siea.Account.Id) {
                    NcTask.Run (() => {
                        if (siea.Account.FastNotificationEnabled) {
                            if (IsStartOrParked ()) {
                                PostEvent (SmEvt.E.Launch, "FAST_NOTIF_ENABLED");
                            } else {
                                Log.Warn (Log.LOG_PUSH, "Got a fast notification enabled status when PA is already running");
                            }
                        } else {
                            PostEvent (PAEvt.E.Stop, "FAST_NOTIF_DISABLED");
                        }
                    }, "PushAssistConfigurationChanged");
                }
                break;
            }
        }

        protected string ProtocolToString (PushAssistProtocol protocol)
        {
            switch (protocol) {
            case PushAssistProtocol.UNKNOWN:
                return "unknown";
            case PushAssistProtocol.ACTIVE_SYNC:
                return "ActiveSync";
            case PushAssistProtocol.IMAP:
                return "IMAP";
            default:
                Log.Error (Log.LOG_PUSH, "Unexpected push assist protocol {0}", (uint)protocol);
                break;
            }
            return null;
        }

        protected async Task<PushAssistHttpResult> DoHttpRequest (string url, object jsonRequest, CancellationToken cToken)
        {
            if (String.IsNullOrEmpty (url)) {
                Log.Error (Log.LOG_PUSH, "null URL");
                return null;
            }
            if (null == jsonRequest) {
                Log.Error (Log.LOG_PUSH, "null json request");
                return null;
            }

            // Set up the request
            HttpRequestMessage request = new HttpRequestMessage (HttpMethod.Post, url);
            Log.Info (Log.LOG_PUSH, "PA request: scheme={0}, url={1}, port={2}, method={3}",
                request.RequestUri.Scheme, request.RequestUri.AbsoluteUri, request.RequestUri.Port, request.Method);

            // Set up the POST content
            try {
                var content = JsonConvert.SerializeObject (jsonRequest);
                request.Content = new StringContent (content);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue ("application/json");
                request.Content.Headers.ContentLength = content.Length;
            } catch (Exception e) {
                Log.Error (Log.LOG_PUSH, "fail to encode push JSON - {0}", e);
                return null;
            }

            // Make the request
            var result = new PushAssistHttpResult ();
            ResetTimeout ();
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource (cToken, Cts.Token)) {
                try {
                    var response = await Client
                        .SendAsync (request, HttpCompletionOption.ResponseContentRead, cts.Token)
                        .ConfigureAwait (false);
                    if (HttpStatusCode.OK == response.StatusCode) {
                        Log.Info (Log.LOG_PUSH, "PA response: statusCode={0}, content={1}", response.StatusCode,
                            await response.Content.ReadAsStringAsync ().ConfigureAwait (false));
                    } else {
                        Log.Warn (Log.LOG_PUSH, "PA response: statusCode={0}", response.StatusCode);
                    }
                    result.Response = response;
                } catch (OperationCanceledException e) {
                    if (cToken.IsCancellationRequested) {
                        DisposeTimeoutTimer ();
                        DisposeRetryTimer ();
                        result.Exception = e;
                        Log.Warn (Log.LOG_PUSH, "DoHttpRequest: canceled");
                    } else if (Cts.Token.IsCancellationRequested) {
                        result.Exception = new TimeoutException ("HTTP operation timed out");
                        Log.Warn (Log.LOG_PUSH, "DoHttpRequest: timed out");
                    } else {
                        result.Exception = e;
                    }
                } catch (WebException e) {
                    result.Exception = e;
                    Log.Warn (Log.LOG_PUSH, "DoHttpRequest: Caught network exception - {0}", e);
                } catch (Exception e) {
                    result.Exception = e;
                    Log.Warn (Log.LOG_PUSH, "DoHttpRequest: Caught unexpected http exception - {0}", e);
                }
            }
            DisposeTimeoutTimer ();
            DisposeCts ();

            return result;
        }

        private void DeviceTokenLost ()
        {
            PostEvent (PAEvt.E.DevTokLoss, "DEV_TOK_LOSS");
        }

        private void ScheduleRetry (uint eventType, string mnemonic)
        {
            lock (LockObj) {
                if (null != RetryTimer) {
                    RetryTimer.Dispose ();
                }
                RetryTimer = new NcTimer ("PARetry", (state) => {
                    Sm.PostEvent (eventType, mnemonic);
                }, null, new TimeSpan (0, 0, 0, 0, RetryDelayMsec), TimeSpan.Zero);
            }
        }

        private void ClearRetry ()
        {
            lock (LockObj) {
                NumRetries = 0;
                DisposeRetryTimer ();
            }
        }

        private void DisposeTimeoutTimer ()
        {
            if (null != TimeoutTimer) {
                TimeoutTimer.Dispose ();
                TimeoutTimer = null;
            }
        }

        private void DisposeRetryTimer ()
        {
            if (null != RetryTimer) {
                RetryTimer.Dispose ();
                RetryTimer = null;
            }
        }

        private void DisposeDeferTimer ()
        {
            if (null != DeferTimer) {
                DeferTimer.Dispose ();
                DeferTimer = null;
            }
        }

        private void DisposeCts ()
        {
            if (null != Cts) {
                Cts.Dispose ();
                Cts = null;
            }
        }

        private void ResetTimeout ()
        {
            DisposeTimeoutTimer ();
            DisposeCts ();
            Cts = new CancellationTokenSource ();
            TimeoutTimer = new NcTimer ("PATimeout", (state) => {
                Cts.Cancel ();
            }, null, new TimeSpan (0, 0, 0, 0, MaxTimeoutMsec), TimeSpan.Zero);
        }

        private void ResetDefer ()
        {
            DisposeDeferTimer ();
            DeferTimer = new NcTimer ("PADefer", (state) => {
                Defer ();
            }, null, DeferPeriodMsec, Timeout.Infinite);
        }
    }
}

