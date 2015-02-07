//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using ModernHttpClient;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public class PushAssist
    {
        private static volatile PushAssist instance;
        private static object syncRoot = new Object ();

        public static PushAssist Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new PushAssist ();
                        }
                    }
                }
                return instance; 
            }
        }

        private NcStateMachine Sm;
        private HttpClient Client;
        private int AccountId;

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
            };
        }

        const string k_ios = "ios";
        const string k_devicetoken = "devicetoken";
        const string k_pushassist_state = "pushassist_state";

        private PushAssist (int accountId)
        {
            Client = new HttpClient (new NativeMessageHandler (), true);
            AccountId = accountId;
            Sm = new NcStateMachine ("PUSH") {
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
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)St.Start },
                        }
                    },
                    new Node { 
                        State = (uint)Lst.DevTokW,
                        Drop = new [] {
                            (uint)PAEvt.E.CliTok,
                            (uint)PAEvt.E.CliTokLoss,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoGetDevTok, State = (uint)Lst.DevTokW },
                            new Trans { Event = (uint)PAEvt.E.DevTok, Act = DoGetCliTok, State = (uint)Lst.CliTokW },
                            new Trans { Event = (uint)PAEvt.E.DevTokLoss, Act = DoGetDevTok, State = (uint)Lst.DevTokW },
                        },
                    },
                    new Node {
                        State = (uint)Lst.CliTokW,
                        Drop = new [] {
                            (uint)PAEvt.E.DevTok,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoGetCliTok, State = (uint)Lst.CliTokW },
                            new Trans { Event = (uint)PAEvt.E.DevTokLoss, Act = DoGetDevTok, State = (uint)Lst.DevTokW },
                            new Trans { Event = (uint)PAEvt.E.CliTok, Act = DoGetSess, State = (uint)Lst.SessTokW },
                            new Trans { Event = (uint)PAEvt.E.CliTokLoss, Act = DoGetCliTok, State = (uint)Lst.CliTokW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.SessTokW,
                        Drop = new [] {
                            (uint)PAEvt.E.DevTok,
                            (uint)PAEvt.E.CliTok,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoGetSess, State = (uint)Lst.SessTokW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoNop, State = (uint)Lst.Active },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoGetSess, State = (uint)Lst.SessTokW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoGetSess, State = (uint)Lst.SessTokW },
                            new Trans { Event = (uint)PAEvt.E.DevTokLoss, Act = DoGetDevTok, State = (uint)Lst.DevTokW },
                            new Trans { Event = (uint)PAEvt.E.CliTokLoss, Act = DoGetCliTok, State = (uint)Lst.CliTokW },
                        }
                    },
                    // FIXME in the Active state, we periodically push-back the pinger server. We need to decide 
                    // if this activty should be within the scope of the SM.
                    new Node {
                        State = (uint)Lst.Active,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        Drop = new [] {
                            (uint)PAEvt.E.DevTok,
                            (uint)PAEvt.E.CliTok,
                        },
                        On = new [] {
                            new Trans { Event = (uint)PAEvt.E.DevTok, Act = DoGetSess, State = (uint)Lst.SessTokW },
                            new Trans { Event = (uint)PAEvt.E.DevTokLoss, Act = DoGetDevTok, State = (uint)Lst.DevTokW },
                            new Trans { Event = (uint)PAEvt.E.CliTokLoss, Act = DoGetCliTok, State = (uint)Lst.CliTokW },
                        }
                    },
                }
            };
        }

        private void DoNop ()
        {
        }

        private void DoGetDevTok ()
        {
            var devTok = McMutables.Get (McAccount.GetDeviceAccount ().Id, k_ios, k_devicetoken);
            if (null != devTok) {
                Sm.PostEvent ((uint)PAEvt.E.DevTok, "DEVTOKFOUND");
            }
        }

        private async void DoGetCliTok ()
        {
            var clientId = Telemetry.SharedInstance.GetUserName ();
            if (null != clientId) {
                Sm.PostEvent ((uint)SmEvt.E.Success, "GOTCLITOK");
            }
            // FIXME - we need to be able to register for changes to client token.
        }

        private void DoGetSess ()
        {
            var clientId = Telemetry.SharedInstance.GetUserName ();
            if (null == clientId) {
                Sm.PostEvent ((uint)PAEvt.E.CliTokLoss, "DOSESSCTL");
                return;
            }
            var creds = McCred.QueryByAccountId<McCred> (AccountId);
            if (null == creds) {
                // FIXME - need to recover when creds are added.
            }
            var jsonRequest = new StartSessionRequest () {
                ClientId = clientId,
                MailServerUrl = "FIXME",

            };

            MemoryStream jsonStream = new MemoryStream ();
            DataContractJsonSerializer ser = new DataContractJsonSerializer (typeof(StartSessionRequest));
            ser.WriteObject (jsonStream, jsonRequest);
            HttpRequestMessage request = new HttpRequestMessage (HttpMethod.Post, "https://nco9.com/start-session");
            request.Content = new StreamContent (jsonStream);
            //var response = await Client.SendAsync (request, ctoken);
            // FIXME try/catch +/-.
        }

        // This API is called by code in AppDelegate on receipt of the iOS APNS device token.
        public void SetDeviceToken (byte[] deviceToken)
        {
            NcTask.Run (delegate {
                var b64tok = Convert.ToBase64String (deviceToken);
                McMutables.Set (McAccount.GetDeviceAccount ().Id, k_ios, k_devicetoken, b64tok);
                Sm.PostEvent ((uint)PAEvt.E.DevTok, "DEVTOKSET");
            }, "PushAssist");
        }

        // This API is called by the code in AppDelegate to clear the iOS APNS device token.
        public void ResetDeviceToken ()
        {
            NcTask.Run (delegate {
                // Because we aren't interlocking the DB delete and the SM event, all code
                // must check device token before using it.
                McMutables.Delete (McAccount.GetDeviceAccount ().Id, k_ios, k_devicetoken);
                Sm.PostEvent ((uint)PAEvt.E.DevTokLoss, "DEVTOKLOSS");
            });
        }

        public void DeferPushAssist ()
        {
            // FIXME - do we have a function here, or do we watch events? the latter...
        }

        private async HttpResponseMessage ApiGet (string urlString)
        {
            try {
                await Client.GetAsync (urlString);
            } catch (Exception ex) {
                return null;
            }
        }

        [DataContract]
        public class Credentials
        {
            [DataMember]
            public string Username;
            [DataMember]
            public string Password;
        }

        [DataContract]
        public class StartSessionRequest
        {
            [DataMember]
            public string ClientId;
            [DataMember]
            public string MailServerUrl;
            [DataMember]
            public Credentials MailServerCredentials;
            [DataMember]
            public string Protocol;
            [DataMember]
            public string Platform;
            [DataMember]
            public Header[] HttpHeaders;
            [DataMember]
            public string HttpRequestData;
            [DataMember]
            public string HttpExpectedReply;
            [DataMember]
            public string HttpNoChangeReply;
            [DataMember]
            public string CommandTerminator;
            [DataMember]
            public string CommandAcknowledgement;
            [DataMember]
            public string ResponseTimeout;
            [DataMember]
            public string WaitBeforeUse;
            [DataMember]
            public string PushToken;
            [DataMember]
            public string PushService;
        }

        [DataContract]
        public class Header
        {
            [DataMember]
            public string Name;
            [DataMember]
            public string Value;
        }
    }
}

