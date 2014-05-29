//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
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

        public enum Lst : uint
        {
            DevTokW = (St.Last + 1),
            CliTokW,
            SessTokW,
            Active,
        };

        public class PAEvt : SmEvt
        {
            new public enum E : uint
            {
                DevTok = (SmEvt.E.Last + 1),
            };
        }

        const string k_ios = "ios";
        const string k_clienttoken = "clienttoken";
        const string k_devicetoken = "devicetoken";
        const string k_pushassist_state = "pushassist_state";

        private PushAssist ()
        {
            Client = new HttpClient ();
            Sm = new NcStateMachine ("PUSH") {
                LocalStateType = typeof(Lst),
                LocalEventType = typeof(PAEvt),
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoNop, State = (uint)St.Start },
                            new Trans { Event = (uint)PAEvt.E.DevTok, Act = DoGetCt, State = (uint)Lst.CliTokW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.CliTokW,
                        Drop = new [] {
                            (uint)PAEvt.E.DevTok,
                        },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoGetCt, State = (uint)Lst.CliTokW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSess, State = (uint)Lst.SessTokW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoGetCt, State = (uint)Lst.CliTokW },
                        }
                    },
                    new Node {
                        State = (uint)Lst.SessTokW,
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoSess, State = (uint)Lst.SessTokW },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoNop, State = (uint)Lst.Active },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoSess, State = (uint)Lst.SessTokW },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoSess, State = (uint)Lst.SessTokW },
                            new Trans { Event = (uint)PAEvt.E.DevTok, Act = DoSess, State = (uint)Lst.SessTokW },
                        }
                    },
                    new Node {
                        // FIXME - what do we do if the server rejects a defer-ping?
                        State = (uint)Lst.Active,
                        On = new [] {
                            new Trans { Event = (uint)PAEvt.E.DevTok, Act = DoSess, State = (uint)Lst.SessTokW },
                        }
                    },
                }
            };
        }

        private void DoNop ()
        {
        }

        private async void DoGetCt ()
        {
            var response = await Client.GetAsync ("https://nco9.com/client-token");
            if (response.StatusCode == System.Net.HttpStatusCode.OK) {
                try {
                    DataContractJsonSerializer ser = new DataContractJsonSerializer (typeof(ClientTokenResponse));
                    var clientTokenResponse = (ClientTokenResponse)ser.ReadObject (await response.Content.ReadAsStreamAsync ());
                    McMutables.Set(k_ios, k_clienttoken, clientTokenResponse.Token);
                    Sm.PostEvent (SmEvt.E.Success, "GOTCLITOK");
                } catch {
                    Sm.PostEvent (SmEvt.E.HardFail, "CLITOKEX");
                }
            } else {
                Sm.PostEvent (SmEvt.E.HardFail, "CLITOKNOTOK");
            }
        }

        private async void DoSess ()
        {
            var requestJson = new StartSessionRequest () {
                ClientToken = McMutables.Get (k_ios, k_clienttoken),
                DeviceToken = McMutables.Get (k_ios, k_devicetoken),
                // FIXME... capture Ping wbxml via event, or ask strategy..?
            };
            MemoryStream jsonStream = new MemoryStream ();
            DataContractJsonSerializer ser = new DataContractJsonSerializer (typeof(StartSessionRequest));
            ser.WriteObject (jsonStream, requestJson);
            HttpRequestMessage request = new HttpRequestMessage (HttpMethod.Post, "https://nco9.com/start-session");
            request.Content = new StreamContent (jsonStream);
            var response = await Client.SendAsync (request, ctoken);
            // FIXME try/catch +/-.
        }

        public void SetDeviceToken (byte[] deviceToken)
        {
            var existing = McMutables.Get (k_ios, k_devicetoken);
            var b64tok = Convert.ToBase64String (deviceToken);
            if (null == existing && b64tok != existing) {
                McMutables.Set (k_ios, k_devicetoken, b64tok);
                Sm.PostEvent (PAEvt.E.DevTok, "SETDEVTOK");
            }
        }

        public void ResetDeviceToken ()
        {
            // FIXME - we need to park the SM.
            McMutables.Delete (k_ios, k_devicetoken);
        }

        public void DeferPushAssist ()
        {
            // FIXME - do we have a function here, or do we watch events? the latter...
        }

        [DataContract]
        public class ClientTokenResponse
        {
            [DataMember]
            public string Token;
        }

        [DataContract]
        public class StartSessionRequest
        {
            [DataMember]
            public string ClientToken;
            [DataMember]
            public string DeviceToken;
            [DataMember]
            public string EASUrl;
            [DataMember]
            public string Username;
            [DataMember]
            public string Password;
            [DataMember]
            public string WbxmlToSend;
            [DataMember]
            public string ExpectedWbxmlResponse;
            [DataMember]
            public string EASTimeoutSecs;
            [DataMember]
            public string WaitBeforeUseSecs;
            [DataMember]
            public Header[] Headers;
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

