﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using ModernHttpClient;
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
            Client = new HttpClient (new NativeMessageHandler (), true);
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
                            new Trans { Event = (uint)PAEvt.E.DevTokLoss, Act = DoGetDevTok, State = (uint)Lst.DevTokW },
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
                            new Trans { Event = (uint)PAEvt.E.DevTokLoss, Act = DoGetDevTok, State = (uint)Lst.DevTokW },
                            new Trans { Event = (uint)PAEvt.E.CliTok, Act = DoGetSess, State = (uint)Lst.SessTokW },
                            new Trans { Event = (uint)PAEvt.E.CliTokLoss, Act = DoGetCliTok, State = (uint)Lst.CliTokW },
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
                            new Trans { Event = (uint)PAEvt.E.DevTokLoss, Act = DoGetDevTok, State = (uint)Lst.DevTokW },
                            new Trans { Event = (uint)PAEvt.E.CliTokLoss, Act = DoGetCliTok, State = (uint)Lst.CliTokW },
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
                            new Trans { Event = (uint)PAEvt.E.DevTokLoss, Act = DoGetDevTok, State = (uint)Lst.DevTokW },
                            new Trans { Event = (uint)PAEvt.E.CliTok, Act = DoGetSess, State = (uint)Lst.SessTokW },
                            new Trans { Event = (uint)PAEvt.E.CliTokLoss, Act = DoGetCliTok, State = (uint)Lst.CliTokW },
                            new Trans { Event = (uint)PAEvt.E.HoldOff, Act = DoHoldOff, State = (uint)Lst.Active },
                            new Trans { Event = (uint)PAEvt.E.Park, Act = DoCancel, State = (uint)St.Start },
                        }
                    },
                }
            };
            NcApplication.Instance.StatusIndEvent += DeviceTokenWatcher;
        }

        public void Dispose ()
        {
            // FIXME - bool to protect against double de-registration (-=).
            NcApplication.Instance.StatusIndEvent -= DeviceTokenWatcher;
            // FIXME - need to stop any outstanding http requests and dispose of client.
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
            // JEFF_TODO - consider how to move AWS identity "north" of telemetry.
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
                // Yes, the SM is SOL at this point.
                Log.Error (Log.LOG_PUSH, "DoGetSess: No McCred for accountId {0}", AccountId);
            }
            var mailServerUrl = Owner.PushAssistRequestUrl ();
            var httpHeaders = Owner.PushAssistRequestHeaders ();
            var httpRequestData = Owner.PushAssistRequestData ();
            var httpNoChangeReply = Owner.PushAssistResponseData ();
            var jsonRequest = new StartSessionRequest () {
                ClientId = clientId,
                // FIXME - add other properties here.
            };

            MemoryStream jsonStream = new MemoryStream ();
            DataContractJsonSerializer ser = new DataContractJsonSerializer (typeof(StartSessionRequest));
            ser.WriteObject (jsonStream, jsonRequest);
            HttpRequestMessage request = new HttpRequestMessage (HttpMethod.Post, "FIXME URL STRING HERE");
            request.Content = new StreamContent (jsonStream);
            //var response = await Client.SendAsync (request, ctoken);
        }

        private void DoHoldOff ()
        {
            // FIXME.
        }

        private void DeviceTokenWatcher (object sender, EventArgs ea)
        {
            StatusIndEventArgs siea = (StatusIndEventArgs)ea;
            if (NcResult.SubKindEnum.Info_PushAssistDeviceToken == siea.Status.SubKind) {
                if (null == siea.Status.Value) {
                    ResetDeviceToken ();
                } else {
                    SetDeviceToken ((byte[])siea.Status.Value);
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

