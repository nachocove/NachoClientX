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
using ModernHttpClient;
using Newtonsoft.Json;
using NachoCore.Utils;
using NachoCore.Model;

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

    public class PushAssist : IDisposable
    {
        public static Type HttpClientType = typeof(MockableHttpClient);
        public static int IncrementalDelayMsec = 500;
        public static int MinDelayMsec = 5000;
        public static int MaxDelayMsec = 15000;

        protected IPushAssistOwner Owner;
        protected NcStateMachine Sm;
        protected IHttpClient Client;
        private CookieContainer CookieJar;
        private int AccountId;
        private bool IsDisposed;
        private string ClientContext;
        protected string SessionToken;
        protected int NumRetries;

        protected PushAssistParameters CachedParams;

        private static ConcurrentDictionary <string, WeakReference> ContextObjectMap =
            new ConcurrentDictionary <string, WeakReference> ();

        public static string PingerHostName = "pinger.officetaco.com";
        //public static string PingerHostName = "localhost";

        public const int ApiVersion = 1;

        private string BaseUrl {
            get {
                return String.Format ("https://{0}/{1}", PingerHostName, ApiVersion);
                //return String.Format ("http://{0}:8001/{1}", PingerHostName, ApiVersion);
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

        // JEFF_TODO We are using McMutables for now, consider modifying protocol state instead.
        const string KPushAssist = "pushassist";
        const string KDeviceToken = "devicetoken";
        const string KPushAssistState = "pushassiststate";

        public static PushAssist GetPAObjectByContext (string context)
        {
            WeakReference weakRef;
            if (ContextObjectMap.TryGetValue (context, out weakRef)) {
                if (weakRef.IsAlive) {
                    return (PushAssist)weakRef.Target;
                }
            }
            return null;
        }

        public static void ProcessRemoteNotification (PingerNotification pinger, NotificationFetchFunc fetch)
        {
            foreach (var context in pinger) {
                // Look up the account
                var pa = GetPAObjectByContext (context.Key);
                if (null == pa) {
                    continue;
                }
                if (0 == pa.AccountId) {
                    Log.Error (Log.LOG_PUSH, "Cannot find account for context {0} for remote notification", pa.AccountId);
                    continue;
                }

                switch (pinger.GetAction (context.Value)) {
                case PingerNotificationActionEnum.NEW:
                    fetch (pa.AccountId);
                    break;
                case PingerNotificationActionEnum.REGISTER:
                    pa.DeviceTokenLost ();
                    break;
                }

                // TODO - We don't have multiple account support yet. So, for now, perform fetch always
                //        fetches the one account. In the future, we have to figure out which account
                //        should participate in the fetch and pass them in.
                break;
            }
        }

        public PushAssist (IPushAssistOwner owner)
        {
            // FIXME - we will need to do cert-pinning, and also ensure SSL.
            CookieJar = new CookieContainer ();
            var handler = new NativeMessageHandler (false, true) {
                CookieContainer = CookieJar,
                UseCookies = true,
            };
            Client = (IHttpClient)Activator.CreateInstance (HttpClientType, handler, true);
            Owner = owner;
            AccountId = Owner.Account.Id;
            var account = McAccount.QueryById<McAccount> (AccountId);
            ClientContext = GetClientContext (account);
            // FIXME - This entry is never freed even if the account is deleted. If the account is
            //         recreated, the existing entry will be overwritten. If the account is just 
            //         deleted, this entry is orphaned. I am assuming account deletion is rare
            //         enough that leaking a few tens of bytes every once a while is ok.
            var paRef = new WeakReference (this);
            if (!ContextObjectMap.TryAdd (ClientContext, paRef)) {
                WeakReference oldPaRef = null;
                ContextObjectMap.TryGetValue (ClientContext, out oldPaRef);
                ContextObjectMap.TryUpdate (ClientContext, paRef, oldPaRef);
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
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
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
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoNop, State = (uint)Lst.Active },
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
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
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
                            new Trans { Event = (uint)PAEvt.E.Park, Act = DoPark, State = (uint)Lst.Parked },
                            new Trans { Event = (uint)PAEvt.E.Stop, Act = DoStopSession, State = (uint)St.Start },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Parked,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
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
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)Lst.Active },
                            new Trans { Event = (uint)PAEvt.E.Stop, Act = DoStopSession, State = (uint)St.Start },
                        }
                    }
                }
            };
            NcApplication.Instance.StatusIndEvent += TokensWatcher;
        }

        protected string GetClientContext (McAccount account)
        {
            string prefix = null;
            string id = null;
            switch (account.AccountType) {
            case McAccount.AccountTypeEnum.Device:
                prefix = "device";
                id = account.EmailAddr;
                break;
            case McAccount.AccountTypeEnum.Exchange:
                prefix = "exchange";
                id = account.EmailAddr;
                break;
            }
            return HashHelper.Sha256 (prefix + ":" + id).Substring (0, 8);
        }

        public void Dispose ()
        {
            if (!IsDisposed) {
                IsDisposed = true;
                NcApplication.Instance.StatusIndEvent -= TokensWatcher;
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
            PostEvent (SmEvt.E.Launch, "PAEXE");
        }

        public void Defer ()
        {
            PostEvent (PAEvt.E.Defer, "PAHO");
        }

        public void Park ()
        {
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

        private void PostEvent (SmEvt.E evt, string mnemonic)
        {
            Sm.PostEvent ((uint)evt, mnemonic);
        }

        private void PostEvent (PAEvt.E evt, string mnemoic)
        {
            Sm.PostEvent ((uint)evt, mnemoic);
        }

        private void PostSuccess (string mnemonic)
        {
            PostEvent (SmEvt.E.Success, mnemonic);
        }

        private void PostTempFail (string mnemonic)
        {
            PostEvent (SmEvt.E.TempFail, mnemonic);
        }

        private void PostHardFail (string mnemonic)
        {
            PostEvent (SmEvt.E.HardFail, mnemonic);
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
                    {
                        if (!String.IsNullOrEmpty (response.Message)) {
                            Log.Info (Log.LOG_PUSH, "ParsePingerResposne: response={0}", response.Message);
                        }
                        break;
                    }
                case PingerResponse.Warn:
                    {
                        Log.Warn (Log.LOG_PUSH, "ParsePingerResposne: response={0}", response.Message);
                        break;
                    }
                case PingerResponse.Error:
                    {
                        Log.Error (Log.LOG_PUSH, "ParsePingerResponse: response={0}", response.Message);
                        break;
                    }
                }
                return response;
            } catch (Exception e) {
                Log.Error (Log.LOG_PUSH, "ParsePingerResponse: Fail to parse JSON response (jsonResponse={0}, exception={1})", jsonResponse, e);
                return null;
            }
        }

        // ACTION FUNCTIONS FOR STATE MACHINE
        private void DoNop ()
        {
        }


        private void DoGetDevTok ()
        {
            var devTok = McMutables.Get (McAccount.GetDeviceAccount ().Id, KPushAssist, KDeviceToken);
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
            var clientId = NcApplication.Instance.ClientId;
            if (null == clientId) {
                PostEvent (PAEvt.E.CliTokLoss, "DOSESSCTL");
                return;
            }
            var cred = McCred.QueryByAccountId<McCred> (AccountId).FirstOrDefault ();
            if (null == cred) {
                // Yes, the SM is SOL at this point.
                Log.Error (Log.LOG_PUSH, "DoStartSession: No McCred for accountId {0}", AccountId);
                PostHardFail ("PARAM_ERROR");
                return;
            }
            var parameters = Owner.PushAssistParameters ();
            // FIXME - Figure out why this is not working
            if (null != parameters.RequestData) {
                CachedParams = parameters;
            } else {
                parameters = CachedParams;
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
                ClientId = clientId,
                DeviceId = NachoPlatform.Device.Instance.Identity (),
                ClientContext = ClientContext,
                MailServerUrl = parameters.RequestUrl,
                MailServerCredentials = new Credentials {
                    Username = cred.Username,
                    Password = cred.GetPassword ()
                },
                Protocol = ProtocolToString (parameters.Protocol),
                Platform = NcApplication.Instance.GetPlatformName (),
                HttpHeaders = httpHeadersDict,
                HttpRequestData = SafeToBase64 (parameters.RequestData),
                HttpExpectedReply = SafeToBase64 (parameters.ExpectedResponseData),
                HttpNoChangeReply = SafeToBase64 (parameters.NoChangeResponseData),
                CommandTerminator = SafeToBase64 (parameters.CommandTerminator),
                CommandAcknowledgement = SafeToBase64 (parameters.CommandAcknowledgement),
                ResponseTimeout = parameters.ResponseTimeoutMsec,
                WaitBeforeUse = parameters.WaitBeforeUseMsec,
                PushToken = McMutables.Get (McAccount.GetDeviceAccount ().Id, KPushAssist, KDeviceToken),
                PushService = NcApplication.Instance.GetPushService (),
            };

            try {
                var task = DoHttpRequest (StartSessionUrl, jsonRequest, NcTask.Cts.Token);
                var httpResponse = task.Result;
                if (HttpStatusCode.OK != httpResponse.StatusCode) {
                    Log.Warn (Log.LOG_PUSH, "DoStartSession: HTTP failure (statusCode={0}, content={1})",
                        httpResponse.StatusCode, httpResponse.Content);
                    NumRetries++;
                    PostTempFail ("HTTP_ERROR");
                    return;
                }
                var jsonResponse = await httpResponse.Content.ReadAsStringAsync ().ConfigureAwait (false);
                var response = ParsePingerResponse (jsonResponse);
                if (!response.IsOkOrWarn () || String.IsNullOrEmpty (response.Token)) {
                    NumRetries++;
                    PostTempFail ("START_SESS_ERROR");
                } else {
                    SessionToken = response.Token;
                    PostSuccess ("START_SESS_OK");
                }
            } catch (OperationCanceledException) {
                throw;
            } catch (System.Net.WebException e) {
                Log.Warn (Log.LOG_PUSH, "DoStartSession: Caught network exception - {0}", e);
                NumRetries++;
                PostTempFail ("NET_ERROR");
            } catch (Exception e) {
                Log.Warn (Log.LOG_PUSH, "DoStartSession: Caught unexpected exception - {0}", e);
                NumRetries++;
                PostHardFail ("UNEXPECTED_EX");
            }
        }

        private async void DoDeferSession ()
        {
            var clientId = NcApplication.Instance.ClientId;
            var parameters = Owner.PushAssistParameters ();
            if (String.IsNullOrEmpty (clientId) ||
                String.IsNullOrEmpty (ClientContext) ||
                String.IsNullOrEmpty (SessionToken)) {
                Log.Error (Log.LOG_PUSH,
                    "DoDeferSession: missing required parameters (clientId={0}, clientContext={1}, token={2})",
                    clientId, ClientContext, SessionToken);
                PostHardFail ("PARAM_ERROR");
            }
            var jsonRequest = new DeferSessionRequest () {
                ClientId = clientId,
                DeviceId = NachoPlatform.Device.Instance.Identity (),
                ClientContext = ClientContext,
                Token = SessionToken,
                ResponseTimeout = parameters.ResponseTimeoutMsec
            };

            try {
                var task = DoHttpRequest (DeferSessionUrl, jsonRequest, NcTask.Cts.Token);
                var httpResponse = task.Result;
                if (HttpStatusCode.OK != httpResponse.StatusCode) {
                    Log.Warn (Log.LOG_PUSH, "DoDeferSession: HTTP failure (statusCode={0}, content={1})",
                        httpResponse.StatusCode, httpResponse.Content);
                    NumRetries++;
                    PostTempFail ("HTTP_ERROR");
                    return;
                }

                var jsonResponse = await httpResponse.Content.ReadAsStringAsync ().ConfigureAwait (false);
                var response = ParsePingerResponse (jsonResponse);
                if (!response.IsOk ()) {
                    NumRetries++;
                    PostTempFail ("DEFER_SESS_ERROR");
                } else {
                    PostSuccess ("DEFER_SESS_OK");
                }
            } catch (OperationCanceledException) {
                throw;
            } catch (System.Net.WebException e) {
                Log.Warn (Log.LOG_PUSH, "DoStartSession: Caught network exception - {0}", e);
                NumRetries++;
                PostTempFail ("NET_ERROR");
            } catch (Exception e) {
                Log.Warn (Log.LOG_PUSH, "DoDeferSession: Caught unexpected exception - {0}", e);
                NumRetries++;
                PostHardFail ("UNEXPECTED_ERROR");
            }
        }

        private async void DoStopSession ()
        {
            Sm.ClearEventQueue ();
            var clientId = NcApplication.Instance.ClientId;
            if (String.IsNullOrEmpty (clientId) ||
                String.IsNullOrEmpty (ClientContext) ||
                String.IsNullOrEmpty (SessionToken)) {
                Log.Error (Log.LOG_PUSH,
                    "DoStopSession: missing required parameters (clientId={0}, clientContext={1}, token={2})",
                    clientId, ClientContext, SessionToken);
                PostHardFail ("PARAM_ERROR");
            }
            var jsonRequest = new StopSessionRequest () {
                ClientId = NcApplication.Instance.ClientId,
                DeviceId = NachoPlatform.Device.Instance.Identity (),
                ClientContext = ClientContext,
                Token = SessionToken,
            };

            try {
                var task = DoHttpRequest (StopSessionUrl, jsonRequest, NcTask.Cts.Token);
                var httpResponse = task.Result;
                if (HttpStatusCode.OK != httpResponse.StatusCode) {
                    Log.Warn (Log.LOG_PUSH, "DoStopSession: HTTP failure (statusCode={0}, content={1})",
                        httpResponse.StatusCode, httpResponse.Content);
                    NumRetries++;
                    PostTempFail ("HTTP_ERROR");
                    return;
                }

                var jsonResponse = await httpResponse.Content.ReadAsStringAsync ().ConfigureAwait (false);
                var response = ParsePingerResponse (jsonResponse);
                if (!response.IsOk ()) {
                    NumRetries++;
                    PostTempFail ("STOP_SESS_ERROR");
                }
            } catch (OperationCanceledException) {
                throw;
            } catch (System.Net.WebException e) {
                Log.Warn (Log.LOG_PUSH, "DoStopSession: Caught network exception - {0}", e);
                NumRetries++;
                PostTempFail ("NET_ERROR");
            } catch (Exception e) {
                Log.Warn (Log.LOG_PUSH, "DoStopSession: Caught unexpected http exception - {0}", e);
                NumRetries++;
                PostHardFail ("UNEXPECTED_ERROR");
            }
        }

        private void DoPark ()
        {
            // Do not stop the existing pinger session to server. But do cancel any HTTP request to pinger.
        }

        // MISCELLANEOUS STUFF
        private void TokensWatcher (object sender, EventArgs ea)
        {
            StatusIndEventArgs siea = (StatusIndEventArgs)ea;
            switch (siea.Status.SubKind) {
            case NcResult.SubKindEnum.Info_PushAssistDeviceToken:
                {
                    if (null == siea.Status.Value) {
                        ResetDeviceToken ();
                    } else {
                        SetDeviceToken ((byte[])siea.Status.Value);
                    }
                    break;
                }
            case NcResult.SubKindEnum.Info_PushAssistClientToken:
                {
                    if (null == siea.Status.Value) {
                        PostEvent (PAEvt.E.CliTokLoss, "CLITOKLOST");
                    } else {
                        DoGetCliTok ();
                    }
                    break;
                }
            }
        }

        // This API is "called" by platform code on receipt of the APNS/GCD device token.
        public void SetDeviceToken (byte[] deviceToken)
        {
            var b64tok = Convert.ToBase64String (deviceToken);
            McMutables.Set (McAccount.GetDeviceAccount ().Id, KPushAssist, KDeviceToken, b64tok);
            PostEvent (PAEvt.E.DevTok, "DEVTOKSET");
        }

        // This API is called by platform code to clear the APNS/GCD device token.
        public void ResetDeviceToken ()
        {
            // Because we aren't interlocking the DB delete and the SM event, all code
            // must check device token before using it.
            McMutables.Delete (McAccount.GetDeviceAccount ().Id, KPushAssist, KDeviceToken);
            PostEvent (PAEvt.E.DevTokLoss, "DEVTOKLOSS");
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
            }
            return null;
        }

        protected async Task<HttpResponseMessage> DoHttpRequest (string url, object jsonRequest, CancellationToken cToken)
        {
            await Task.Delay (new TimeSpan (0, 0, 0, 0, RetryDelayMsec), cToken).ConfigureAwait (false);

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
            var content = JsonConvert.SerializeObject (jsonRequest);
            request.Content = new StringContent (content);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue ("application/json");
            request.Content.Headers.ContentLength = content.Length;

            // Make the request
            var response = await Client
                .SendAsync (request, HttpCompletionOption.ResponseContentRead, cToken)
                .ConfigureAwait (false);
            Log.Info (Log.LOG_PUSH, "PA response: statusCode={0}, content={1}", response.StatusCode,
                await response.Content.ReadAsStringAsync ().ConfigureAwait (false));

            // On any successful call to pinger. Clear the retry count
            if (HttpStatusCode.OK == response.StatusCode) {
                NumRetries = 0;
            }
            return response;
        }

        private void DeviceTokenLost ()
        {
            PostEvent (PAEvt.E.DevTokLoss, "DEVTOKLOSS");
        }
    }
}

