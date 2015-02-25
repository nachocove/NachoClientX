//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Http;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
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
    public class PushAssist : IDisposable
    {
        private IPushAssistOwner Owner;
        private NcStateMachine Sm;
        private HttpClient Client;
        private int AccountId;
        private bool IsDisposed;

        public static string PingerHostName = "pinger.officetaco.com";
        //public static string PingerHostName = "localhost";

        public const int ApiVersion = 1;

        private string BaseUrl {
            get {
                return String.Format ("https://{0}/{1}", PingerHostName, ApiVersion);
                //return String.Format ("http://{0}:8001/{1}", PingerHostName, ApiVersion);
            }
        }

        private string StartSessionUrl {
            get {
                return BaseUrl + "/register";
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
                HoldOff,
                // The protocol controller wants us to cancel pinger service and chill.
                Park,
            };
        }

        // JEFF_TODO We are using McMutables for now, consider modifying protocol state instead.
        const string KPushAssist = "pushassist";
        const string KDeviceToken = "devicetoken";
        const string KPushAssistState = "pushassiststate";

        public PushAssist (IPushAssistOwner owner)
        {
            // FIXME - we will need to do cert-pinning, and also ensure SSL.
            Client = new HttpClient (new NativeMessageHandler (false, true), true);
            Owner = owner;
            AccountId = Owner.Account.Id;
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
                            (uint)PAEvt.E.HoldOff,
                            (uint)PAEvt.E.Park,
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
                            (uint)PAEvt.E.HoldOff,
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
                        },
                    },
                    new Node {
                        State = (uint)Lst.CliTokW,
                        Drop = new [] {
                            (uint)PAEvt.E.DevTok,
                            (uint)PAEvt.E.HoldOff,
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
                            new Trans { Event = (uint)PAEvt.E.CliTok, Act = DoGetSess, State = (uint)Lst.SessTokW },
                            new Trans {
                                Event = (uint)PAEvt.E.CliTokLoss,
                                Act = DoGetCliTok,
                                State = (uint)Lst.CliTokW
                            },
                            new Trans { Event = (uint)PAEvt.E.Park, Act = DoCancel, State = (uint)St.Start },
                        }
                    },
                    new Node {
                        State = (uint)Lst.SessTokW,
                        Drop = new [] {
                            (uint)PAEvt.E.DevTok,
                            (uint)PAEvt.E.CliTok,
                            (uint)PAEvt.E.HoldOff,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoGetSess, State = (uint)Lst.SessTokW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoNop, State = (uint)Lst.Active },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoGetSess, State = (uint)Lst.SessTokW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoGetSess, State = (uint)Lst.SessTokW },
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
                            new Trans { Event = (uint)PAEvt.E.Park, Act = DoCancel, State = (uint)St.Start },
                        }
                    },
                    new Node {
                        State = (uint)Lst.Active,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)Lst.Active },
                            new Trans { Event = (uint)PAEvt.E.DevTok, Act = DoGetSess, State = (uint)Lst.SessTokW },
                            new Trans {
                                Event = (uint)PAEvt.E.DevTokLoss,
                                Act = DoGetDevTok,
                                State = (uint)Lst.DevTokW
                            },
                            new Trans { Event = (uint)PAEvt.E.CliTok, Act = DoGetSess, State = (uint)Lst.SessTokW },
                            new Trans {
                                Event = (uint)PAEvt.E.CliTokLoss,
                                Act = DoGetCliTok,
                                State = (uint)Lst.CliTokW
                            },
                            new Trans { Event = (uint)PAEvt.E.HoldOff, Act = DoHoldOff, State = (uint)Lst.Active },
                            new Trans { Event = (uint)PAEvt.E.Park, Act = DoCancel, State = (uint)St.Start },
                        }
                    },
                }
            };
            NcApplication.Instance.StatusIndEvent += TokensWatcher;
        }

        public void Dispose ()
        {
            if (!IsDisposed) {
                IsDisposed = true;
                NcApplication.Instance.StatusIndEvent -= TokensWatcher;
            }
        }

        public void Execute ()
        {
            Sm.PostEvent ((uint)SmEvt.E.Launch, "PAEXE");
        }

        public void HoldOff ()
        {
            Sm.PostEvent ((uint)PAEvt.E.HoldOff, "PAHO");
        }

        private void DoNop ()
        {
        }

        private void DoCancel ()
        {
            // FIXME Cancel any outstanding http request. This is tricky, given concurrency.
            // Only one SM action can execute at a time, but there is a Q in front of the SM.
        }

        private void DoGetDevTok ()
        {
            var devTok = McMutables.Get (McAccount.GetDeviceAccount ().Id, KPushAssist, KDeviceToken);
            if (null != devTok) {
                Sm.PostEvent ((uint)PAEvt.E.DevTok, "DEVTOKFOUND");
            }
        }

        private void DoGetCliTok ()
        {
            var clientId = NcApplication.Instance.GetClientId ();
            if (null != clientId) {
                Sm.PostEvent ((uint)PAEvt.E.CliTok, "GOTCLITOK");
            }
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

        private void DoGetSess ()
        {
            var clientId = NcApplication.Instance.GetClientId ();
            if (null == clientId) {
                Sm.PostEvent ((uint)PAEvt.E.CliTokLoss, "DOSESSCTL");
                return;
            }
            var cred = McCred.QueryByAccountId<McCred> (AccountId).FirstOrDefault ();
            if (null == cred) {
                // Yes, the SM is SOL at this point.
                Log.Error (Log.LOG_PUSH, "DoGetSess: No McCred for accountId {0}", AccountId);
                return;
            }
            var parameters = Owner.PushAssistParameters ();
            Dictionary<string, string> httpHeadersDict = new Dictionary<string, string> ();
            foreach (var header in parameters.RequestHeaders) {
                httpHeadersDict.Add (header.Key, ExtractHttpHeaderValue (header));
            }
            var jsonRequest = new StartSessionRequest () {
                ClientId = clientId,
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
                PushToken = McMutables.Get (Owner.Account.Id, KPushAssist, KDeviceToken),
                PushService = NcApplication.Instance.GetPushService (),
            };

            try {
                var response = DoHttpRequest (StartSessionUrl, jsonRequest, NcTask.Cts.Token);
            } catch (OperationCanceledException) {
            } catch (Exception e) {
                Log.Warn (Log.LOG_PUSH, "Caught push assist http exception: {0}", e);
            }
        }

        private void DoHoldOff ()
        {
            // FIXME.
        }

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
                        Sm.PostEvent ((uint)PAEvt.E.CliTokLoss, "CLITOKLOST");
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
            NcTask.Run (delegate {
                var b64tok = Convert.ToBase64String (deviceToken);
                McMutables.Set (McAccount.GetDeviceAccount ().Id, KPushAssist, KDeviceToken, b64tok);
                Sm.PostEvent ((uint)PAEvt.E.DevTok, "DEVTOKSET");
            }, "PushAssist");
        }

        // This API is called by platform code to clear the APNS/GCD device token.
        public void ResetDeviceToken ()
        {
            NcTask.Run (delegate {
                // Because we aren't interlocking the DB delete and the SM event, all code
                // must check device token before using it.
                McMutables.Delete (McAccount.GetDeviceAccount ().Id, KPushAssist, KDeviceToken);
                Sm.PostEvent ((uint)PAEvt.E.DevTokLoss, "DEVTOKLOSS");
            }, "PushAssist:ResetDeviceToken");
        }

        public class Credentials
        {
            public string Username;
            public string Password;
        }

        public class StartSessionRequest
        {
            public string ClientId;
            public string MailServerUrl;
            public Credentials MailServerCredentials;
            public string Protocol;
            public string Platform;
            public Dictionary<string, string> HttpHeaders;
            public string HttpRequestData;
            public string HttpExpectedReply;
            public string HttpNoChangeReply;
            public string CommandTerminator;
            public string CommandAcknowledgement;
            public int ResponseTimeout;
            public int WaitBeforeUse;
            public string PushToken;
            public string PushService;
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
            Log.Info (Log.LOG_PUSH, "request: scheme={0}, url={1}, port={2}, method={3}",
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
            Log.Info (Log.LOG_PUSH, "response: statusCode={0}, content={1}", response.StatusCode,
                await response.Content.ReadAsStringAsync ().ConfigureAwait (false));
            return response;
        }
    }
}

